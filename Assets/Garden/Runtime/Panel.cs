using System.Collections.Generic;
using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Panel : MonoBehaviour
    {
        public Vector2 size = new Vector2(1, .3f);
        public Vector3 center;
        public float depth = .08f;
        public float distance = 1.2f;
        public float height = .3f;
        public float margin = .04f;
        public LayerMask blockingLayers = 1;
        public bool fitChildren;
        public bool followTravel = true;
        public bool HasPose { get; private set; }
        readonly Collider[] overlaps = new Collider[32];
        readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly float[] angles = { 0, 25, -25, 50, -50, 80, -80, 115, -115, 180 };
        Transform viewer;
        RealityPlayer player;
        Vector3 origin;
        float nextCheck;
        float blockedSince = -1;
        bool measured;
        Renderer[] sources;
        readonly List<Renderer> nearby = new List<Renderer>();
        Vector3 checkedEye;
        float nextVisualCheck;

        void Awake()
        {
            player = FindFirstObjectByType<RealityPlayer>(FindObjectsInactive.Include);
            RefreshBounds();
        }

        public void RefreshBounds()
        {
            measured = true;
            if (!fitChildren) return;
            var renderers = GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds();
            bool found = false;
            foreach (var item in renderers)
            {
                var local = item.localBounds;
                if (local.size.sqrMagnitude < .00001f) continue;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            var point = transform.InverseTransformPoint(item.transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
                            if (found) bounds.Encapsulate(point);
                            else { bounds = new Bounds(point, Vector3.zero); found = true; }
                        }
            }
            if (!found) return;
            size = new Vector2(Mathf.Max(.1f, bounds.size.x), Mathf.Max(.1f, bounds.size.y));
            depth = Mathf.Max(.08f, bounds.size.z);
            center = bounds.center;
        }

        public bool Show(Transform eye = null)
        {
            viewer = eye ? eye : RealityPlayer.Head;
            if (!viewer) return false;
            if (!measured) RefreshBounds();
            gameObject.SetActive(true);
            return Place(viewer.position, viewer.forward);
        }

        public bool Place(Vector3 eye, Vector3 forward)
        {
            if (!TryPose(eye, forward, out var position, out var rotation)) return false;
            transform.SetPositionAndRotation(position, rotation);
            origin = eye;
            HasPose = true;
            blockedSince = -1;
            nextCheck = Time.unscaledTime + .25f;
            return true;
        }

        public bool TryPose(Vector3 eye, Vector3 forward, out Vector3 position, out Quaternion rotation)
        {
            if (!measured) RefreshBounds();
            CacheVisuals(eye);
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            var scale = transform.lossyScale;
            float minimum = Mathf.Max(.65f, size.x * Mathf.Abs(scale.x) * .65f);
            for (int elevation = 0; elevation < 3; elevation++)
                for (int a = 0; a < angles.Length; a++)
                    for (int range = 0; range < 4; range++)
                    {
                        var facing = Quaternion.AngleAxis(angles[a], Vector3.up) * forward;
                        float reach = range == 0 ? distance : range == 1 ? distance * .8f : range == 2 ? distance * 1.3f : minimum;
                        reach = Mathf.Max(minimum, reach);
                        float offset = height + (elevation == 0 ? 0 : elevation == 1 ? .32f : -.32f);
                        rotation = Quaternion.LookRotation(facing, Vector3.up);
                        var middle = eye + facing * reach + Vector3.up * offset;
                        position = middle - rotation * Vector3.Scale(center, scale);
                        if (Clear(eye, position, rotation)) return true;
                    }
            position = transform.position;
            rotation = transform.rotation;
            return false;
        }

        public bool CanRead(Vector3 eye)
        {
            CacheVisuals(eye);
            return Clear(eye, transform.position, transform.rotation);
        }

        void CacheVisuals(Vector3 eye)
        {
            if (sources == null)
            {
                var root = GetComponentInParent<Loop>();
                var candidates = root ? root.GetComponentsInChildren<Renderer>(true) : FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var opaque = new List<Renderer>();
                foreach (var item in candidates)
                {
                    if (!item || item.transform.IsChildOf(transform) || item.GetComponent<TMP_Text>() || item.GetComponentInParent<NavPad>() || PlayerPart(item.transform)) continue;
                    var material = item.sharedMaterial;
                    if (!material || material.renderQueue > 2500) continue;
                    var extent = item.bounds.size;
                    if (Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z)) > 4) continue;
                    var shape = Vector3.Scale(item.localBounds.size, Abs(item.transform.lossyScale));
                    if (Mathf.Min(shape.x, Mathf.Min(shape.y, shape.z)) > .18f) continue;
                    opaque.Add(item);
                }
                sources = opaque.ToArray();
                nextVisualCheck = -1;
            }
            if (Time.unscaledTime < nextVisualCheck && (eye - checkedEye).sqrMagnitude < .0625f) return;
            nextVisualCheck = Time.unscaledTime + 1;
            checkedEye = eye;
            nearby.Clear();
            foreach (var item in sources)
                if (item && item.bounds.SqrDistance(eye) < 25) nearby.Add(item);
        }

        bool Clear(Vector3 eye, Vector3 position, Quaternion rotation)
        {
            var scale = transform.lossyScale;
            var middle = position + rotation * Vector3.Scale(center, scale);
            var half = new Vector3(size.x * Mathf.Abs(scale.x) * .5f + margin, size.y * Mathf.Abs(scale.y) * .5f + margin, depth * Mathf.Abs(scale.z) * .5f + .015f);
            int count = Physics.OverlapBoxNonAlloc(middle, half, overlaps, rotation, blockingLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!Ignored(overlaps[i])) return false;
            var extent = Abs(rotation * Vector3.right) * half.x + Abs(rotation * Vector3.up) * half.y + Abs(rotation * Vector3.forward) * half.z;
            var panel = new Bounds(middle, extent * 2);
            foreach (var item in nearby)
                if (item && item.enabled && item.gameObject.activeInHierarchy && item.bounds.Intersects(panel)) return false;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                {
                    var point = middle + rotation * new Vector3(x * half.x, y * half.y, -half.z);
                    var delta = point - eye;
                    count = Physics.RaycastNonAlloc(eye, delta.normalized, hits, delta.magnitude, blockingLayers, QueryTriggerInteraction.Ignore);
                    if (count == hits.Length) return false;
                    for (int i = 0; i < count; i++)
                        if (!Ignored(hits[i].collider)) return false;
                    var ray = new Ray(eye, delta.normalized);
                    foreach (var item in nearby)
                    {
                        if (!item || !item.enabled || !item.gameObject.activeInHierarchy) continue;
                        var bounds = item.bounds;
                        if (bounds.Contains(eye)) continue;
                        if (bounds.IntersectRay(ray, out float hit) && hit < delta.magnitude) return false;
                    }
                }
            return true;
        }

        bool Ignored(Collider collider)
        {
            if (!collider || collider.isTrigger || collider.transform.IsChildOf(transform)) return true;
            return PlayerPart(collider.transform);
        }

        static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        bool PlayerPart(Transform item)
        {
            if (!player) player = FindFirstObjectByType<RealityPlayer>(FindObjectsInactive.Include);
            if (!player) return false;
            return (player.xrOrigin && item.IsChildOf(player.xrOrigin.transform)) ||
                   (player.desktopRoot && item.IsChildOf(player.desktopRoot.transform)) ||
                   (player.avatarBody && item.IsChildOf(player.avatarBody)) ||
                   (player.avatarHead && item.IsChildOf(player.avatarHead));
        }

        void LateUpdate()
        {
            if (!followTravel || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .25f;
            if (!viewer) viewer = RealityPlayer.Head;
            if (!viewer) return;
            var middle = transform.TransformPoint(center);
            float gap = Vector3.Distance(viewer.position, middle);
            if (!HasPose || (viewer.position - origin).sqrMagnitude > .81f || gap < .5f || gap > 2.5f)
            {
                Place(viewer.position, viewer.forward);
                return;
            }
            if (CanRead(viewer.position)) { blockedSince = -1; return; }
            if (blockedSince < 0) blockedSince = Time.unscaledTime;
            if (Time.unscaledTime - blockedSince >= .3f) Place(viewer.position, viewer.forward);
        }
    }
}

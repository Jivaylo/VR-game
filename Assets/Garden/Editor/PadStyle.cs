using System.Collections.Generic;
using RealityPlayground;
using RealityPlayground.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Garden.Editor
{
    public static class PadStyle
    {
        const string Folder = "Assets/Garden/Art/Teleport/";

        public static void Apply(GameObject root)
        {
            var player = root.GetComponentInChildren<RealityPlayer>(true);
            if (player && player.xrOrigin)
            {
                var rig = player.xrOrigin.gameObject;
                if (!rig.GetComponentInParent<PadRig>(true)) rig.AddComponent<PadRig>();
                PadRig.Configure(rig);
            }
            var material = AimMaterial();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Teleport.prefab");
            var surface = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Teleport.mat");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Cycle.anim");
            var hover = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Hover.anim");
            if (!prefab || !surface || !clip || !hover) throw new System.InvalidOperationException("Teleport assets are missing.");
            surface.enableInstancing = true;
            EditorUtility.SetDirty(surface);
            var fit = Measure(prefab, clip, hover);
            var aim = MakeMesh(true);
            foreach (var pad in root.GetComponentsInChildren<NavPad>(true))
            {
                var old = pad.marker;
                var glow = Setup(pad.gameObject, prefab, surface, material, aim, fit, Vector3.zero);
                pad.marker = glow.ring;
                glow.route = null;
                if (old && old != glow.ring && !old.transform.IsChildOf(glow.model))
                {
                    old.enabled = false;
                    if (old.transform != pad.transform && old.transform.childCount == 0 && old.GetComponents<Component>().Length == 3)
                        Object.DestroyImmediate(old.gameObject);
                }
                EditorUtility.SetDirty(pad);
            }
            foreach (var marker in root.GetComponentsInChildren<StoryNavMarker>(true))
            {
                var offset = marker.isPortal ? new Vector3(0, 0, -.55f) : Vector3.zero;
                var glow = Setup(marker.gameObject, prefab, surface, material, aim, fit, offset);
                glow.route = marker;
                if (marker.glyph)
                    foreach (var renderer in marker.glyph.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                var halo = ObjectNames.Find(marker.transform, "Arrival halo");
                if (halo && !marker.isPortal)
                    foreach (var renderer in halo.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                if (!marker.isPortal)
                    foreach (var label in marker.GetComponentsInChildren<TextMesh>(true))
                    {
                        var position = label.transform.localPosition;
                        position.y = Mathf.Max(position.y, 1.05f);
                        label.transform.localPosition = position;
                    }
                EditorUtility.SetDirty(glow);
            }
        }

        static Material AimMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Materials/Pad.mat");
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Pad", enableInstancing = true };
                material.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(material, "Assets/Garden/Materials/Pad.mat");
            }
            return material;
        }

        static Vector2 Measure(GameObject prefab, params AnimationClip[] clips)
        {
            var sample = Object.Instantiate(prefab);
            try
            {
                sample.hideFlags = HideFlags.HideAndDontSave;
                foreach (var child in sample.GetComponentsInChildren<Transform>(true)) child.name = ObjectNames.Short(child.name);
                sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                sample.transform.localScale = Vector3.one;
                foreach (var animator in sample.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var renderers = sample.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new System.InvalidOperationException("Teleport model has no geometry.");
                var bounds = renderers[0].bounds;
                foreach (var clip in clips)
                    for (int frame = 0; frame <= 32; frame++)
                    {
                        clip.SampleAnimation(sample, clip.length * frame / 32f);
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    }
                float scale = Mathf.Min(.74f / Mathf.Max(.01f, bounds.size.x, bounds.size.z), .85f / Mathf.Max(.01f, bounds.size.y));
                return new Vector2(scale, .018f - bounds.min.y * scale);
            }
            finally { Object.DestroyImmediate(sample); }
        }

        static PadGlow Setup(GameObject owner, GameObject prefab, Material surface, Material material, Mesh aim, Vector2 fit, Vector3 offset)
        {
            var glow = owner.GetComponent<PadGlow>();
            if (!glow) glow = owner.AddComponent<PadGlow>();
            var model = glow.model;
            if (!model)
            {
                model = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, owner.transform)).transform;
                model.name = "Teleport";
            }
            model.localPosition = offset + Vector3.up * fit.y;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one * fit.x;
            glow.model = model;
            glow.parts = model.GetComponentsInChildren<Renderer>(true);
            glow.ring = null;
            foreach (var renderer in glow.parts)
            {
                renderer.sharedMaterial = surface;
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.gameObject.layer = 9;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                if (ObjectNames.Matches(renderer.name, "Base Ring")) glow.ring = renderer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
            }
            if (!glow.ring && glow.parts.Length > 0) glow.ring = glow.parts[0];
            glow.motion = model.GetComponent<Animator>();
            if (glow.motion)
            {
                glow.motion.cullingMode = AnimatorCullingMode.CullCompletely;
                glow.motion.updateMode = AnimatorUpdateMode.UnscaledTime;
                glow.motion.applyRootMotion = false;
                glow.motion.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(glow.motion);
            }
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            var child = ObjectNames.Find(owner.transform, "Aim");
            if (!child)
            {
                child = new GameObject("Aim", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                child.SetParent(owner.transform, false);
            }
            child.localPosition = offset + new Vector3(0, .046f, 0);
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.GetComponent<MeshFilter>().sharedMesh = aim;
            glow.aim = child.GetComponent<MeshRenderer>();
            glow.aim.sharedMaterial = material;
            glow.aim.shadowCastingMode = ShadowCastingMode.Off;
            glow.aim.receiveShadows = false;
            glow.aim.enabled = false;
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            var target = glow.target ? glow.target.transform : ObjectNames.Find(owner.transform, "AimTarget");
            if (!target)
            {
                target = new GameObject("AimTarget", typeof(CapsuleCollider)).transform;
                target.SetParent(owner.transform, false);
            }
            target.localPosition = offset;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
            target.gameObject.layer = 9;
            var hit = target.GetComponent<CapsuleCollider>();
            if (!hit) hit = target.gameObject.AddComponent<CapsuleCollider>();
            hit.center = new Vector3(0, .46f, 0);
            hit.radius = .24f;
            hit.height = .92f;
            hit.isTrigger = true;
            glow.target = hit;
            var anchor = owner.GetComponent<TeleportationAnchor>();
            if (anchor && !anchor.colliders.Contains(hit)) anchor.colliders.Add(hit);
            glow.idleColor = new Color(.16f, .78f, .82f);
            glow.focusColor = new Color(.64f, 1f, .91f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.gameObject);
            EditorUtility.SetDirty(glow);
            if (anchor) EditorUtility.SetDirty(anchor);
            return glow;
        }

        static Mesh MakeMesh(bool focus)
        {
            string name = focus ? "PadAim" : "Pad";
            string path = "Assets/Garden/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
            var points = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 Point(float angle, float radius) => new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
            for (int i = 0; i < 48; i++)
            {
                if (focus && i % 12 >= 8) continue;
                float a = i * Mathf.PI / 24;
                float b = (i + 1) * Mathf.PI / 24;
                int n = points.Count;
                float outer = focus ? .49f : .38f;
                float inner = focus ? .475f : .34f;
                points.Add(Point(a, outer)); points.Add(Point(b, outer));
                points.Add(Point(a, inner)); points.Add(Point(b, inner));
                triangles.AddRange(new[] { n, n + 1, n + 2, n + 2, n + 1, n + 3 });
            }
            if (focus)
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * .5f;
                    int n = points.Count;
                    points.Add(Point(a - .07f, .44f));
                    points.Add(Point(a + .07f, .44f));
                    points.Add(Point(a, .39f));
                    triangles.AddRange(new[] { n, n + 1, n + 2 });
                }
            mesh.Clear(); mesh.SetVertices(points); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}

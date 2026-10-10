using System.Collections;
using RealityPlayground;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Garden
{
    public sealed class Poster : PlaygroundTarget
    {
        public Loop loop;
        public Transform paper;
        public GameObject rift;
        public float tearDistance = .4f;
        public float reach = .75f;
        public GameObject[] panels = new GameObject[0];
        public TMP_Text status;
        public UnityEvent torn = new UnityEvent();
        public bool Torn { get; private set; }
        public override bool UsesTrackedHandForGrab => true;

        Transform heldBy;
        Mesh sheet;
        Vector3[] source;
        Vector3[] bent;
        float[] weights;
        Vector3 handStart;
        Vector3 lastHand;
        Vector3 velocity;
        Vector3 paperStart;
        Quaternion paperFacing;
        Quaternion wristStart;
        Vector3 home;
        Quaternion rotation;
        Rigidbody body;
        bool opening;
        bool raised;
        bool ready;

        void Awake()
        {
            if (paper)
            {
                home = paper.localPosition;
                rotation = paper.localRotation;
                var filter = paper.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh && filter.sharedMesh.isReadable && filter.sharedMesh.vertexCount >= 9)
                {
                    sheet = Instantiate(filter.sharedMesh);
                    sheet.name = "Paper";
                    sheet.MarkDynamic();
                    filter.sharedMesh = sheet;
                    source = sheet.vertices;
                    bent = new Vector3[source.Length];
                    weights = new float[source.Length];
                }
            }
            SetPanels(-1);
            if (rift) rift.SetActive(false);
            ready = true;
        }

        void OnEnable()
        {
            if (loop) loop.Changed += Refresh;
            if (ready) Refresh();
        }

        void Start() { Refresh(); }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            heldBy = null;
            StopAllCoroutines();
            opening = false;
            SetPanels(-1);
        }

        void OnDestroy()
        {
            if (sheet) Destroy(sheet);
        }

        void Refresh()
        {
            if (!ready || !loop) return;
            if (loop.state.poster && !Torn)
            {
                Torn = true;
                if (paper) paper.gameObject.SetActive(false);
            }
            if (Torn && !opening && !raised) Open();
            if (rift && loop.state.local) rift.SetActive(false);
        }

        public override void BeginInteraction(Transform hand)
        {
            BeginInteraction(hand, hand ? hand.position : transform.position);
        }

        public override void BeginInteraction(Transform hand, Vector3 selectionPoint)
        {
            if (Session.IsOpen || Torn || !paper || !hand || heldBy) return;
            var surface = paper.GetComponent<Collider>();
            Vector3 contact = surface && surface.enabled ? surface.ClosestPoint(hand.position) : paper.position;
            if (Vector3.Distance(hand.position, contact) > reach) return;
            heldBy = hand;
            handStart = lastHand = hand.position;
            velocity = Vector3.zero;
            wristStart = hand.rotation;
            paperStart = paper.position;
            paperFacing = paper.rotation;
            if (sheet)
            {
                Vector3 point = paper.InverseTransformPoint(selectionPoint);
                int corner = 0;
                float nearest = float.MaxValue;
                for (int i = 0; i < source.Length; i++)
                {
                    float distance = (source[i] - point).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; corner = i; }
                }
                float extent = Mathf.Max(.1f, sheet.bounds.size.magnitude * .8f);
                for (int i = 0; i < source.Length; i++) weights[i] = Mathf.Pow(Mathf.Clamp01(1 - Vector3.Distance(source[i], source[corner]) / extent), 1.5f);
            }
        }

        public override void UpdateInteraction(Transform hand)
        {
            if (Session.IsOpen) { if (heldBy) EndInteraction(heldBy); return; }
            if (Torn || !paper || !hand || hand != heldBy) return;
            Vector3 delta = hand.position - handStart;
            velocity = Vector3.Lerp(velocity, (hand.position - lastHand) / Mathf.Max(.001f, Time.deltaTime), .4f);
            lastHand = hand.position;
            float progress = Mathf.Clamp01(delta.magnitude / Mathf.Max(.1f, tearDistance));
            if (sheet)
            {
                Vector3 bend = paper.InverseTransformVector(delta);
                for (int i = 0; i < source.Length; i++) bent[i] = source[i] + bend * weights[i];
                sheet.vertices = bent;
                sheet.RecalculateBounds();
                sheet.RecalculateNormals();
            }
            else
            {
                paper.position = paperStart + delta * .55f;
                Vector3 axis = Vector3.Cross(paperFacing * Vector3.forward, delta);
                if (axis.sqrMagnitude < .001f) axis = paperFacing * Vector3.up;
                Quaternion wrist = hand.rotation * Quaternion.Inverse(wristStart);
                paper.rotation = Quaternion.Slerp(Quaternion.identity, wrist, .3f) * Quaternion.AngleAxis(progress * 42, axis) * paperFacing;
            }
            if (progress >= 1) Tear();
        }

        public override void EndInteraction(Transform hand)
        {
            if (hand != heldBy) return;
            heldBy = null;
            if (Torn || !paper) return;
            paper.localPosition = home;
            paper.localRotation = rotation;
            if (sheet) { sheet.vertices = source; sheet.RecalculateBounds(); sheet.RecalculateNormals(); }
        }

        public void Tear()
        {
            if (Session.IsOpen || Torn || !ready) return;
            Torn = true;
            opening = true;
            heldBy = null;
            Drop();
            if (loop)
            {
                loop.state.poster = true;
                loop.Notify();
                loop.Announce("SIGNAL", "Do you remember an unfiltered sky?");
                loop.Cue(.8f);
            }
            StartCoroutine(Hack());
        }

        void Drop()
        {
            if (!paper) return;
            paper.SetParent(null, true);
            foreach (var collider in paper.GetComponents<Collider>()) collider.enabled = false;
            var box = paper.gameObject.AddComponent<BoxCollider>();
            Bounds bounds = sheet ? sheet.bounds : new Bounds(Vector3.zero, new Vector3(.75f, 1, .02f));
            box.center = bounds.center;
            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, .025f); size.y = Mathf.Max(size.y, .025f); size.z = Mathf.Max(size.z, .025f);
            box.size = size;
            body = paper.GetComponent<Rigidbody>();
            if (!body) body = paper.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.mass = .02f;
            body.linearDamping = .7f;
            body.angularDamping = 1.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.ClampMagnitude(velocity, 2.5f);
            body.angularVelocity = Vector3.Cross(paper.forward, body.linearVelocity) * .8f;
        }

        IEnumerator Hack()
        {
            string[] steps = { "Carrier found", "Checking presentation", "Local channel open" };
            for (int i = 0; i < steps.Length; i++)
            {
                SetPanels(i);
                if (status) status.text = steps[i];
                yield return new WaitForSeconds(1);
            }
            SetPanels(-1);
            if (status) status.text = "Enter when ready";
            opening = false;
            Open();
        }

        void SetPanels(int step)
        {
            for (int i = 0; i < panels.Length; i++) if (panels[i]) panels[i].SetActive(step >= 0 && i <= step);
        }

        void Open()
        {
            if (rift) rift.SetActive(!loop || !loop.state.local);
            if (raised) return;
            raised = true;
            torn.Invoke();
        }
    }
}

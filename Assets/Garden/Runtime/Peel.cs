using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Peel : PlaygroundTarget
    {
        public enum Kind { Wrapper, Ad }
        public Kind kind;
        public Loop loop;
        public Transform sheet;
        public GameObject clue;
        public GameObject presentation;
        public Vector3 pull = new Vector3(0, -.65f, -1);
        public float distance = .18f;
        public bool Opened { get; private set; }
        Transform hand;
        Vector3 start;
        Vector3 origin;
        Quaternion rotation;
        float amount;
        float target;
        int day;

        public override bool UsesTrackedHandForGrab => true;

        void Awake()
        {
            if (!loop) loop = Loop.Instance;
            if (sheet) { origin = sheet.localPosition; rotation = sheet.localRotation; }
            day = loop ? loop.state.day : 1;
        }

        void OnEnable() { if (loop) { loop.Changed += Refresh; loop.WrapperOpened += OpenWrapper; } }
        void Start() { Refresh(); }
        void OnDisable() { if (loop) { loop.Changed -= Refresh; loop.WrapperOpened -= OpenWrapper; } hand = null; }

        void OpenWrapper()
        {
            if (kind != Kind.Wrapper || Opened) return;
            Opened = true;
            target = 1;
            hand = null;
            Refresh();
        }

        public override void BeginInteraction(Transform value)
        {
            if (!value || !sheet || hand || Opened || Vector3.Distance(value.position, sheet.position) > .6f) return;
            if (!loop || !loop.CanUse(kind == Kind.Wrapper ? "Peel" : "MetroClue")) return;
            hand = value;
            start = value.position;
        }

        public override void BeginInteraction(Transform value, Vector3 selectionPoint) { BeginInteraction(value); }

        public override void UpdateInteraction(Transform value)
        {
            if (!hand || hand != value || Opened) return;
            Vector3 axis = transform.TransformDirection(pull.normalized);
            float motion = Vector3.Dot(value.position - start, axis);
            target = Mathf.Clamp01(motion / Mathf.Max(.15f, distance));
            if (target >= .98f) Open();
        }

        public override void EndInteraction(Transform value)
        {
            if (hand != value) return;
            hand = null;
            if (!Opened) target = 0;
        }

        public void Open()
        {
            if (!loop || Opened) return;
            if (!loop.TryUse(kind == Kind.Wrapper ? "Peel" : "MetroClue")) return;
            Opened = true;
            target = 1;
            hand = null;
            loop.Cue(.85f);
            if (kind == Kind.Wrapper) loop.Peel();
            else loop.MetroClue();
            Refresh();
        }

        void Update()
        {
            if (hand && !hand.gameObject.activeInHierarchy) { hand = null; if (!Opened) target = 0; }
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime * 4f);
            if (sheet)
            {
                sheet.localPosition = origin + pull.normalized * (amount * distance);
                sheet.localRotation = rotation * Quaternion.Euler(amount * 62, 0, 0);
            }
            if (clue) clue.SetActive(amount > .45f);
        }

        void Refresh()
        {
            if (!loop) return;
            if (day != loop.state.day)
            {
                day = loop.state.day;
                if (kind == Kind.Wrapper) { Opened = false; amount = target = 0; hand = null; }
            }
            if (kind == Kind.Ad && loop.state.metroClue) { Opened = true; target = 1; }
            if (presentation) presentation.SetActive(!Opened && !loop.state.local);
        }
    }
}

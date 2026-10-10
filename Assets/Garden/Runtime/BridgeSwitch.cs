using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class BridgeSwitch : PlaygroundTarget
    {
        public Loop loop;
        public Bridge bridge;
        public Transform knob;
        public Vector3 axis = Vector3.right;
        public float travel = .07f;
        public TMP_Text display;
        public float Progress { get; private set; }
        public bool AtDetent => offeredGarden && Progress >= .54f && Progress < .98f;
        public override bool SupportsTriggerActivation => true;
        public override bool UsesTrackedHandForGrab => true;

        Transform hand;
        Collider surface;
        Vector3 handStart;
        Vector3 knobHome;
        Vector3 feet;
        float startValue;
        float next;
        float blocked;
        bool warned;
        bool offeredGarden;
        bool released;
        bool ready;

        void Awake()
        {
            surface = GetComponent<Collider>();
            if (knob) knobHome = knob.localPosition;
            feet = RealityPlayer.FeetPosition;
            ready = true;
        }

        void OnEnable()
        {
            if (loop) loop.Changed += Refresh;
        }

        void Start() { Refresh(); }

        bool CanUse => !Session.IsOpen && loop && loop.state.installed && !loop.state.local && !loop.Busy && !loop.InAngel;

        public override void BeginInteraction(Transform target)
        {
            if (!CanUse || !target || hand || Time.unscaledTime < blocked) return;
            var arm = bridge && bridge.Arm ? bridge.Arm : RealityPlayer.RightHand;
            if (RealityPlayer.IsXR && (target == arm || arm && target.IsChildOf(arm))) return;
            Vector3 contact = surface ? surface.ClosestPoint(target.position) : transform.position;
            if ((contact - target.position).sqrMagnitude > .0256f) return;
            hand = target;
            handStart = transform.InverseTransformPoint(target.position);
            startValue = Progress;
            Button.Haptic(hand, .06f, .03f);
        }

        public override void BeginInteraction(Transform target, Vector3 selectionPoint) { BeginInteraction(target); }

        public override void UpdateInteraction(Transform target)
        {
            if (!CanUse || hand != target || !target || Time.unscaledTime < blocked) return;
            Vector3 from = transform.TransformPoint(handStart);
            Vector3 displacement = target.position - from;
            if (displacement.sqrMagnitude > .36f) { EndInteraction(target); return; }
            float amount = Vector3.Dot(displacement, transform.TransformDirection(axis.normalized)) / Mathf.Max(.03f, travel);
            SetProgress(startValue + amount);
        }

        public override void EndInteraction(Transform target)
        {
            if (hand != target) return;
            hand = null;
            if (offeredGarden && Progress >= .54f) released = true;
            else if (Progress > 0f && Progress < .54f) Cancel();
        }

        public override void Activate()
        {
            if (!CanUse || hand || Time.unscaledTime < next) return;
            next = Time.unscaledTime + .8f;
            if (!warned) SetProgress(.24f);
            else if (!offeredGarden) { SetProgress(.55f); released = true; }
            else { released = true; SetProgress(1f); }
        }

        public void Cancel()
        {
            if (!loop || loop.state.local) return;
            bool changed = warned || offeredGarden;
            hand = null;
            Progress = 0f;
            warned = offeredGarden = released = false;
            if (changed) loop.KeepNetwork();
            Apply();
        }

        void SetProgress(float value)
        {
            if (!CanUse) return;
            value = Mathf.Clamp01(value);
            if (warned && value < .06f) { Cancel(); return; }
            if (!warned && value >= .2f)
            {
                warned = true;
                loop.OfferLocal(false);
                Button.Haptic(hand, .12f, .045f);
            }
            if (!offeredGarden && value >= .55f)
            {
                offeredGarden = true;
                released = false;
                loop.ConfirmLocal(false);
                Button.Haptic(hand, .23f, .06f);
            }
            Progress = !released && offeredGarden ? Mathf.Min(value, .55f) : value;
            if (Progress >= .98f && released)
            {
                Progress = 1f;
                hand = null;
                loop.Disconnect();
            }
            Apply();
        }

        void Update()
        {
            Vector3 currentFeet = RealityPlayer.FeetPosition;
            if ((currentFeet - feet).sqrMagnitude > .16f)
            {
                hand = null;
                blocked = Time.unscaledTime + .4f;
                if (warned && !offeredGarden) Cancel();
            }
            feet = currentFeet;
            if (hand && (!hand.gameObject.activeInHierarchy || !CanUse)) hand = null;
        }

        void Refresh()
        {
            if (!ready || !loop) return;
            if (loop.state.local) Progress = 1f;
            else if (!loop.state.installed) Progress = 0f;
            Apply();
        }

        void Apply()
        {
            if (knob && knob != transform)
            {
                var offset = transform.TransformDirection(axis.normalized) * (Progress * travel);
                knob.localPosition = knobHome + (knob.parent ? knob.parent.InverseTransformVector(offset) : offset);
            }
            if (display) display.text = loop && loop.state.local ? "LOCAL\nCONTACT OFFLINE" : !loop || !loop.state.installed ? "NETWORK" : AtDetent ? "NETWORK / LOCAL\nREGRIP TO CONTINUE" : warned ? "NETWORK / LOCAL\nSLIDE TO CHOOSE" : "NETWORK / LOCAL\nSLIDE SWITCH";
        }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            hand = null;
        }
    }
}

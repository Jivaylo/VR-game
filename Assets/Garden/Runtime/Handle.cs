using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Handle : PlaygroundTarget
    {
        public enum Kind { Selector, Lever, Wheel }
        public Gate gate;
        public Kind kind;
        public float reach = 0.65f;
        public float direction = -1f;

        Transform heldBy;
        Vector3 axis;
        Vector3 previous;
        Vector3 wrist;
        float value;

        public override bool UsesTrackedHandForGrab => true;

        public override void BeginInteraction(Transform hand)
        {
            if (Session.IsOpen || !gate || !hand || heldBy || Vector3.Distance(hand.position, transform.position) > reach) return;
            heldBy = hand;
            Vector3 localAxis = kind == Kind.Selector ? gate.selectorAxis : kind == Kind.Lever ? gate.leverAxis : gate.wheelAxis;
            axis = transform.TransformDirection(localAxis).normalized;
            previous = Vector3.ProjectOnPlane(hand.position - transform.position, axis);
            wrist = Vector3.ProjectOnPlane(hand.up, axis);
            value = kind == Kind.Selector ? gate.Selector : kind == Kind.Lever ? gate.Lever : 0f;
        }

        public override void BeginInteraction(Transform hand, Vector3 selectionPoint) { BeginInteraction(hand); }

        public override void UpdateInteraction(Transform hand)
        {
            if (Session.IsOpen) { heldBy = null; return; }
            if (!gate || hand != heldBy || !hand) return;
            if (Vector3.Distance(hand.position, transform.position) > reach * 1.35f) { EndInteraction(hand); return; }
            Vector3 radial = Vector3.ProjectOnPlane(hand.position - transform.position, axis);
            Vector3 facing = Vector3.ProjectOnPlane(hand.up, axis);
            float delta = 0f;
            if (radial.sqrMagnitude > 0.0016f && previous.sqrMagnitude > 0.0016f)
                delta = Vector3.SignedAngle(previous, radial, axis);
            else if (facing.sqrMagnitude > 0.04f && wrist.sqrMagnitude > 0.04f)
                delta = Vector3.SignedAngle(wrist, facing, axis);
            previous = radial;
            wrist = facing;
            delta = Mathf.Clamp(delta * direction, -35f, 35f);
            if (kind == Kind.Wheel) gate.TurnWheel(delta);
            else
            {
                value = Mathf.Clamp01(value + delta / (kind == Kind.Selector ? 80f : 65f));
                if (kind == Kind.Selector) gate.SetSelector(value);
                else gate.SetLever(value);
            }
        }

        public override void EndInteraction(Transform hand)
        {
            if (hand == heldBy) heldBy = null;
        }

        void OnDisable() { heldBy = null; }
    }
}

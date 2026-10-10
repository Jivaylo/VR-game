using RealityPlayground;
using UnityEngine;
using UnityEngine.Events;

namespace Garden
{
    public sealed class Grip : PlaygroundTarget
    {
        public enum Mode { Pull, Turn, Touch, Hold }
        public Mode mode;
        public UnityEvent used = new UnityEvent();
        public string action;
        public Transform model;
        public Vector3 axis = Vector3.back;
        public float travel = .16f;
        public float angle = 45;
        public float holdTime = .65f;
        public bool once, leftOnly;
        public bool interactive = true;
        struct Contact
        {
            public bool tracked, ready, used;
            public Vector3 point;
            public float time;
        }
        Contact left, right;
        Collider surface;
        Transform hand;
        Vector3 home, start, radial, wrist, feet, previous;
        Quaternion rotation;
        float value, held, next, blocked, turn;
        bool fired, completed;
        public override bool UsesTrackedHandForGrab => true;
        public float Progress => value;

        void Start()
        {
            surface = GetComponent<Collider>();
            if (model && model != transform) { home = model.localPosition; rotation = model.localRotation; }
            feet = RealityPlayer.FeetPosition;
        }

        public override void Activate()
        {
            if (!RealityPlayer.IsXR) Fire(null);
        }

        bool Allowed(Transform target)
        {
            return !Session.IsOpen && target && (!leftOnly || !RealityPlayer.IsXR || RealityPlayer.LeftHand && (target == RealityPlayer.LeftHand || target.IsChildOf(RealityPlayer.LeftHand)));
        }

        bool Near(Transform target, float distance)
        {
            if (!target) return false;
            Vector3 closest = surface ? surface.ClosestPoint(target.position) : transform.position;
            return (closest - target.position).sqrMagnitude <= distance * distance;
        }

        public override void BeginInteraction(Transform target)
        {
            if (Moved() || !interactive || !isActiveAndEnabled || !Allowed(target) || hand || completed && once) return;
            if (RealityPlayer.IsXR && !Near(target, .12f)) return;
            hand = target;
            start = previous = target.position;
            Vector3 normal = transform.TransformDirection(axis.normalized);
            radial = Vector3.ProjectOnPlane(target.position - transform.position, normal);
            wrist = Vector3.ProjectOnPlane(target.up, normal);
            value = held = turn = 0;
            fired = false;
            Button.Haptic(target, .045f, .025f);
        }

        public override void BeginInteraction(Transform target, Vector3 point) { BeginInteraction(target); }

        public override void UpdateInteraction(Transform target)
        {
            if (Moved() || !interactive || !target || target != hand || fired) return;
            if ((target.position - previous).sqrMagnitude > .16f) { EndInteraction(target); return; }
            previous = target.position;
            if ((target.position - start).sqrMagnitude > 1.44f) { EndInteraction(target); return; }
            Vector3 normal = transform.TransformDirection(axis.normalized);
            if (mode == Mode.Pull) value = Mathf.Clamp01(Vector3.Dot(target.position - start, normal) / Mathf.Max(.02f, travel));
            else if (mode == Mode.Turn)
            {
                var current = Vector3.ProjectOnPlane(target.position - transform.position, normal);
                var facing = Vector3.ProjectOnPlane(target.up, normal);
                float radialDelta = current.sqrMagnitude > .002f && radial.sqrMagnitude > .002f
                    ? Vector3.SignedAngle(radial, current, normal) : 0;
                float wristDelta = facing.sqrMagnitude > .04f && wrist.sqrMagnitude > .04f
                    ? Vector3.SignedAngle(wrist, facing, normal) : 0;
                float delta = Mathf.Abs(wristDelta) > Mathf.Abs(radialDelta) ? wristDelta : radialDelta;
                radial = current;
                wrist = facing;
                float limit = Mathf.Max(5, angle);
                turn = Mathf.Clamp(turn + Mathf.Clamp(delta, -70, 70), -limit, limit);
                value = Mathf.Abs(turn) / limit;
            }
            else if (Near(target, .06f))
            {
                held += Time.unscaledDeltaTime;
                value = Mathf.Clamp01(held / (mode == Mode.Touch ? .10f : Mathf.Max(.1f, holdTime)));
            }
            else held = value = 0;
            if (value >= .99f) { fired = true; Fire(target); }
        }

        public override void EndInteraction(Transform target)
        {
            if (target != hand) return;
            hand = null;
            value = held = turn = 0;
        }

        void Fire(Transform target)
        {
            if (Session.IsOpen || !interactive || !isActiveAndEnabled || completed && once || Time.unscaledTime < next) return;
            completed = true;
            next = Time.unscaledTime + .5f;
            Button.Haptic(target, .2f, .05f);
            if (Loop.Instance) Loop.Instance.Cue(1.15f);
            used.Invoke();
            if (!string.IsNullOrEmpty(action) && Loop.Instance) Loop.Instance.Run(action);
        }

        bool Moved()
        {
            var currentFeet = RealityPlayer.FeetPosition;
            if ((currentFeet - feet).sqrMagnitude > .09f)
            {
                hand = null;
                value = held = turn = 0;
                left = right = default;
                blocked = Time.unscaledTime + .3f;
            }
            feet = currentFeet;
            return Time.unscaledTime < blocked;
        }

        void Update()
        {
            Moved();
            if (RealityPlayer.IsXR && interactive && !hand && !completed && (mode == Mode.Touch || mode == Mode.Hold) && Time.unscaledTime >= blocked)
            {
                value = 0;
                ContactHand(RealityPlayer.LeftHand, ref left);
                if (!leftOnly) ContactHand(RealityPlayer.RightHand, ref right);
            }
            else if (completed && !once && Time.unscaledTime >= next) completed = false;
            if (!model || model == transform) return;
            float blend = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
            Vector3 move = mode == Mode.Pull ? model.parent.InverseTransformVector(transform.TransformDirection(axis.normalized) * (travel * value)) : Vector3.zero;
            model.localPosition = Vector3.Lerp(model.localPosition, home + move, blend);
            var turnAxis = model.parent.InverseTransformDirection(transform.TransformDirection(axis.normalized));
            Quaternion pose = mode == Mode.Turn ? Quaternion.AngleAxis(turn, turnAxis) * rotation : rotation;
            model.localRotation = Quaternion.Slerp(model.localRotation, pose, blend);
        }

        void ContactHand(Transform target, ref Contact contact)
        {
            if (!Allowed(target)) { contact = default; return; }
            if (!contact.tracked || (target.position - contact.point).sqrMagnitude > .16f)
            {
                contact = new Contact { tracked = true, point = target.position };
                return;
            }
            if (!Near(target, .085f))
            {
                contact.ready = true;
                contact.used = false;
                contact.time = 0;
            }
            else if (contact.ready && !contact.used && Near(target, .025f))
            {
                contact.time += Time.unscaledDeltaTime;
                float progress = contact.time / (mode == Mode.Touch ? .09f : Mathf.Max(.1f, holdTime));
                value = Mathf.Max(value, Mathf.Clamp01(progress));
                if (progress >= 1)
                {
                    contact.used = true;
                    Fire(target);
                }
            }
            else if (!Near(target, .025f)) contact.time = 0;
            contact.point = target.position;
        }

        void OnDisable() { hand = null; value = held = turn = 0; left = right = default; }
    }
}

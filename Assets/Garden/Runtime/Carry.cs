using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Carry : PlaygroundTarget
    {
        public Transform model;
        public bool cup;
        public bool meal;
        public bool dinner;
        public bool offered;
        public bool Held => hand;
        public bool Eaten => consumed;
        Transform hand;
        Loop loop;
        Rigidbody body;
        Collider[] shapes;
        bool[] shapeStates;
        Renderer[] visuals;
        bool[] visualStates;
        Vector3 home;
        Quaternion rotation;
        Vector3 grip, lastPosition, velocity, spin;
        Quaternion gripRotation, lastRotation;
        float sampled, bite;
        float released = -1;
        int day;
        bool consumed;
        public override bool UsesTrackedHandForGrab => true;

        void Awake()
        {
            if (!model) model = transform;
            home = transform.position;
            rotation = transform.rotation;
            shapes = GetComponentsInChildren<Collider>(true);
            shapeStates = new bool[shapes.Length];
            for (int i = 0; i < shapes.Length; i++) shapeStates[i] = shapes[i].enabled;
            visuals = GetComponentsInChildren<Renderer>(true);
            visualStates = new bool[visuals.Length];
            for (int i = 0; i < visuals.Length; i++) visualStates[i] = visuals[i].enabled;
            body = GetComponent<Rigidbody>();
            if (!body) body = gameObject.AddComponent<Rigidbody>();
            body.mass = .2f;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            loop = Loop.Instance;
        }

        void OnEnable()
        {
            if (!loop) loop = Loop.Instance;
            if (loop) loop.Changed += Refresh;
        }

        void Start() { Refresh(); }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            hand = null;
            bite = 0;
        }

        public override void BeginInteraction(Transform value)
        {
            if (Session.IsOpen || !value || hand || consumed || !body) return;
            if (dinner && (!loop || !loop.CanUse("EatDinner"))) return;
            if (offered && (!loop || !loop.state.outside)) return;
            float gap = Vector3.Distance(value.position, transform.position);
            foreach (var shape in shapes)
                if (shape && shape.enabled) gap = Mathf.Min(gap, Vector3.Distance(value.position, shape.ClosestPoint(value.position)));
            if (gap > .45f) return;
            StopBody();
            released = -1;
            hand = value;
            grip = hand.InverseTransformPoint(transform.position);
            gripRotation = Quaternion.Inverse(hand.rotation) * transform.rotation;
            velocity = spin = Vector3.zero;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            sampled = Time.unscaledTime;
            bite = 0;
            if (cup && loop && !loop.state.cup) loop.Cup();
        }

        public override void BeginInteraction(Transform value, Vector3 selectionPoint) { BeginInteraction(value); }

        public override void UpdateInteraction(Transform value)
        {
            if (hand == value && value) Follow();
        }

        public override void EndInteraction(Transform value)
        {
            if (hand != value || !hand) return;
            Drop();
        }

        void Follow()
        {
            if (!hand) return;
            var position = hand.TransformPoint(grip);
            var facing = hand.rotation * gripRotation;
            float step = Time.unscaledTime - sampled;
            if (step > .0001f)
            {
                var delta = position - lastPosition;
                velocity = delta.sqrMagnitude > 1 ? Vector3.zero : Vector3.ClampMagnitude(delta / step, 3.5f);
                var turn = facing * Quaternion.Inverse(lastRotation);
                turn.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180) angle -= 360;
                spin = Mathf.Abs(angle) < .01f || delta.sqrMagnitude > 1 ? Vector3.zero : Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / step), 12);
                lastPosition = position;
                lastRotation = facing;
                sampled = Time.unscaledTime;
            }
            body.position = position;
            body.rotation = facing;
            transform.SetPositionAndRotation(position, facing);
        }

        void Drop()
        {
            hand = null;
            bite = 0;
            released = Time.unscaledTime;
            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Time.unscaledTime - sampled < .15f ? velocity : Vector3.zero;
            body.angularVelocity = Time.unscaledTime - sampled < .15f ? spin : Vector3.zero;
        }

        void StopBody()
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
        }

        void ResetHome()
        {
            hand = null;
            bite = 0;
            released = -1;
            StopBody();
            body.position = home;
            body.rotation = rotation;
            transform.SetPositionAndRotation(home, rotation);
        }

        void Refresh()
        {
            if (!loop || !meal && !dinner) return;
            if (day != 0 && day != loop.state.day) ResetHome();
            day = loop.state.day;
            bool eaten = dinner ? loop.state.dinnerEaten : loop.state.meal;
            SetConsumed(eaten);
        }

        void SetConsumed(bool eaten)
        {
            if (consumed == eaten) return;
            consumed = eaten;
            if (consumed) { hand = null; StopBody(); }
            if (model && model != transform) model.gameObject.SetActive(!consumed);
            else for (int i = 0; i < visuals.Length; i++) if (visuals[i]) visuals[i].enabled = !consumed && visualStates[i];
            for (int i = 0; i < shapes.Length; i++) if (shapes[i]) shapes[i].enabled = !consumed && shapeStates[i];
            bite = 0;
        }

        void Update()
        {
            if (hand)
            {
                if (!hand.gameObject.activeInHierarchy) { Drop(); return; }
                Follow();
                var head = RealityPlayer.Head;
                bool canEat = !Session.IsOpen && loop && (offered ? loop.state.outside : loop.CanUse(dinner ? "EatDinner" : "Meal"));
                if ((meal || dinner || offered) && !consumed && canEat && head && Vector3.Distance(model.position, head.position) <= .28f)
                {
                    bite += Time.unscaledDeltaTime;
                    if (bite >= .35f)
                    {
                        if (offered) SetConsumed(true);
                        else if (dinner) loop.EatDinner();
                        else loop.Meal();
                    }
                }
                else bite = 0;
                return;
            }
            if (!consumed && (transform.position.y < home.y - 3 || (transform.position - home).sqrMagnitude > 400)) ResetHome();
            else if (!consumed && (meal || dinner || offered) && released >= 0 && Time.unscaledTime - released > 5
                && (transform.position.y < home.y - .45f || (transform.position - home).sqrMagnitude > 9)) ResetHome();
        }
    }
}

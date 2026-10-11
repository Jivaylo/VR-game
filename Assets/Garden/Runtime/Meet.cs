using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Meet : MonoBehaviour
    {
        public Loop loop;
        public string action = "Mara";
        public Transform face;
        public float distance = 2.1f;
        public Vector3 forward = Vector3.back;
        public bool OncePerVisit { get; private set; }
        Quaternion resting;
        float attention, next;
        int spoken = int.MinValue;

        void Start()
        {
            if (!loop) loop = Loop.Instance;
            if (face) resting = face.localRotation;
        }

        void Update()
        {
            if (!loop) loop = Loop.Instance;
            var head = RealityPlayer.Head;
            if (!loop || !head) return;
            Vector3 target = face ? face.position : transform.position + Vector3.up * 1.5f;
            Vector3 offset = target - head.position;
            float range = offset.magnitude;
            Turn(head, range);
            if (range > distance + 1.2f) OncePerVisit = false;
            int context = Context();
            bool ready = !loop.Busy && (!OncePerVisit || action == "Mara") && loop.CanUse(action) && context != spoken && Time.unscaledTime >= next;
            if (!ready || range > distance || Vector3.Dot(head.forward, offset.normalized) < .88f
                || loop.talk && (loop.talk.voice && loop.talk.voice.isPlaying || loop.talk.board && loop.talk.board.gameObject.activeInHierarchy))
            {
                attention = 0;
                return;
            }
            if (Physics.Raycast(head.position, offset.normalized, out var hit, Mathf.Max(0, range - .12f), 1, QueryTriggerInteraction.Ignore)
                && hit.transform != transform && !hit.transform.IsChildOf(transform))
            {
                attention = 0;
                return;
            }
            attention += Time.unscaledDeltaTime;
            if (attention < .65f) return;
            spoken = context;
            OncePerVisit = true;
            next = Time.unscaledTime + 8;
            attention = 0;
            loop.Run(action);
        }

        int Context()
        {
            int value = loop.state.day * 16;
            if (action == "Mara")
            {
                if (loop.state.label) value += 1;
                if (loop.state.glove) value += 2;
                if (loop.state.local) value += 4;
            }
            else if (action == "Noor")
            {
                if (loop.state.cup) value += 1;
                if (loop.state.ended) value += 2;
            }
            return value;
        }

        void Turn(Transform head, float range)
        {
            if (!face) return;
            Quaternion pose = resting;
            if (range < distance + 1)
            {
                Vector3 toward = head.position - face.position;
                if (face.parent) toward = face.parent.InverseTransformDirection(toward);
                Vector3 facing = resting * forward;
                float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(facing, Vector3.up), Vector3.ProjectOnPlane(toward, Vector3.up), Vector3.up);
                pose = Quaternion.AngleAxis(Mathf.Clamp(yaw, -35, 35), Vector3.up) * resting;
            }
            face.localRotation = Quaternion.Slerp(face.localRotation, pose, 1 - Mathf.Exp(-3 * Time.unscaledDeltaTime));
        }

        void OnDisable() { attention = 0; }
    }
}

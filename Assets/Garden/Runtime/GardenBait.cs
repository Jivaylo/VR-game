using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class GardenBait : MonoBehaviour
    {
        public float distance = 2.4f;
        public float height = -.05f;
        void OnEnable()
        {
            var head = RealityPlayer.Head;
            if (!head) return;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            float space = distance;
            if (Physics.Raycast(head.position, forward, out var hit, distance + .3f, 1, QueryTriggerInteraction.Ignore)) space = Mathf.Max(.9f, hit.distance - .2f);
            transform.SetPositionAndRotation(head.position + forward * space + Vector3.up * height, Quaternion.LookRotation(forward));
            transform.localScale = Vector3.one * Mathf.Min(2.2f, space * .78f);
        }
    }
}

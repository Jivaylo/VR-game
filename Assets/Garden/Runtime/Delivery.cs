using RealityPlayground;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Garden
{
    public sealed class Delivery : MonoBehaviour
    {
        public Loop loop;
        public Transform drone;
        public Transform landing;
        public GameObject package;
        Vector3 home;
        Quaternion facing;
        bool placed;
        int day;
        float elapsed = -1;

        void Awake()
        {
            if (!loop) loop = Loop.Instance;
            if (drone) { home = drone.position; facing = drone.rotation; }
            day = loop ? loop.state.day : 0;
            if (package)
            {
                package.layer = 8;
                var shape = package.GetComponent<Collider>();
                if (!shape) shape = package.AddComponent<BoxCollider>();
                var carry = package.GetComponent<Carry>();
                if (!carry) carry = package.AddComponent<Carry>();
                carry.dinner = true;
                var interactable = package.GetComponent<XRSimpleInteractable>();
                if (!interactable) interactable = package.AddComponent<XRSimpleInteractable>();
                interactable.colliders.Clear();
                interactable.colliders.Add(shape);
                if (!package.GetComponent<XRExperimentBridge>()) package.AddComponent<XRExperimentBridge>();
            }
        }

        void OnEnable() { if (loop) loop.Changed += Refresh; }
        void Start() { Refresh(); }
        void OnDisable() { if (loop) loop.Changed -= Refresh; }

        void Refresh()
        {
            if (!loop) return;
            if (day != loop.state.day)
            {
                day = loop.state.day;
                elapsed = -1;
                placed = false;
                if (drone) drone.SetPositionAndRotation(home, facing);
            }
            bool arrived = loop.state.dinner;
            if (arrived && !loop.state.dinnerReady && elapsed < 0) { elapsed = 0; placed = false; }
            if (!arrived) { elapsed = -1; placed = false; }
            if (loop.state.dinnerReady) placed = true;
            if (package) package.SetActive(arrived && placed && !loop.state.dinnerEaten);
            if (drone) drone.gameObject.SetActive(elapsed >= 0);
        }

        void Update()
        {
            if (elapsed < 0) return;
            elapsed += Time.deltaTime;
            float approach = Mathf.SmoothStep(0, 1, elapsed / 2.2f);
            float depart = Mathf.SmoothStep(0, 1, (elapsed - 4f) / 2f);
            if (drone && landing)
            {
                drone.position = Vector3.Lerp(home, landing.position, approach * (1 - depart));
                drone.rotation = facing * Quaternion.Euler(0, elapsed > 2.2f && elapsed < 4 ? Mathf.Sin(elapsed * 3) * 12 : 0, 0);
            }
            if (elapsed > 3f && !placed)
            {
                placed = true;
                if (package) package.SetActive(true);
                if (loop) { loop.DinnerArrived(); loop.Cue(.85f); }
            }
            if (elapsed > 6f) { if (drone) drone.gameObject.SetActive(false); elapsed = -1; }
        }
    }
}

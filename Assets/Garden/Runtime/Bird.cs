using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Bird : MonoBehaviour
    {
        public Loop loop;
        public Transform[] wings;
        public Vector3 flight = new Vector3(7, 5, 5);
        public AudioSource call;
        public Transform head;
        public bool Noticed => elapsed >= 0;
        Vector3 home;
        Quaternion facing;
        float elapsed = -1;
        float nextCall;

        void Awake()
        {
            if (!loop) loop = Loop.Instance;
            home = transform.localPosition;
            facing = transform.localRotation;
        }

        void Update()
        {
            if (!loop || !loop.state.outside) return;
            if (elapsed < 0)
            {
                if (Time.time > nextCall)
                {
                    nextCall = Time.time + 3.4f + Mathf.Abs(Mathf.Sin(Time.time * .43f)) * 4.2f;
                    if (call) { call.pitch = .97f + Mathf.Sin(Time.time) * .05f; call.Play(); }
                }
                if (head) head.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 1.13f) * 35, 0);
                if (!RealityPlayer.Head || Vector3.Distance(RealityPlayer.Head.position, transform.position) > 3.7f) return;
                elapsed = 0;
                if (call) call.Stop();
            }
            elapsed += Time.deltaTime;
            if (elapsed < 2.5f)
            {
                transform.localRotation = facing * Quaternion.Euler(0, Mathf.Sin(elapsed * 3) * 20, 0);
                return;
            }
            float t = Mathf.Clamp01((elapsed - 2.5f) / 5f);
            transform.localPosition = home + flight * t + Vector3.up * Mathf.Sin(t * Mathf.PI) * .6f;
            transform.localRotation = Quaternion.LookRotation(flight.normalized, Vector3.up);
            if (wings != null)
                for (int i = 0; i < wings.Length; i++)
                    if (wings[i]) wings[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 24) * 48 * (i == 0 ? 1 : -1));
            if (t >= 1) gameObject.SetActive(false);
        }

        void OnDisable() { if (call) call.Stop(); }
    }
}

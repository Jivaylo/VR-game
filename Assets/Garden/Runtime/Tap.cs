using UnityEngine;

namespace Garden
{
    public sealed class Tap : MonoBehaviour
    {
        public Loop loop;
        public GameObject stream;
        Vector3 size;
        float until;
        bool bound;

        void Awake()
        {
            if (stream) { size = stream.transform.localScale; stream.SetActive(false); }
        }

        void OnEnable() { Bind(); }
        void Start() { Bind(); }

        void Bind()
        {
            if (bound) return;
            if (!loop) loop = Loop.Instance;
            if (!loop) return;
            loop.Washed += Flow;
            bound = true;
        }

        void Flow()
        {
            until = Time.unscaledTime + 3;
            if (stream) stream.SetActive(true);
        }

        void Update()
        {
            if (!bound) Bind();
            if (!stream || !stream.activeSelf) return;
            float remaining = until - Time.unscaledTime;
            if (remaining <= 0) { stream.SetActive(false); stream.transform.localScale = size; return; }
            float edge = Mathf.Clamp01(Mathf.Min(remaining, 3 - remaining) / .15f);
            float width = Mathf.Lerp(.12f, 1, edge) * (1 + Mathf.Sin(Time.unscaledTime * 14) * .035f);
            stream.transform.localScale = new Vector3(size.x * width, size.y, size.z * width);
        }

        void OnDisable()
        {
            if (bound && loop) loop.Washed -= Flow;
            bound = false;
            until = 0;
            if (stream) { stream.SetActive(false); stream.transform.localScale = size; }
        }
    }
}

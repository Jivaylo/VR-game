using UnityEngine;

namespace Garden
{
    public sealed class Water : MonoBehaviour
    {
        public Loop loop;
        public Transform spout;
        public Transform soil;
        public Transform[] drops;
        float until;

        void Awake() { if (!loop) loop = Loop.Instance; }

        void OnEnable() { if (loop) loop.Watered += Pour; }

        public void Pour()
        {
            if (loop && loop.state.outside) until = Time.time + 2.4f;
        }

        void Update()
        {
            if (drops == null || !spout || !soil) return;
            bool active = Time.time < until;
            for (int i = 0; i < drops.Length; i++)
            {
                if (!drops[i]) continue;
                drops[i].gameObject.SetActive(active);
                if (!active) continue;
                float t = Mathf.Repeat(Time.time * 1.8f + i / (float)drops.Length, 1);
                drops[i].position = Vector3.Lerp(spout.position, soil.position, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .12f;
            }
        }

        void OnDisable()
        {
            if (loop) loop.Watered -= Pour;
            if (drops != null) foreach (var drop in drops) if (drop) drop.gameObject.SetActive(false);
        }
    }
}

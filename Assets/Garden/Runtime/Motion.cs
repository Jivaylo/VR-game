using UnityEngine;

namespace Garden
{
    public sealed class Motion : MonoBehaviour
    {
        public Vector3 spin = new Vector3(0, 18, 0);
        public float bob = .06f;
        public float rate = .8f;
        public AudioSource voice;
        public float response = .3f;

        Vector3 origin;
        Vector3 size;
        Quaternion facing;
        readonly float[] samples = new float[64];
        float strength;
        float sampleAt;
        float elapsed;
        bool ready;

        void Awake()
        {
            origin = transform.localPosition;
            size = transform.localScale;
            facing = transform.localRotation;
            ready = true;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            if (voice && voice.isPlaying && Time.unscaledTime >= sampleAt)
            {
                voice.GetOutputData(samples, 0);
                float energy = 0;
                for (int i = 0; i < samples.Length; i++) energy += samples[i] * samples[i];
                strength = Mathf.Clamp01(Mathf.Sqrt(energy / samples.Length) * 14);
                sampleAt = Time.unscaledTime + .05f;
            }
            else if (!voice || !voice.isPlaying) strength = Mathf.MoveTowards(strength, 0, Time.deltaTime * 3);
            transform.localPosition = origin + Vector3.up * (Mathf.Sin(elapsed * rate * Mathf.PI * 2) * bob);
            transform.localRotation = facing * Quaternion.Euler(spin * elapsed);
            transform.localScale = Vector3.Lerp(transform.localScale, size * (1 + strength * response), 1 - Mathf.Exp(-Time.deltaTime * 12));
        }

        void OnDisable()
        {
            if (!ready) return;
            transform.localPosition = origin;
            transform.localRotation = facing;
            transform.localScale = size;
        }
    }
}

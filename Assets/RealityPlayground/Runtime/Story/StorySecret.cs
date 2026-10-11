using UnityEngine;

namespace RealityPlayground.Story
{

    public sealed class StorySecret : PlaygroundTarget
    {
        public StoryEnvironment environment;
        public Transform lid, discovery;
        public TextMesh instruction;
        public string revealedText;
        [Range(0, 1)] public float minimumReveal;
        public int soundVariation;
        public bool Discovered { get; private set; }
        public override bool SupportsTriggerActivation => true;
        public Vector3 AimWorldPoint => interactionVolume ? interactionVolume.bounds.center : transform.position;
        [SerializeField] Collider interactionVolume;
        Quaternion lidRest;
        Vector3 contentScale;
        AudioSource source;
        AudioClip discoverySound;
        float opening, nextTouch;

        public void Configure(Collider volume)
        {
            interactionVolume = volume;
            if (discovery) discovery.localScale = Vector3.zero;
        }

        void Awake()
        {
            lidRest = lid ? lid.localRotation : Quaternion.identity;
            contentScale = Vector3.one;
            if (discovery) discovery.localScale = Vector3.zero;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = .6f;
            source.maxDistance = 7;
            source.volume = .55f;
            discoverySound = MakeDiscoverySound(soundVariation);
        }

        public override void Activate()
        {
            if (Discovered || Time.unscaledTime < nextTouch || !Available()) return;
            nextTouch = Time.unscaledTime + .7f;
            Discovered = true;
            if (instruction) instruction.text = revealedText;
            if (source && discoverySound) source.PlayOneShot(discoverySound);
        }

        bool Available() => !environment || environment.RevealAmount + .001f >= minimumReveal;

        void Update()
        {
            bool available = Available();
            if (interactionVolume) interactionVolume.enabled = available;
            if (instruction) instruction.gameObject.SetActive(available);
            if (available && !Discovered && RealityPlayer.IsXR && Time.unscaledTime >= nextTouch)
            {
                if (Touches(RealityPlayer.LeftHand) || Touches(RealityPlayer.RightHand)) Activate();
            }
            opening = Mathf.MoveTowards(opening, Discovered ? 1 : 0, Time.deltaTime * .85f);
            float eased = opening * opening * (3 - 2 * opening);
            if (lid) lid.localRotation = lidRest * Quaternion.Euler(105 * eased, 0, 0);
            if (discovery)
            {
                discovery.localScale = contentScale * eased;

                discovery.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * .35f) * 9, 0);
            }
        }

        bool Touches(Transform hand)
            => hand && interactionVolume && Vector3.Distance(interactionVolume.ClosestPoint(hand.position), hand.position) < .045f;

        void OnDestroy() { if (discoverySound) Destroy(discoverySound); }

        static AudioClip MakeDiscoverySound(int variation)
        {
            const int rate = 24000;
            const float duration = 3.4f;
            var samples = new float[(int)(duration * rate)];
            float[] notes = variation == 0 ? new[] { 523.25f, 659.25f, 783.99f, 587.33f } : new[] { 220f, 329.63f, 440f, 554.37f };
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float value = 0;
                for (int n = 0; n < notes.Length; n++)
                {
                    float age = t - n * .28f;
                    if (age < 0) continue;
                    float env = Mathf.Min(age * 110, 1) * Mathf.Exp(-age * 2.2f);
                    value += (Mathf.Sin(age * notes[n] * Mathf.PI * 2) + .22f * Mathf.Sin(age * notes[n] * 6.006f * Mathf.PI)) * env * .13f;
                }
                if (variation == 2)
                {
                    float age = t - 1.3f;
                    if (age > 0 && age < 1.2f)
                    {
                        float chirp = Mathf.Pow(Mathf.Max(0, Mathf.Sin(age * 14)), 4) * Mathf.Sin(age * Mathf.PI * 2 * (1500 + 130 * Mathf.Sin(age * 11)));
                        value += chirp * .08f;
                    }
                }
                samples[i] = Mathf.Clamp(value, -.8f, .8f) * Mathf.Clamp01((duration - t) * 3);
            }
            var clip = AudioClip.Create("Memory " + variation, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

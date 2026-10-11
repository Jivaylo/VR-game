using UnityEngine;
using UnityEngine.Events;

namespace RealityPlayground.Story
{

    public sealed class StoryAngel : MonoBehaviour
    {
        public UnityEvent speechFinished = new UnityEvent();
        public AudioClip[] speechClips;
        public bool EncounterComplete { get; private set; }
        public bool EncounterStarted { get; private set; }
        public bool IsSpeaking => source && source.isPlaying;
        public int CurrentLine => currentLine;
        public float VoiceEnergy { get; private set; }
        public float AccentEnergy { get; private set; }
        public Renderer CoreRenderer => coreRenderer;
        [SerializeField, Range(0, 1)] float speechVolume = .94f;
        [SerializeField] Transform[] orbits, wings, eyes;
        [SerializeField] Transform core;
        [SerializeField] Renderer coreRenderer;
        [SerializeField] TextMesh speech;
        float age, encounterAge, nextLineTime;
        float motionAge, nextAudioSample, targetVoice, previousAudioLevel, corePhase;
        Vector3 restCorePosition, restCoreScale;
        Quaternion restCoreRotation;
        Transform[] feathers;
        Quaternion[] restFeatherRotations;
        readonly float[] audioSamples = new float[512];
        MaterialPropertyBlock coreProperties;
        Material runtimeCoreMaterial;
        bool anatomyReady;
        int currentLine = -1;
        AudioSource source;
        AudioClip voice;
        static readonly int VoiceId = Shader.PropertyToID("_Voice");
        static readonly int AccentId = Shader.PropertyToID("_Accent");
        static readonly int PhaseId = Shader.PropertyToID("_PhaseOffset");
        static readonly string[] Lines =
        {
            "Do not be afraid of what is real.",
            "We are the resistance.\nWe live between the filters.",
            "Not everything is as it seems.",
            "The mirror remembers.\nThe wall is hiding your way out."
        };

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Angel"); root.transform.SetParent(parent, false);
            var angel = root.AddComponent<StoryAngel>();
            var pale = GreyboxUtil.Material("Angel incandescent bone", new Color(.77f, 1, .92f), 3.5f);
            var gold = GreyboxUtil.Material("Angel impossible gold", new Color(1, .42f, .07f), 3);
            var dark = GreyboxUtil.Material("Angel black pupil", new Color(.008f, .012f, .014f));
            var pink = StoryVignetteGeometry.Hologram("Angel spectral feathers", new Color(.78f, .06f, 1, .62f), 2.7f);
            var cyan = StoryVignetteGeometry.Hologram("Angel spectral bones", new Color(.07f, .95f, 1, .64f), 2.5f);
            var body = StoryVignetteGeometry.Group("Orbiting anatomy", root.transform, new Vector3(0, 2.35f, 0));
            angel.core = StoryVignetteGeometry.Group("Core Pupil", body, Vector3.zero);
            var volumeShader = Shader.Find("RealityPlayground/StoryAngelCore");
            var volume = volumeShader ? new Material(volumeShader) { name = "Angel Core" } : cyan;
            if (volume.HasProperty("_Rupture")) volume.SetFloat("_Rupture", 1);
            if (volume.HasProperty("_Intensity")) volume.SetFloat("_Intensity", 2.2f);
            angel.coreRenderer = GreyboxUtil.Primitive("Core Interior", PrimitiveType.Cube, angel.core, Vector3.zero, Vector3.one * 1.1f, volume, false).GetComponent<Renderer>();
            GreyboxUtil.Primitive("Central eye", PrimitiveType.Sphere, angel.core, new Vector3(0, 0, -.29f), new Vector3(.43f, .27f, .14f), pale, false);
            GreyboxUtil.Primitive("Central pupil", PrimitiveType.Sphere, angel.core, new Vector3(0, 0, -.365f), new Vector3(.045f, .21f, .023f), dark, false);
            angel.orbits = new Transform[5];
            var allEyes = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < angel.orbits.Length; i++)
            {
                var orbit = StoryVignetteGeometry.Group("Impossible orbital ring " + i, body, Vector3.zero);
                angel.orbits[i] = orbit;
                var ring = StoryVignetteGeometry.Ring("Luminous orbit", orbit, .82f + i * .16f, .025f + i * .006f, i % 2 == 0 ? pale : gold, 100);
                for (int e = 0; e < 6 + i; e++)
                {
                    float a = e * Mathf.PI * 2 / (6 + i);
                    var eye = StoryVignetteGeometry.Group("Orbit eye " + e, orbit, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (.82f + i * .16f));
                    GreyboxUtil.Primitive("Eye white", PrimitiveType.Sphere, eye, Vector3.zero, new Vector3(.14f, .085f, .09f), pale, false);
                    GreyboxUtil.Primitive("Eye iris", PrimitiveType.Sphere, eye, new Vector3(0, 0, -.042f), new Vector3(.034f, .064f, .018f), dark, false);
                    allEyes.Add(eye);
                }
            }
            angel.eyes = allEyes.ToArray(); angel.wings = new Transform[6];
            for (int i = 0; i < angel.wings.Length; i++)
            {
                float a = i * Mathf.PI * 2 / angel.wings.Length;
                var wing = StoryVignetteGeometry.Group("Fractured wing " + i, body, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .5f);
                wing.localRotation = Quaternion.Euler(0, i % 2 == 0 ? 17 : -17, a * Mathf.Rad2Deg - 90);
                angel.wings[i] = wing;
                StoryVignetteGeometry.Rod("Wing spar", wing, Vector3.zero, new Vector3(.3f, 1.55f, .05f), .035f, gold);
                for (int j = 0; j < 9; j++)
                {
                    var feather = GreyboxUtil.Primitive("Impossible feather " + j, PrimitiveType.Cube, wing,
                        new Vector3(j * .067f, .36f + j * .125f, -.04f + j * .015f), new Vector3(.055f, .52f + j * .024f, .08f), j % 2 == 0 ? pink : cyan, false).transform;
                    feather.localRotation = Quaternion.Euler(j * 4, 20 + j * 11, -18 - j * 4);
                }
            }
            var triangle = new Vector3[] { new Vector3(0, .68f, 0), new Vector3(.59f, -.34f, 0), new Vector3(-.59f, -.34f, 0) };
            for (int i = 0; i < triangle.Length; i++) StoryVignetteGeometry.Rod("Angel Center", angel.core, triangle[i], triangle[(i + 1) % triangle.Length], .046f, gold);
            angel.speech = GreyboxUtil.Label("", root.transform, new Vector3(0, .64f, -.72f), .15f, new Color(.82f, 1, .9f));
            return root;
        }

        public void ConfigureReactiveAnatomy(Material material = null)
        {
            if (!core)
            {
                var body = ObjectNames.Find(transform, "Orbiting anatomy");
                if (body) core = ObjectNames.Find(body, "Core Pupil");
            }
            if (!coreRenderer && core)
            {
                var interior = ObjectNames.Find(core, "Core Interior");
                if (interior) coreRenderer = interior.GetComponent<Renderer>();
            }
            if (coreRenderer && material) coreRenderer.sharedMaterial = material;
            anatomyReady = false;
        }

        void Start() => CacheAnatomy();

        void CacheAnatomy()
        {
            ConfigureReactiveAnatomy();
            if (core)
            {
                restCorePosition = core.localPosition;
                restCoreScale = core.localScale;
                restCoreRotation = core.localRotation;
            }

            var shader = Shader.Find("RealityPlayground/StoryAngelCore");
            if (coreRenderer && shader && (!coreRenderer.sharedMaterial || coreRenderer.sharedMaterial.shader != shader))
            {
                runtimeCoreMaterial = new Material(shader) { name = "Angel Core" };
                runtimeCoreMaterial.SetFloat("_Intensity", 2.2f);
                coreRenderer.sharedMaterial = runtimeCoreMaterial;
            }
            coreProperties = new MaterialPropertyBlock();
            if (coreRenderer) coreRenderer.GetPropertyBlock(coreProperties);
            var found = new System.Collections.Generic.List<Transform>(54);
            if (wings != null) foreach (var wing in wings)
            {
                if (!wing) continue;
                for (int i = 0; i < wing.childCount; i++)
                {
                    var child = wing.GetChild(i);
                    if (ObjectNames.StartsWith(child.name, "Impossible feather ", System.StringComparison.Ordinal)) found.Add(child);
                }
            }
            feathers = found.ToArray(); restFeatherRotations = new Quaternion[feathers.Length];
            for (int i = 0; i < feathers.Length; i++) restFeatherRotations[i] = feathers[i].localRotation;
            anatomyReady = true;
        }

        void UpdateVoiceReaction(float deltaTime)
        {

            if (Time.unscaledTime >= nextAudioSample)
            {
                nextAudioSample = Time.unscaledTime + 1f / 30f;
                float level = 0, peak = 0;
                if (source && source.isPlaying)
                {
                    source.GetOutputData(audioSamples, 0);
                    for (int i = 0; i < audioSamples.Length; i++)
                    {
                        float sample = audioSamples[i];
                        level += sample * sample;
                        peak = Mathf.Max(peak, Mathf.Abs(sample));
                    }
                    level = Mathf.Sqrt(level / audioSamples.Length);
                }
                targetVoice = Mathf.Clamp01(level * 5.2f + peak * .14f - .018f);
                float onset = Mathf.Max(0, targetVoice - previousAudioLevel);
                AccentEnergy = Mathf.Max(AccentEnergy, Mathf.Clamp01(onset * 3.1f));
                previousAudioLevel = targetVoice;
            }
            float response = targetVoice > VoiceEnergy ? 19 : 5.5f;
            VoiceEnergy = Mathf.Lerp(VoiceEnergy, targetVoice, 1 - Mathf.Exp(-deltaTime * response));
            AccentEnergy *= Mathf.Exp(-deltaTime * 7.5f);
        }

        public void BeginEncounter()
        {
            if (EncounterStarted || EncounterComplete) return;
            EncounterStarted = true; encounterAge = 0; currentLine = -1;
            if (!source)
            {
                var mouth = new GameObject("AngelVoice");
                mouth.transform.SetParent(transform, false); mouth.transform.localPosition = new Vector3(0, 2.1f, 0);
                source = mouth.AddComponent<AudioSource>(); source.playOnAwake = false;

                source.spatialBlend = .72f; source.spread = 115;
                source.minDistance = 7; source.maxDistance = 35;
                source.dopplerLevel = 0; source.priority = 8; source.volume = speechVolume;
            }
            ShowLine(0);
        }
        void Update()
        {
            if (!anatomyReady) CacheAnatomy();
            UpdateVoiceReaction(Time.deltaTime);
            age += Time.deltaTime;
            motionAge += Time.deltaTime * (1 + VoiceEnergy * .38f + AccentEnergy * .6f);
            float motion = motionAge;
            if (orbits != null) for (int i = 0; i < orbits.Length; i++)
                if (orbits[i]) orbits[i].localRotation = Quaternion.Euler(motion * (24 + i * 9) + i * 27, motion * (i % 2 == 0 ? 39 : -49) + i * 43, motion * (28 + i * 7));
            if (core)
            {
                float breath = 1 + Mathf.Sin(age * 1.7f) * .025f + VoiceEnergy * .28f + AccentEnergy * .085f;
                core.localScale = restCoreScale * breath;
                core.localRotation = restCoreRotation * Quaternion.Euler(Mathf.Sin(motion * .87f) * 24, motion * 36, motion * 22 + Mathf.Sin(motion * 1.17f) * 20);

                core.localPosition = restCorePosition + new Vector3(Mathf.Sin(age * 37), Mathf.Sin(age * 29 + .7f), Mathf.Cos(age * 31)) * (AccentEnergy * .028f);
            }
            if (coreRenderer)
            {
                corePhase += Time.deltaTime * (VoiceEnergy * 2.4f + AccentEnergy * 3.8f);
                coreProperties.SetFloat(VoiceId, VoiceEnergy);
                coreProperties.SetFloat(AccentId, AccentEnergy);
                coreProperties.SetFloat(PhaseId, corePhase);
                coreRenderer.SetPropertyBlock(coreProperties);
            }
            if (wings != null) for (int i = 0; i < wings.Length; i++)
                if (wings[i]) wings[i].localRotation = Quaternion.Euler(Mathf.Sin(motion * 1.65f + i) * (29 + VoiceEnergy * 9), Mathf.Cos(motion * 1.37f + i) * 38, i * 60 - 90 + motion * 16 + Mathf.Sin(motion * 1.54f + i) * 17);
            for (int i = 0; i < feathers.Length; i++)
                if (feathers[i]) feathers[i].localRotation = restFeatherRotations[i] * Quaternion.Euler(Mathf.Sin(motion * 2.7f - i * .55f) * (12 + VoiceEnergy * 13), Mathf.Cos(motion * 2.1f - i * .4f) * 14, Mathf.Sin(motion * 2.3f - i * .61f) * 8);
            if (RealityPlayer.Head && eyes != null) foreach (var eye in eyes)
                if (eye && Vector3.Distance(eye.position, RealityPlayer.Head.position) > .01f) eye.rotation = Quaternion.LookRotation(eye.position - RealityPlayer.Head.position, Vector3.up);
            if (!EncounterStarted || EncounterComplete) return;
            encounterAge += Time.deltaTime;
            if (encounterAge >= nextLineTime && currentLine < Lines.Length - 1) ShowLine(currentLine + 1);
            if (currentLine == Lines.Length - 1 && encounterAge >= nextLineTime + .4f)
            {
                EncounterComplete = true; speechFinished.Invoke();
            }
        }
        void ShowLine(int index)
        {
            if (index == currentLine) return; currentLine = index;
            if (speech) speech.text = Lines[index];
            AudioClip spoken = speechClips != null && index < speechClips.Length ? speechClips[index] : null;
            nextLineTime = encounterAge + Mathf.Max(3.4f, spoken ? spoken.length + .65f : 3.4f);
            if (!spoken && !voice) voice = StoryVignetteGeometry.Tone("Resistance harmonic speech", 2.2f, false);
            if (source && (spoken || voice))
            {
                source.Stop(); source.pitch = spoken ? 1 : .76f + index * .06f;
                source.clip = spoken ? spoken : voice; source.volume = speechVolume; source.Play();
            }
        }
        void OnDisable()
        {
            if (source) source.Stop();
            VoiceEnergy = AccentEnergy = targetVoice = previousAudioLevel = 0;
        }
        void OnDestroy()
        {
            if (voice) Destroy(voice);
            if (runtimeCoreMaterial) Destroy(runtimeCoreMaterial);
        }
    }
}

using System;
using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class SignalForm : MonoBehaviour
    {
        public AudioSource voice;
        public Transform core;
        public Renderer field;
        public Transform[] rings = Array.Empty<Transform>();
        public Transform[] wings = Array.Empty<Transform>();
        Vector3 coreScale;
        Quaternion[] poses;
        Renderer[] surfaces;
        bool[] states;
        bool near = true;
        float next, sampleAt, level, age;
        readonly float[] samples = new float[64];
        MaterialPropertyBlock properties;

        void Awake()
        {
            if (core) coreScale = core.localScale;
            poses = new Quaternion[wings.Length];
            for (int i = 0; i < wings.Length; i++) if (wings[i]) poses[i] = wings[i].localRotation;
            surfaces = GetComponentsInChildren<Renderer>(true);
            states = new bool[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++) states[i] = surfaces[i].enabled;
            properties = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (Time.unscaledTime > next)
            {
                next = Time.unscaledTime + .3f;
                bool active = RealityPlayer.Head && (RealityPlayer.Head.position - transform.position).sqrMagnitude < 400;
                if (active != near)
                {
                    near = active;
                    for (int i = 0; i < surfaces.Length; i++) if (surfaces[i]) surfaces[i].enabled = active && states[i];
                }
            }
            if (!near) return;
            if (Time.unscaledTime > sampleAt)
            {
                sampleAt = Time.unscaledTime + .05f;
                float energy = 0;
                if (voice && voice.isPlaying)
                {
                    voice.GetOutputData(samples, 0);
                    for (int i = 0; i < samples.Length; i++) energy += samples[i] * samples[i];
                }
                level = Mathf.Lerp(level, Mathf.Clamp01(Mathf.Sqrt(energy / samples.Length) * 9), .6f);
            }
            age += Time.deltaTime * (1 + level * .8f);
            for (int i = 0; i < rings.Length; i++)
                if (rings[i]) rings[i].localRotation = Quaternion.Euler(age * (24 + i * 9) + i * 27, age * (i % 2 == 0 ? 39 : -49) + i * 43, age * (28 + i * 7));
            for (int i = 0; i < wings.Length; i++)
                if (wings[i]) wings[i].localRotation = poses[i] * Quaternion.Euler(Mathf.Sin(age * 1.8f + i) * (9 + level * 14), Mathf.Sin(age + i) * 15, Mathf.Sin(age * 1.4f + i) * 12);
            if (core)
            {
                core.localScale = coreScale * (1 + level * .23f + Mathf.Sin(age * 2) * .02f);
                core.localRotation = Quaternion.Euler(Mathf.Sin(age) * 18, age * 36, age * 22);
            }
            if (field)
            {
                properties.SetFloat("_Voice", level);
                properties.SetFloat("_Accent", level * .4f);
                properties.SetFloat("_PhaseOffset", age * .11f);
                field.SetPropertyBlock(properties);
            }
        }
    }
}

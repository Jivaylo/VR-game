using System;
using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Holo : MonoBehaviour
    {
        [Serializable]
        public sealed class Part
        {
            public Transform target;
            public Vector3 spin;
            public float lift;
            public float rate = 1;
            public float phase;
            [NonSerialized] public Vector3 position;
            [NonSerialized] public Quaternion rotation;
        }

        public Part[] parts = Array.Empty<Part>();
        public float distance = 26;
        Renderer[] surfaces;
        bool[] enabledStates;
        bool visible = true;
        float nextCheck;

        void Awake()
        {
            surfaces = GetComponentsInChildren<Renderer>(true);
            enabledStates = new bool[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++) enabledStates[i] = surfaces[i].enabled;
            foreach (var part in parts)
            {
                if (!part.target) continue;
                part.position = part.target.localPosition;
                part.rotation = part.target.localRotation;
            }
        }

        void Update()
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + .4f;
                var head = RealityPlayer.Head;
                bool near = !head || (head.position - transform.position).sqrMagnitude < distance * distance;
                if (near != visible)
                {
                    visible = near;
                    for (int i = 0; i < surfaces.Length; i++) if (surfaces[i]) surfaces[i].enabled = near && enabledStates[i];
                }
            }
            if (!visible) return;
            float time = Time.time;
            foreach (var part in parts)
            {
                if (!part.target) continue;
                part.target.localPosition = part.position + Vector3.up * (Mathf.Sin(time * part.rate + part.phase) * part.lift);
                part.target.localRotation = part.rotation * Quaternion.Euler(part.spin * time);
            }
        }
    }
}

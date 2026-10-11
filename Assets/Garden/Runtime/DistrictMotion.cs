using System;
using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class DistrictMotion : MonoBehaviour
    {
        [Serializable]
        public sealed class Part
        {
            public Transform target;
            public Vector3 spin;
            public float lift;
            public float pulse;
            public float rate = 1;
            public float phase;
            [NonSerialized] public Vector3 position;
            [NonSerialized] public Vector3 scale;
            [NonSerialized] public Quaternion rotation;
        }

        [Serializable]
        public sealed class Group
        {
            public Transform anchor;
            public Renderer[] surfaces = Array.Empty<Renderer>();
            public Part[] parts = Array.Empty<Part>();
            public float range = 30;
            [NonSerialized] public bool visible = true;
            [NonSerialized] public bool[] enabled;
        }

        public Group[] groups = Array.Empty<Group>();
        float nextCheck;

        void Awake()
        {
            foreach (var group in groups)
            {
                group.enabled = new bool[group.surfaces.Length];
                for (int i = 0; i < group.surfaces.Length; i++)
                    group.enabled[i] = group.surfaces[i] && group.surfaces[i].enabled;
                foreach (var part in group.parts)
                {
                    if (!part.target) continue;
                    part.position = part.target.localPosition;
                    part.rotation = part.target.localRotation;
                    part.scale = part.target.localScale;
                }
            }
        }

        void Update()
        {
            bool check = Time.unscaledTime >= nextCheck;
            if (check) nextCheck = Time.unscaledTime + .35f;
            var head = RealityPlayer.Head;
            float time = Time.time;
            foreach (var group in groups)
            {
                if (!group.anchor) continue;
                if (check)
                {
                    bool visible = !head || (head.position - group.anchor.position).sqrMagnitude <= group.range * group.range;
                    if (visible != group.visible)
                    {
                        group.visible = visible;
                        for (int i = 0; i < group.surfaces.Length; i++)
                            if (group.surfaces[i]) group.surfaces[i].enabled = visible && group.enabled[i];
                    }
                }
                if (!group.visible) continue;
                foreach (var part in group.parts)
                {
                    if (!part.target) continue;
                    float wave = Mathf.Sin(time * part.rate + part.phase);
                    part.target.localPosition = part.position + Vector3.up * wave * part.lift;
                    part.target.localRotation = part.rotation * Quaternion.Euler(part.spin * time);
                    if (part.pulse != 0) part.target.localScale = part.scale * (1 + wave * part.pulse);
                }
            }
        }
    }
}

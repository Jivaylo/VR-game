using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Habitat : MonoBehaviour
    {
        public Loop loop;
        public AudioSource water;
        public Transform[] ribbons;
        public GameObject sky;
        public Transform exit;
        Quaternion[] rotations;

        void Awake()
        {
            if (!loop) loop = Loop.Instance;
            rotations = new Quaternion[ribbons == null ? 0 : ribbons.Length];
            for (int i = 0; i < rotations.Length; i++) if (ribbons[i]) rotations[i] = ribbons[i].localRotation;
        }

        void Update()
        {
            var head = RealityPlayer.Head;
            bool daylight = loop && loop.state.outside && head && exit && exit.InverseTransformPoint(head.position).x > -.35f;
            if (sky && sky.activeSelf != daylight) sky.SetActive(daylight);
            bool nearby = loop && loop.state.outside && head && (head.position - transform.position).sqrMagnitude < 484f;
            if (water)
            {
                water.volume = Mathf.MoveTowards(water.volume, nearby ? .20f : 0, Time.deltaTime * .14f);
                if (nearby && !water.isPlaying) water.Play();
                else if (!nearby && water.volume <= 0 && water.isPlaying) water.Stop();
            }
            if (!nearby) return;
            for (int i = 0; i < rotations.Length; i++)
                if (ribbons[i]) ribbons[i].localRotation = rotations[i] * Quaternion.Euler(Mathf.Sin(Time.time * 1.7f + i) * 5, 0, Mathf.Sin(Time.time * 1.13f + i * 2) * 8);
        }

        void OnDisable() { if (water) water.Stop(); if (sky) sky.SetActive(false); }
    }
}

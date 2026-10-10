using System;
using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Citizen : MonoBehaviour
    {
        public enum Task { Wait, Offer, Repair }
        [Serializable]
        public struct Arm
        {
            public Transform upper, lower, palm;
            public Vector3 shoulder;
        }
        public Task task;
        public Arm left, right;
        public Transform chest;
        public AudioSource foley;
        public float phase;
        Vector3 scale;
        float nextTap;

        void Awake() { if (chest) scale = chest.localScale; }

        void LateUpdate()
        {
            var head = RealityPlayer.Head;
            if (!head || (head.position - transform.position).sqrMagnitude > 625f) return;
            float time = Time.time + phase;
            float breath = Mathf.Sin(time * 1.25f) * .008f;
            if (chest) chest.localScale = Vector3.Scale(scale, new Vector3(1, 1 + breath, 1 + breath * .6f));
            Vector3 a = new Vector3(-.31f, .98f, -.17f);
            Vector3 b = new Vector3(.31f, .98f, -.17f);
            Vector3 u = new Vector3(-.31f, 1.15f, -.06f);
            Vector3 v = new Vector3(.31f, 1.15f, -.06f);
            if (task == Task.Repair)
            {
                float cycle = Mathf.Repeat(time, 3.6f);
                float strike = cycle < 1.7f ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(cycle * Mathf.PI * 2f)), 2) : 0;
                a = new Vector3(-.18f, .83f, -.55f);
                b = new Vector3(.18f, .85f + strike * .17f, -.56f + strike * .04f);
                u = new Vector3(-.32f, 1.07f, -.27f);
                v = new Vector3(.34f, 1.10f + strike * .05f, -.22f);
                if (cycle < 1.7f && strike < .04f && Time.time > nextTap)
                {
                    nextTap = Time.time + .4f;
                    if (foley) { foley.pitch = .93f + Mathf.Sin(time * 11.7f) * .06f; foley.Play(); }
                }
            }
            else if (task == Task.Offer)
            {
                a = new Vector3(-.16f, 1.05f + breath, -.35f);
                b = new Vector3(.18f, 1.05f + breath, -.35f);
                u = new Vector3(-.31f, 1.14f, -.12f);
                v = new Vector3(.31f, 1.14f, -.12f);
            }
            else
            {
                a.y += breath;
                b = new Vector3(.19f, 1.05f + breath, -.26f);
            }
            Pose(left, u, a);
            Pose(right, v, b);
        }

        void Pose(Arm arm, Vector3 elbow, Vector3 hand)
        {
            Segment(arm.upper, arm.shoulder, elbow, .14f);
            Segment(arm.lower, elbow, hand, .115f);
            if (arm.palm) arm.palm.SetLocalPositionAndRotation(hand, Quaternion.Euler(-15, 0, 0));
        }

        static void Segment(Transform part, Vector3 a, Vector3 b, float width)
        {
            if (!part) return;
            Vector3 direction = b - a;
            part.localPosition = (a + b) * .5f;
            part.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            part.localScale = new Vector3(width, direction.magnitude * .5f, width);
        }
    }
}

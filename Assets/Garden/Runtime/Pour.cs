using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Pour : MonoBehaviour
    {
        public Carry carry;
        public Water water;
        public Transform spout;
        public AudioSource sound;
        public bool Pouring { get; private set; }

        void Update()
        {
            var loop = Loop.Instance;
            bool ready = loop && loop.state.outside && carry && carry.Held && water && water.soil && spout;
            if (ready)
            {
                Vector3 gap = spout.position - water.soil.position;
                ready = Vector3.Dot(transform.up, Vector3.up) < .68f && gap.y > .06f && gap.y < .7f
                    && new Vector2(gap.x, gap.z).sqrMagnitude < .09f;
            }
            Pouring = ready;
            if (ready)
            {
                water.spout = spout;
                water.Pour();
            }
            if (!sound) return;
            if (ready && !sound.isPlaying) sound.Play();
            else if (!ready && sound.isPlaying) sound.Stop();
        }

        void OnDisable()
        {
            Pouring = false;
            if (sound) sound.Stop();
        }
    }
}

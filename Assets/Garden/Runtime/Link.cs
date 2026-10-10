using UnityEngine;

namespace Garden
{
    [DefaultExecutionOrder(-3000)]
    public sealed class Link : MonoBehaviour
    {
        void Awake()
        {
            var loop = Loop.Instance;
            if (!loop) return;
            foreach (var c in GetComponentsInChildren<Assembly>(true)) if (!c.loop) c.loop = loop;
            foreach (var c in GetComponentsInChildren<Gate>(true)) if (!c.loop) c.loop = loop;
            foreach (var c in GetComponentsInChildren<Hatch>(true)) if (!c.loop) c.loop = loop;
            foreach (var c in GetComponentsInChildren<Mirror>(true)) if (!c.loop) c.loop = loop;
            foreach (var c in GetComponentsInChildren<Bridge>(true)) if (!c.loop) c.loop = loop;
            foreach (var c in GetComponentsInChildren<Drone>(true)) if (!c.loop) c.loop = loop;
            foreach (var c in GetComponentsInChildren<Pulse>(true)) if (!c.loop) c.loop = loop;
            foreach (var c in GetComponentsInChildren<Poster>(true)) if (!c.loop) c.loop = loop;
            foreach (var c in GetComponentsInChildren<Train>(true)) if (!c.loop) c.loop = loop;
        }
    }
}

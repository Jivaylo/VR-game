using UnityEngine;

namespace Garden
{
    public sealed class CupStand : MonoBehaviour
    {
        public Loop loop;
        public GameObject cup;
        void Awake() { if (!loop) loop = Loop.Instance; }
        void OnEnable() { if (loop) loop.Changed += Refresh; }
        void Start() { Refresh(); }
        void OnDisable() { if (loop) loop.Changed -= Refresh; }
        void Refresh() { if (cup && loop) cup.SetActive(loop.state.cup && loop.state.outside); }
    }
}

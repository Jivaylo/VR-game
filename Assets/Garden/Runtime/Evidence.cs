using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Evidence : PlaygroundTarget
    {
        public Loop loop;
        public TMP_Text title;
        public GameObject[] slots;
        public int Selected { get; private set; }
        public override bool SupportsTriggerActivation => true;
        int mask = -1;
        float next;
        public override void Activate() { Next(); }
        public void Next()
        {
            if (!loop || !loop.state.glove || Time.unscaledTime < next) return;
            next = Time.unscaledTime + .3f;
            Selected = (Selected + 1) % 3;
            Refresh();
            loop.Cue(1.15f);
        }
        void OnEnable() { if (!loop) loop = Loop.Instance; if (loop) loop.Changed += Refresh; }
        void OnDisable() { if (loop) loop.Changed -= Refresh; }
        void Start() { Refresh(); }
        public void Refresh()
        {
            if (!loop || slots == null) return;
            var s = loop.state;
            int found = (s.metroClue || s.casing ? 1 : 0) | (s.calibrated ? 2 : 0) | (s.raw ? 4 : 0);
            if (mask >= 0 && found != mask)
                for (int i = 0; i < 3; i++) if ((found & 1 << i) != 0 && (mask & 1 << i) == 0) Selected = i;
            mask = found;
            for (int i = 0; i < slots.Length; i++) if (slots[i]) slots[i].SetActive(i == Selected && (found & 1 << i) != 0);
            if (title) title.text = (Selected + 1) + "/3  " + ((found & 1 << Selected) == 0 ? "NO RECORD" : Selected == 0 ? "ARM PATH" : Selected == 1 ? "SERVICE FRAME" : "EXTERIOR 07");
        }
    }
}

using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class NewsFeed : MonoBehaviour
    {
        public Loop loop;
        public Transform presenter, arm;
        public TMP_Text headline;
        public GameObject screen;
        Vector3 position;
        Quaternion pose;
        float started = -1;
        int day;
        void Start() { if (presenter) position = presenter.localPosition; if (arm) pose = arm.localRotation; }
        void Update()
        {
            if (!loop) return;
            if (day != loop.state.day) { day = loop.state.day; started = -1; }
            bool shown = !loop.state.local && loop.state.news && loop.Near(loop.home, 6);
            if (screen && screen.activeSelf != shown) screen.SetActive(shown);
            if (!shown) return;
            if (started < 0) started = Time.unscaledTime;
            float t = Mathf.Repeat(Time.unscaledTime - started, 18);
            float held = t > 9 && t < 9.6f ? 9 : t;
            if (presenter) presenter.localPosition = position + Vector3.up * (Mathf.Sin(held * .7f) * .006f);
            if (arm) arm.localRotation = pose * Quaternion.Euler(0,0,Mathf.Sin(held*.8f)*8);
            if (headline) headline.text = t < 10 ? "12 NEW HORIZONS\nMore room to be yourself" : "GARDEN DISTRICT 07\nExterior services unavailable";
        }
    }
}

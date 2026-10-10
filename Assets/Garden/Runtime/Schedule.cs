using System;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Schedule : MonoBehaviour
    {
        public enum Kind { Day, Receiver, Bed, Station }
        public Loop loop;
        public Kind kind;
        public TMP_Text title, detail;
        string previous;

        void Start() { Refresh(); }
        void OnEnable() { if (!loop) loop = Loop.Instance; if (loop) loop.Changed += Refresh; }
        void OnDisable() { if (loop) loop.Changed -= Refresh; }
        public void Refresh()
        {
            if (!loop) loop = Loop.Instance;
            if (!loop) return;
            var s = loop.state;
            if (kind == Kind.Bed)
                foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = !s.local && s.dinnerEaten;
            string heading = "CIVIC";
            string text;
            if (kind == Kind.Receiver)
            {
                heading = "EVENING SERVICE";
                text = s.local ? "Network service unavailable" : !s.work ? "Delivery opens after your shift" : !s.returnedHome ? "Ready when you return" : s.dinnerEaten ? "Container received\nTomorrow's menu is prepared" : s.dinnerReady ? "Meal delivered\nLift the container to eat" : s.dinner ? "Delivery approaching" : "Your evening meal\nPull to order";
            }
            else if (kind == Kind.Bed)
            {
                heading = "REST";
                text = s.local ? "Rest" : s.dinnerEaten ? "Tomorrow is prepared\n" + new DateTime(2056, 10, 7).AddDays(s.day).ToString("dd MMM").ToUpperInvariant() + "\n07:30 Commons  |  09:00 Assembly\n19:00 Evening meal  |  22:00 Rest" : "Rest follows your evening meal";
            }
            else if (kind == Kind.Station)
            {
                heading = "ASSEMBLY 07";
                text = s.local ? "Public production offline" : s.work ? "Shift complete\nReturn via the arcade" : s.casing ? "Service inspection\nAcknowledge at the station" : "CORRECT aligned parts\nINCORRECT damaged parts";
            }
            else
            {
                heading = "TODAY  " + new DateTime(2056, 10, 7).AddDays(s.day - 1).ToString("dd MMM").ToUpperInvariant();
                text = s.local ? "Presentation unavailable" : loop.Objective + "\n\n07:30  Commons\n09:00  Assembly\n19:00  Evening meal\n22:00  Rest";
            }
            string key = heading + text;
            if (key == previous) return;
            previous = key;
            if (title) title.text = heading;
            if (detail) detail.text = text;
        }
    }
}

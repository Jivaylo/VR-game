using TMPro;
using UnityEngine;
using RealityPlayground;

namespace Garden
{
    public sealed class Gate : MonoBehaviour
    {
        public Loop loop;
        public Transform selector;
        public Transform lever;
        public Transform wheel;
        public Transform bolt;
        public Transform door;
        public Transform actuator;
        public Transform needle;
        public Transform returnLatch;
        public TMP_Text returnDisplay;
        public Collider barrier;
        public TMP_Text display;
        public TMP_Text gauge;
        public Vector3 selectorAxis = Vector3.forward;
        public Vector3 leverAxis = Vector3.right;
        public Vector3 wheelAxis = Vector3.up;
        public Vector3 doorTravel = new Vector3(0f, 3.3f, 0f);
        public Vector3 boltTravel = new Vector3(-0.42f, 0f, 0f);
        public Vector3 actuatorTravel = new Vector3(0f, .12f, 0f);
        public float wheelTurns = 1.5f;

        public float Selector { get; private set; }
        public float Lever { get; private set; }
        public float Wheel { get; private set; }
        public bool ReturnClosed { get; private set; }

        Quaternion selectorHome;
        Quaternion leverHome;
        Quaternion wheelHome;
        Vector3 boltHome;
        Vector3 doorHome;
        Vector3 actuatorHome;
        Vector3 gateExtent;
        Quaternion needleHome;
        Quaternion returnHome;
        float shownSelector;
        float shownLever;
        float shownWheel;
        float opening;
        float automaticTurn;
        float lastRefusal = -100f;
        bool ready;

        void Awake()
        {
            if (selector) selectorHome = selector.localRotation;
            if (lever) leverHome = lever.localRotation;
            if (wheel) wheelHome = wheel.localRotation;
            if (bolt) boltHome = bolt.localPosition;
            if (door) doorHome = door.localPosition;
            if (actuator) actuatorHome = actuator.localPosition;
            if (needle) needleHome = needle.localRotation;
            if (returnLatch) returnHome = returnLatch.localRotation;
            if (barrier) gateExtent = barrier.bounds.extents;
            ready = true;
        }

        void OnEnable()
        {
            if (loop) loop.Changed += Refresh;
        }

        void Start()
        {
            Refresh();
            shownSelector = Selector;
            shownLever = Lever;
            shownWheel = Wheel;
            opening = loop && loop.state.gateOpen ? 1f : 0f;
            Apply();
        }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            automaticTurn = 0f;
        }

        void Update()
        {
            if (!loop || Session.IsOpen) return;
            if (automaticTurn > 0f && !loop.state.gateOpen)
            {
                float turn = Mathf.Min(automaticTurn, 240f * Time.deltaTime);
                automaticTurn -= turn;
                TurnWheel(turn);
            }
            shownSelector = Mathf.MoveTowards(shownSelector, Selector, Time.deltaTime * 2.5f);
            shownLever = Mathf.MoveTowards(shownLever, Lever, Time.deltaTime * 1.25f);
            shownWheel = Mathf.MoveTowards(shownWheel, Wheel, Time.deltaTime * 320f);
            if (ReturnClosed && InThreshold())
            {
                ReturnClosed = false;
                if (returnDisplay) returnDisplay.text = "THRESHOLD OCCUPIED\nGATE HELD OPEN";
            }
            if (loop.state.gateOpen && shownWheel >= RequiredTurn - 0.5f) opening = Mathf.MoveTowards(opening, ReturnClosed ? 0f : 1f, Time.deltaTime * 0.5f);
            Apply();
        }

        float RequiredTurn => Mathf.Max(0.25f, wheelTurns) * 360f;

        public void Isolate() { SetSelector(1f); }
        public void Equalize() { SetLever(1f); }

        public void Release()
        {
            if (!CanRelease()) return;
            automaticTurn = Mathf.Max(0f, RequiredTurn - Wheel);
            loop.Cue(0.85f);
        }

        public void Request()
        {
            if (!loop) return;
            loop.Announce("CIVIC", "No supported destination is available.");
            if (display) display.text = "DEPARTURE SERVICE UNAVAILABLE\nLocal service release beside terminal";
        }

        public void ToggleReturn()
        {
            if (Session.IsOpen || !loop || !loop.state.outside || !loop.state.gateOpen) return;
            if (!ReturnClosed && InThreshold())
            {
                if (returnDisplay) returnDisplay.text = "THRESHOLD OCCUPIED\nSTEP CLEAR";
                return;
            }
            ReturnClosed = !ReturnClosed;
            loop.Cue(ReturnClosed ? .82f : 1.08f);
            if (returnDisplay) returnDisplay.text = ReturnClosed ? "RETURN LATCH\nPULL TO OPEN" : "RETURN LATCH\nPULL TO CLOSE";
        }

        bool InThreshold()
        {
            if (!door || !barrier || !RealityPlayer.Head) return false;
            Vector3 center = door.parent ? door.parent.TransformPoint(doorHome) : doorHome;
            Vector3 feet = RealityPlayer.FeetPosition;
            Vector3 size = gateExtent + Vector3.one * .28f;
            return Mathf.Abs(feet.x - center.x) < size.x && Mathf.Abs(feet.z - center.z) < size.z;
        }

        public void SetSelector(float value)
        {
            if (!LocalReady() || loop.state.gateLocal) return;
            Selector = Mathf.Clamp01(value);
            if (Selector < 0.98f) return;
            Selector = 1f;
            loop.state.gateLocal = true;
            loop.Notify();
            loop.Cue(0.9f);
            loop.Announce("CIVIC", "This route is not included in your service agreement.");
        }

        public void SetLever(float value)
        {
            if (!LocalReady() || loop.state.gateEqual) return;
            if (!loop.state.gateLocal)
            {
                Refuse("First isolate the remote actuator. Turn the selector to Local.");
                return;
            }
            Lever = Mathf.Clamp01(value);
            if (Lever < 0.98f) return;
            Lever = 1f;
            loop.state.gateEqual = true;
            loop.Notify();
            loop.Cue(0.75f);
            loop.Announce("CIVIC", "Food, shelter and medical support cannot be guaranteed beyond the perimeter.");
        }

        public void TurnWheel(float degrees)
        {
            if (!CanRelease()) return;
            float before = Wheel;
            Wheel = Mathf.Clamp(Wheel + degrees, 0f, RequiredTurn);
            if (before < RequiredTurn * 0.4f && Wheel >= RequiredTurn * 0.4f)
                loop.Announce("CIVIC", "You can remain here.");
            if (Wheel < RequiredTurn - 0.1f) return;
            Wheel = RequiredTurn;
            automaticTurn = 0f;
            loop.state.gateOpen = true;
            loop.Notify();
            loop.Cue(0.55f);
            loop.Announce("CIVIC", "I can't tell you what happens next.");
        }

        bool LocalReady()
        {
            if (!loop || Session.IsOpen) return false;
            if (loop.state.local) return true;
            Refuse("The service reader requires Local maintenance mode.");
            return false;
        }

        bool CanRelease()
        {
            if (!LocalReady() || loop.state.gateOpen) return false;
            if (!loop.state.gateLocal) { Refuse("First isolate the remote actuator."); return false; }
            if (!loop.state.gateEqual) { Refuse("Pull the bypass lever. Wait for the gauge to reach SAFE."); return false; }
            if (shownLever < 0.97f) { Refuse("The chamber is equalizing. Wait for SAFE."); return false; }
            return true;
        }

        void Refuse(string text)
        {
            if (display) display.text = "LOCAL SERVICE\n" + text;
            if (!loop || Time.unscaledTime - lastRefusal < 2f) return;
            lastRefusal = Time.unscaledTime;
            loop.Announce("SERVICE", text);
            loop.Cue(0.6f);
        }

        void Refresh()
        {
            if (!ready || !loop) return;
            if (loop.state.gateLocal) Selector = 1f;
            if (loop.state.gateEqual) Lever = 1f;
            if (loop.state.gateOpen) Wheel = RequiredTurn;
            if (display)
            {
                display.text = loop.state.gateOpen ? "BOLT RELEASED\nOpen path" : loop.state.gateEqual ? "3  RELEASE\nTurn the handwheel" : loop.state.gateLocal ? "2  EQUALIZE\nPull the bypass lever" : "1  ISOLATE\nTurn NETWORK to LOCAL";
            }
        }

        void Apply()
        {
            if (selector) selector.localRotation = selectorHome * Quaternion.AngleAxis(-80f * shownSelector, selectorAxis);
            if (lever) lever.localRotation = leverHome * Quaternion.AngleAxis(-65f * shownLever, leverAxis);
            if (wheel) wheel.localRotation = wheelHome * Quaternion.AngleAxis(-shownWheel, wheelAxis);
            if (actuator) actuator.localPosition = actuatorHome + actuatorTravel * shownSelector;
            if (needle) needle.localRotation = needleHome * Quaternion.Euler(0f, 0f, Mathf.Lerp(110f, -35f, shownLever));
            if (returnLatch) returnLatch.localRotation = returnHome * Quaternion.Euler(0f, 0f, ReturnClosed ? -65f : 0f);
            float retraction = Mathf.Clamp01(shownWheel / RequiredTurn);
            if (bolt) bolt.localPosition = boltHome + boltTravel * retraction;
            if (door) door.localPosition = doorHome + doorTravel * Mathf.SmoothStep(0f, 1f, opening);
            if (barrier) barrier.enabled = opening < 0.98f;
            if (gauge) gauge.text = shownLever >= 0.97f ? "[ SAFE ]\nPressure equal" : "PRESSURE\n" + Mathf.RoundToInt((1f - shownLever) * 100f) + "%";
        }
    }
}

using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Hatch : PlaygroundTarget
    {
        public Loop loop;
        public Transform door;
        public Collider barrier;
        public GameObject overlay;
        public GameObject socket;
        public TMP_Text display;
        public TMP_Text marker;
        public Vector2 size = new Vector2(1.5f, 2.2f);
        public Vector3 travel = new Vector3(1.65f, 0f, 0f);
        public float stroke = 0.16f;

        sealed class Contact
        {
            public Transform hand;
            public Vector3 previous;
            public bool known;
            public bool armed;
            public bool touching;
            public float distance;
        }

        readonly Contact left = new Contact();
        Vector3 home;
        float opening;
        bool ready;

        public override bool UsesTrackedHandForGrab => true;

        void Awake()
        {
            if (door) home = door.localPosition;
            ready = true;
        }

        void OnEnable()
        {
            if (loop) loop.Changed += Refresh;
        }

        void Start()
        {
            Refresh();
            opening = loop && loop.state.hatch ? 1f : 0f;
            Apply();
        }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            left.known = false;
            left.armed = false;
        }

        void Update()
        {
            if (Session.IsOpen) { left.known = left.armed = left.touching = false; left.distance = 0; return; }
            if (!loop) return;
            if (loop.state.hatch)
            {
                opening = Mathf.MoveTowards(opening, 1f, Time.deltaTime * 0.75f);
                Apply();
                return;
            }
            if (!loop.state.glove || !loop.state.calibrated || loop.state.traced) return;
            ContactHand(left, RealityPlayer.LeftHand ? RealityPlayer.LeftHand : RealityPlayer.RightHand);
        }

        public override void BeginInteraction(Transform hand)
        {
            if (Session.IsOpen || !loop || !hand || !loop.state.glove || !loop.state.calibrated || loop.state.traced || loop.state.hatch) return;
            Transform glove = RealityPlayer.LeftHand ? RealityPlayer.LeftHand : RealityPlayer.RightHand;
            if (hand == glove) ContactHand(left, hand);
        }

        public override void UpdateInteraction(Transform hand) { BeginInteraction(hand); }

        public void Trace()
        {
            if (Session.IsOpen || !loop || loop.state.hatch || loop.state.traced) return;
            if (!loop.state.glove)
            {
                loop.Announce("SERVICE", "No diagnostic contact.");
                return;
            }
            if (!loop.state.calibrated)
            {
                loop.Announce("SERVICE", "Calibrate the glove at a mirror first.");
                return;
            }
            loop.state.traced = true;
            loop.Notify();
            loop.Cue(0.8f);
            loop.Announce("SERVICE", "Service hatch exposed. Fit the glove to the socket to release the latch.");
        }

        public void Unlock()
        {
            if (Session.IsOpen || !loop || loop.state.hatch) return;
            if (!loop.state.glove || !loop.state.calibrated || !loop.state.traced)
            {
                loop.Announce("SERVICE", "Calibrate at the mirror. Trace the seam to expose the socket.");
                return;
            }
            loop.state.hatch = true;
            loop.Notify();
            loop.Cue(0.65f);
            loop.Announce("SERVICE", "Latch released. Forearm bridge inside.");
        }

        void ContactHand(Contact contact, Transform hand)
        {
            if (!hand || !hand.gameObject.activeInHierarchy)
            {
                contact.hand = null;
                contact.known = contact.armed = contact.touching = false;
                contact.distance = 0f;
                return;
            }
            if (contact.hand != hand)
            {
                contact.hand = hand;
                contact.known = contact.armed = contact.touching = false;
                contact.distance = 0f;
            }
            Vector3 current = hand.position;
            Vector3 delta = current - contact.previous;
            if (!contact.known || delta.sqrMagnitude > 0.45f * 0.45f)
            {
                contact.previous = current;
                contact.known = true;
                contact.armed = contact.touching = false;
                contact.distance = 0f;
                return;
            }
            contact.previous = current;
            Vector3 point = transform.InverseTransformPoint(current);
            float depth = Mathf.Abs(Vector3.Dot(current - transform.position, transform.forward));
            bool within = Mathf.Abs(point.x) < size.x * 0.5f && Mathf.Abs(point.y) < size.y * 0.5f;
            if (depth > 0.2f || !within)
            {
                contact.armed = true;
                contact.touching = false;
                contact.distance = 0f;
                return;
            }
            if (!contact.armed || depth > 0.13f) return;
            if (!contact.touching)
            {
                contact.touching = true;
                contact.distance = 0f;
                return;
            }
            float motion = Vector3.ProjectOnPlane(delta, transform.forward).magnitude;
            if (motion > 0.0025f) contact.distance += motion;
            if (display) display.text = "TRACE SEAM\n" + Mathf.RoundToInt(Mathf.Clamp01(contact.distance / Mathf.Max(0.15f, stroke)) * 100f) + "%";
            if (contact.distance >= Mathf.Max(0.15f, stroke)) Trace();
        }

        void Refresh()
        {
            if (!ready || !loop) return;
            bool revealed = loop.state.traced || loop.state.hatch || loop.state.local;
            bool known = loop.state.calibrated || revealed;
            if (overlay) overlay.SetActive(!revealed);
            if (socket) socket.SetActive(revealed);
            if (marker) marker.text = known ? "SERVICE FACE\n" + (loop.state.hatch ? "Local access" : "MIRROR > CONTACT > TRACE") : "GARDEN VIEW\nA moment of calm";
            if (display)
            {
                display.gameObject.SetActive(known);
                display.text = loop.state.hatch ? "HATCH OPEN\nBridge: NETWORK / LOCAL" : loop.state.traced ? "GLOVE SOCKET\nRelease latch" : loop.state.calibrated ? "TRACE THE SEAM\nMove the glove along the frame" : "SERVICE FACE\nMIRROR > CONTACT > TRACE";
            }
        }

        void Apply()
        {
            if (door) door.localPosition = home + travel * Mathf.SmoothStep(0f, 1f, opening);
            if (barrier) barrier.enabled = opening < 0.98f;
        }
    }
}

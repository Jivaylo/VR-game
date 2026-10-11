using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Mirror : MonoBehaviour
    {
        public Loop loop;
        public Transform surface;
        public Vector2 size = new Vector2(1.1f, 1.5f);
        public GameObject diagram;
        public BleakMirror view;
        public float hold = 3;
        float rawUntil;
        public bool Raw => loop && (loop.state.local || Time.unscaledTime < rawUntil);
        public bool Domestic => loop && loop.home && (transform.position - loop.home.position).sqrMagnitude < 36;

        sealed class Contact
        {
            public Transform hand;
            public Vector3 previous;
            public bool known;
            public bool armed;
            public bool touching;
        }

        readonly Contact left = new Contact();

        void OnEnable()
        {
            if (loop) loop.Changed += Refresh;
        }

        void Start()
        {
            if (!view) view = GetComponent<BleakMirror>();
            Refresh();
            View();
        }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            left.known = false;
            left.armed = false;
            left.touching = false;
        }

        void Update()
        {
            if (!loop) return;
            if (!view) view = GetComponent<BleakMirror>();
            View();
            if (Session.IsOpen) { left.known = left.armed = left.touching = false; return; }
            if (!loop.state.glove || !Nearby()) { left.known = left.armed = left.touching = false; return; }
            Check(left, RealityPlayer.LeftHand ? RealityPlayer.LeftHand : RealityPlayer.RightHand);
        }

        public void Calibrate()
        {
            if (!loop || Session.IsOpen || !Nearby()) return;
            if (!loop.state.glove)
            {
                loop.Announce("CIVIC", "Your appearance is within your preferred range.");
                return;
            }
            bool first = !loop.state.calibrated;
            bool domestic = Domestic && !loop.state.homeMirror && !loop.state.local;
            loop.state.calibrated = true;
            if (Domestic) loop.state.homeMirror = true;
            rawUntil = Time.unscaledTime + hold;
            loop.Notify();
            loop.Cue(1.15f);
            View();
            if (domestic) loop.Announce("SIGNAL", "The room hasn't become smaller.");
            else if (first) loop.Announce("SERVICE", "Contact calibrated. Raw reference saved.");
        }

        void Check(Contact contact, Transform hand)
        {
            if (!hand || !hand.gameObject.activeInHierarchy)
            {
                contact.hand = null;
                contact.known = contact.armed = contact.touching = false;
                return;
            }
            if (contact.hand != hand)
            {
                contact.hand = hand;
                contact.known = contact.armed = contact.touching = false;
            }
            Transform face = surface ? surface : transform;
            Vector3 current = hand.position;
            if (!contact.known || (current - contact.previous).sqrMagnitude > 0.45f * 0.45f)
            {
                contact.previous = current;
                contact.known = true;
                contact.armed = false;
                contact.touching = false;
                return;
            }
            contact.previous = current;
            Vector3 point = face.InverseTransformPoint(current);
            float depth = Mathf.Abs(Vector3.Dot(current - face.position, face.forward));
            bool within = Mathf.Abs(point.x) <= size.x * 0.5f && Mathf.Abs(point.y) <= size.y * 0.5f;
            if (depth >= 0.18f || !within) contact.armed = true;
            if (!within || depth > 0.075f) { contact.touching = false; return; }
            if (!contact.armed)
            {
                if (contact.touching) rawUntil = Time.unscaledTime + hold;
                return;
            }
            contact.armed = false;
            contact.touching = true;
            Calibrate();
        }

        void Refresh()
        {
            if (diagram && loop) diagram.SetActive(loop.state.calibrated);
            View();
        }

        bool Nearby()
        {
            var head = RealityPlayer.Head;
            var face = surface ? surface : transform;
            return head && (head.position - face.position).sqrMagnitude <= 9;
        }

        void View()
        {
            if (!view) return;
            bool value = Raw;
            view.SetView(value ? .14f : .96f, value ? .76f : 1.04f, value ? 1 << 11 : 0);
        }
    }
}

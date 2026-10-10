using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class DinnerInspect : MonoBehaviour
    {
        public Loop loop;
        public Delivery delivery;
        public Transform menu;
        public TMP_Text title;
        public GameObject raw;
        public GameObject description;
        public GameObject[] previews = System.Array.Empty<GameObject>();
        public GameObject[] meals = System.Array.Empty<GameObject>();
        public int Selection { get; private set; }
        public bool Revealed => loop && (loop.state.local || Time.unscaledTime < revealUntil);
        float revealUntil, next;
        bool armed, tracked;
        Vector3 previous;
        int state = -1;

        void OnEnable()
        {
            if (!loop) loop = Loop.Instance;
            if (loop) loop.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            tracked = armed = false;
        }

        public void Next()
        {
            if (!loop || Session.IsOpen || loop.state.local || loop.Busy || Time.unscaledTime < next) return;
            next = Time.unscaledTime + .25f;
            Selection = (Selection + 1) % 3;
            Refresh();
            loop.Cue(1.05f + Selection * .08f);
        }

        public bool Inspect(Transform hand)
        {
            if (!loop || Session.IsOpen || loop.Busy || !loop.state.glove || !hand || !Near(hand.position)) return false;
            revealUntil = Time.unscaledTime + 3;
            if (!loop.state.dinnerSeen)
            {
                loop.state.dinnerSeen = true;
                loop.Notify();
                loop.Cue(.78f);
                Button.Haptic(hand, .08f, .055f);
            }
            Refresh();
            return true;
        }

        bool Near(Vector3 point)
        {
            if (menu)
            {
                Vector3 local = menu.InverseTransformPoint(point);
                if (Mathf.Abs(local.x) < .26f && Mathf.Abs(local.y) < .15f && Mathf.Abs(local.z) < .16f) return true;
            }
            return delivery && delivery.package && delivery.package.activeInHierarchy
                && (delivery.package.transform.position - point).sqrMagnitude < .22f * .22f;
        }

        void Update()
        {
            if (!loop) return;
            if (Session.IsOpen) { tracked = armed = false; return; }
            int value = Selection | (Revealed ? 4 : 0) | (loop.state.local ? 8 : 0);
            if (value != state) Refresh();
            var hand = RealityPlayer.LeftHand ? RealityPlayer.LeftHand : RealityPlayer.RightHand;
            if (!hand || !hand.gameObject.activeInHierarchy || !loop.state.glove)
            {
                tracked = armed = false;
                return;
            }
            Vector3 point = hand.position;
            if (!tracked || (point - previous).sqrMagnitude > .45f * .45f)
            {
                previous = point;
                tracked = true;
                armed = false;
                return;
            }
            previous = point;
            if (!Near(point)) armed = true;
            else if (armed) Inspect(hand);
        }

        public void Refresh()
        {
            if (!loop) return;
            bool exposed = Revealed;
            state = Selection | (exposed ? 4 : 0) | (loop.state.local ? 8 : 0);
            if (raw) raw.SetActive(exposed);
            if (description) description.SetActive(!exposed);
            for (int i = 0; i < previews.Length; i++)
                if (previews[i]) previews[i].SetActive(i == Selection && !exposed);
            for (int i = 0; i < meals.Length; i++)
                if (meals[i]) meals[i].SetActive(i == Selection && !exposed);
            if (title) title.text = exposed ? "PRODUCTION 07" : Selection == 0 ? "GARDEN BOWL" : Selection == 1 ? "GINGER BROTH" : "HERB TOAST";
        }
    }
}

using UnityEngine;

namespace Garden
{
    public sealed class Door : MonoBehaviour
    {
        public Transform leaf;
        public Collider barrier;
        public bool localOnly;
        public Vector3 travel = new Vector3(1.8f,0,0);
        public bool open;
        Vector3 home;
        float amount;
        int day;
        void Awake() { if (leaf) home = leaf.localPosition; day = Loop.Instance ? Loop.Instance.state.day : 0; }
        public void Toggle()
        {
            if (localOnly && (!Loop.Instance || !Loop.Instance.state.local)) { if (Loop.Instance) Loop.Instance.Announce("SERVICE", "Inner door requires local control. The bridge switch is separate from installation."); return; }
            open = !open;
            if (Loop.Instance) Loop.Instance.Cue(.8f);
        }
        void Update()
        {
            if (Loop.Instance && day != Loop.Instance.state.day)
            {
                day = Loop.Instance.state.day;
                if (!localOnly) open = false;
            }
            amount = Mathf.MoveTowards(amount,open?1:0,Time.deltaTime*1.2f);
            if (leaf) leaf.localPosition = home + travel * Mathf.SmoothStep(0,1,amount);
            if (barrier) barrier.enabled = amount < .99f;
        }
    }
}

using UnityEngine;

namespace RealityPlayground
{

    public abstract class PlaygroundTarget : MonoBehaviour
    {
        public virtual bool SupportsTriggerActivation => false;
        public virtual bool UsesTrackedHandForGrab => false;
        public virtual void BeginInteraction(Transform hand) { Activate(); }
        public virtual void BeginInteraction(Transform hand, Vector3 selectionPoint) { BeginInteraction(hand); }
        public virtual void UpdateInteraction(Transform hand) { }
        public virtual void EndInteraction(Transform hand) { }
        public virtual void Activate() { }
    }
}

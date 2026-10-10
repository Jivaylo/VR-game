using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RealityPlayground
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class XRExperimentBridge : MonoBehaviour
    {
        PlaygroundTarget target;
        XRSimpleInteractable interactable;
        readonly Dictionary<IXRSelectInteractor, Transform> hands = new Dictionary<IXRSelectInteractor, Transform>();
        readonly HashSet<Transform> uniqueHands = new HashSet<Transform>();
        readonly Dictionary<IXRSelectInteractor, GameObject> rayProxies = new Dictionary<IXRSelectInteractor, GameObject>();
        readonly HashSet<XRBaseInputInteractor> buttonHoverers = new HashSet<XRBaseInputInteractor>();

        sealed class HoverActivationOverride
        {
            public bool previousValue;
            public readonly HashSet<XRExperimentBridge> owners = new HashSet<XRExperimentBridge>();
        }
        static readonly Dictionary<XRBaseInputInteractor, HoverActivationOverride> hoverActivationOverrides = new Dictionary<XRBaseInputInteractor, HoverActivationOverride>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHoverActivationOverrides() { hoverActivationOverrides.Clear(); }

        void Awake() { target = GetComponent<PlaygroundTarget>(); interactable = GetComponent<XRSimpleInteractable>(); }
        void OnEnable()
        {
            if (!interactable) return;
            interactable.selectEntered.AddListener(Begin);
            interactable.selectExited.AddListener(End);
            if (target && target.SupportsTriggerActivation)
            {
                interactable.hoverEntered.AddListener(ButtonHoverEntered);
                interactable.hoverExited.AddListener(ButtonHoverExited);
                interactable.activated.AddListener(ActivateButton);
                foreach (var hovering in interactable.interactorsHovering)
                    if (hovering is XRBaseInputInteractor interactor) AcquireHoveredActivation(interactor);
            }
        }
        void OnDisable()
        {
            if (interactable)
            {
                interactable.selectEntered.RemoveListener(Begin);
                interactable.selectExited.RemoveListener(End);
                interactable.hoverEntered.RemoveListener(ButtonHoverEntered);
                interactable.hoverExited.RemoveListener(ButtonHoverExited);
                interactable.activated.RemoveListener(ActivateButton);
            }
            foreach (var interactor in buttonHoverers) ReleaseHoveredActivation(interactor);
            buttonHoverers.Clear();
            uniqueHands.Clear();
            if (target) foreach (var hand in hands.Values) if (uniqueHands.Add(hand)) target.EndInteraction(hand);
            hands.Clear();
            foreach (var proxy in rayProxies.Values) if (proxy) Destroy(proxy);
            rayProxies.Clear();
        }
        void ButtonHoverEntered(HoverEnterEventArgs e)
        {
            if (e.interactorObject is XRBaseInputInteractor interactor) AcquireHoveredActivation(interactor);
        }
        void AcquireHoveredActivation(XRBaseInputInteractor interactor)
        {
            if (!buttonHoverers.Add(interactor)) return;

            if (!hoverActivationOverrides.TryGetValue(interactor, out var state))
            {
                state = new HoverActivationOverride { previousValue = interactor.allowHoveredActivate };
                hoverActivationOverrides.Add(interactor, state);
            }
            state.owners.Add(this);
            interactor.allowHoveredActivate = true;
        }
        void ButtonHoverExited(HoverExitEventArgs e)
        {
            if (e.interactorObject is XRBaseInputInteractor interactor && buttonHoverers.Remove(interactor))
                ReleaseHoveredActivation(interactor);
        }
        void ReleaseHoveredActivation(XRBaseInputInteractor interactor)
        {
            if (!hoverActivationOverrides.TryGetValue(interactor, out var state)) return;
            state.owners.Remove(this);
            if (state.owners.Count != 0) return;
            if (interactor) interactor.allowHoveredActivate = state.previousValue;
            hoverActivationOverrides.Remove(interactor);
        }
        void ActivateButton(ActivateEventArgs e) { if (target && target.isActiveAndEnabled && target.SupportsTriggerActivation) target.Activate(); }
        void Begin(SelectEnterEventArgs e)
        {
            if (!target || !target.isActiveAndEnabled) return;
            var hand = e.interactorObject.transform;
            var attachment=e.interactorObject.GetAttachTransform(interactable);
            Vector3 selectionPoint=attachment?attachment.position:hand.position;
            if(e.interactorObject is XRRayInteractor pointingRay && pointingRay.TryGetCurrent3DRaycastHit(out var pointingHit))selectionPoint=pointingHit.point;
            if (target && target.UsesTrackedHandForGrab)
            {
                if (RealityPlayer.LeftHand && hand.IsChildOf(RealityPlayer.LeftHand)) hand = RealityPlayer.LeftHand;
                else if (RealityPlayer.RightHand && hand.IsChildOf(RealityPlayer.RightHand)) hand = RealityPlayer.RightHand;
            }
            else if (e.interactorObject is NearFarInteractor)
            {

                hand = e.interactorObject.GetAttachTransform(interactable);
            }
            else if (e.interactorObject is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out var hit))
            {
                var proxy = new GameObject("Experimentraygrabpoint");
                proxy.transform.SetParent(ray.rayOriginTransform ? ray.rayOriginTransform : ray.transform, false);
                proxy.transform.position = hit.point;
                rayProxies[e.interactorObject] = proxy;
                hand = proxy.transform;
            }
            else if (RealityPlayer.LeftHand && hand.IsChildOf(RealityPlayer.LeftHand)) hand = RealityPlayer.LeftHand;
            else if (RealityPlayer.RightHand && hand.IsChildOf(RealityPlayer.RightHand)) hand = RealityPlayer.RightHand;
            bool alreadySelected = hands.ContainsValue(hand);
            hands[e.interactorObject] = hand;
            if (target && !alreadySelected) target.BeginInteraction(hand,selectionPoint);
        }
        void End(SelectExitEventArgs e)
        {
            hands.TryGetValue(e.interactorObject, out var hand);
            hands.Remove(e.interactorObject);
            if (hand && target && !hands.ContainsValue(hand)) target.EndInteraction(hand);
            if (rayProxies.TryGetValue(e.interactorObject, out var proxy))
            {
                if (proxy) Destroy(proxy);
                rayProxies.Remove(e.interactorObject);
            }
        }
        readonly List<Transform> updatingHands = new List<Transform>();
        void Update()
        {
            if (!target || !target.isActiveAndEnabled) return;

            uniqueHands.Clear(); updatingHands.Clear();
            foreach (var hand in hands.Values) if (hand && uniqueHands.Add(hand)) updatingHands.Add(hand);
            foreach (var hand in updatingHands)
                if (hand && target && target.isActiveAndEnabled && hands.ContainsValue(hand)) target.UpdateInteraction(hand);
        }
    }
}

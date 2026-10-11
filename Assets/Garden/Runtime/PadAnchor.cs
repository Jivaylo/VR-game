using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Garden
{
    public sealed class PadAnchor : TeleportationAnchor
    {
        NavPad pad;

        protected override void Awake()
        {
            base.Awake();
            pad = GetComponent<NavPad>();
        }

        public bool Travel()
        {
            return SendTeleportRequest(null);
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (!pad) pad = GetComponent<NavPad>();
            return pad && pad.Available && base.IsSelectableBy(interactor);
        }

        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit hit, ref TeleportRequest request)
        {
            if (!pad) pad = GetComponent<NavPad>();
            if (!pad || !pad.Available || !pad.CanTravel(out _)) return false;
            teleportAnchorTransform = pad.destination ? pad.destination : pad.transform;
            return base.GenerateTeleportRequest(interactor, hit, ref request);
        }
    }
}

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace Garden
{
    [DefaultExecutionOrder(-1100)]
    [DisallowMultipleComponent]
    public sealed class PadRig : MonoBehaviour
    {
        void Awake()
        {
            Configure(gameObject);
        }

        public static void Configure(GameObject root)
        {
            if (!root) return;
            foreach (var caster in root.GetComponentsInChildren<CurveInteractionCaster>(true))
            {
                caster.raycastMask |= (1 << 8) | (1 << 9);
                caster.castDistance = 14f;
            }
            foreach (var caster in root.GetComponentsInChildren<SphereInteractionCaster>(true))
                caster.physicsLayerMask |= 1 << 8;
            foreach (var ray in root.GetComponentsInChildren<XRRayInteractor>(true))
            {
                ray.raycastMask |= 1 << 9;
                ray.maxRaycastDistance = 14f;
            }
        }
    }
}

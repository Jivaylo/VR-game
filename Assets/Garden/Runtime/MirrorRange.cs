using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class MirrorRange : MonoBehaviour
    {
        public BleakMirror view;
        void Update()
        {
            if (!view || !RealityPlayer.Head) return;
            float distance=(RealityPlayer.Head.position-transform.position).sqrMagnitude;
            bool near=distance<(view.enabled?64:42.25f);
            if(view.enabled!=near) view.enabled=near;
        }
    }
}

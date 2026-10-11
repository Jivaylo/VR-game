using System;
using RealityPlayground;
using UnityEngine;

namespace Garden
{
    [DefaultExecutionOrder(1500)]
    public sealed class Wrist : MonoBehaviour
    {
        public Transform housing;
        public Transform board;
        public Transform hand;
        public bool rightHand;
        public Vector3 offset = new Vector3(0, .005f, -.14f);
        public Vector3 panelOffset = new Vector3(.255f, .10f, -.025f);
        public Vector3 panelAngles = new Vector3(65, 0, 0);
        public float panelScale = .4f;
        public Vector3 screenOffset = new Vector3(0, .058f, .008f);
        public Vector3 screenAngles = new Vector3(90, 0, 0);
        public float screenScale = .14f;
        public bool Attached { get; private set; }
        Vector3 boardScale = Vector3.one;
        bool measured;

        public static bool Supports(string speaker) => string.Equals(speaker, "CIVIC", StringComparison.OrdinalIgnoreCase) || string.Equals(speaker, "SIGNAL", StringComparison.OrdinalIgnoreCase) || string.Equals(speaker, "SERVICE", StringComparison.OrdinalIgnoreCase) || string.Equals(speaker, "GLOVE", StringComparison.OrdinalIgnoreCase) || string.Equals(speaker, "BRIDGE", StringComparison.OrdinalIgnoreCase) || string.Equals(speaker, "CONTROLS", StringComparison.OrdinalIgnoreCase);

        void Awake() { Measure(); }
        void OnEnable() { Application.onBeforeRender += Follow; }
        void OnDisable() { Application.onBeforeRender -= Follow; if (housing) housing.gameObject.SetActive(false); }
        void LateUpdate() { Follow(); }

        void Measure()
        {
            if (measured || !board) return;
            boardScale = board.localScale;
            measured = true;
        }

        public void Present(bool attached)
        {
            Measure();
            Attached = attached;
            if (board) board.localScale = attached ? boardScale * panelScale : boardScale;
            Follow();
        }

        public void Follow()
        {
            var anchor = hand ? hand : rightHand ? RealityPlayer.RightHand : RealityPlayer.LeftHand;
            bool tracked = anchor && anchor.gameObject.activeInHierarchy;
            if (housing && housing.gameObject.activeSelf != tracked) housing.gameObject.SetActive(tracked);
            if (Attached && board && board.gameObject.activeSelf != tracked) board.gameObject.SetActive(tracked);
            if (!tracked) return;
            Vector3 mount = offset;
            bool local = Loop.Instance && Loop.Instance.state.local;
            Vector3 screen = local ? screenOffset : panelOffset;
            if (rightHand) { mount.x = -mount.x; screen.x = -screen.x; }
            Vector3 position = anchor.position + anchor.rotation * mount;
            if (housing) housing.SetPositionAndRotation(position, anchor.rotation);
            if (!Attached || !board) return;
            board.localScale = boardScale * (local ? screenScale : panelScale);
            board.SetPositionAndRotation(position + anchor.rotation * screen, anchor.rotation * Quaternion.Euler(local ? screenAngles : panelAngles));
        }
    }
}

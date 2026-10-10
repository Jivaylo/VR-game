using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace RealityPlayground
{
    [DefaultExecutionOrder(-900)]
    public sealed class RealityPlayer : MonoBehaviour
    {
        public enum InputMode { AutoHeadset, XRSimulator, Desktop }
        [Tooltip("Auto uses a running headset, otherwise desktop. XRSimulator uses the installed XRI simulator and real XR rig/interactors.")]
        public InputMode inputMode = InputMode.AutoHeadset;
        public GameObject xrSimulatorPrefab;
        public XROrigin xrOrigin;
        public Transform xrLeftHand, xrRightHand;
        public GameObject desktopRoot;
        public Camera desktopCamera;
        public Transform desktopLeftHand, desktopRightHand;
        public Transform[] stationViewpoints;
        public Transform avatarBody, avatarHead;
        public float walkingSpeed = 3f;
        [Tooltip("Story scenes allow movement only through their AR navigation anchors.")]
        public bool markerLocomotionOnly;
        static RealityPlayer instance;
        bool usingXR;
        bool modeInitialized;
        CharacterController motor;
        PlaygroundTarget selected;
        float selectionDistance, pitch, yaw, verticalSpeed;
        Vector3 home;
        GameObject simulatorObject;
        bool hardwareActive;
        float nextDeviceCheck;
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        public static bool IsXR => instance && instance.usingXR;
        public static bool HasLiveHeadset => instance && instance.hardwareActive;
        public static Transform Head => instance ? (instance.usingXR ? (instance.xrOrigin && instance.xrOrigin.Camera ? instance.xrOrigin.Camera.transform : null) : (instance.desktopCamera ? instance.desktopCamera.transform : null)) : (Camera.main ? Camera.main.transform : null);
        public static Transform LeftHand => instance ? (instance.usingXR ? instance.xrLeftHand : instance.desktopLeftHand) : null;
        public static Transform RightHand => instance ? (instance.usingXR ? instance.xrRightHand : instance.desktopRightHand) : null;
        public static Vector3 FeetPosition => instance && Head ? new Vector3(Head.position.x, instance.usingXR && instance.xrOrigin ? instance.xrOrigin.transform.position.y : (instance.desktopRoot ? instance.desktopRoot.transform.position.y : 0), Head.position.z) : Vector3.zero;

        public static void FaceDirection(float degrees)
        {
            if (!instance || instance.usingXR) return;
            instance.yaw = degrees;
            instance.pitch = 0;
        }
        public void SetDesktopWakePose(float seated)
        {
            if (usingXR || !desktopCamera) return;
            desktopCamera.transform.localPosition = new Vector3(0, Mathf.Lerp(.93f, 1.68f, seated), 0);
            pitch = Mathf.Lerp(-58, 0, seated);
        }

        void Awake()
        {
            instance = this;
            motor = desktopRoot.GetComponent<CharacterController>();
            home = desktopRoot.transform.position;
            yaw = desktopRoot.transform.eulerAngles.y;
            if (XRInteractionSimulator.instance) simulatorObject = XRInteractionSimulator.instance.gameObject;
            RefreshDevices();
            ApplyInputMode();
        }
        void RefreshDevices()
        {
            displays.Clear();
            SubsystemManager.GetSubsystems(displays);
            hardwareActive = XRSettings.isDeviceActive || displays.Exists(display => display.running);
        }
        public void SetInputMode(InputMode mode)
        {
            inputMode = mode;
            RefreshDevices();
            ApplyInputMode();
        }
        void ApplyInputMode()
        {
            bool simulate = !hardwareActive && inputMode == InputMode.XRSimulator;
            if (!simulatorObject && XRInteractionSimulator.instance) simulatorObject = XRInteractionSimulator.instance.gameObject;
            if (!simulatorObject && simulate && xrSimulatorPrefab) simulatorObject = Instantiate(xrSimulatorPrefab);

            if (simulatorObject && simulatorObject.activeSelf != simulate) simulatorObject.SetActive(simulate);
            SetMode(inputMode != InputMode.Desktop && (hardwareActive || simulate));
        }
        void SetMode(bool xr)
        {
            bool preservePosition=modeInitialized && usingXR!=(xr && xrOrigin) && Head;
            Vector3 previousFeet=preservePosition?FeetPosition:Vector3.zero;
            float previousFacing=Head?Head.eulerAngles.y:0;
            Release();
            usingXR = xr && xrOrigin;
            if (xrOrigin) xrOrigin.gameObject.SetActive(usingXR);
            desktopRoot.SetActive(!usingXR);
            if(preservePosition){TeleportTo(previousFeet);if(!usingXR)FaceDirection(previousFacing);}
            modeInitialized=true;
        }
        public static void TeleportTo(Vector3 floorPosition)
        {
            if (!instance || !Head) return;
            if (instance.usingXR)
            {
                float height = Mathf.Max(.5f, Head.position.y - instance.xrOrigin.transform.position.y);
                instance.xrOrigin.MoveCameraToWorldLocation(floorPosition + Vector3.up * height);
            }
            else
            {
                instance.motor.enabled = false;
                instance.desktopRoot.transform.position = floorPosition + Vector3.up * .02f;
                instance.motor.enabled = true;
                instance.verticalSpeed = 0;
            }
        }
        void Update()
        {

            if (XRInteractionSimulator.instance && simulatorObject != XRInteractionSimulator.instance.gameObject)
            {
                simulatorObject = XRInteractionSimulator.instance.gameObject;
                ApplyInputMode();
            }
            if (Time.unscaledTime > nextDeviceCheck)
            {
                nextDeviceCheck = Time.unscaledTime + .5f;
                bool previousHardware = hardwareActive;
                RefreshDevices();
                if (previousHardware != hardwareActive) ApplyInputMode();
            }
            var keys = Keyboard.current;
            if (keys != null && keys.f8Key.wasPressedThisFrame && !hardwareActive)
                SetInputMode(inputMode == InputMode.XRSimulator ? InputMode.Desktop : InputMode.XRSimulator);
            if (usingXR) return;
            var mouse = Mouse.current;
            if (keys == null || mouse == null) return;
            if (!markerLocomotionOnly && keys.homeKey.wasPressedThisFrame) TeleportTo(home);
            for (int i = 0; !markerLocomotionOnly && i < Mathf.Min(7, stationViewpoints == null ? 0 : stationViewpoints.Length); i++)
            {
                if (keys[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    Release();
                    TeleportTo(stationViewpoints[i].position);
                    yaw = stationViewpoints[i].eulerAngles.y;
                    pitch = 0;
                }
            }
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * .12f;
                pitch = Mathf.Clamp(pitch - delta.y * .12f, -80, 80);
            }
            desktopRoot.transform.rotation = Quaternion.Euler(0, yaw, 0);
            desktopCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector3 move = new Vector3((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0), 0, (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            move = desktopRoot.transform.TransformDirection(Vector3.ClampMagnitude(move, 1)) * walkingSpeed * (keys.leftShiftKey.isPressed ? 1.7f : 1);
            if (markerLocomotionOnly) move = Vector3.zero;
            verticalSpeed = motor.isGrounded ? -1 : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            motor.Move((move + Vector3.up * verticalSpeed) * Time.deltaTime);
            Ray ray = desktopCamera.ScreenPointToRay(mouse.position.ReadValue());
            PlaygroundTarget hovered = null;
            float distance = 1.1f;
            if (TryGetDesktopHit(ray, out var hit))
            {
                hovered = hit.collider.GetComponentInParent<PlaygroundTarget>();
                distance = hit.distance;
            }
            bool wristTarget = hovered is Garden.BridgeSwitch control && control.bridge && control.bridge.Arm == desktopRightHand;
            if (wristTarget) distance = 1.1f;
            if (selected)
            {
                selectionDistance = Mathf.Clamp(selectionDistance + mouse.scroll.ReadValue().y * .0015f, .25f, 8f);
                desktopRightHand.position = ray.GetPoint(selectionDistance);
                selected.UpdateInteraction(desktopRightHand);
            }
            else desktopRightHand.position = ray.GetPoint(Mathf.Min(distance, 4f));
            desktopRightHand.rotation = desktopCamera.transform.rotation;
            desktopLeftHand.position = desktopCamera.transform.TransformPoint(new Vector3(-.23f, -.28f, .4f));
            desktopLeftHand.rotation = desktopCamera.transform.rotation;
            if ((mouse.leftButton.wasPressedThisFrame || keys.eKey.wasPressedThisFrame) && hovered && hovered.isActiveAndEnabled)
            {
                if (wristTarget) hovered.Activate();
                else
                {
                    selected = hovered;
                    selectionDistance = distance;
                    desktopRightHand.position = ray.GetPoint(distance);
                    selected.BeginInteraction(desktopRightHand,ray.GetPoint(distance));
                }
            }
            if (mouse.leftButton.wasReleasedThisFrame || keys.eKey.wasReleasedThisFrame || keys.escapeKey.wasPressedThisFrame) Release();
            if (desktopRoot.transform.position.y < -8) TeleportTo(home);
        }
        void LateUpdate()
        {
            if (!Head) return;
            if (avatarBody)
            {
                avatarBody.position = Head.position - Vector3.up * .62f - Head.forward * .08f;
                avatarBody.rotation = Quaternion.Euler(0, Head.eulerAngles.y, 0);
            }
            if (avatarHead) avatarHead.SetPositionAndRotation(Head.position, Head.rotation);
        }
        public bool TryGetDesktopHit(Ray ray, out RaycastHit hit)
        {
            hit=default;float nearest=float.PositiveInfinity;
            foreach(var candidate in Physics.RaycastAll(ray,markerLocomotionOnly?22f:8f,~0,QueryTriggerInteraction.Collide))
            {
                if(!candidate.collider || (desktopRoot && candidate.collider.transform.IsChildOf(desktopRoot.transform)))continue;
                if(candidate.distance>=nearest)continue;
                hit=candidate;nearest=candidate.distance;
            }
            return hit.collider;
        }
        void Release() { if (selected) selected.EndInteraction(desktopRightHand); selected = null; }
        void OnDisable() { Release(); if (instance == this) instance = null; }
    }
}

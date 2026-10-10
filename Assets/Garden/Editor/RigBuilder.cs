using System;
using System.Collections.Generic;
using System.Linq;
using RealityPlayground;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class RigBuilder
    {
        public static RealityPlayer Build(Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RealityPlayground/StoryPrefabs/RealityStory.prefab");
            if (!source) throw new InvalidOperationException("RealityStory prefab is missing.");
            var copy = Object.Instantiate(source);
            copy.name = "RigSource";
            copy.SetActive(false);
            GameObject root = null;
            try
            {
                var player = copy.GetComponentInChildren<RealityPlayer>(true);
                if (!player || !player.xrOrigin || !player.desktopRoot || !player.desktopCamera)
                    throw new InvalidOperationException("RealityStory has an incomplete player rig.");
                root = new GameObject("Rig");
                root.transform.SetParent(parent, false);
                root.SetActive(false);
                var keep = new HashSet<Transform> { player.transform, player.xrOrigin.transform, player.desktopRoot.transform };
                if (player.avatarBody) keep.Add(player.avatarBody);
                if (player.avatarHead) keep.Add(player.avatarHead);
                foreach (var manager in copy.GetComponentsInChildren<XRInteractionManager>(true)) keep.Add(manager.transform);
                foreach (var manager in copy.GetComponentsInChildren<InputActionManager>(true)) keep.Add(manager.transform);
                if (keep.Contains(copy.transform)) throw new InvalidOperationException("Input components must have a separate rig root.");
                var roots = keep.Where(candidate => !keep.Any(other => other != candidate && candidate.IsChildOf(other))).ToArray();
                foreach (var item in roots) item.SetParent(root.transform, true);
                Object.DestroyImmediate(copy);
                copy = null;
                player.name = "Player";
                player.inputMode = RealityPlayer.InputMode.AutoHeadset;
                player.markerLocomotionOnly = true;
                player.stationViewpoints = Array.Empty<Transform>();
                player.xrOrigin.name = "XRRig";
                player.xrOrigin.transform.localPosition = Vector3.zero;
                player.xrOrigin.transform.localRotation = Quaternion.identity;
                player.desktopRoot.name = "Desktop";
                player.desktopRoot.transform.localPosition = Vector3.zero;
                player.desktopRoot.transform.localRotation = Quaternion.identity;
                if (player.avatarBody) player.avatarBody.name = "Body";
                if (player.avatarHead) player.avatarHead.name = "Head";
                var disabled = new HashSet<string>
                {
                    "DynamicMoveProvider", "ContinuousMoveProvider", "ContinuousTurnProvider", "ClimbProvider",
                    "GrabMoveProvider", "TwoHandedGrabMoveProvider", "JumpProvider", "ClimbTeleportInteractor"
                };
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour && disabled.Contains(behaviour.GetType().Name)) behaviour.enabled = false;
                foreach (var controller in root.GetComponentsInChildren<ControllerInputActionManager>(true))
                {
                    controller.smoothMotionEnabled = false;
                    controller.smoothTurnEnabled = false;
                }
                root.AddComponent<PadRig>();
                PadRig.Configure(root);
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) RigQuality.Configure(camera);
                root.AddComponent<RigQuality>();
                root.SetActive(true);
                return player;
            }
            catch
            {
                if (root) Object.DestroyImmediate(root);
                throw;
            }
            finally
            {
                if (copy) Object.DestroyImmediate(copy);
            }
        }
    }
}

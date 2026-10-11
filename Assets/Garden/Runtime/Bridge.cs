using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Bridge : PlaygroundTarget
    {
        public Loop loop;
        public Transform port;
        public GameObject model;
        public TMP_Text display;
        public Vector3 holdOffset = new Vector3(0f, 0f, 0.065f);
        public Vector3 portOffset = new Vector3(0f, -0.03f, -0.16f);

        public bool AtPort { get; private set; }
        public bool Held => heldBy;
        public Transform Arm => arm;
        public Vector3 EffectivePortOffset => OnLeft ? new Vector3(portOffset.x, -.09f, portOffset.z) : portOffset;

        Transform heldBy;
        Transform arm;
        Vector3 home;
        Quaternion rotation;
        Transform parent;
        Transform portParent;
        Vector3 portScale;
        Collider[] bodyColliders;
        bool[] colliderEnabled;
        bool ready;
        bool returning;
        bool announcedPort;
        float alignedSince = -1;

        public override bool UsesTrackedHandForGrab => true;

        Transform Body => model ? model.transform : transform;

        void Awake()
        {
            parent = Body.parent;
            home = Body.localPosition;
            rotation = Body.localRotation;
            if (port) { portParent = port.parent; portScale = port.localScale; }
            bodyColliders = Body.GetComponentsInChildren<Collider>(true);
            colliderEnabled = new bool[bodyColliders.Length];
            for (int i = 0; i < bodyColliders.Length; i++) colliderEnabled[i] = bodyColliders[i].enabled;
            ready = true;
        }

        void OnEnable()
        {
            if (loop) loop.Changed += Refresh;
        }

        void Start() { Refresh(); }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            if (heldBy && (!loop || !loop.state.installed)) returning = true;
            heldBy = null;
            AtPort = false;
        }

        void LateUpdate()
        {
            if (!loop || !ready) return;
            if (loop.state.installed)
            {
                if (!arm || !arm.gameObject.activeInHierarchy) { arm = InstalledArm; PlacePort(); }
                if (arm || port) AttachToPort();
                return;
            }
            if (heldBy)
            {
                if (!heldBy.gameObject.activeInHierarchy) { Drop(); return; }
                FollowHand();
                UpdatePort();
                return;
            }
            if (!returning) return;
            Vector3 destination = parent ? parent.TransformPoint(home) : home;
            Quaternion facing = parent ? parent.rotation * rotation : rotation;
            Body.SetPositionAndRotation(Vector3.MoveTowards(Body.position, destination, Time.deltaTime * 3f), Quaternion.RotateTowards(Body.rotation, facing, Time.deltaTime * 300f));
            if (Vector3.Distance(Body.position, destination) < 0.005f)
            {
                Body.SetPositionAndRotation(destination, facing);
                returning = false;
            }
        }

        public void Take()
        {
            if (!loop || !loop.state.hatch || loop.state.installed) return;
            TakeWith(RealityPlayer.LeftHand ? RealityPlayer.LeftHand : RealityPlayer.RightHand);
        }

        void TakeWith(Transform hand)
        {
            if (Session.IsOpen || !hand || !loop || !loop.state.hatch || loop.state.installed || Vector3.Distance(hand.position, Body.position) > .45f) return;
            heldBy = hand;
            arm = ServiceArm;
            PlacePort();
            returning = false;
            announcedPort = false;
            alignedSince = -1;
            loop.state.bridge = true;
            loop.Notify();
            loop.Cue(1.05f);
            loop.Announce("CIVIC", "This component is not assigned to you.");
            if (heldBy) FollowHand();
        }

        public void Install()
        {
            if (Session.IsOpen || !loop || loop.state.installed) return;
            if (!Held || !AtPort || !loop.state.hatch) return;
            if (!loop.state.bridge || !loop.state.glove)
            {
                loop.Announce("SERVICE", "Take the bridge and equip the service glove first.");
                return;
            }
            arm = ServiceArm;
            PlacePort();
            loop.state.installed = true;
            loop.state.bridgeLeft = arm && RealityPlayer.LeftHand && (arm == RealityPlayer.LeftHand || arm.IsChildOf(RealityPlayer.LeftHand));
            heldBy = null;
            returning = false;
            AtPort = true;
            loop.Notify();
            loop.Cue(1.25f);
            loop.Announce("BRIDGE", "Bridge connected. Network control retained.");
            AttachToPort();
        }

        public void OfferLocal()
        {
            if (!loop) return;
            if (!loop.state.installed)
            {
                loop.Announce("BRIDGE", "Connect the bridge to the forearm port first.");
                return;
            }
            loop.OfferLocal();
        }

        public override void BeginInteraction(Transform hand)
        {
            if (!hand || heldBy || !loop || loop.state.installed) return;
            if (Vector3.Distance(hand.position, Body.position) > .45f) return;
            TakeWith(hand);
        }

        public override void BeginInteraction(Transform hand, Vector3 selectionPoint) { BeginInteraction(hand); }

        public override void UpdateInteraction(Transform hand)
        {
            if (heldBy != hand || !hand) return;
            FollowHand();
            UpdatePort();
        }

        public override void EndInteraction(Transform hand)
        {
            if (heldBy == hand && loop && !loop.state.installed) Drop();
        }

        public void Drop()
        {
            heldBy = null;
            returning = true;
            AtPort = false;
            announcedPort = false;
            alignedSince = -1;
            Refresh();
        }

        Transform ServiceArm => heldBy && heldBy == RealityPlayer.RightHand ? RealityPlayer.LeftHand : RealityPlayer.RightHand ? RealityPlayer.RightHand : RealityPlayer.LeftHand;
        Transform InstalledArm => loop && loop.state.bridgeLeft ? RealityPlayer.LeftHand ? RealityPlayer.LeftHand : RealityPlayer.RightHand : RealityPlayer.RightHand ? RealityPlayer.RightHand : RealityPlayer.LeftHand;

        void FollowHand()
        {
            if (!heldBy) return;
            Body.SetPositionAndRotation(heldBy.position + heldBy.rotation * holdOffset, heldBy.rotation);
        }

        bool OnLeft => arm ? RealityPlayer.LeftHand && (arm == RealityPlayer.LeftHand || arm.IsChildOf(RealityPlayer.LeftHand)) : loop && loop.state.installed && loop.state.bridgeLeft;
        Vector3 PortPosition => arm ? arm.position + arm.rotation * EffectivePortOffset : port ? port.position : Body.position;
        Quaternion PortRotation => arm ? arm.rotation * (OnLeft ? Quaternion.identity : Quaternion.Euler(55f, 0f, 0f)) : port ? port.rotation : Body.rotation;

        void UpdatePort()
        {
            if (Session.IsOpen) { alignedSince = -1f; AtPort = false; return; }
            AtPort = arm && heldBy && arm != heldBy && Vector3.Distance(Body.position, PortPosition) <= .14f;
            if (!AtPort)
            {
                alignedSince = -1;
                if (announcedPort) { announcedPort = false; Refresh(); }
                return;
            }
            if (!loop.state.glove) { if (display) display.text = "Service glove required"; alignedSince = -1; return; }
            if (alignedSince < 0) alignedSince = Time.unscaledTime;
            if (!announcedPort) { announcedPort = true; loop.Cue(1.1f); }
            float progress = Mathf.Clamp01((Time.unscaledTime - alignedSince) / .65f);
            if (display) display.SetText("Connecting {0:0}%", progress * 100);
            if (progress >= 1) Install();
        }

        void AttachToPort()
        {
            if (!arm && !port) return;
            PlacePort();
            Body.SetPositionAndRotation(PortPosition, PortRotation);
        }

        void PlacePort()
        {
            if (!port || !arm || port == Body || Body.IsChildOf(port)) return;
            if (port.parent != portParent) port.SetParent(portParent, false);
            port.localScale = portScale;
            port.SetPositionAndRotation(PortPosition, PortRotation);
        }

        void Refresh()
        {
            if (!ready || !loop) return;
            if (model && model != gameObject) model.SetActive(loop.state.hatch || loop.state.installed);
            if (port && port != Body && !Body.IsChildOf(port)) port.gameObject.SetActive(loop.state.bridge || loop.state.installed);
            if (bodyColliders != null)
                for (int i = 0; i < bodyColliders.Length; i++)
                    if (bodyColliders[i] && !bodyColliders[i].GetComponentInParent<BridgeSwitch>()) bodyColliders[i].enabled = colliderEnabled[i] && !loop.state.installed;
            if (loop.state.installed)
            {
                if (!arm) arm = InstalledArm;
                PlacePort();
            }
            if (display)
                display.text = loop.state.local ? "Local" : loop.state.installed ? "Bridge connected" : heldBy ? "Hold at wrist port" : "Service bridge";
        }
    }
}

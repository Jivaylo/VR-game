using System.Collections;
using System.Collections.Generic;
using RealityPlayground;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Garden
{
    [RequireComponent(typeof(PadAnchor))]
    public sealed class NavPad : PlaygroundTarget
    {
        public Transform destination;
        public float maxDistance = 11f;
        public float maxRise = 2f;
        public bool unlocked = true;
        public bool requireSight = true;
        public LayerMask blockingLayers = 1;
        public UnityEvent arrived = new UnityEvent();
        public string action;
        public Renderer marker;
        public Color readyColor = new Color(.12f, .65f, .72f);
        public Color lockedColor = new Color(.15f, .2f, .22f);
        public float drawDistance = 18f;
        public bool hideUnavailable = true;
        public Guide guide;
        public bool Offered { get; private set; }
        public string Role { get; private set; }
        PadAnchor anchor;
        PadGlow glow;
        RealityPlayer player;
        bool pending;
        float retryAfter;
        float nextCheck;
        bool available;
        bool visible = true;
        bool lastReady;
        bool colorSet;
        bool managed;
        Renderer[] visuals;
        bool[] visualStates;
        Collider[] targets;
        bool[] targetStates;
        readonly Collider[] overlaps = new Collider[48];
        readonly RaycastHit[] hits = new RaycastHit[48];
        readonly HashSet<XRBaseInputInteractor> hoverers = new HashSet<XRBaseInputInteractor>();
        MaterialPropertyBlock properties;
        static readonly Dictionary<XRBaseInputInteractor, HoverState> hoverStates = new Dictionary<XRBaseInputInteractor, HoverState>();

        sealed class HoverState
        {
            public bool previous;
            public int count;
        }

        public bool Ready => isActiveAndEnabled && unlocked && !pending && !Session.IsOpen && Time.unscaledTime >= retryAfter && (!Loop.Instance || !Loop.Instance.Busy);

        public bool Available
        {
            get
            {
                if (guide) managed = true;
                if (!Ready) return false;
                if (Time.unscaledTime >= nextCheck)
                {
                    available = CanTravel(out _);
                    nextCheck = Time.unscaledTime + .1f;
                }
                return available;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHoverers()
        {
            hoverStates.Clear();
        }

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            anchor = GetComponent<PadAnchor>();
            glow = GetComponent<PadGlow>();
            player = FindFirstObjectByType<RealityPlayer>(FindObjectsInactive.Include);
            visuals = GetComponentsInChildren<Renderer>(true);
            visualStates = new bool[visuals.Length];
            for (int i = 0; i < visuals.Length; i++) visualStates[i] = visuals[i].enabled;
            targets = GetComponentsInChildren<Collider>(true);
            targetStates = new bool[targets.Length];
            for (int i = 0; i < targets.Length; i++) targetStates[i] = targets[i].enabled;
            if (!guide) guide = FindFirstObjectByType<Guide>(FindObjectsInactive.Include);
            managed = guide;
        }

        void OnEnable()
        {
            if (!anchor) anchor = GetComponent<PadAnchor>();
            anchor.teleportAnchorTransform = destination ? destination : transform;
            anchor.matchOrientation = MatchOrientation.None;
            anchor.matchDirectionalInput = false;
            anchor.interactionLayers = ~0;
            anchor.filterSelectionByHitNormal = false;
            anchor.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            anchor.teleporting.AddListener(Queued);
            anchor.activated.AddListener(Triggered);
            anchor.hoverEntered.AddListener(HoverEntered);
            anchor.hoverExited.AddListener(HoverExited);
            nextCheck = 0;
            Refresh();
        }

        void OnDisable()
        {
            if (anchor)
            {
                anchor.teleporting.RemoveListener(Queued);
                anchor.activated.RemoveListener(Triggered);
                anchor.hoverEntered.RemoveListener(HoverEntered);
                anchor.hoverExited.RemoveListener(HoverExited);
            }
            foreach (var interactor in hoverers) ReleaseHover(interactor);
            hoverers.Clear();
            StopAllCoroutines();
            pending = false;
            visible = false;
            if (targets != null) foreach (var target in targets) if (target) target.enabled = false;
            if (visuals != null) foreach (var visual in visuals) if (visual) visual.enabled = false;
            if (glow) glow.Show(false, false, false);
        }

        public bool CanTravel(out string reason)
        {
            reason = "";
            if (!isActiveAndEnabled || !unlocked) { reason = "Locked"; return false; }
            if (!RealityPlayer.Head) { reason = "No player"; return false; }
            if (!player) player = FindFirstObjectByType<RealityPlayer>(FindObjectsInactive.Include);
            var landing = destination ? destination.position : transform.position;
            var feet = RealityPlayer.FeetPosition;
            var offset = landing - feet;
            if (new Vector2(offset.x, offset.z).sqrMagnitude > maxDistance * maxDistance) { reason = "Too far"; return false; }
            if (Mathf.Abs(offset.y) > maxRise) { reason = "Use stairs"; return false; }
            if (requireSight && !Visible(RealityPlayer.Head.position, landing + Vector3.up * .12f)) { reason = "Blocked"; return false; }
            if (!Grounded(landing)) { reason = "No floor"; return false; }
            float height = Mathf.Clamp(RealityPlayer.Head.position.y - feet.y, 1.7f, 2.2f);
            const float radius = .22f;
            int count = Physics.OverlapCapsuleNonAlloc(landing + Vector3.up * (radius + .05f), landing + Vector3.up * (height - radius), radius, overlaps, blockingLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) { reason = "Crowded"; return false; }
            for (int i = 0; i < count; i++)
                if (!Ignored(overlaps[i])) { reason = "No room"; return false; }
            return true;
        }

        bool Visible(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < .01f) return true;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, delta.magnitude, blockingLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!Ignored(hits[i].collider)) return false;
            return true;
        }

        bool Grounded(Vector3 landing)
        {
            int count = Physics.RaycastNonAlloc(landing + Vector3.up * .18f, Vector3.down, hits, .38f, blockingLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!Ignored(hits[i].collider) && hits[i].normal.y > .7f && Mathf.Abs(hits[i].point.y - landing.y) < .16f) return true;
            return false;
        }

        bool Ignored(Collider collider)
        {
            if (!collider || collider.isTrigger || collider.transform.IsChildOf(transform)) return true;
            if (collider.GetComponentInParent<NavPad>()) return true;
            if (!player) return false;
            var t = collider.transform;
            return (player.xrOrigin && t.IsChildOf(player.xrOrigin.transform)) ||
                   (player.desktopRoot && t.IsChildOf(player.desktopRoot.transform)) ||
                   (player.avatarBody && t.IsChildOf(player.avatarBody)) ||
                   (player.avatarHead && t.IsChildOf(player.avatarHead));
        }

        void HoverEntered(HoverEnterEventArgs args)
        {
            if (!(args.interactorObject is XRBaseInputInteractor input) || !hoverers.Add(input)) return;
            if (!hoverStates.TryGetValue(input, out var state))
            {
                state = new HoverState { previous = input.allowHoveredActivate };
                hoverStates.Add(input, state);
            }
            state.count++;
            input.allowHoveredActivate = true;
            if (Available) input.SendHapticImpulse(.07f, .025f);
        }

        void HoverExited(HoverExitEventArgs args)
        {
            if (args.interactorObject is XRBaseInputInteractor input && hoverers.Remove(input)) ReleaseHover(input);
        }

        static void ReleaseHover(XRBaseInputInteractor input)
        {
            if (!hoverStates.TryGetValue(input, out var state)) return;
            if (--state.count > 0) return;
            if (input) input.allowHoveredActivate = state.previous;
            hoverStates.Remove(input);
        }

        void Triggered(ActivateEventArgs args)
        {
            if (Available && args.interactorObject is XRBaseInputInteractor input) input.SendHapticImpulse(.18f, .05f);
            Activate();
        }

        void Queued(TeleportingEventArgs args)
        {
            if (pending) return;
            pending = true;
            StartCoroutine(WaitForArrival(args.teleportRequest.destinationPosition));
        }

        public override void Activate()
        {
            if (!Available || !CanTravel(out _)) return;
            if (RealityPlayer.IsXR) anchor.Travel();
            else
            {
                var landing = destination ? destination.position : transform.position;
                pending = true;
                RealityPlayer.TeleportTo(landing);
                StartCoroutine(WaitForArrival(landing));
            }
        }

        IEnumerator WaitForArrival(Vector3 landing)
        {
            float deadline = Time.unscaledTime + 2f;
            bool reached = Vector3.Distance(RealityPlayer.FeetPosition, landing) <= .3f;
            yield return null;
            yield return null;
            while (!reached && Time.unscaledTime < deadline)
            {
                reached = Vector3.Distance(RealityPlayer.FeetPosition, landing) <= .3f;
                if (!reached) yield return null;
            }
            pending = false;
            retryAfter = Time.unscaledTime + .25f;
            nextCheck = 0;
            if (reached)
            {
                if (guide) guide.Record(this);
                arrived.Invoke();
                if (!string.IsNullOrEmpty(action) && Loop.Instance) Loop.Instance.Run(action);
            }
        }

        public void Offer(bool value, string role = "")
        {
            Offered = value;
            Role = value ? role : "";
            nextCheck = 0;
            Refresh();
        }

        void Update() { Refresh(); }

        void Refresh()
        {
            if (visuals == null) return;
            bool ready = Available;
            bool targetable = RealityPlayer.Head && ready && (transform.position - RealityPlayer.Head.position).sqrMagnitude <= drawDistance * drawDistance;
            bool focused = anchor && anchor.isHovered;
            bool inRange = targetable && (!managed || Offered || focused);
            if (RealityPlayer.Head)
            {
                var gap = (destination ? destination.position : transform.position) - RealityPlayer.FeetPosition;
                if (gap.x * gap.x + gap.z * gap.z < .36f && Mathf.Abs(gap.y) < .35f) inRange = targetable = false;
            }
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] && targets[i].enabled != (targetable && targetStates[i])) targets[i].enabled = targetable && targetStates[i];
            if (inRange != visible)
            {
                visible = inRange;
                for (int i = 0; i < visuals.Length; i++)
                    if (visuals[i]) visuals[i].enabled = visible && visualStates[i];
            }
            if (glow)
            {
                glow.idleColor = guide && guide.loop && guide.loop.state.local
                    ? new Color(.66f, .72f, .73f)
                    : Role == "NEXT" ? new Color(.27f, .90f, .77f) : new Color(.27f, .39f, .40f);
                glow.focusColor = guide && guide.loop && guide.loop.state.local ? new Color(1f, .86f, .59f) : new Color(.65f, 1f, .88f);
                glow.Show(visible, ready, anchor && anchor.isHovered);
                return;
            }
            if (!visible || !marker) return;
            if (colorSet && ready == lastReady) return;
            colorSet = true;
            lastReady = ready;
            marker.GetPropertyBlock(properties);
            var color = ready ? readyColor : lockedColor;
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", color * .5f);
            marker.SetPropertyBlock(properties);
        }
    }
}

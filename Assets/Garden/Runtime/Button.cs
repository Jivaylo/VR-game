using RealityPlayground;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Garden
{
    public sealed class Button : PlaygroundTarget
    {
        public UnityEvent pressed = new UnityEvent();
        public Transform visual;
        public bool touch = true;
        public string action;
        struct Contact
        {
            public bool tracked, armed, down;
            public Vector3 point;
            public float depth, ready;
        }
        Contact left, right;
        float next, flash, depression;
        Vector3 home, feet;
        Transform source;
        BoxCollider surface;
        Renderer face;
        Color color;
        MaterialPropertyBlock block;
        XRSimpleInteractable interactable;
        AudioSource click;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        public override bool SupportsTriggerActivation => true;
        public override bool UsesTrackedHandForGrab => true;

        void Start()
        {
            if (visual)
            {
                home = visual.localPosition;
                face = visual.GetComponent<Renderer>();
                if (face && face.sharedMaterial)
                    color = face.sharedMaterial.HasProperty(BaseColor) ? face.sharedMaterial.GetColor(BaseColor) : face.sharedMaterial.color;
            }
            block = new MaterialPropertyBlock();
            surface = GetComponent<BoxCollider>();
            interactable = GetComponent<XRSimpleInteractable>();
            feet = RealityPlayer.FeetPosition;
        }

        public override void BeginInteraction(Transform hand) { source = hand; Activate(); source = null; }
        public override void BeginInteraction(Transform hand, Vector3 point) { BeginInteraction(hand); }

        public override void Activate()
        {
            if (!isActiveAndEnabled || Time.unscaledTime < next || Session.IsOpen && !GetComponentInParent<Session>()) return;
            next = Time.unscaledTime + .35f;
            flash = Time.unscaledTime + .18f;
            Transform hand = source;
            if (!hand && interactable)
            {
                if (interactable.interactorsSelecting.Count > 0) hand = interactable.interactorsSelecting[0].transform;
                else if (interactable.interactorsHovering.Count > 0) hand = interactable.interactorsHovering[0].transform;
            }
            Feedback(hand);
            pressed.Invoke();
            if (!string.IsNullOrEmpty(action) && Loop.Instance) Loop.Instance.Run(action);
        }

        void Feedback(Transform hand)
        {
            Haptic(hand, .16f, .045f);
            if (!Loop.Instance || !Loop.Instance.clickSound) return;
            if (!click)
            {
                click = gameObject.AddComponent<AudioSource>();
                click.playOnAwake = false;
                click.spatialBlend = 1;
                click.minDistance = .3f;
                click.maxDistance = 6;
                click.rolloffMode = AudioRolloffMode.Linear;
            }
            click.PlayOneShot(Loop.Instance.clickSound, .24f);
        }

        public static void Haptic(Transform hand, float strength, float duration)
        {
            if (!hand || !RealityPlayer.IsXR) return;
            bool isLeft = RealityPlayer.LeftHand && (hand == RealityPlayer.LeftHand || hand.IsChildOf(RealityPlayer.LeftHand));
            bool isRight = RealityPlayer.RightHand && (hand == RealityPlayer.RightHand || hand.IsChildOf(RealityPlayer.RightHand));
            if (!isLeft && !isRight) return;
            var device = InputDevices.GetDeviceAtXRNode(isLeft ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid) device.SendHapticImpulse(0, strength, duration);
        }

        void Update()
        {
            depression = 0;
            var currentFeet = RealityPlayer.FeetPosition;
            if ((currentFeet - feet).sqrMagnitude > .09f) { left = default; right = default; }
            feet = currentFeet;
            if (touch && RealityPlayer.IsXR && surface)
            {
                Check(RealityPlayer.LeftHand, ref left);
                Check(RealityPlayer.RightHand, ref right);
            }
            bool lit = interactable && interactable.isHovered || left.armed || right.armed;
            bool pushed = Time.unscaledTime < flash;
            if (pushed) depression = 1;
            if (visual)
            {
                var offset = visual.parent.InverseTransformVector(transform.forward * (.008f * depression));
                visual.localPosition = Vector3.Lerp(visual.localPosition, home + offset, 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime));
            }
            if (face)
            {
                face.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(color, Color.white, pushed ? .32f : lit ? .13f : 0));
                block.SetColor(Emission, color * (pushed ? .5f : lit ? .18f : .02f));
                face.SetPropertyBlock(block);
            }
        }

        void Check(Transform hand, ref Contact contact)
        {
            if (!hand) { contact = default; return; }
            var point = surface.transform.InverseTransformPoint(hand.position) - surface.center;
            var scale = surface.transform.lossyScale;
            float depth = (point.z + surface.size.z * .5f) * Mathf.Abs(scale.z);
            bool within = Mathf.Abs(point.x) <= surface.size.x * .5f + .012f / Mathf.Max(.001f, Mathf.Abs(scale.x))
                && Mathf.Abs(point.y) <= surface.size.y * .5f + .012f / Mathf.Max(.001f, Mathf.Abs(scale.y));
            if (!contact.tracked || (hand.position - contact.point).sqrMagnitude > .16f)
            {
                contact = new Contact { tracked = true, point = hand.position, depth = depth };
                return;
            }
            if (!within || depth > .10f || depth < -.3f)
            {
                contact.armed = contact.down = false;
                contact.ready = 0;
            }
            else if (depth < -.045f)
            {
                contact.down = false;
                contact.ready += Time.unscaledDeltaTime;
                contact.armed = contact.ready >= .045f;
            }
            else if (contact.armed && !contact.down)
            {
                depression = Mathf.Max(depression, Mathf.InverseLerp(-.014f, .008f, depth));
                if (depth >= .008f && contact.depth < .008f)
                {
                    contact.down = true;
                    contact.armed = false;
                    contact.ready = 0;
                    source = hand;
                    Activate();
                    source = null;
                }
            }
            else if (contact.down) depression = Mathf.Max(depression, Mathf.Clamp01(1 + depth * 20));
            contact.point = hand.position;
            contact.depth = depth;
        }

        void OnDisable() { left = default; right = default; source = null; }
    }
}

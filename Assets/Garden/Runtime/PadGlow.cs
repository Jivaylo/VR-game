using UnityEngine;
using RealityPlayground;
using RealityPlayground.Story;

namespace Garden
{
    public sealed class PadGlow : MonoBehaviour
    {
        public Renderer ring;
        public Renderer aim;
        public Transform model;
        public Renderer[] parts;
        public Animator motion;
        public Collider target;
        public StoryNavMarker route;
        public Color idleColor = new Color(.18f, .58f, .61f);
        public Color focusColor = new Color(.65f, 1f, .88f);
        public Color blockedColor = new Color(.23f, .28f, .29f);
        public bool Focused { get; private set; }
        public bool Visible { get; private set; }
        public float Focus { get; private set; }
        MaterialPropertyBlock properties;
        Vector3 scale;
        Vector3 modelScale;
        Vector3 aimPosition;
        bool initialized;
        bool shown;
        bool visibilitySet;
        float phase;
        int hoverLayer = -1;
        static readonly int HoverState = Animator.StringToHash("Hover.Hover");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void Init()
        {
            if (initialized || !ring) return;
            initialized = true;
            properties = new MaterialPropertyBlock();
            scale = ring.transform.localScale;
            if (model) modelScale = model.localScale;
            if (aim) aimPosition = aim.transform.localPosition;
            phase = (transform.position.x + transform.position.z) * .37f;
            if (motion)
            {
                hoverLayer = motion.GetLayerIndex("Hover");
                motion.updateMode = AnimatorUpdateMode.UnscaledTime;
                motion.cullingMode = AnimatorCullingMode.CullCompletely;
                motion.keepAnimatorStateOnDisable = true;
                motion.enabled = false;
            }
        }

        void Update()
        {
            if (route) Show(route.Available, route.Available, route.anchor && route.anchor.isHovered);
        }

        public void Show(bool visible, bool ready, bool hovered)
        {
            Init();
            if (!initialized) return;
            if (visible && !route && RealityPlayer.Head)
            {
                var offset = transform.position - RealityPlayer.FeetPosition;
                if (offset.x * offset.x + offset.z * offset.z < .1225f && Mathf.Abs(offset.y) < .35f) visible = false;
            }
            Visible = visible;
            bool wasFocused = Focused;
            Focused = visible && ready && hovered;
            Focus = Mathf.MoveTowards(Focus, Focused ? 1 : 0, Time.unscaledDeltaTime * 7f);
            if (!visibilitySet || shown != visible || ring.enabled != visible)
            {
                if (parts != null)
                    for (int i = 0; i < parts.Length; i++)
                        if (parts[i]) parts[i].enabled = visible;
                ring.enabled = visible;
                if (motion) motion.enabled = visible;
                visibilitySet = true;
            }
            if (target && route) target.enabled = visible && ready;
            if (aim) aim.enabled = visible && Focus > .01f;
            if (motion)
            {
                if (visible && !shown) motion.Play(0, 0, Mathf.Repeat(phase, 1));
                if (hoverLayer >= 0)
                {
                    if (Focused && !wasFocused) motion.Play(HoverState, hoverLayer, 0);
                    motion.SetLayerWeight(hoverLayer, Focus);
                }
                motion.speed = 1f + Focus * .35f;
            }
            shown = visible;
            if (!visible) return;
            float wave = Mathf.Sin(Time.unscaledTime * 3f + phase);
            float size = 1f + (ready ? .025f * wave : 0) + Focus * .12f;
            var color = Color.Lerp(ready ? idleColor * (.9f + .1f * wave) : blockedColor, focusColor, Focus);
            if (model)
            {
                model.localScale = modelScale * Mathf.Lerp(1f, 1.08f, Focus);
                SetTint(color, ready ? Mathf.Lerp(.75f, 1.35f, Focus) : .12f);
                if (parts != null)
                    for (int i = 0; i < parts.Length; i++)
                        if (parts[i]) parts[i].SetPropertyBlock(properties);
            }
            else
            {
                ring.transform.localScale = new Vector3(scale.x * size, scale.y, scale.z * size);
                Tint(ring, color);
            }
            if (!aim || Focus <= .01f) return;
            aim.transform.localRotation = Quaternion.Euler(0, Time.unscaledTime * 32f, 0);
            aim.transform.localPosition = aimPosition + Vector3.up * Focus * .018f;
            aim.transform.localScale = Vector3.one * Mathf.Lerp(.78f, 1f, Focus);
            Tint(aim, focusColor * (.75f + .25f * Focus));
        }

        void Tint(Renderer target, Color color, float emission = 0)
        {
            SetTint(color, emission);
            target.SetPropertyBlock(properties);
        }

        void SetTint(Color color, float emission)
        {
            color.a = 1;
            properties.SetColor(BaseColor, color);
            properties.SetColor(ColorId, color);
            properties.SetColor(Emission, color * emission);
        }

        void OnDisable()
        {
            Visible = false;
            Focused = false;
            Focus = 0;
            shown = false;
            visibilitySet = false;
            if (motion) motion.enabled = false;
            if (target) target.enabled = false;
            if (initialized && model) model.localScale = modelScale;
            if (initialized && ring && !model) ring.transform.localScale = scale;
            if (parts != null)
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i]) parts[i].enabled = false;
            if (aim) aim.enabled = false;
        }
    }
}

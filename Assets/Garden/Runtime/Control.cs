using UnityEngine;

namespace Garden
{
    public sealed class Control : MonoBehaviour
    {
        public Loop loop;
        public string action;
        public Grip grip;
        public Button button;
        public GameObject model;
        public bool hide;
        public bool Ready { get; private set; }
        float next;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        bool initialized;
        bool last;

        void Awake()
        {
            if (!loop) loop = GetComponentInParent<Loop>();
            if (!grip) grip = GetComponent<Grip>();
            if (!button) button = GetComponent<Button>();
            if (!model && grip) model = grip.model ? grip.model.gameObject : null;
            if (!model && button) model = button.visual ? button.visual.gameObject : null;
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + .2f;
            Refresh();
        }

        public void Refresh()
        {
            if (!loop) loop = Loop.Instance;
            if (!loop) return;
            Ready = loop.CanUse(action);
            if (grip) grip.interactive = Ready;
            if (button) button.enabled = Ready;
            if (initialized && last == Ready) return;
            initialized = true;
            last = Ready;
            if (hide && model && model != gameObject) model.SetActive(Ready);
            if (renderers == null || block == null) return;
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                if (hide) renderer.enabled = Ready;
                if (renderer.GetComponent<TMPro.TMP_Text>()) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Ready ? new Color(.27f, .68f, .63f) : new Color(.14f, .19f, .20f));
                renderer.SetPropertyBlock(block);
            }
        }
    }
}

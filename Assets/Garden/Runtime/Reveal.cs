using System.Collections;
using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Reveal : MonoBehaviour
    {
        public Loop loop;
        public Material material;
        public float duration = 4.2f;
        public AudioSource rupture;
        public IEnumerator Play()
        {
            if (!loop || !loop.overlays || !material) yield break;
            var renderers = loop.overlays.GetComponentsInChildren<Renderer>(false);
            Vector3 origin = RealityPlayer.Head ? RealityPlayer.Head.position : transform.position;
            var block = new MaterialPropertyBlock();
            Shader.SetGlobalFloat("_GardenBreak", 0);
            foreach (var surface in renderers)
            {
                if (!surface || surface is ParticleSystemRenderer || surface is LineRenderer) continue;
                if (surface.GetComponent<TMP_Text>()) { surface.enabled = false; continue; }
                var materials = surface.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    Color color = source && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source && source.HasProperty("_Color") ? source.GetColor("_Color") : new Color(.25f,.7f,.64f);
                    block.Clear();
                    block.SetColor("_Color", color);
                    block.SetFloat("_Delay", Mathf.Clamp01(Vector3.Distance(surface.bounds.center, origin) / 36f) * .45f);
                    surface.SetPropertyBlock(block, i);
                    materials[i] = material;
                }
                surface.sharedMaterials = materials;
            }
            foreach (var motion in loop.overlays.GetComponentsInChildren<Holo>()) motion.enabled = false;
            foreach (var motion in loop.overlays.GetComponentsInChildren<DistrictMotion>()) motion.enabled = false;
            if (rupture) rupture.Play();
            float volume = loop.ambience ? loop.ambience.volume : .2f;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                Shader.SetGlobalFloat("_GardenBreak", progress * 1.5f);
                if (loop.ambience) loop.ambience.volume = volume * (1 - progress);
                yield return null;
            }
            loop.overlays.SetActive(false);
            if (loop.ambience) loop.ambience.volume = volume;
            Shader.SetGlobalFloat("_GardenBreak", 0);
        }
    }
}

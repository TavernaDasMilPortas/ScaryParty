using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScaryParty.Pizzeria.Presentation
{
    public sealed class KitchenItemFade : MonoBehaviour
    {
        private readonly Dictionary<Renderer, Material[]> _originals = new Dictionary<Renderer, Material[]>();
        private readonly List<(Material material, string property, Color color)> _materials = new List<(Material, string, Color)>();
        private float _start;
        private float _duration;

        public void Begin(float duration)
        {
            Restore();
            _start = Time.unscaledTime;
            _duration = Mathf.Max(0.01f, duration);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var copies = new Material[originals.Length];
                _originals[renderer] = originals;
                for (int i = 0; i < originals.Length; i++)
                {
                    if (originals[i] == null) continue;
                    var material = copies[i] = new Material(originals[i]);
                    string property = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                    Color color = material.HasProperty(property) ? material.GetColor(property) : Color.white;
                    if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                    if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                    if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    _materials.Add((material, property, color));
                }
                renderer.sharedMaterials = copies;
            }
            Apply(0f);
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _start) / _duration);
            Apply(t);
            if (t >= 1f) { Restore(); Destroy(this); }
        }

        private void Apply(float alpha)
        {
            foreach (var entry in _materials)
            {
                if (!entry.material.HasProperty(entry.property)) continue;
                var color = entry.color;
                color.a *= alpha;
                entry.material.SetColor(entry.property, color);
            }
        }

        private void Restore()
        {
            foreach (var entry in _originals)
                if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
            foreach (var entry in _materials)
                if (entry.material != null) Destroy(entry.material);
            _originals.Clear();
            _materials.Clear();
        }

        private void OnDestroy() => Restore();
    }
}
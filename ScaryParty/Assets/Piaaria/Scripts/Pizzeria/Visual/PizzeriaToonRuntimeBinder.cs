using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Componente opcional anexado à pizzaria para remapear em tempo de execução
    /// materiais de instâncias dinâmicas locais (ingredientes, pizzas prontas, embalagens)
    /// geradas durante simulações no showcase, sem alterar fábricas nem o estado de rede.
    /// </summary>
    [AddComponentMenu("ScaryParty/Pizzeria/Visual/Pizzeria Toon Runtime Binder")]
    public class PizzeriaToonRuntimeBinder : MonoBehaviour
    {
        [System.Serializable]
        public struct MaterialRemapEntry
        {
            public Material sourceMaterial;
            public Material toonMaterial;
        }

        [Header("Mapeamento Dinâmico")]
        [Tooltip("Tabela de substituição local de materiais gerados em tempo de jogo.")]
        [SerializeField] private List<MaterialRemapEntry> _remapEntries = new List<MaterialRemapEntry>();

        [Tooltip("Quando ativado, monitora adições de filhos sob as bancadas e aplica a substituição.")]
        [SerializeField] private bool _autoRemapSpawnedChildren = true;

        private readonly Dictionary<Material, Material> _lookup = new Dictionary<Material, Material>();

        private void Awake()
        {
            BuildLookup();
        }

        public void RegisterRemap(Material source, Material toon)
        {
            if (source == null || toon == null) return;
            _lookup[source] = toon;
        }

        public void ApplyToRenderer(Renderer r)
        {
            if (r == null || _lookup.Count == 0) return;
            Material[] shared = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] != null && _lookup.TryGetValue(shared[i], out Material toonMat))
                {
                    shared[i] = toonMat;
                    changed = true;
                }
            }
            if (changed)
            {
                r.sharedMaterials = shared;
            }
        }

        public void ApplyToHierarchy(GameObject root)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ApplyToRenderer(renderers[i]);
            }
        }

        private void BuildLookup()
        {
            _lookup.Clear();
            for (int i = 0; i < _remapEntries.Count; i++)
            {
                var entry = _remapEntries[i];
                if (entry.sourceMaterial != null && entry.toonMaterial != null)
                {
                    _lookup[entry.sourceMaterial] = entry.toonMaterial;
                }
            }
        }
    }
}

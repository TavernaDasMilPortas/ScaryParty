using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Interaction
{
    [DisallowMultipleComponent]
    public class OutlineTarget : MonoBehaviour
    {
        [Header("Renderers")]
        [Tooltip("Renderers incluídos explicitamente no contorno. Se vazio, descobre automaticamente.")]
        [SerializeField] private Renderer[] _eligibleRenderers;

        [Tooltip("Renderers ou objetos excluídos explicitamente (ex: textos, Canvas, barras, partículas).")]
        [SerializeField] private Renderer[] _excludedRenderers;

        [Header("Estilo do Contorno")]
        public Color outlineColor = new Color(1f, 0.85f, 0.2f, 1f); // Dourado/Amarelo padrão
        [Range(0f, 10f)]
        public float outlineWidth = 2.5f;

        [Header("Oclusão")]
        [Tooltip("Se true, o contorno é ocluído por objetos à frente (ZTest LEqual).")]
        public bool occludedByGeometry = true;

        private readonly List<Renderer> _runtimeRenderers = new List<Renderer>();
        private bool _isCacheValid = false;

        public IReadOnlyList<Renderer> GetEligibleRenderers()
        {
            if (_isCacheValid && _runtimeRenderers.Count > 0)
            {
                // Verifica se algum renderer foi destruído
                bool allAlive = true;
                for (int i = 0; i < _runtimeRenderers.Count; i++)
                {
                    if (_runtimeRenderers[i] == null)
                    {
                        allAlive = false;
                        break;
                    }
                }
                if (allAlive) return _runtimeRenderers;
            }

            RebuildRendererCache();
            return _runtimeRenderers;
        }

        public void RebuildRendererCache()
        {
            _runtimeRenderers.Clear();

            var excludeSet = new HashSet<Renderer>();
            if (_excludedRenderers != null)
            {
                for (int i = 0; i < _excludedRenderers.Length; i++)
                {
                    if (_excludedRenderers[i] != null)
                        excludeSet.Add(_excludedRenderers[i]);
                }
            }

            if (_eligibleRenderers != null && _eligibleRenderers.Length > 0)
            {
                for (int i = 0; i < _eligibleRenderers.Length; i++)
                {
                    var r = _eligibleRenderers[i];
                    if (r != null && !excludeSet.Contains(r) && IsRendererAllowed(r))
                    {
                        _runtimeRenderers.Add(r);
                    }
                }
                _isCacheValid = true;
                return;
            }

            // Descoberta automática respeitando exclusões de TMP, Canvas, partículas e filhos interagíveis
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                var r = allRenderers[i];
                if (r == null || excludeSet.Contains(r)) continue;

                // Não incluir renderers de outros interagíveis/alvos filhos (nem de slots vinculados com seu próprio OutlineTarget)
                if (r.transform != transform)
                {
                    var childTarget = r.GetComponentInParent<OutlineTarget>();
                    if (childTarget != null && childTarget != this) continue;

                    var childInteractable = r.GetComponentInParent<IInteractable>();
                    if (childInteractable != null && (childInteractable as Component) != (this.GetComponent<IInteractable>() as Component))
                        continue;
                }

                if (IsRendererAllowed(r))
                {
                    _runtimeRenderers.Add(r);
                }
            }

            _isCacheValid = true;
        }

        private bool IsRendererAllowed(Renderer r)
        {
            if (r == null) return false;
            if (r is ParticleSystemRenderer) return false;
            if (r.GetComponent<CanvasRenderer>() != null) return false;
            if (r.GetComponent<TMPro.TMP_Text>() != null) return false;

            string name = r.gameObject.name;
            if (name.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("canvas", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return true;
        }

        public void InvalidateCache()
        {
            _isCacheValid = false;
        }

        private void OnDisable()
        {
            if (OutlineService.Instance != null && OutlineService.Instance.CurrentTarget == this)
            {
                OutlineService.Instance.ClearTarget(this);
            }
        }

        private void OnDestroy()
        {
            if (OutlineService.Instance != null && OutlineService.Instance.CurrentTarget == this)
            {
                OutlineService.Instance.ClearTarget(this);
            }
        }
    }
}

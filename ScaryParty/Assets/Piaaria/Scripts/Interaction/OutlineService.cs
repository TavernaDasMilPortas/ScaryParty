using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScaryParty.Interaction
{
    [DefaultExecutionOrder(200)]
    public class OutlineService : MonoBehaviour
    {
        public static OutlineService Instance { get; private set; }

        [Header("Materiais de Contorno URP")]
        [SerializeField] private Material _maskMaterial;
        [SerializeField] private Material _fillMaterial;

        [Header("Configuração Global Padrão")]
        public Color defaultOutlineColor = new Color(1f, 0.85f, 0.2f, 1f);
        public float defaultOutlineWidth = 2.5f;

        public OutlineTarget CurrentTarget { get; private set; }
        public bool HasActiveHighlight => CurrentTarget != null && CurrentTarget.gameObject.activeInHierarchy && CurrentTarget.enabled;

        private bool _lastNotifiedHighlightState;
        private MaterialPropertyBlock _propBlock;
        private Material _fillMaterialOccluded;
        private Material _fillMaterialAlways;
        private bool _createdMaskMaterial;
        private bool _createdFillMaterial;
        private Camera _cachedMainCamera;
        private readonly Dictionary<SkinnedMeshRenderer, Mesh> _tempSkinnedMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                // This service can live on a network player; never destroy its owner.
                enabled = false;
                Destroy(this);
                return;
            }

            _propBlock = new MaterialPropertyBlock();
            EnsureMaterials();
        }

        public void EnsureMaterials()
        {
            if (_maskMaterial == null)
            {
                var maskShader = Shader.Find("ScaryParty/OutlineMaskURP") ?? Shader.Find("Custom/Outline Mask");
                if (maskShader != null)
                {
                    _maskMaterial = new Material(maskShader) { name = "OutlineMask_AutoInstance" };
                    _createdMaskMaterial = true;
                }
            }

            if (_fillMaterial == null)
            {
                var fillShader = Shader.Find("ScaryParty/OutlineFillURP") ?? Shader.Find("Custom/Outline Fill");
                if (fillShader != null)
                {
                    _fillMaterial = new Material(fillShader) { name = "OutlineFill_AutoInstance" };
                    _createdFillMaterial = true;
                }
            }

            if (_fillMaterial != null)
            {
                if (_fillMaterialOccluded == null)
                {
                    _fillMaterialOccluded = new Material(_fillMaterial) { name = "OutlineFill_Occluded" };
                    _fillMaterialOccluded.SetFloat("_ZTest", 4f);
                }
                if (_fillMaterialAlways == null)
                {
                    _fillMaterialAlways = new Material(_fillMaterial) { name = "OutlineFill_Always" };
                    _fillMaterialAlways.SetFloat("_ZTest", 8f);
                }
            }
        }

        public void SetTarget(OutlineTarget target)
        {
            if (CurrentTarget == target) return;
            CurrentTarget = target;
        }

        public void ClearTarget(OutlineTarget target = null)
        {
            if (target == null || CurrentTarget == target)
            {
                CurrentTarget = null;
            }
        }

        private void LateUpdate()
        {
            if (CurrentTarget == null || !CurrentTarget.gameObject.activeInHierarchy || !CurrentTarget.enabled)
            {
                CurrentTarget = null;
                NotifyHighlightState(false);
                return;
            }

            if (_maskMaterial == null || _fillMaterial == null)
            {
                EnsureMaterials();
                if (_maskMaterial == null || _fillMaterial == null)
                {
                    NotifyHighlightState(false);
                    return;
                }
            }

            var renderers = CurrentTarget.GetEligibleRenderers();
            if (renderers == null || renderers.Count == 0)
            {
                NotifyHighlightState(false);
                return;
            }

            NotifyHighlightState(true);

            Color targetColor = CurrentTarget.outlineColor;
            float targetWidth = CurrentTarget.outlineWidth > 0f ? CurrentTarget.outlineWidth : defaultOutlineWidth;
            Material activeFillMat = CurrentTarget.occludedByGeometry
                ? (_fillMaterialOccluded != null ? _fillMaterialOccluded : _fillMaterial)
                : (_fillMaterialAlways != null ? _fillMaterialAlways : _fillMaterial);

            _propBlock.SetColor("_OutlineColor", targetColor);
            _propBlock.SetFloat("_OutlineWidth", targetWidth);

            if (_cachedMainCamera == null || !_cachedMainCamera.isActiveAndEnabled)
                _cachedMainCamera = Camera.main;
            Camera cam = _cachedMainCamera;

            for (int rIdx = 0; rIdx < renderers.Count; rIdx++)
            {
                var rend = renderers[rIdx];
                if (rend == null || !rend.enabled || !rend.gameObject.activeInHierarchy) continue;

                int layer = rend.gameObject.layer;

                if (rend is MeshRenderer mr)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    Mesh mesh = mf.sharedMesh;
                    Matrix4x4 matrix = mr.transform.localToWorldMatrix;
                    int submeshCount = mesh.subMeshCount;

                    for (int s = 0; s < submeshCount; s++)
                    {
                        Graphics.DrawMesh(mesh, matrix, _maskMaterial, layer, cam, s, _propBlock);
                        Graphics.DrawMesh(mesh, matrix, activeFillMat, layer, cam, s, _propBlock);
                    }
                }
                else if (rend is SkinnedMeshRenderer smr)
                {
                    if (smr.sharedMesh == null) continue;

                    if (!_tempSkinnedMeshes.TryGetValue(smr, out var bakedMesh) || bakedMesh == null)
                    {
                        bakedMesh = new Mesh { name = $"Temp_Skinned_{smr.name}" };
                        _tempSkinnedMeshes[smr] = bakedMesh;
                    }

                    smr.BakeMesh(bakedMesh);
                    Matrix4x4 matrix = smr.transform.localToWorldMatrix;
                    int submeshCount = bakedMesh.subMeshCount;

                    for (int s = 0; s < submeshCount; s++)
                    {
                        Graphics.DrawMesh(bakedMesh, matrix, _maskMaterial, layer, cam, s, _propBlock);
                        Graphics.DrawMesh(bakedMesh, matrix, activeFillMat, layer, cam, s, _propBlock);
                    }
                }
            }
        }

        private void NotifyHighlightState(bool active)
        {
            if (_lastNotifiedHighlightState == active) return;
            _lastNotifiedHighlightState = active;
            ScaryParty.VisualStyles.VisualStyleManager.SetInteractionHighlightActive(active);
        }

        private void OnDisable()
        {
            NotifyHighlightState(false);
        }

        private void OnDestroy()
        {
            NotifyHighlightState(false);
            if (Instance == this) Instance = null;

            foreach (var kvp in _tempSkinnedMeshes)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _tempSkinnedMeshes.Clear();

            if (_fillMaterialOccluded != null) { Destroy(_fillMaterialOccluded); _fillMaterialOccluded = null; }
            if (_fillMaterialAlways != null) { Destroy(_fillMaterialAlways); _fillMaterialAlways = null; }
            if (_createdMaskMaterial && _maskMaterial != null) { Destroy(_maskMaterial); _maskMaterial = null; }
            if (_createdFillMaterial && _fillMaterial != null) { Destroy(_fillMaterial); _fillMaterial = null; }
        }
    }
}

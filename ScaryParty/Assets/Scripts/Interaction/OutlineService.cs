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

        private MaterialPropertyBlock _propBlock;
        private readonly Dictionary<SkinnedMeshRenderer, Mesh> _tempSkinnedMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
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
                }
            }

            if (_fillMaterial == null)
            {
                var fillShader = Shader.Find("ScaryParty/OutlineFillURP") ?? Shader.Find("Custom/Outline Fill");
                if (fillShader != null)
                {
                    _fillMaterial = new Material(fillShader) { name = "OutlineFill_AutoInstance" };
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
                return;
            }

            if (_maskMaterial == null || _fillMaterial == null)
            {
                EnsureMaterials();
                if (_maskMaterial == null || _fillMaterial == null) return;
            }

            var renderers = CurrentTarget.GetEligibleRenderers();
            if (renderers == null || renderers.Count == 0) return;

            Color targetColor = CurrentTarget.outlineColor;
            float targetWidth = CurrentTarget.outlineWidth > 0f ? CurrentTarget.outlineWidth : defaultOutlineWidth;
            float zTestVal = CurrentTarget.occludedByGeometry ? 4f : 8f; // 4 = LEqual, 8 = Always

            _propBlock.SetColor("_OutlineColor", targetColor);
            _propBlock.SetFloat("_OutlineWidth", targetWidth);
            _propBlock.SetFloat("_ZTest", zTestVal);

            Camera cam = Camera.main;

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
                        Graphics.DrawMesh(mesh, matrix, _fillMaterial, layer, cam, s, _propBlock);
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
                        Graphics.DrawMesh(bakedMesh, matrix, _fillMaterial, layer, cam, s, _propBlock);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            foreach (var kvp in _tempSkinnedMeshes)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _tempSkinnedMeshes.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Wizard Odin para catalogar peças reais da pizzaria modular, comparar preview L vs T,
    /// aplicar âncoras/regiões de pátio idempotentemente com Undo e validar cobertura física/navegação.
    /// </summary>
    public class PizzeriaPatioLayoutWizard : OdinEditorWindow
    {
        private const string PreviewContainerName = "__PizzeriaPatioLayoutPreview";

        [MenuItem("Tools/Scary Party/Wizards/Pizzaria/Pátio e Layout Modular (L-T)")]
        public static void OpenWindow()
        {
            GetWindow<PizzeriaPatioLayoutWizard>("Pátio & Layout L/T");
        }

        [TabGroup("Layout")]
        [AssetsOnly]
        public GameObject modularPrefab;

        [TabGroup("Layout")]
        public PizzeriaLayoutProfile profile;

        [TabGroup("Layout")]
        [EnumToggleButtons]
        public PizzeriaLayoutVariant previewVariant = PizzeriaLayoutVariant.L_RecuoOperacional;

        [TabGroup("Peças")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 12)]
        public List<ModularPieceCatalogEntry> catalogPreview = new List<ModularPieceCatalogEntry>();

        [TabGroup("Validação")]
        [ReadOnly, MultiLineProperty(8)]
        public string lastValidationReport = "Clique em 'Validar Setup' para inspecionar o pátio e as âncoras.";

        private void OnEnable()
        {
            if (modularPrefab == null)
            {
                modularPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab");
            }
        }

        [TabGroup("Peças")]
        [Button("Escanear Peças Reais do Prefab", ButtonSizes.Medium)]
        public void ScanModularPieces()
        {
            catalogPreview.Clear();
            if (modularPrefab == null)
            {
                lastValidationReport = "Prefab modular não atribuído.";
                return;
            }

            var filters = modularPrefab.GetComponentsInChildren<MeshFilter>(true);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                string key = $"{mf.gameObject.name}:{mf.sharedMesh.name}";
                if (!seen.Add(key)) continue;

                var rend = mf.GetComponent<Renderer>();
                var col = mf.GetComponent<Collider>();
                string lower = mf.gameObject.name.ToLowerInvariant();
                string cat = lower.Contains("parede") ? "Parede"
                    : lower.Contains("piso") ? "Piso"
                    : lower.Contains("porta") ? "Porta"
                    : lower.Contains("janela") ? "Janela"
                    : "Outro";

                catalogPreview.Add(new ModularPieceCatalogEntry
                {
                    pieceName = mf.gameObject.name,
                    category = cat,
                    boundsSize = mf.sharedMesh.bounds.size,
                    boundsCenter = mf.sharedMesh.bounds.center,
                    hasCollider = col != null,
                    materialCount = rend != null && rend.sharedMaterials != null ? rend.sharedMaterials.Length : 0
                });
            }

            if (profile != null)
            {
                Undo.RecordObject(profile, "Scan Pizzeria Modular Pieces");
                profile.scannedPieces = new List<ModularPieceCatalogEntry>(catalogPreview);
                EditorUtility.SetDirty(profile);
            }

            lastValidationReport = $"Escaneadas {catalogPreview.Count} peças reais com MeshFilter em {modularPrefab.name}.";
        }

        [TabGroup("Layout")]
        [Button("Gerar Preview na Cena (L / T)", ButtonSizes.Large)]
        public void BuildScenePreview()
        {
            RevertScenePreview();

            var root = FindFirstObjectByType<PizzeriaRoot>();
            Transform parent = root != null ? root.transform : null;

            var previewRoot = new GameObject(PreviewContainerName);
            Undo.RegisterCreatedObjectUndo(previewRoot, "Preview Pizzeria Patio L/T");
            if (parent != null)
            {
                previewRoot.transform.SetParent(parent, false);
            }

            var fp = root != null ? root.GetComponent<PizzeriaFootprint>() : null;
            float floorY = fp != null ? fp.floorSurfaceLocalY : -1.6f;

            Rect patioRect = previewVariant == PizzeriaLayoutVariant.T_PatioLateral
                ? (profile != null ? profile.patioRectT : Rect.MinMaxRect(-16.4f, -27.6f, 0f, -9.2f))
                : (profile != null ? profile.patioRectL : Rect.MinMaxRect(0f, -27.6f, 16.4f, -13.2f));

            CreatePreviewBox(previewRoot.transform, $"Preview_Patio_{previewVariant}", patioRect, floorY, 0.08f, new Color(0.2f, 0.7f, 0.35f, 0.45f));
            lastValidationReport = $"Preview {previewVariant} criado: X=[{patioRect.xMin:F1}, {patioRect.xMax:F1}], Z=[{patioRect.yMin:F1}, {patioRect.yMax:F1}].";
        }

        [TabGroup("Layout")]
        [Button("Reverter Preview", ButtonSizes.Medium)]
        public void RevertScenePreview()
        {
            var existing = GameObject.Find(PreviewContainerName);
            while (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                existing = GameObject.Find(PreviewContainerName);
            }
        }

        [TabGroup("Pátio")]
        [Button("Aplicar Pátio e Âncoras na Cena (Idempotente)", ButtonSizes.Large)]
        public void ApplyToSceneRoot()
        {
            RevertScenePreview();

            var root = FindFirstObjectByType<PizzeriaRoot>();
            if (root == null)
            {
                lastValidationReport = "Nenhum PizzeriaRoot encontrado na cena aberta.";
                return;
            }

            Undo.RecordObject(root.gameObject, "Apply Pizzeria Patio & Anchors");
            var fp = root.GetComponent<PizzeriaFootprint>();
            if (fp != null)
            {
                Undo.RecordObject(fp, "Update PizzeriaFootprint Exterior");
                Rect targetPatio = previewVariant == PizzeriaLayoutVariant.T_PatioLateral
                    ? Rect.MinMaxRect(-16.4f, -27.6f, 0f, -9.2f)
                    : Rect.MinMaxRect(0f, -27.6f, 16.4f, -13.2f);
                fp.exteriorRegions = new[] { targetPatio };
                EditorUtility.SetDirty(fp);
            }

            PizzeriaParkingLotBuilder.EnsureBuilt(root);
            EditorUtility.SetDirty(root.gameObject);
            ValidateCurrentSetup();
        }

        [TabGroup("Validação")]
        [Button("Validar Setup", ButtonSizes.Medium)]
        public void ValidateCurrentSetup()
        {
            var root = FindFirstObjectByType<PizzeriaRoot>();
            if (root == null)
            {
                lastValidationReport = "Nenhum PizzeriaRoot na cena aberta para validar.";
                return;
            }

            var fp = root.GetComponent<PizzeriaFootprint>();
            var anchors = root.GetComponent<PizzeriaExteriorAnchors>();

            int issues = 0;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Relatório de Validação ({root.name}):");
            if (fp == null) { sb.AppendLine("• [ERRO] PizzeriaFootprint ausente."); issues++; }
            else
            {
                sb.AppendLine($"• Regiões estruturais: {fp.GetStructuralRegions().Length}, externas: {fp.GetExteriorRegions().Length}");
            }

            if (anchors == null || !anchors.HasCompleteExteriorAnchors())
            {
                sb.AppendLine("• [AVISO] PizzeriaExteriorAnchors incompleto (execute Aplicar Pátio).");
                issues++;
            }
            else
            {
                sb.AppendLine("• [OK] Todas as âncoras externas obrigatórias estão conectadas.");
            }

            var pad = root.transform.Find("__PizzeriaExteriorFacilities/ParkingLotPad");
            if (pad == null || pad.GetComponent<Collider>() == null)
            {
                sb.AppendLine("• [AVISO] Piso do pátio sem colisor físico.");
                issues++;
            }
            else
            {
                sb.AppendLine("• [OK] Piso do pátio possui BoxCollider ativo.");
            }

            sb.AppendLine(issues == 0 ? "Status: APROVADO" : $"Status: {issues} pendência(s).");
            lastValidationReport = sb.ToString();
        }

        private static void CreatePreviewBox(Transform parent, string name, Rect rect, float y, float height, Color tint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(rect.center.x, y + height * 0.5f, rect.center.y);
            go.transform.localScale = new Vector3(rect.width, height, rect.height);
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var pb = new MaterialPropertyBlock();
                ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(rend, 0, tint, pb);
            }
        }
    }
}

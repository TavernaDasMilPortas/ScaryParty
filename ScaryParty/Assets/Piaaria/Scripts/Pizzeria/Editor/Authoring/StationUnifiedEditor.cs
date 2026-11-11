using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Editor unificado para Bancadas e Estações da pizzaria no Odin Content Wizard.
    /// Suporta edição da definição do catálogo (StationDef), do layout compartilhado (StationSlotLayoutAsset)
    /// e de ocorrências físicas no prefab modular (PizzeriaModular_Manual.prefab).
    /// </summary>
    [Serializable]
    public class StationUnifiedEditor
    {
        [HideInInspector] public StationDef Definition;
        [HideInInspector] public StationView PhysicalOccurrence;
        [HideInInspector] public StationSlotLayoutAsset LayoutAsset;

        private PizzeriaSharedViewport3D _embeddedViewport;

        public StationUnifiedEditor(StationDef def)
        {
            Definition = def;
            if (def != null && def.defaultSlotLayout != null)
            {
                LayoutAsset = def.defaultSlotLayout;
            }
            EnsureLayoutExists();
        }

        public StationUnifiedEditor(StationView occurrence)
        {
            PhysicalOccurrence = occurrence;
            if (occurrence != null && occurrence.slotLayoutAsset != null)
            {
                LayoutAsset = occurrence.slotLayoutAsset;
            }
            EnsureLayoutExists();
        }

        public StationUnifiedEditor(StationSlotLayoutAsset layout)
        {
            LayoutAsset = layout;
        }

        private string HeaderTitle => Definition != null
            ? $"Bancada / Estação: {Definition.stationName} (#{Definition.id})"
            : (PhysicalOccurrence != null ? $"Ocorrência: {PhysicalOccurrence.name} (#{PhysicalOccurrence.stationId})" : "Layout de Estação");

        [ShowInInspector, HideLabel, Title("$HeaderTitle", subtitle: null, titleAlignment: TitleAlignments.Centered)]
        [DisplayAsString(false)]
        private string _headerNotice => "Configure aqui o tipo, capacidade, layout geométrico de slots e teste visualmente no tampo com ingredientes e ferramentas.";

        #region Aba 1: Identificação
        [TabGroup("Abas", "📋 Identificação")]
        [ShowIf("@Definition != null")]
        [Title("Dados do Catálogo (StationDef)")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public int ID
        {
            get => Definition != null ? Definition.id : 0;
            set { if (Definition != null) { Definition.id = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📋 Identificação")]
        [ShowIf("@Definition != null")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public string NomeDaEstacao
        {
            get => Definition != null ? Definition.stationName : "";
            set { if (Definition != null) { Definition.stationName = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📋 Identificação")]
        [ShowIf("@Definition != null")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public PizzeriaStationKind TipoDeEstacao
        {
            get => Definition != null ? Definition.defaultStationKind : PizzeriaStationKind.Prep;
            set { if (Definition != null) { Definition.defaultStationKind = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📋 Identificação")]
        [ShowIf("@Definition != null")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public GameObject ModeloVisual
        {
            get => Definition != null ? Definition.visualPrefab : null;
            set { if (Definition != null) { Definition.visualPrefab = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📋 Identificação")]
        [ShowIf("@PhysicalOccurrence != null")]
        [Title("Ocorrência Física no Prefab")]
        [ReadOnly]
        public string OcorrenciaGameObject => PhysicalOccurrence != null ? PhysicalOccurrence.gameObject.name : "Nenhuma";

        [TabGroup("Abas", "📋 Identificação")]
        [ShowIf("@PhysicalOccurrence != null")]
        [ReadOnly]
        public int PhysicalStationId => PhysicalOccurrence != null ? PhysicalOccurrence.stationId : 0;
        #endregion

        #region Aba 2: Layout de Slots & Presets
        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        [OnValueChanged(nameof(OnLayoutChanged))]
        public StationSlotLayoutAsset LayoutDeSlots
        {
            get => LayoutAsset;
            set { LayoutAsset = value; OnLayoutChanged(); }
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset == null")]
        [Button("➕ Criar Novo Layout Reutilizável para esta Estação", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.8f, 0.4f)]
        public void CriarNovoLayout()
        {
            string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria/Layouts";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string baseName = Definition != null ? Definition.name : (PhysicalOccurrence != null ? PhysicalOccurrence.name : "Estacao");
            string assetPath = $"{dir}/Layout_{baseName}.asset";

            var layout = ScriptableObject.CreateInstance<StationSlotLayoutAsset>();
            layout.layoutName = $"Layout_{baseName}";
            layout.targetStationKind = Definition != null ? Definition.defaultStationKind : PizzeriaStationKind.Prep;
            layout.presetType = StationPresetType.Grid;
            layout.rows = 2;
            layout.columns = 2;
            layout.spacingX = 0.35f;
            layout.spacingZ = 0.35f;

            layout.RecalculateFromPreset(out _, out _, out _, out _);

            AssetDatabase.CreateAsset(layout, assetPath);
            AssetDatabase.SaveAssets();

            LayoutAsset = layout;
            if (Definition != null) Definition.defaultSlotLayout = layout;
            if (PhysicalOccurrence != null) PhysicalOccurrence.slotLayoutAsset = layout;

            OnLayoutChanged();
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [BoxGroup("Abas/📐 Layout de Slots & Presets/Configuração do Preset")]
        [OnValueChanged(nameof(OnPresetParamsChanged))]
        public StationPresetType TipoDoPreset
        {
            get => LayoutAsset != null ? LayoutAsset.presetType : StationPresetType.Grid;
            set { if (LayoutAsset != null) { LayoutAsset.presetType = value; OnPresetParamsChanged(); } }
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [BoxGroup("Abas/📐 Layout de Slots & Presets/Configuração do Preset")]
        [OnValueChanged(nameof(OnPresetParamsChanged))]
        public int Linhas
        {
            get => LayoutAsset != null ? LayoutAsset.rows : 2;
            set { if (LayoutAsset != null) { LayoutAsset.rows = Mathf.Clamp(value, 1, 10); OnPresetParamsChanged(); } }
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [BoxGroup("Abas/📐 Layout de Slots & Presets/Configuração do Preset")]
        [OnValueChanged(nameof(OnPresetParamsChanged))]
        public int Colunas
        {
            get => LayoutAsset != null ? LayoutAsset.columns : 2;
            set { if (LayoutAsset != null) { LayoutAsset.columns = Mathf.Clamp(value, 1, 10); OnPresetParamsChanged(); } }
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [BoxGroup("Abas/📐 Layout de Slots & Presets/Configuração do Preset")]
        [OnValueChanged(nameof(OnPresetParamsChanged))]
        public float EspacamentoX
        {
            get => LayoutAsset != null ? LayoutAsset.spacingX : 0.35f;
            set { if (LayoutAsset != null) { LayoutAsset.spacingX = value; OnPresetParamsChanged(); } }
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [BoxGroup("Abas/📐 Layout de Slots & Presets/Configuração do Preset")]
        [OnValueChanged(nameof(OnPresetParamsChanged))]
        public float EspacamentoZ
        {
            get => LayoutAsset != null ? LayoutAsset.spacingZ : 0.35f;
            set { if (LayoutAsset != null) { LayoutAsset.spacingZ = value; OnPresetParamsChanged(); } }
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [BoxGroup("Abas/📐 Layout de Slots & Presets/Configuração do Preset")]
        [OnValueChanged(nameof(OnPresetParamsChanged))]
        public Vector3 OffsetCentral
        {
            get => LayoutAsset != null ? LayoutAsset.originOffset : Vector3.zero;
            set { if (LayoutAsset != null) { LayoutAsset.originOffset = value; OnPresetParamsChanged(); } }
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [Button("🔄 Recalcular Preset (Preservando Slots com Ajuste Manual)", ButtonSizes.Large)]
        [GUIColor(0.25f, 0.75f, 0.95f)]
        public void RecalcularPreset()
        {
            if (LayoutAsset == null) return;
            LayoutAsset.RecalculateFromPreset(out int mantidos, out int movidos, out int criados, out int removidos);
            PizzeriaContentStudioSession.NotifySessionChanged();
            EditorUtility.DisplayDialog("Preset Recalculado",
                $"Slots processados com sucesso!\n\n• Mantidos (ajustes manuais): {mantidos}\n• Movidos (atualizados pelo preset): {movidos}\n• Criados: {criados}\n• Removidos: {removidos}",
                "OK");
        }

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 10)]
        [Title("Slots Numerados da Bancada")]
        public List<StationSlotDefinition> TabelaDeSlots => LayoutAsset != null ? LayoutAsset.slots : null;

        [TabGroup("Abas", "📐 Layout de Slots & Presets")]
        [ShowIf("@LayoutAsset != null")]
        [Button("↩️ Restaurar Todos os Slots ao Preset Puro", ButtonSizes.Medium)]
        public void RestaurarTodosAoPreset()
        {
            if (LayoutAsset == null) return;
            for (int i = 0; i < LayoutAsset.SlotCount; i++)
            {
                LayoutAsset.ResetSlotToPreset(i);
            }
            PizzeriaContentStudioSession.NotifySessionChanged();
        }
        #endregion

        #region Aba 3: Processos & Utensílios
        [TabGroup("Abas", "🔪 Processos & Utensílios")]
        [ShowIf("@Definition != null")]
        [Title("Processos Aceitos nesta Bancada")]
        public List<ProcessDef> ProcessosAceitos
        {
            get => Definition != null ? Definition.acceptedProcesses : null;
            set { if (Definition != null) { Definition.acceptedProcesses = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "🔪 Processos & Utensílios")]
        [ShowIf("@Definition != null")]
        [Title("Overrides de Duração nesta Bancada")]
        public List<ProcessDurationOverride> OverridesDeDuracao
        {
            get => Definition != null ? Definition.durationOverrides : null;
            set { if (Definition != null) { Definition.durationOverrides = value; SaveDefinitionChanges(); } }
        }
        #endregion

        #region Aba 4: Teste Visual 3D & Simulação
        [TabGroup("Abas", "👁️ Teste Visual 3D")]
        [Button("📌 Fixar esta Bancada no Estúdio 3D Independente", ButtonSizes.Large)]
        [GUIColor(1.0f, 0.65f, 0.25f)]
        public void FixarNoStudio()
        {
            PizzeriaContentStudioSession.PinAssembly(
                PhysicalOccurrence,
                Definition,
                0,
                null,
                null);
            PizzeriaContentStudioWindow.ShowWindow();
        }

        [TabGroup("Abas", "👁️ Teste Visual 3D")]
        [Button("🍅 Apoiar Tomate no Slot 2 e Testar Altura do Corte com Faca", ButtonSizes.Medium)]
        [GUIColor(0.35f, 0.85f, 0.45f)]
        public void TestarTomateEFaca()
        {
            IngredientDef tomate = null;
            string[] ingGuids = AssetDatabase.FindAssets("Ingredient_");
            foreach (var g in ingGuids)
            {
                var ing = AssetDatabase.LoadAssetAtPath<IngredientDef>(AssetDatabase.GUIDToAssetPath(g));
                if (ing != null && (ing.name.Contains("Tomate") || ing.name.Contains("Calabresa") || ing.name.Contains("Massa")))
                {
                    tomate = ing;
                    break;
                }
            }

            ToolDef faca = null;
            string[] toolGuids = AssetDatabase.FindAssets("Tool_");
            foreach (var g in toolGuids)
            {
                var t = AssetDatabase.LoadAssetAtPath<ToolDef>(AssetDatabase.GUIDToAssetPath(g));
                if (t != null && t.name.Contains("Faca"))
                {
                    faca = t;
                    break;
                }
            }

            PizzeriaContentStudioSession.PinAssembly(
                PhysicalOccurrence,
                Definition,
                Mathf.Min(2, LayoutAsset != null ? LayoutAsset.SlotCount - 1 : 2),
                tomate,
                faca);

            PizzeriaContentStudioWindow.ShowWindow();
        }

        [TabGroup("Abas", "👁️ Teste Visual 3D")]
        [OnInspectorGUI]
        private void DrawEmbeddedViewport()
        {
            if (_embeddedViewport == null)
            {
                _embeddedViewport = new PizzeriaSharedViewport3D();
            }

            Rect rect = GUILayoutUtility.GetRect(300, 240, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                _embeddedViewport.DrawViewportGUI(rect);
            }
        }
        #endregion

        #region Aba 5: Aplicação & Prefab
        [TabGroup("Abas", "💾 Gravação & Aplicação")]
        [Button("🏗️ Aplicar Layout à Bancada Física no Prefab Stage", ButtonSizes.Large)]
        [GUIColor(0.2f, 0.85f, 0.35f)]
        public void AplicarAoPrefab()
        {
            if (PhysicalOccurrence == null && Definition != null)
            {
                // Tenta achar a ocorrência pelo ID da estação no prefab manual
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PizzeriaPhase1Repair.ManualPrefabPath);
                if (prefab != null)
                {
                    var stations = prefab.GetComponentsInChildren<StationView>(true);
                    PhysicalOccurrence = stations.FirstOrDefault(s => s != null && s.stationId == Definition.id);
                }
            }

            if (PhysicalOccurrence != null && LayoutAsset != null)
            {
                bool ok = LayoutAsset.ApplyToStation(PhysicalOccurrence);
                if (ok)
                {
                    EditorUtility.SetDirty(PhysicalOccurrence);
                    EditorUtility.DisplayDialog("Layout Aplicado", $"Layout aplicado com sucesso à bancada '{PhysicalOccurrence.name}' com {LayoutAsset.SlotCount} slots!", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Aviso", "Nenhuma ocorrência física vinculada ou layout de slots ausente.", "OK");
            }
        }
        #endregion

        private void EnsureLayoutExists()
        {
            if (LayoutAsset == null && Definition != null && Definition.defaultSlotLayout != null)
            {
                LayoutAsset = Definition.defaultSlotLayout;
            }
        }

        private void OnLayoutChanged()
        {
            if (LayoutAsset != null)
            {
                EditorUtility.SetDirty(LayoutAsset);
                PizzeriaContentStudioSession.NotifySessionChanged();
            }
        }

        private void OnPresetParamsChanged()
        {
            if (LayoutAsset != null)
            {
                LayoutAsset.RecalculateFromPreset(out _, out _, out _, out _);
                OnLayoutChanged();
            }
        }

        private void SaveDefinitionChanges()
        {
            if (Definition != null)
            {
                EditorUtility.SetDirty(Definition);
                PizzeriaContentStudioSession.NotifySessionChanged();
            }
        }
    }
}

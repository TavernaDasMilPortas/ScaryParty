using System;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Editor unificado de Processos Culinários para o PizzeriaContentWizard.
    /// Permite conectar entrada, saída, utensílio, estação e testar a simulação interativa
    /// de início, meio (contato do corte) e fim (transformação para produto preparado).
    /// </summary>
    [Serializable]
    public class ProcessUnifiedEditor
    {
        [HideInInspector] public ProcessDef Definition;

        public ProcessUnifiedEditor(ProcessDef def)
        {
            Definition = def;
        }

        private string HeaderTitle => Definition != null
            ? $"Processo: {Definition.processName} (#{Definition.id})"
            : "Processo Culinário";

        [ShowInInspector, HideLabel, Title("$HeaderTitle", subtitle: null, titleAlignment: TitleAlignments.Centered)]
        [DisplayAsString(false)]
        private string _headerNotice => "Configure a transformação física e temporal do ingrediente e simule o corte no Estúdio 3D.";

        #region Aba 1: Vínculos & Parâmetros
        [TabGroup("Abas", "⚙️ Configuração")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public int ID
        {
            get => Definition != null ? Definition.id : 0;
            set { if (Definition != null) { Definition.id = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "⚙️ Configuração")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public string NomeDoProcesso
        {
            get => Definition != null ? Definition.processName : "";
            set { if (Definition != null) { Definition.processName = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "⚙️ Configuração")]
        [Title("Entrada & Saída")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public IngredientDef IngredienteEntrada
        {
            get => Definition != null ? Definition.inputIngredient : null;
            set { if (Definition != null) { Definition.inputIngredient = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "⚙️ Configuração")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public ProcessingStage EstagioEntrada
        {
            get => Definition != null ? Definition.inputStage : ProcessingStage.Raw;
            set { if (Definition != null) { Definition.inputStage = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "⚙️ Configuração")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public IngredientDef IngredienteSaida
        {
            get => Definition != null ? Definition.outputIngredient : null;
            set { if (Definition != null) { Definition.outputIngredient = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "⚙️ Configuração")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public ProcessingStage EstagioSaida
        {
            get => Definition != null ? Definition.outputStage : ProcessingStage.Prepared;
            set { if (Definition != null) { Definition.outputStage = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "⚙️ Configuração")]
        [Title("Requisitos")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public ToolCapability FerramentaExigida
        {
            get => Definition != null ? Definition.requiredToolCapability : ToolCapability.Slice;
            set { if (Definition != null) { Definition.requiredToolCapability = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "⚙️ Configuração")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public float DuracaoBaseSegundos
        {
            get => Definition != null ? Definition.baseDuration : 3.0f;
            set { if (Definition != null) { Definition.baseDuration = Mathf.Max(0.5f, value); SaveDefinitionChanges(); } }
        }
        #endregion

        #region Aba 2: Simulação 3D Interativa
        [TabGroup("Abas", "👁️ Simulação 3D")]
        [Button("▶️ Simular este Processo no Estúdio 3D", ButtonSizes.Large)]
        [GUIColor(0.35f, 0.85f, 0.45f)]
        public void SimularNoStudio()
        {
            ToolDef matchingTool = null;
            if (Definition != null && Definition.requiredToolCapability != ToolCapability.None)
            {
                string[] guids = AssetDatabase.FindAssets("Tool_");
                foreach (var g in guids)
                {
                    var t = AssetDatabase.LoadAssetAtPath<ToolDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (t != null && (t.capabilities & Definition.requiredToolCapability) != 0)
                    {
                        matchingTool = t;
                        break;
                    }
                }
            }

            PizzeriaContentStudioSession.PinAssembly(
                null,
                null,
                0,
                Definition != null ? Definition.inputIngredient : null,
                matchingTool,
                Definition);

            PizzeriaContentStudioWindow.ShowWindow();
        }

        [TabGroup("Abas", "👁️ Simulação 3D")]
        [Title("Validação de Vínculos Culinários")]
        [ShowInInspector, DisplayAsString(false)]
        private string StatusValidacao
        {
            get
            {
                if (Definition == null) return "Nenhum processo selecionado.";
                if (Definition.inputIngredient == null) return "⚠️ Atenção: Ingrediente de entrada não atribuído!";
                if (Definition.outputIngredient == null) return "⚠️ Atenção: Ingrediente de saída não atribuído!";
                return "✅ Vínculos de entrada, saída e ferramenta válidos para gameplay.";
            }
        }
        #endregion

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

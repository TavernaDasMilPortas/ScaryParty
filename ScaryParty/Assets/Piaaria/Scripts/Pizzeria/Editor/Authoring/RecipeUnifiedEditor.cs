using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Editor.PizzaVisual;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Editor unificado de Receitas para o PizzeriaContentWizard.
    /// Permite configurar os ingredientes da pizza, navegar diretamente para cada ingrediente
    /// e abrir a prévia no Pizza Studio 2.0.
    /// </summary>
    [Serializable]
    public class RecipeUnifiedEditor
    {
        [HideInInspector] public RecipeDef Definition;

        public RecipeUnifiedEditor(RecipeDef def)
        {
            Definition = def;
        }

        private string HeaderTitle => Definition != null
            ? $"Receita: {Definition.recipeName} (#{Definition.id})"
            : "Receita de Pizza";

        [ShowInInspector, HideLabel, Title("$HeaderTitle", subtitle: null, titleAlignment: TitleAlignments.Centered)]
        [DisplayAsString(false)]
        private string _headerNotice => "Configure aqui a composição gastronômica, os ingredientes exigidos e teste a montagem visual 3D.";

        #region Aba 1: Composição
        [TabGroup("Abas", "📜 Ficha da Receita")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public int ID
        {
            get => Definition != null ? Definition.id : 0;
            set { if (Definition != null) { Definition.id = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📜 Ficha da Receita")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public string NomeDaReceita
        {
            get => Definition != null ? Definition.recipeName : "";
            set { if (Definition != null) { Definition.recipeName = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📜 Ficha da Receita")]
        [Title("Base da Pizza")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public IngredientDef IngredienteBase
        {
            get => Definition != null ? Definition.baseIngredient : null;
            set { if (Definition != null) { Definition.baseIngredient = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📜 Ficha da Receita")]
        [Title("Ingredientes e Coberturas")]
        [TableList(ShowPaging = true)]
        public List<IngredientRequirement> IngredientesRequeridos
        {
            get => Definition != null ? Definition.ingredients : null;
            set { if (Definition != null) { Definition.ingredients = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📜 Ficha da Receita")]
        [Title("Economia & Forno")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public int PrecoBase
        {
            get => Definition != null ? Definition.basePrice : 20;
            set { if (Definition != null) { Definition.basePrice = Mathf.Max(1, value); SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📜 Ficha da Receita")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public float DuracaoFornoSegundos
        {
            get => Definition != null ? Definition.baseBakeDuration : 15f;
            set { if (Definition != null) { Definition.baseBakeDuration = Mathf.Max(1f, value); SaveDefinitionChanges(); } }
        }
        #endregion

        #region Aba 2: Montagem Visual 3D
        [TabGroup("Abas", "🍕 Montagem & Pizza Studio")]
        [Button("🍕 Abrir esta Receita no Pizza Studio 2.0 (Prévia 3D Modular)", ButtonSizes.Large)]
        [GUIColor(0.95f, 0.55f, 0.22f)]
        public void AbrirNoPizzaStudio()
        {
            PizzaVisualPreviewWindow.ShowWindow();
            var win = EditorWindow.GetWindow<PizzaVisualPreviewWindow>("Pizza Studio 2.0", false);
            if (win != null && Definition != null)
            {
                win.ActiveIngredients.Clear();
                if (Definition.ingredients != null)
                {
                    foreach (var req in Definition.ingredients)
                    {
                        if (req == null) continue;
                        int ingId = req.EffectiveIngredientId;
                        string ingName = req.ingredient != null ? req.ingredient.name : $"Ingrediente_{ingId}";
                        win.ActiveIngredients.Add(new PizzaVisualPreviewWindow.PreviewIngredientEntry
                        {
                            IngredientDefId = ingId,
                            Name = ingName,
                            Stage = req.requiredStage,
                            Count = Mathf.Max(1, req.quantity)
                        });
                    }
                }
                win.RebuildPizzaPreview();
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

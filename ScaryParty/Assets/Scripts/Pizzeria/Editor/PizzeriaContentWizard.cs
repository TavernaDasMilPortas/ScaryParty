using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.Pizzeria.Authoring;

namespace ScaryParty.Pizzeria.Editor
{
    public class PizzeriaContentWizard : OdinMenuEditorWindow
    {
        [MenuItem("Tools/Scary Party/Pizzeria/Configurar conteúdo", false, 12)]
        public static void ShowWindow()
        {
            var window = GetWindow<PizzeriaContentWizard>("Configurar Conteúdo da Pizzaria");
            window.minSize = new Vector2(850, 620);
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();

            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset");
            if (config != null)
            {
                tree.Add("Visão Geral", config);
                tree.AddAllAssetsAtPath("Ingredientes", "Assets/ScriptableObjects/Pizzeria", typeof(IngredientDef), true);
                tree.AddAllAssetsAtPath("Utensílios", "Assets/ScriptableObjects/Pizzeria", typeof(ToolDef), true);
                tree.AddAllAssetsAtPath("Processos", "Assets/ScriptableObjects/Pizzeria", typeof(ProcessDef), true);
                tree.AddAllAssetsAtPath("Receitas", "Assets/ScriptableObjects/Pizzeria", typeof(RecipeDef), true);
                tree.Add("Cenário e Validação", new ValidationActions());
            }
            else
            {
                tree.Add("Aviso", new WarningObject("PizzeriaConfig não encontrado! Execute 'Build or Update' primeiro."));
            }

            return tree;
        }

        private class ValidationActions
        {
            [Title("Ações de Manutenção e Validação", "Execute verificações automáticas e builders idempotentes", TitleAlignments.Centered)]
            [InfoBox("O sistema de melhorias (upgrades) foi dispensado do fluxo de execução conforme especificação.")]
            
            [Button("🔍 Validar Setup Completo da Pizzaria", ButtonSizes.Large)]
            [GUIColor(0.3f, 0.8f, 0.4f)]
            public void ValidateSetup()
            {
                PizzeriaValidator.ValidateSetup();
            }

            [Button("🏗️ Reconstruir PizzeriaPrefab (Idempotente)", ButtonSizes.Medium)]
            [GUIColor(0.2f, 0.6f, 0.9f)]
            public void RebuildPrefab()
            {
                PizzeriaBuilder.BuildOrUpdate();
            }

            [Button("🏡 Configurar Pontos de Entrega na Cidade", ButtonSizes.Medium)]
            [GUIColor(0.9f, 0.6f, 0.2f)]
            public void ConfigureCityDeliveries()
            {
                DeliveryAddressBuilder.ConfigureCityDeliveryPoints();
            }
        }

        private class WarningObject
        {
            [DisplayAsString(false)]
            [HideLabel]
            public string message;

            public WarningObject(string msg) => message = msg;
        }
    }
}

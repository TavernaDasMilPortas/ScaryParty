using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor
{
    public class PizzeriaContentWizard : OdinMenuEditorWindow
    {
        [MenuItem("Tools/Scary Party/Pizzeria/Configurar conteúdo", false, 12)]
        public static void ShowWindow()
        {
            var window = GetWindow<PizzeriaContentWizard>("Configurar Conteúdo");
            window.minSize = new Vector2(800, 600);
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();

            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset");
            if (config != null)
            {
                tree.Add("Configuração Global", config);
                tree.AddAllAssetsAtPath("Ingredientes", "Assets/ScriptableObjects/Pizzeria", typeof(IngredientDef), true);
                tree.AddAllAssetsAtPath("Processos", "Assets/ScriptableObjects/Pizzeria", typeof(ProcessDef), true);
                tree.AddAllAssetsAtPath("Receitas", "Assets/ScriptableObjects/Pizzeria", typeof(RecipeDef), true);
                tree.AddAllAssetsAtPath("Utensílios", "Assets/ScriptableObjects/Pizzeria", typeof(ToolDef), true);
                tree.AddAllAssetsAtPath("Upgrades", "Assets/ScriptableObjects/Pizzeria", typeof(UpgradeDef), true);
            }
            else
            {
                tree.Add("Aviso", new WarningObject("PizzeriaConfig não encontrado! Rode o Build or Update primeiro."));
            }

            return tree;
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

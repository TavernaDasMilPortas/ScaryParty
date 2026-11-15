using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Wizard de configuração de Regras de Composição de Pizza (PizzaCompositionRules)
    /// e Catálogo de Traços de Clientes Presenciais (CustomerTraitDef — 5 positivos e 5 negativos).
    /// Também executa sincronização idempotente ao carregar o Editor caso os assets ainda não existam.
    /// </summary>
    [InitializeOnLoad]
    public class CustomerTraitWizard : EditorWindow
    {
        public const string ConfigAssetPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig.asset";
        public const string RulesAssetPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/PizzaCompositionRules.asset";
        public const string TraitsFolderPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/Traits";

        private Vector2 _scroll;

        static CustomerTraitWizard()
        {
            EditorApplication.delayCall += EnsureContentAssetsInitializedSilent;
        }

        [MenuItem("Tools/Scary Party/Wizards/Pizzaria/Clientes e Traços (Customer Trait Wizard)", false, 14)]
        public static void ShowWindow()
        {
            var win = GetWindow<CustomerTraitWizard>("Clientes & Composição");
            win.minSize = new Vector2(680f, 520f);
        }

        private static void EnsureContentAssetsInitializedSilent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(ConfigAssetPath);
            if (config == null) return;

            bool needsSync = config.compositionRules == null || config.customerTraits == null || config.customerTraits.Count < 10;
            if (needsSync)
            {
                EnsureCompositionRulesAndTraitsOnConfig(config, saveAssets: true);
            }
        }

        public static void EnsureCompositionRulesAndTraitsOnConfig(PizzeriaConfig config, bool saveAssets = true)
        {
            if (config == null) return;

            EnsureFolderExists("Assets/Piaaria/ScriptableObjects/Pizzeria");
            EnsureFolderExists(TraitsFolderPath);

            // 1. Garantir PizzaCompositionRules.asset
            var rules = AssetDatabase.LoadAssetAtPath<PizzaCompositionRules>(RulesAssetPath);
            if (rules == null)
            {
                rules = CreateInstance<PizzaCompositionRules>();
                rules.minToppings = 1;
                rules.maxToppings = 2;
                rules.maxSupportedToppings = 4;
                rules.basePizzaPrice = 10;
                rules.defaultBakeDuration = 15f;
                rules.candidateToppings = new List<ToppingCandidateEntry>
                {
                    new ToppingCandidateEntry
                    {
                        ingredientId = 5, // Queijo Ralado
                        requiredStage = ProcessingStage.Prepared,
                        portionCount = 1,
                        selectionWeight = 1.0f,
                        priceContribution = 5,
                        unlockedByDefault = true,
                        rawStorageIngredientId = 4
                    },
                    new ToppingCandidateEntry
                    {
                        ingredientId = 7, // Calabresa Fatiada
                        requiredStage = ProcessingStage.Prepared,
                        portionCount = 1,
                        selectionWeight = 1.0f,
                        priceContribution = 10,
                        unlockedByDefault = true,
                        rawStorageIngredientId = 6
                    },
                    new ToppingCandidateEntry
                    {
                        ingredientId = 9, // Cogumelo Fatiado
                        requiredStage = ProcessingStage.Prepared,
                        portionCount = 1,
                        selectionWeight = 0.8f,
                        priceContribution = 12,
                        unlockedByDefault = false,
                        rawStorageIngredientId = 8
                    }
                };
                AssetDatabase.CreateAsset(rules, RulesAssetPath);
                EditorUtility.SetDirty(rules);
            }

            // Vincular referências de IngredientDef se disponíveis no config
            if (config.ingredients != null && rules.candidateToppings != null)
            {
                foreach (var cand in rules.candidateToppings)
                {
                    if (cand.ingredientAsset == null)
                    {
                        cand.ingredientAsset = config.ingredients.Find(i => i != null && i.id == cand.ingredientId);
                    }
                }
                EditorUtility.SetDirty(rules);
            }

            config.compositionRules = rules;

            // 2. Garantir os 10 CustomerTraitDef assets
            var defaults = CustomerTraitData.CreateDefaultTenTraits();
            if (config.customerTraits == null)
                config.customerTraits = new List<CustomerTraitDef>();

            config.customerTraits.RemoveAll(t => t == null);

            foreach (var defData in defaults)
            {
                string safeName = defData.Name
                    .Replace(" ", "_")
                    .Replace("ã", "a")
                    .Replace("ç", "c")
                    .Replace("-", "_");
                string assetPath = $"{TraitsFolderPath}/Trait_{defData.Id}_{safeName}.asset";

                var traitAsset = AssetDatabase.LoadAssetAtPath<CustomerTraitDef>(assetPath);
                if (traitAsset == null)
                {
                    traitAsset = CreateInstance<CustomerTraitDef>();
                    traitAsset.traitId = defData.Id;
                    traitAsset.displayName = defData.Name;
                    traitAsset.description = defData.Description;
                    traitAsset.polarity = defData.Polarity;
                    traitAsset.queuePatienceMultiplier = defData.QueuePatienceMultiplier;
                    traitAsset.foodPatienceMultiplier = defData.FoodPatienceMultiplier;
                    traitAsset.stagnationSensitivityMultiplier = defData.StagnationSensitivityMultiplier;
                    traitAsset.tipMultiplier = defData.TipMultiplier;
                    traitAsset.perfectionFlatBonus = defData.PerfectionFlatBonus;
                    traitAsset.errorToleranceDelta = defData.ErrorToleranceDelta;
                    traitAsset.walkSpeedMultiplier = defData.WalkSpeedMultiplier;
                    traitAsset.incompatibleTraitIds = new List<int>(defData.IncompatibleTraitIds);

                    AssetDatabase.CreateAsset(traitAsset, assetPath);
                    EditorUtility.SetDirty(traitAsset);
                }

                if (!config.customerTraits.Exists(t => t != null && t.traitId == traitAsset.traitId))
                {
                    config.customerTraits.Add(traitAsset);
                }
            }

            EditorUtility.SetDirty(config);
            if (saveAssets)
            {
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Catálogo de Traços de Clientes & Composição por Ingredientes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gerencia os 10 traços iniciais de clientes presenciais (5 positivos e 5 negativos) com regras de compatibilidade e o PizzaCompositionRules (1–2 toppings iniciais, Queijo Ralado como topping candidato).",
                MessageType.Info);

            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(ConfigAssetPath);
            if (config == null)
            {
                EditorGUILayout.HelpBox($"PizzeriaConfig não encontrado em '{ConfigAssetPath}'.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Gerar / Sincronizar PizzaCompositionRules + 10 CustomerTraitDef no PizzeriaConfig", GUILayout.Height(32)))
            {
                EnsureCompositionRulesAndTraitsOnConfig(config, saveAssets: true);
                Debug.Log("[CustomerTraitWizard] PizzaCompositionRules e 10 CustomerTraitDef sincronizados com sucesso!");
            }

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("1. Regras de Composição (PizzaCompositionRules)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Asset Vinculado", config.compositionRules, typeof(PizzaCompositionRules), false);
            EditorGUI.EndDisabledGroup();

            if (config.compositionRules != null)
            {
                EditorGUILayout.LabelField($"Toppings por pizza: {config.compositionRules.minToppings} a {config.compositionRules.maxToppings} (suporte: até {config.compositionRules.maxSupportedToppings})");
                EditorGUILayout.LabelField($"Preço Base: R$ {config.compositionRules.basePizzaPrice}");
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField($"2. Traços de Clientes ({config.customerTraits.Count} registrados)", EditorStyles.boldLabel);

            for (int i = 0; i < config.customerTraits.Count; i++)
            {
                var t = config.customerTraits[i];
                if (t == null) continue;
                EditorGUILayout.BeginVertical("box");
                string pol = t.polarity == TraitPolarity.Positive ? "[POSITIVO]" : "[NEGATIVO]";
                EditorGUILayout.LabelField($"#{t.traitId} {pol} {t.displayName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(t.description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    $"Fila: {t.queuePatienceMultiplier:0.00}x | Preparo: {t.foodPatienceMultiplier:0.00}x | Gorjeta: {t.tipMultiplier:0.00}x | Estagnação: {t.stagnationSensitivityMultiplier:0.00}x | Bônus Perfeição: +R${t.perfectionFlatBonus}",
                    EditorStyles.miniLabel);
                EditorGUILayout.ObjectField("Asset", t, typeof(CustomerTraitDef), false);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}

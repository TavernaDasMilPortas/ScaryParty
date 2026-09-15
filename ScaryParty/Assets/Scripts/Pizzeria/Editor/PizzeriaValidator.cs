using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Editor
{
    public static class PizzeriaValidator
    {
        [MenuItem("Tools/Scary Party/Pizzeria/Validate Setup", false, 11)]
        public static void ValidateSetup()
        {
            int errorCount = 0;
            int warningCount = 0;

            Debug.Log("[PizzeriaValidator] 🔍 Iniciando validação do Módulo Pizzaria...");

            // 1. Check Config
            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset");
            if (config == null)
            {
                Debug.LogError("[PizzeriaValidator] ❌ PizzeriaConfig.asset não encontrado em Assets/ScriptableObjects/Pizzeria/!");
                errorCount++;
            }
            else
            {
                Debug.Log($"[PizzeriaValidator] ✅ PizzeriaConfig carregado: {config.ingredients.Count} ingredientes, {config.recipes.Count} receitas, {config.tools.Count} utensílios, {config.processes.Count} processos, {config.upgrades.Count} upgrades.");

                // Validate unique IDs
                var ingIds = new HashSet<int>();
                foreach (var ing in config.ingredients)
                {
                    if (ing == null) continue;
                    if (!ingIds.Add(ing.id))
                    {
                        Debug.LogError($"[PizzeriaValidator] ❌ ID duplicado em ingrediente: {ing.id} ({ing.ingredientName})");
                        errorCount++;
                    }
                }

                var recIds = new HashSet<int>();
                foreach (var rec in config.recipes)
                {
                    if (rec == null) continue;
                    if (!recIds.Add(rec.id))
                    {
                        Debug.LogError($"[PizzeriaValidator] ❌ ID duplicado em receita: {rec.id} ({rec.recipeName})");
                        errorCount++;
                    }
                }
            }

            // 2. Check Pizzeria Prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab");
            if (prefab == null)
            {
                Debug.LogError("[PizzeriaValidator] ❌ PizzeriaPrefab.prefab não encontrado em Assets/Prefabs/Pizzeria/!");
                errorCount++;
            }
            else
            {
                var netObj = prefab.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError("[PizzeriaValidator] ❌ PizzeriaPrefab não possui componente NetworkObject no root!");
                    errorCount++;
                }

                var rootComp = prefab.GetComponent<PizzeriaRoot>();
                if (rootComp == null)
                {
                    Debug.LogError("[PizzeriaValidator] ❌ PizzeriaPrefab não possui componente PizzeriaRoot no root!");
                    errorCount++;
                }
                else
                {
                    if (rootComp.playerSpawnAnchors == null || rootComp.playerSpawnAnchors.Length < 4)
                    {
                        Debug.LogWarning("[PizzeriaValidator] ⚠️ PizzeriaPrefab possui menos de 4 player spawn anchors.");
                        warningCount++;
                    }
                }
            }

            // 3. Check NetworkPrefabsList
            var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (prefabsList == null)
            {
                Debug.LogError("[PizzeriaValidator] ❌ DefaultNetworkPrefabs.asset não encontrado em Assets/!");
                errorCount++;
            }
            else
            {
                bool hasPizzeria = false;
                foreach (var entry in prefabsList.PrefabList)
                {
                    if (entry.Prefab != null && entry.Prefab.name == "PizzeriaPrefab")
                    {
                        hasPizzeria = true;
                        break;
                    }
                }

                if (!hasPizzeria)
                {
                    Debug.LogWarning("[PizzeriaValidator] ⚠️ PizzeriaPrefab não está registrado em DefaultNetworkPrefabs.asset.");
                    warningCount++;
                }
            }

            if (errorCount == 0 && warningCount == 0)
            {
                Debug.Log("[PizzeriaValidator] 🎉 Validação concluída sem erros ou avisos!");
                EditorUtility.DisplayDialog("Validação da Pizzaria", "Tudo correto! Nenhum erro ou aviso encontrado.", "OK");
            }
            else
            {
                string msg = $"Validação concluída com {errorCount} erro(s) e {warningCount} aviso(s). Veja o console do Unity para detalhes.";
                Debug.LogWarning($"[PizzeriaValidator] {msg}");
                EditorUtility.DisplayDialog("Validação da Pizzaria", msg, "OK");
            }
        }
    }
}

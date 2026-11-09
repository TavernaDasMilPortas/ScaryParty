#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;

namespace ScaryParty.Pizzeria.Editor
{
    public static class PizzeriaContentMigration
    {
        [MenuItem("Tools/Scary Party/Pizzeria/Migrar Referências de Conteúdo")]
        public static void MigrateContentReferences()
        {
            var ingredientsById = new Dictionary<int, IngredientDef>();
            var processesById = new Dictionary<int, ProcessDef>();
            var stationsById = new Dictionary<int, StationDef>();

            string[] ingredientGuids = AssetDatabase.FindAssets("t:IngredientDef");
            var allIngredients = new List<IngredientDef>();
            foreach (var guid in ingredientGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ing = AssetDatabase.LoadAssetAtPath<IngredientDef>(path);
                if (ing != null)
                {
                    ingredientsById[ing.id] = ing;
                    allIngredients.Add(ing);
                }
            }

            string[] processGuids = AssetDatabase.FindAssets("t:ProcessDef");
            var allProcesses = new List<ProcessDef>();
            foreach (var guid in processGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var proc = AssetDatabase.LoadAssetAtPath<ProcessDef>(path);
                if (proc != null)
                {
                    processesById[proc.id] = proc;
                    allProcesses.Add(proc);
                }
            }

            string[] recipeGuids = AssetDatabase.FindAssets("t:RecipeDef");
            var allRecipes = new List<RecipeDef>();
            foreach (var guid in recipeGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var rec = AssetDatabase.LoadAssetAtPath<RecipeDef>(path);
                if (rec != null) allRecipes.Add(rec);
            }

            string[] stationGuids = AssetDatabase.FindAssets("t:StationDef");
            var allStations = new List<StationDef>();
            foreach (var guid in stationGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var st = AssetDatabase.LoadAssetAtPath<StationDef>(path);
                if (st != null)
                {
                    stationsById[st.id] = st;
                    allStations.Add(st);
                }
            }

            int migratedCount = 0;

            // 1. Migrar IngredientDef
            foreach (var ing in allIngredients)
            {
                bool modified = false;
                if (ing.preparedIngredient == null && ing.preparedIngredientId > 0)
                {
                    if (ingredientsById.TryGetValue(ing.preparedIngredientId, out var target))
                    {
                        ing.preparedIngredient = target;
                        modified = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[Migração] Ingredient {ing.name} (id {ing.id}) referencia preparedIngredientId {ing.preparedIngredientId} inexistente.");
                    }
                }
                if (modified)
                {
                    EditorUtility.SetDirty(ing);
                    migratedCount++;
                }
            }

            // 2. Migrar ProcessDef
            foreach (var proc in allProcesses)
            {
                bool modified = false;
                if (proc.inputIngredient == null && proc.inputIngredientId > 0)
                {
                    if (ingredientsById.TryGetValue(proc.inputIngredientId, out var target))
                    {
                        proc.inputIngredient = target;
                        modified = true;
                    }
                }
                if (proc.outputIngredient == null && proc.outputIngredientId > 0)
                {
                    if (ingredientsById.TryGetValue(proc.outputIngredientId, out var target))
                    {
                        proc.outputIngredient = target;
                        modified = true;
                    }
                }
                if (modified)
                {
                    EditorUtility.SetDirty(proc);
                    migratedCount++;
                }
            }

            // 3. Migrar RecipeDef
            foreach (var rec in allRecipes)
            {
                bool modified = false;
                if (rec.baseIngredient == null && rec.baseIngredientId > 0)
                {
                    if (ingredientsById.TryGetValue(rec.baseIngredientId, out var target))
                    {
                        rec.baseIngredient = target;
                        modified = true;
                    }
                }
                if (rec.ingredients != null)
                {
                    foreach (var req in rec.ingredients)
                    {
                        if (req.ingredient == null && req.ingredientId > 0)
                        {
                            if (ingredientsById.TryGetValue(req.ingredientId, out var target))
                            {
                                req.ingredient = target;
                                modified = true;
                            }
                        }
                    }
                }
                if (modified)
                {
                    EditorUtility.SetDirty(rec);
                    migratedCount++;
                }
            }

            // 4. Migrar StationDef
            foreach (var st in allStations)
            {
                bool modified = false;
                if (st.acceptedProcessIds != null)
                {
                    foreach (var pId in st.acceptedProcessIds)
                    {
                        if (processesById.TryGetValue(pId, out var target))
                        {
                            if (!st.acceptedProcesses.Contains(target))
                            {
                                st.acceptedProcesses.Add(target);
                                modified = true;
                            }
                        }
                    }
                }
                if (st.durationOverrides != null)
                {
                    foreach (var ov in st.durationOverrides)
                    {
                        if (ov.process == null && ov.processId > 0)
                        {
                            if (processesById.TryGetValue(ov.processId, out var target))
                            {
                                ov.process = target;
                                modified = true;
                            }
                        }
                    }
                }
                if (modified)
                {
                    EditorUtility.SetDirty(st);
                    migratedCount++;
                }
            }

            // 5. Migrar PizzeriaConfig (garantir initialStorageSlots e listas)
            string[] configGuids = AssetDatabase.FindAssets("t:PizzeriaConfig");
            foreach (var guid in configGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(path);
                if (config != null)
                {
                    bool modified = false;
                    if (config.initialStorageSlots == null || config.initialStorageSlots.Count == 0)
                    {
                        config.initialStorageSlots = new List<StorageSlotConfig>();
                        int storageSlotCounter = 1;
                        foreach (var ing in allIngredients)
                        {
                            if (!ing.requiresPrep && ing.category != Domain.Types.ItemCategory.Pizza && ing.category != Domain.Types.ItemCategory.Tool)
                            {
                                config.initialStorageSlots.Add(new StorageSlotConfig
                                {
                                    storageId = storageSlotCounter++,
                                    ingredient = ing,
                                    ingredientId = ing.id,
                                    initialStock = 20,
                                    capacity = 30
                                });
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                    {
                        EditorUtility.SetDirty(config);
                        migratedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PizzeriaContentMigration] Concluída! {migratedCount} assets atualizados com referências diretas.");
        }
    }
}
#endif

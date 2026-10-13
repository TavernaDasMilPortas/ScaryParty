using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public class StationInstanceConfig
    {
        [Tooltip("ID numérico único da instância da bancada no mundo")]
        public int instanceId;
        [Tooltip("Definição de modelo da bancada")]
        public StationDef definition;
        [Tooltip("Nome descritivo exibido")]
        public string displayName;
        [Tooltip("Override de slots de trabalho/espera (0 = usa definition.slotCount)")]
        public int overrideSlotCount = 0;
        [Tooltip("Overrides locais de duração para processos nesta instância específica")]
        public List<ProcessDurationOverride> durationOverrides = new List<ProcessDurationOverride>();

        public int EffectiveSlotCount => (overrideSlotCount > 0) ? overrideSlotCount : (definition != null ? definition.slotCount : 1);
    }

    [Serializable]
    public class StorageSlotConfig
    {
        [Tooltip("ID do compartimento/armazenamento")]
        public int storageId;
        [Tooltip("Ingrediente armazenado")]
        public IngredientDef ingredient;
        [Tooltip("ID de fallback")]
        public int ingredientId;
        [Tooltip("Estoque inicial de unidades")]
        public int initialStock = 20;
        [Tooltip("Capacidade máxima do slot")]
        public int capacity = 30;

        public int EffectiveIngredientId => ingredient != null ? ingredient.id : ingredientId;
    }

    [CreateAssetMenu(fileName = "PizzeriaConfig", menuName = "Scary Party/Pizzeria/Pizzeria Config")]
    public class PizzeriaConfig : ScriptableObject
    {
        [Header("Definições de Conteúdo")]
        public List<IngredientDef> ingredients = new List<IngredientDef>();
        public List<RecipeDef> recipes = new List<RecipeDef>();
        public List<ToolDef> tools = new List<ToolDef>();
        public List<ProcessDef> processes = new List<ProcessDef>();
        public List<StationDef> stations = new List<StationDef>();
        public List<StationInstanceConfig> stationInstances = new List<StationInstanceConfig>();
        public List<SupplyItemDef> supplyItems = new List<SupplyItemDef>();
        public List<UpgradeDef> upgrades = new List<UpgradeDef>();

        [Header("Estoque Inicial Estruturado")]
        public List<StorageSlotConfig> initialStorageSlots = new List<StorageSlotConfig>();

        [Header("Parâmetros de Rede e Leases")]
        public float workLeaseSeconds = 0.75f;
        public float serverDistanceTolerance = 1.5f;
        public float interactDistance = 3.0f;

        [Header("Durações Padrão de Trabalho")]
        public float defaultBakeDuration = 15.0f;
        public float packagingDuration = 2.0f;
        public float discardDuration = 1.0f;
        public float archiveHoldDuration = 1.0f;

        [Header("Capacidades e Limites")]
        public int maxActiveOrders = 6;
        public int maxPendingCalls = 4;
        public float callExpireSeconds = 30.0f;
        public int stackCount = 2;
        public int stackHeight = 4;
        public int backpackCount = 2;
        public int backpackCapacity = 4;
        public int ticketBoardCapacity = 6;

        [Header("Tuning de Economia e Prazos")]
        public int startingBudget = 500;
        public float deliveryTimeLimit = 300.0f;
        public float phoneRingDuration = 30.0f;
        public float minOrderInterval = 15.0f;
        public float maxOrderInterval = 35.0f;
        public int maxPendingSupplyOrders = 3;

        [Header("Tuning de Cozinha")]
        public float ovenBurnGraceDuration = 10.0f;
        public float burnedPayoutFactor = 0f;
        public float unbakedPayoutFactor = 0f;
        public int restaurantBaseBonus = 10;

        [Header("Tuning Legado (Compatibilidade)")]
        public int fridgeCapacityPerIngredient = 20;
        public int cupboardCapacityPerIngredient = 30;
        public int backpackInitialSlots = 2;
        public int stagingInitialSlots = 4;
        public int ovenInitialSlots = 2;
        public int initialDoughStock = 20;
        public int initialSauceStock = 20;
        public int initialCheeseStock = 20;
        public int initialCalabresaStock = 10;
        public int initialMushroomStock = 0;

        public DefinitionCatalog BuildCatalog()
        {
            var catalog = new DefinitionCatalog();

            if (ingredients != null)
            {
                foreach (var ing in ingredients)
                {
                    if (ing == null) continue;
                    catalog.RegisterIngredient(new IngredientData(ing.id, ing.ingredientName, ing.category, ing.requiresPrep, ing.EffectivePreparedIngredientId));
                }
            }

            if (recipes != null)
            {
                foreach (var rec in recipes)
                {
                    if (rec == null) continue;
                    var reqs = new List<IngredientEntry>();
                    if (rec.ingredients != null)
                    {
                        foreach (var req in rec.ingredients)
                        {
                            reqs.Add(new IngredientEntry(req.EffectiveIngredientId, req.requiredStage, req.quantity));
                        }
                    }
                    catalog.RegisterRecipe(new RecipeData(rec.id, rec.recipeName, rec.EffectiveBaseIngredientId, reqs, rec.basePrice, rec.baseBakeDuration));
                }
            }

            if (tools != null)
            {
                foreach (var tool in tools)
                {
                    if (tool == null) continue;
                    catalog.RegisterTool(new ToolData(tool.id, tool.toolName, tool.capabilities, tool.initialSharedCount));
                }
            }

            if (processes != null)
            {
                foreach (var proc in processes)
                {
                    if (proc == null) continue;
                    catalog.RegisterProcess(new ProcessData(proc.id, proc.processName, proc.EffectiveInputIngredientId, proc.inputStage, proc.EffectiveOutputIngredientId, proc.outputStage, proc.requiredToolCapability, proc.baseDuration));
                }
            }

            // Registrar definições de estações
            if (stations != null)
            {
                foreach (var st in stations)
                {
                    if (st == null) continue;
                    var overrides = new Dictionary<int, float>();
                    if (st.durationOverrides != null)
                    {
                        foreach (var ov in st.durationOverrides)
                        {
                            overrides[ov.EffectiveProcessId] = ov.durationInSeconds;
                        }
                    }
                    catalog.RegisterStation(new StationData(st.id, st.stationName, st.slotCount, st.GetEffectiveProcessIds(), overrides));
                }
            }

            // Registrar instâncias de estações (precedência: base -> override da definição -> override da instância)
            if (stationInstances != null)
            {
                foreach (var inst in stationInstances)
                {
                    if (inst == null || inst.definition == null) continue;

                    var mergedOverrides = new Dictionary<int, float>();
                    
                    // 1. Overrides da definição base
                    if (inst.definition.durationOverrides != null)
                    {
                        foreach (var ov in inst.definition.durationOverrides)
                        {
                            mergedOverrides[ov.EffectiveProcessId] = ov.durationInSeconds;
                        }
                    }

                    // 2. Overrides específicos da instância (precedem)
                    if (inst.durationOverrides != null)
                    {
                        foreach (var ov in inst.durationOverrides)
                        {
                            mergedOverrides[ov.EffectiveProcessId] = ov.durationInSeconds;
                        }
                    }

                    string stName = !string.IsNullOrEmpty(inst.displayName) ? inst.displayName : inst.definition.stationName;
                    catalog.RegisterStation(new StationData(inst.instanceId, stName, inst.EffectiveSlotCount, inst.definition.GetEffectiveProcessIds(), mergedOverrides));
                }
            }

            if (supplyItems != null)
            {
                foreach (var sup in supplyItems)
                {
                    if (sup == null) continue;
                    catalog.RegisterSupplyItem(new SupplyItemData(sup.id, sup.ingredientId, sup.unitsPerPack, sup.packPrice, sup.baseETA));
                }
            }

            if (upgrades != null)
            {
                foreach (var up in upgrades)
                {
                    if (up == null) continue;
                    catalog.RegisterUpgrade(new UpgradeData(up.id, up.upgradeName, up.scope, up.statName, up.operation, up.valuePerLevel, up.maxLevel));
                }
            }

            return catalog;
        }
    }
}

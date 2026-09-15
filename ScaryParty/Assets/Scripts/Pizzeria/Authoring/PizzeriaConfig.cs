using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;

namespace ScaryParty.Pizzeria.Authoring
{
    [CreateAssetMenu(fileName = "PizzeriaConfig", menuName = "Scary Party/Pizzeria/Pizzeria Config")]
    public class PizzeriaConfig : ScriptableObject
    {
        [Header("Definições de Conteúdo")]
        public List<IngredientDef> ingredients = new List<IngredientDef>();
        public List<RecipeDef> recipes = new List<RecipeDef>();
        public List<ToolDef> tools = new List<ToolDef>();
        public List<ProcessDef> processes = new List<ProcessDef>();
        public List<StationDef> stations = new List<StationDef>();
        public List<SupplyItemDef> supplyItems = new List<SupplyItemDef>();
        public List<UpgradeDef> upgrades = new List<UpgradeDef>();

        [Header("Tuning de Economia e Prazos")]
        public int startingBudget = 500;
        public float deliveryTimeLimit = 300.0f;
        public float phoneRingDuration = 30.0f;
        public float minOrderInterval = 15.0f;
        public float maxOrderInterval = 35.0f;
        public int maxActiveOrders = 6;
        public int maxPendingSupplyOrders = 3;

        [Header("Tuning de Cozinha")]
        public float ovenBurnGraceDuration = 10.0f;
        public float burnedPayoutFactor = 0f;
        public float unbakedPayoutFactor = 0f;
        public int restaurantBaseBonus = 10;

        [Header("Tuning de Capacidades Iniciais")]
        public int fridgeCapacityPerIngredient = 20;
        public int cupboardCapacityPerIngredient = 30;
        public int backpackInitialSlots = 2;
        public int stagingInitialSlots = 4;
        public int ovenInitialSlots = 2;

        [Header("Estoque Inicial")]
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
                    catalog.RegisterIngredient(new IngredientData(ing.id, ing.ingredientName, ing.category, ing.requiresPrep, ing.preparedIngredientId));
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
                            reqs.Add(new IngredientEntry(req.ingredientId, req.requiredStage, req.quantity));
                        }
                    }
                    catalog.RegisterRecipe(new RecipeData(rec.id, rec.recipeName, rec.baseIngredientId, reqs, rec.basePrice, rec.baseBakeDuration));
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
                    catalog.RegisterProcess(new ProcessData(proc.id, proc.processName, proc.inputIngredientId, proc.inputStage, proc.outputIngredientId, proc.outputStage, proc.requiredToolCapability, proc.baseDuration));
                }
            }

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
                            overrides[ov.processId] = ov.durationInSeconds;
                        }
                    }
                    catalog.RegisterStation(new StationData(st.id, st.stationName, st.slotCount, st.acceptedProcessIds, overrides));
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

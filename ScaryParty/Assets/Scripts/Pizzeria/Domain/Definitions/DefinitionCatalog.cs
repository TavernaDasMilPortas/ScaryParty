using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Definitions
{
    public class IngredientData
    {
        public int Id { get; }
        public string Name { get; }
        public ItemCategory Category { get; }
        public bool RequiresPrep { get; }
        public int PreparedIngredientId { get; }

        public IngredientData(int id, string name, ItemCategory category, bool requiresPrep = false, int preparedIngredientId = 0)
        {
            Id = id;
            Name = name;
            Category = category;
            RequiresPrep = requiresPrep;
            PreparedIngredientId = preparedIngredientId;
        }
    }

    public class RecipeData
    {
        public int Id { get; }
        public string Name { get; }
        public int BaseIngredientId { get; }
        public IReadOnlyList<IngredientEntry> Ingredients { get; }
        public int BasePrice { get; }
        public float BaseBakeDuration { get; }

        public RecipeData(int id, string name, int baseIngredientId, IEnumerable<IngredientEntry> ingredients, int basePrice, float baseBakeDuration = 15f)
        {
            Id = id;
            Name = name;
            BaseIngredientId = baseIngredientId;
            var list = new List<IngredientEntry>(ingredients ?? Array.Empty<IngredientEntry>());
            list.Sort((a, b) =>
            {
                int c = a.IngredientDefId.CompareTo(b.IngredientDefId);
                return c != 0 ? c : a.Stage.CompareTo(b.Stage);
            });
            Ingredients = list;
            BasePrice = basePrice;
            BaseBakeDuration = baseBakeDuration;
        }
    }

    public class ToolData
    {
        public int Id { get; }
        public string Name { get; }
        public ToolCapability Capabilities { get; }
        public int InitialCount { get; }

        public ToolData(int id, string name, ToolCapability capabilities, int initialCount = 1)
        {
            Id = id;
            Name = name;
            Capabilities = capabilities;
            InitialCount = initialCount;
        }
    }

    public class ProcessData
    {
        public int Id { get; }
        public string Name { get; }
        public int InputIngredientId { get; }
        public ProcessingStage InputStage { get; }
        public int OutputIngredientId { get; }
        public ProcessingStage OutputStage { get; }
        public ToolCapability RequiredCapability { get; }
        public float BaseDuration { get; }

        public ProcessData(int id, string name, int inputId, ProcessingStage inputStage, int outputId, ProcessingStage outputStage, ToolCapability requiredTool, float baseDuration)
        {
            Id = id;
            Name = name;
            InputIngredientId = inputId;
            InputStage = inputStage;
            OutputIngredientId = outputId;
            OutputStage = outputStage;
            RequiredCapability = requiredTool;
            BaseDuration = baseDuration;
        }
    }

    public class StationData
    {
        public int Id { get; }
        public string Name { get; }
        public int SlotCount { get; }
        public IReadOnlyList<int> AcceptedProcessIds { get; }
        public IReadOnlyDictionary<int, float> DurationOverrides { get; }

        public StationData(int id, string name, int slotCount, IEnumerable<int> acceptedProcesses, IDictionary<int, float> overrides = null)
        {
            Id = id;
            Name = name;
            SlotCount = slotCount;
            AcceptedProcessIds = new List<int>(acceptedProcesses ?? Array.Empty<int>());
            DurationOverrides = overrides != null ? new Dictionary<int, float>(overrides) : new Dictionary<int, float>();
        }

        public float GetProcessDuration(ProcessData process)
        {
            if (DurationOverrides.TryGetValue(process.Id, out float customDuration))
                return customDuration;
            return process.BaseDuration;
        }
    }

    public class SupplyItemData
    {
        public int Id { get; }
        public int IngredientId { get; }
        public int UnitsPerPack { get; }
        public int PackPrice { get; }
        public float BaseETA { get; }

        public SupplyItemData(int id, int ingredientId, int unitsPerPack, int packPrice, float baseETA = 20f)
        {
            Id = id;
            IngredientId = ingredientId;
            UnitsPerPack = unitsPerPack;
            PackPrice = packPrice;
            BaseETA = baseETA;
        }
    }

    public class UpgradeData
    {
        public int Id { get; }
        public string Name { get; }
        public UpgradeScope Scope { get; }
        public string StatName { get; }
        public UpgradeOperation Operation { get; }
        public float ValuePerLevel { get; }
        public int MaxLevel { get; }

        public UpgradeData(int id, string name, UpgradeScope scope, string statName, UpgradeOperation operation, float valuePerLevel, int maxLevel = 3)
        {
            Id = id;
            Name = name;
            Scope = scope;
            StatName = statName;
            Operation = operation;
            ValuePerLevel = valuePerLevel;
            MaxLevel = maxLevel;
        }
    }

    public class DefinitionCatalog
    {
        public readonly Dictionary<int, IngredientData> Ingredients = new Dictionary<int, IngredientData>();
        public readonly Dictionary<int, RecipeData> Recipes = new Dictionary<int, RecipeData>();
        public readonly Dictionary<int, ToolData> Tools = new Dictionary<int, ToolData>();
        public readonly Dictionary<int, ProcessData> Processes = new Dictionary<int, ProcessData>();
        public readonly Dictionary<int, StationData> Stations = new Dictionary<int, StationData>();
        public readonly Dictionary<int, SupplyItemData> SupplyItems = new Dictionary<int, SupplyItemData>();
        public readonly Dictionary<int, UpgradeData> Upgrades = new Dictionary<int, UpgradeData>();

        public void RegisterIngredient(IngredientData data) => Ingredients[data.Id] = data;
        public void RegisterRecipe(RecipeData data) => Recipes[data.Id] = data;
        public void RegisterTool(ToolData data) => Tools[data.Id] = data;
        public void RegisterProcess(ProcessData data) => Processes[data.Id] = data;
        public void RegisterStation(StationData data) => Stations[data.Id] = data;
        public void RegisterSupplyItem(SupplyItemData data) => SupplyItems[data.Id] = data;
        public void RegisterUpgrade(UpgradeData data) => Upgrades[data.Id] = data;
    }
}

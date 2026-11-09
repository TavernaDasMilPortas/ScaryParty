using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Definitions
{
    public sealed class IngredientData
    {
        public int Id { get; }
        public string Name { get; }
        public ItemCategory Category { get; }
        public bool RequiresPrep { get; }
        public int PreparedOutputId { get; }
        public int SupplyBatchSize { get; }
        public int SupplyBatchCost { get; }
        public float SupplyDeliveryDelay { get; }

        public IngredientData(
            int id,
            string name,
            ItemCategory category,
            bool requiresPrep = false,
            int preparedOutputId = -1,
            int supplyBatchSize = 10,
            int supplyBatchCost = 25,
            float supplyDeliveryDelay = 10f)
        {
            Id = id;
            Name = name;
            Category = category;
            RequiresPrep = requiresPrep;
            PreparedOutputId = preparedOutputId;
            SupplyBatchSize = supplyBatchSize;
            SupplyBatchCost = supplyBatchCost;
            SupplyDeliveryDelay = supplyDeliveryDelay;
        }
    }

    public sealed class ToolData
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

    public sealed class ProcessData
    {
        public int Id { get; }
        public string Name { get; }
        public int InputIngredientId { get; }
        public ProcessingStage InputStage { get; }
        public int OutputIngredientId { get; }
        public ProcessingStage OutputStage { get; }
        public ToolCapability RequiredCapability { get; }
        public float BaseDuration { get; }

        public ProcessData(
            int id,
            string name,
            int inputIngredientId,
            ProcessingStage inputStage,
            int outputIngredientId,
            ProcessingStage outputStage,
            ToolCapability requiredCapability,
            float baseDuration)
        {
            Id = id;
            Name = name;
            InputIngredientId = inputIngredientId;
            InputStage = inputStage;
            OutputIngredientId = outputIngredientId;
            OutputStage = outputStage;
            RequiredCapability = requiredCapability;
            BaseDuration = baseDuration;
        }
    }

    public sealed class StationData
    {
        public int Id { get; }
        public string Name { get; }
        public int SlotCount { get; }
        public IReadOnlyList<int> AcceptedProcessIds { get; }
        public IReadOnlyDictionary<int, float> DurationOverrides { get; }

        public StationData(
            int id,
            string name,
            int slotCount,
            IEnumerable<int> acceptedProcessIds,
            IDictionary<int, float> durationOverrides = null)
        {
            Id = id;
            Name = name;
            SlotCount = slotCount;
            AcceptedProcessIds = new List<int>(acceptedProcessIds ?? Array.Empty<int>());
            DurationOverrides = durationOverrides != null
                ? new Dictionary<int, float>(durationOverrides)
                : new Dictionary<int, float>();
        }

        public bool AcceptsProcess(int processId)
        {
            for (int i = 0; i < AcceptedProcessIds.Count; i++)
            {
                if (AcceptedProcessIds[i] == processId) return true;
            }
            return false;
        }

        public float GetProcessDuration(ProcessData process)
        {
            if (DurationOverrides.TryGetValue(process.Id, out float overrideDur) && overrideDur > 0.01f)
            {
                return overrideDur;
            }
            return process.BaseDuration;
        }
    }

    public sealed class SupplyItemData
    {
        public int Id { get; }
        public int IngredientId { get; }
        public int UnitsPerPack { get; }
        public int PackPrice { get; }
        public float BaseETA { get; }

        public SupplyItemData(int id, int ingredientId, int unitsPerPack, int packPrice, float baseEta)
        {
            Id = id;
            IngredientId = ingredientId;
            UnitsPerPack = unitsPerPack;
            PackPrice = packPrice;
            BaseETA = baseEta;
        }
    }

    public sealed class ToppingCandidateData
    {
        public int IngredientId { get; }
        public int PreparedIngredientId => IngredientId;
        public ProcessingStage RequiredStage { get; }
        public int PortionCount { get; }
        public float SelectionWeight { get; }
        public int PriceContribution { get; }
        public bool UnlockedByDefault { get; }
        public bool EnabledByDefault => UnlockedByDefault;
        public int RawStorageIngredientId { get; }

        public ToppingCandidateData(
            int ingredientId,
            ProcessingStage requiredStage = ProcessingStage.Prepared,
            int portionCount = 1,
            float selectionWeight = 1.0f,
            int priceContribution = 5,
            bool unlockedByDefault = true,
            int rawStorageIngredientId = 0)
        {
            IngredientId = ingredientId;
            RequiredStage = requiredStage;
            PortionCount = Math.Max(1, portionCount);
            SelectionWeight = Math.Max(0.01f, selectionWeight);
            PriceContribution = Math.Max(0, priceContribution);
            UnlockedByDefault = unlockedByDefault;
            RawStorageIngredientId = rawStorageIngredientId;
        }
    }

    public sealed class PizzaCompositionRulesData
    {
        public int BaseDoughIngredientId { get; }
        public ProcessingStage RequiredDoughStage { get; }
        public int SauceIngredientId { get; }
        public int DefaultSauceIngredientId => SauceIngredientId;
        public bool RequireSauce { get; }
        public bool RequiresSauce => RequireSauce;
        public int SaucePortions { get; }
        public int MinToppings { get; }
        public int MaxToppings { get; }
        public int MaxSupportedToppings { get; }
        public int MaxDistinctIngredientsOnPizza { get; }
        public int BasePizzaPrice { get; }
        public float DefaultBakeDuration { get; }
        public IReadOnlyList<ToppingCandidateData> CandidateToppings { get; }
        public IReadOnlyList<ToppingCandidateData> Candidates => CandidateToppings;

        public PizzaCompositionRulesData(
            int baseDoughIngredientId = 2,
            ProcessingStage requiredDoughStage = ProcessingStage.Prepared,
            int sauceIngredientId = 3,
            bool requireSauce = true,
            int saucePortions = 1,
            int minToppings = 1,
            int maxToppings = 2,
            int maxSupportedToppings = 4,
            int maxDistinctIngredientsOnPizza = 8,
            int basePizzaPrice = 10,
            float defaultBakeDuration = 15f,
            IEnumerable<ToppingCandidateData> candidateToppings = null)
        {
            BaseDoughIngredientId = baseDoughIngredientId > 0 ? baseDoughIngredientId : 2;
            RequiredDoughStage = requiredDoughStage;
            SauceIngredientId = sauceIngredientId > 0 ? sauceIngredientId : 3;
            RequireSauce = requireSauce;
            SaucePortions = Math.Max(1, saucePortions);
            MinToppings = Math.Max(1, minToppings);
            MaxToppings = Math.Max(MinToppings, maxToppings);
            MaxSupportedToppings = Math.Max(MaxToppings, maxSupportedToppings);
            MaxDistinctIngredientsOnPizza = Math.Max(4, maxDistinctIngredientsOnPizza);
            BasePizzaPrice = Math.Max(1, basePizzaPrice);
            DefaultBakeDuration = defaultBakeDuration > 0.1f ? defaultBakeDuration : 15f;
            CandidateToppings = candidateToppings != null
                ? new List<ToppingCandidateData>(candidateToppings)
                : CreateDefaultCandidates();
        }

        public static PizzaCompositionRulesData CreateDefault()
        {
            return new PizzaCompositionRulesData(
                2,
                ProcessingStage.Prepared,
                3,
                true,
                1,
                1,
                2,
                4,
                8,
                10,
                15f,
                CreateDefaultCandidates());
        }

        private static List<ToppingCandidateData> CreateDefaultCandidates()
        {
            return new List<ToppingCandidateData>
            {
                new ToppingCandidateData(5, ProcessingStage.Prepared, 1, 1.0f, 5, true, 4),   // Queijo Ralado
                new ToppingCandidateData(7, ProcessingStage.Prepared, 1, 1.0f, 10, true, 6),  // Calabresa Fatiada
                new ToppingCandidateData(9, ProcessingStage.Prepared, 1, 0.8f, 12, false, 8)  // Cogumelo Fatiado (quando disponível/desbloqueado)
            };
        }
    }

    public sealed class RecipeData
    {
        public int Id { get; }
        public string Name { get; }
        public int BaseIngredientId { get; }
        public IReadOnlyList<IngredientEntry> Ingredients { get; }
        public int BasePrice { get; }
        public float BakeDuration { get; }
        public PizzaSpecification Specification { get; }

        public RecipeData(
            int id,
            string name,
            int baseIngredientId,
            IEnumerable<IngredientEntry> ingredients,
            int basePrice,
            float bakeDuration = 15f)
        {
            Id = id;
            Name = name;
            BaseIngredientId = baseIngredientId;
            var ingList = new List<IngredientEntry>(ingredients ?? Array.Empty<IngredientEntry>());
            Ingredients = ingList;
            BasePrice = basePrice;
            BakeDuration = bakeDuration;

            bool requiresSauce = false;
            int sauceId = 3;
            int saucePortions = 1;
            var toppings = new List<ToppingRequirement>();

            for (int i = 0; i < ingList.Count; i++)
            {
                var entry = ingList[i];
                if (entry.IngredientDefId == 3)
                {
                    requiresSauce = true;
                    sauceId = 3;
                    saucePortions = Math.Max(1, entry.Count);
                }
                else if (entry.IngredientDefId != baseIngredientId)
                {
                    toppings.Add(new ToppingRequirement(entry.IngredientDefId, entry.Stage, entry.Count));
                }
            }

            Specification = new PizzaSpecification(
                baseIngredientId > 0 ? baseIngredientId : 2,
                sauceId,
                requiresSauce,
                toppings,
                basePrice,
                bakeDuration,
                ProcessingStage.Prepared,
                saucePortions,
                id);
        }
    }

    public sealed class UpgradeData
    {
        public int Id { get; }
        public string Name { get; }
        public UpgradeScope Scope { get; }
        public UpgradeOperation Operation { get; }
        public string StatName { get; }
        public float ValuePerLevel { get; }
        public int MaxLevel { get; }
        public IReadOnlyList<int> CostsPerLevel { get; }

        public UpgradeData(
            int id,
            string name,
            UpgradeScope scope,
            string statName,
            UpgradeOperation operation,
            float valuePerLevel,
            int maxLevel,
            IEnumerable<int> costsPerLevel = null)
        {
            Id = id;
            Name = name;
            Scope = scope;
            StatName = statName;
            Operation = operation;
            ValuePerLevel = valuePerLevel;
            MaxLevel = maxLevel;
            CostsPerLevel = new List<int>(costsPerLevel ?? Array.Empty<int>());
        }

        public UpgradeData(
            int id,
            string name,
            UpgradeScope scope,
            UpgradeOperation operation,
            string statName,
            float valuePerLevel,
            int maxLevel,
            IEnumerable<int> costsPerLevel = null)
            : this(id, name, scope, statName, operation, valuePerLevel, maxLevel, costsPerLevel)
        {
        }
    }

    public sealed class DefinitionCatalog
    {
        private readonly Dictionary<int, IngredientData> _ingredients = new Dictionary<int, IngredientData>();
        private readonly Dictionary<int, ToolData> _tools = new Dictionary<int, ToolData>();
        private readonly Dictionary<int, ProcessData> _processes = new Dictionary<int, ProcessData>();
        private readonly Dictionary<int, StationData> _stations = new Dictionary<int, StationData>();
        private readonly Dictionary<int, RecipeData> _recipes = new Dictionary<int, RecipeData>();
        private readonly Dictionary<int, SupplyItemData> _supplyItems = new Dictionary<int, SupplyItemData>();
        private readonly Dictionary<int, UpgradeData> _upgrades = new Dictionary<int, UpgradeData>();
        private readonly Dictionary<int, CustomerTraitData> _customerTraits = new Dictionary<int, CustomerTraitData>();

        public IReadOnlyDictionary<int, IngredientData> Ingredients => _ingredients;
        public IReadOnlyDictionary<int, ToolData> Tools => _tools;
        public IReadOnlyDictionary<int, ProcessData> Processes => _processes;
        public IReadOnlyDictionary<int, StationData> Stations => _stations;
        public IReadOnlyDictionary<int, RecipeData> Recipes => _recipes;
        public IReadOnlyDictionary<int, SupplyItemData> SupplyItems => _supplyItems;
        public IReadOnlyDictionary<int, UpgradeData> Upgrades => _upgrades;
        public IReadOnlyDictionary<int, CustomerTraitData> CustomerTraits => _customerTraits;

        public PizzaCompositionRulesData CompositionRules { get; private set; } = PizzaCompositionRulesData.CreateDefault();

        public DefinitionCatalog()
        {
            EnsureDefaultCustomerTraits();
        }

        public void EnsureDefaultCustomerTraits()
        {
            if (_customerTraits.Count > 0) return;
            var defaults = CustomerTraitData.CreateDefaultTenTraits();
            for (int i = 0; i < defaults.Count; i++)
            {
                _customerTraits[defaults[i].Id] = defaults[i];
            }
        }

        public void SetCompositionRules(PizzaCompositionRulesData rules)
        {
            CompositionRules = rules ?? PizzaCompositionRulesData.CreateDefault();
        }

        public void RegisterIngredient(IngredientData data) => _ingredients[data.Id] = data;
        public void RegisterTool(ToolData data) => _tools[data.Id] = data;
        public void RegisterProcess(ProcessData data) => _processes[data.Id] = data;
        public void RegisterStation(StationData data) => _stations[data.Id] = data;
        public void RegisterRecipe(RecipeData data) => _recipes[data.Id] = data;
        public void RegisterSupplyItem(SupplyItemData data) => _supplyItems[data.Id] = data;
        public void RegisterUpgrade(UpgradeData data) => _upgrades[data.Id] = data;
        public void RegisterCustomerTrait(CustomerTraitData data)
        {
            if (data != null) _customerTraits[data.Id] = data;
        }

        public PizzaSpecification ResolveSpecificationFromRecipeOrDefault(int recipeId)
        {
            if (_recipes.TryGetValue(recipeId, out var rec) && rec.Specification != null)
            {
                return rec.Specification;
            }

            if (recipeId == 2)
            {
                return new PizzaSpecification(
                    2, 3, true,
                    new[]
                    {
                        new ToppingRequirement(5, ProcessingStage.Prepared, 1),
                        new ToppingRequirement(7, ProcessingStage.Prepared, 1)
                    },
                    20, 15f, ProcessingStage.Prepared, 1, 2);
            }
            if (recipeId == 3)
            {
                return new PizzaSpecification(
                    2, 3, true,
                    new[]
                    {
                        new ToppingRequirement(5, ProcessingStage.Prepared, 1),
                        new ToppingRequirement(9, ProcessingStage.Prepared, 1)
                    },
                    22, 15f, ProcessingStage.Prepared, 1, 3);
            }

            return PizzaSpecification.CreateDefaultCheese(15, 15f);
        }

        public int ComputeContentHash()
        {
            int hash = 17;
            hash = hash * 31 + _ingredients.Count;
            hash = hash * 31 + _tools.Count;
            hash = hash * 31 + _processes.Count;
            hash = hash * 31 + _stations.Count;
            hash = hash * 31 + _recipes.Count;
            hash = hash * 31 + _supplyItems.Count;
            hash = hash * 31 + _upgrades.Count;
            if (CompositionRules != null)
            {
                hash = hash * 31 + CompositionRules.MinToppings;
                hash = hash * 31 + CompositionRules.MaxToppings;
                hash = hash * 31 + CompositionRules.CandidateToppings.Count;
            }
            return hash;
        }
    }
}

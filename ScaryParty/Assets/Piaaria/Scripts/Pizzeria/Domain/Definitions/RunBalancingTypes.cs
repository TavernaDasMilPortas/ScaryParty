using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Domain.Definitions
{
    public enum RunEffectTarget : byte
    {
        None = 0,
        VampirePopulation = 1,
        VampireAcquisitionRange = 2,
        OrderArrivalRate = 3,
        SupplyDeliveryEta = 4,
        OvenAutoShutoff = 5,
        BackpackCapacity = 6,
        BackpackResistance = 7,
        EmployeeErrorRate = 8,
        CustomerPatience = 9,
        NightRevenueGoal = 10
    }

    public enum RunEffectOperation : byte
    {
        AddFlat = 0,
        PercentDelta = 1,
        Multiply = 2,
        EnableCapability = 3
    }

    public enum RunEffectUnit : byte
    {
        FlatUnits = 0,
        RelativePercent = 1,
        Multiplier = 2,
        BooleanFlag = 3,
        Seconds = 4
    }

    public enum RunModifierSource : byte
    {
        VoteOnly = 0,
        ShopOnly = 1,
        VoteAndShop = 2
    }

    public enum ShopBenefitKind : byte
    {
        GrantModifier = 0,
        SupplyBundle = 1,
        HireEmployee = 2
    }

    public enum ShopPhaseAvailability : byte
    {
        PreparationOnly = 0,
        ActiveShiftOnly = 1,
        PreparationAndActive = 2
    }

    [Flags]
    public enum RunDifficultyFlags : byte
    {
        None = 0,
        Normal = 1 << 0,
        Hard = 1 << 1,
        All = Normal | Hard
    }

    public static class RunDifficultyFlagsExtensions
    {
        public static bool Includes(this RunDifficultyFlags flags, RunDifficulty difficulty)
        {
            if (flags == RunDifficultyFlags.None) return false;
            return difficulty == RunDifficulty.Hard
                ? (flags & RunDifficultyFlags.Hard) != 0
                : (flags & RunDifficultyFlags.Normal) != 0;
        }
    }

    public sealed class RunModifierEffectData
    {
        public RunEffectTarget Target { get; }
        public RunEffectOperation Operation { get; }
        public RunEffectUnit Unit { get; }
        public float ValuePerLevel { get; }
        public bool HasLimits { get; }
        public float MinLimit { get; }
        public float MaxLimit { get; }

        public RunModifierEffectData(
            RunEffectTarget target,
            RunEffectOperation operation,
            RunEffectUnit unit,
            float valuePerLevel,
            bool hasLimits = false,
            float minLimit = 0f,
            float maxLimit = 10f)
        {
            Target = target;
            Operation = operation;
            Unit = unit;
            ValuePerLevel = valuePerLevel;
            HasLimits = hasLimits;
            MinLimit = minLimit;
            MaxLimit = maxLimit;
        }

        /// <summary>
        /// Converte valor legivel do Editor (ex.: -25 para -25%) para fator de dominio (-0.25).
        /// </summary>
        public float ToDomainValuePerLevel()
        {
            return Unit == RunEffectUnit.RelativePercent
                ? ValuePerLevel / 100f
                : ValuePerLevel;
        }
    }

    public sealed class SupplyBundleLineData
    {
        public int IngredientId { get; }
        public int Quantity { get; }

        public SupplyBundleLineData(int ingredientId, int quantity)
        {
            IngredientId = ingredientId;
            Quantity = Math.Max(1, quantity);
        }
    }

    public sealed class RunShopOfferData
    {
        public string OfferId { get; }
        public string DisplayName { get; }
        public string CustomDescription { get; }
        public bool Enabled { get; }
        public ShopBenefitKind BenefitKind { get; }
        public string ModifierId { get; }
        public EmployeeRole EmployeeRole { get; }
        public IReadOnlyList<SupplyBundleLineData> SupplyBundleLines { get; }
        public int BaseCost { get; }
        public int CostIncreasePerLevel { get; }
        public int MaxPurchasesPerRun { get; }
        public int MaxPurchasesPerNight { get; }
        public ShopPhaseAvailability PhaseAvailability { get; }
        public byte LegacyByteId { get; }

        public RunShopOfferData(
            string offerId,
            string displayName,
            string customDescription,
            bool enabled,
            ShopBenefitKind benefitKind,
            string modifierId,
            EmployeeRole employeeRole,
            IReadOnlyList<SupplyBundleLineData> supplyBundleLines,
            int baseCost,
            int costIncreasePerLevel,
            int maxPurchasesPerRun,
            int maxPurchasesPerNight,
            ShopPhaseAvailability phaseAvailability,
            byte legacyByteId = 0)
        {
            OfferId = offerId ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? OfferId : displayName;
            CustomDescription = customDescription ?? string.Empty;
            Enabled = enabled;
            BenefitKind = benefitKind;
            ModifierId = modifierId ?? string.Empty;
            EmployeeRole = employeeRole;
            SupplyBundleLines = supplyBundleLines ?? Array.Empty<SupplyBundleLineData>();
            BaseCost = Math.Max(0, baseCost);
            CostIncreasePerLevel = Math.Max(0, costIncreasePerLevel);
            MaxPurchasesPerRun = Math.Max(0, maxPurchasesPerRun);
            MaxPurchasesPerNight = Math.Max(0, maxPurchasesPerNight);
            PhaseAvailability = phaseAvailability;
            LegacyByteId = legacyByteId;
        }

        public int ComputeEffectiveCost(int currentLevelOrPurchases)
        {
            return Math.Max(0, BaseCost + CostIncreasePerLevel * Math.Max(0, currentLevelOrPurchases));
        }
    }

    public sealed class EmployeeRoleProfileData
    {
        public EmployeeRole Role { get; }
        public string DisplayName { get; }
        public int HireCost { get; }
        public int MaxLevel { get; }
        public float PromotionCostMultiplier { get; }
        public int PromotionFlatStepCost { get; }
        public float BaseErrorRate { get; }
        public float LevelErrorReduction { get; }
        public float MinErrorRate { get; }
        public float BaseActionSeconds { get; }

        public EmployeeRoleProfileData(
            EmployeeRole role,
            string displayName,
            int hireCost,
            int maxLevel,
            float promotionCostMultiplier,
            int promotionFlatStepCost,
            float baseErrorRate,
            float levelErrorReduction,
            float minErrorRate,
            float baseActionSeconds)
        {
            Role = role;
            DisplayName = string.IsNullOrEmpty(displayName) ? role.ToString() : displayName;
            HireCost = Math.Max(0, hireCost);
            MaxLevel = Math.Max(1, maxLevel);
            PromotionCostMultiplier = Math.Max(0.1f, promotionCostMultiplier);
            PromotionFlatStepCost = Math.Max(0, promotionFlatStepCost);
            BaseErrorRate = Math.Clamp(baseErrorRate, 0f, 0.95f);
            LevelErrorReduction = Math.Clamp(levelErrorReduction, 0f, 0.9f);
            MinErrorRate = Math.Clamp(minErrorRate, 0.01f, 0.9f);
            BaseActionSeconds = Math.Max(0.2f, baseActionSeconds);
        }

        public int ComputeCostForLevel(int targetLevel)
        {
            int lvl = Math.Max(1, targetLevel);
            if (lvl == 1) return HireCost;
            float scaled = HireCost * lvl * PromotionCostMultiplier + PromotionFlatStepCost * (lvl - 1);
            return Math.Max(0, (int)Math.Round(scaled));
        }
    }

    public sealed class NightPreparationScheduleData
    {
        public int NightNumber { get; }
        public int MandatoryHindranceStages { get; }
        public int HindranceAlternatives { get; }
        public int OpportunityStages { get; }
        public int OpportunityAlternatives { get; }
        public int AdditionalEventStages { get; }
        public int EventAlternatives { get; }

        public NightPreparationScheduleData(
            int nightNumber,
            int mandatoryHindranceStages,
            int hindranceAlternatives,
            int opportunityStages,
            int opportunityAlternatives,
            int additionalEventStages,
            int eventAlternatives)
        {
            NightNumber = Math.Max(1, nightNumber);
            MandatoryHindranceStages = Math.Max(0, mandatoryHindranceStages);
            HindranceAlternatives = Math.Max(2, hindranceAlternatives);
            OpportunityStages = Math.Max(0, opportunityStages);
            OpportunityAlternatives = Math.Max(2, opportunityAlternatives);
            AdditionalEventStages = Math.Max(0, additionalEventStages);
            EventAlternatives = Math.Max(2, eventAlternatives);
        }
    }
}

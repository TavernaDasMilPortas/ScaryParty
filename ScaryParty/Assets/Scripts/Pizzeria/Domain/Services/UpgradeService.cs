using System;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class UpgradeService
    {
        public float ComputeEffectiveValue(
            float baseValue,
            UpgradeScope scope,
            string statName,
            PizzeriaState state,
            DefinitionCatalog catalog,
            bool isIntegerStat = false,
            float minClamp = 0.1f,
            float maxClamp = 9999f)
        {
            float flatAdds = 0f;
            float percentAdds = 0f;
            float multiplyFactor = 1.0f;
            bool hasOverride = false;
            float overrideValue = 0f;

            foreach (var kvp in state.UpgradeLevels)
            {
                int upgradeId = kvp.Key;
                int level = kvp.Value;
                if (level <= 0) continue;

                if (!catalog.Upgrades.TryGetValue(upgradeId, out var upData))
                    continue;

                if (upData.Scope != scope || !string.Equals(upData.StatName, statName, StringComparison.OrdinalIgnoreCase))
                    continue;

                float totalContribution = upData.ValuePerLevel * level;

                switch (upData.Operation)
                {
                    case UpgradeOperation.FlatAdd:
                        flatAdds += totalContribution;
                        break;
                    case UpgradeOperation.PercentAdd:
                        percentAdds += totalContribution;
                        break;
                    case UpgradeOperation.Multiply:
                        // Multiply factor compounding
                        multiplyFactor *= (float)Math.Pow(upData.ValuePerLevel, level);
                        break;
                    case UpgradeOperation.Override:
                        hasOverride = true;
                        overrideValue = totalContribution;
                        break;
                }
            }

            float result;
            if (hasOverride)
            {
                result = overrideValue;
            }
            else
            {
                result = (baseValue + flatAdds) * (1.0f + percentAdds) * multiplyFactor;
            }

            if (result < minClamp) result = minClamp;
            if (result > maxClamp) result = maxClamp;

            if (isIntegerStat)
            {
                result = (float)Math.Floor(result);
            }

            return result;
        }

        public bool IsRecipeUnlocked(int recipeId, PizzeriaState state, DefinitionCatalog catalog)
        {
            // By default recipes are unlocked unless tied to an unlock upgrade
            foreach (var kvp in catalog.Upgrades)
            {
                var upData = kvp.Value;
                if (upData.Scope == UpgradeScope.Recipe && upData.Operation == UpgradeOperation.BoolSet)
                {
                    if (int.TryParse(upData.StatName, out int targetRecipeId) && targetRecipeId == recipeId)
                    {
                        // Needs unlock
                        return state.UpgradeLevels.TryGetValue(upData.Id, out int lvl) && lvl > 0;
                    }
                }
            }
            return true;
        }

        public CommandResult SetUpgradeLevel(int upgradeId, int level, PizzeriaState state, DefinitionCatalog catalog)
        {
            if (!catalog.Upgrades.TryGetValue(upgradeId, out var upData))
                return CommandResult.Fail(CommandError.Incompatible, "Upgrade não existe.");

            if (level < 0 || level > upData.MaxLevel)
                return CommandResult.Fail(CommandError.InvalidOperation, $"Nível inválido (0..{upData.MaxLevel}).");

            state.UpgradeLevels[upgradeId] = level;
            state.IncrementRevision();
            return CommandResult.Ok();
        }
    }
}

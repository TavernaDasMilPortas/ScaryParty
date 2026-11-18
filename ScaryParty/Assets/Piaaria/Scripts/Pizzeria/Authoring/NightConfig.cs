using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public struct NightLevelEntry
    {
        public int nightNumber;
        public float durationSeconds;
        public int revenueGoal;
        [Tooltip("Opcional: metas por 1, 2, 3 e 4 jogadores humanos. Se vazio ou <= 0, usa revenueGoal.")]
        public int[] goalsByHumanCount;
        public float prepSeconds;
        public float closingGraceSeconds;
        public int reputationMinGoal;
        public int pizzasMinGoal;
        public bool useFormulaFallback;

        public NightPresetRowData ToDomainRow()
        {
            int[] table = (goalsByHumanCount != null && goalsByHumanCount.Length > 0)
                ? goalsByHumanCount
                : (revenueGoal > 0 ? new[] { revenueGoal } : Array.Empty<int>());

            return new NightPresetRowData(
                nightNumber,
                durationSeconds,
                table,
                prepSeconds,
                closingGraceSeconds,
                reputationMinGoal,
                pizzasMinGoal,
                useFormulaFallback);
        }
    }

    [CreateAssetMenu(fileName = "NightConfig", menuName = "Scary Party/Pizzeria/Night Config")]
    public class NightConfig : ScriptableObject
    {
        public bool enableAllPlayersDeadDefeat = true;

        public List<NightLevelEntry> nights = new List<NightLevelEntry>
        {
            new NightLevelEntry { nightNumber = 1, durationSeconds = 600f, revenueGoal = 60, goalsByHumanCount = new[] { 60, 96, 129, 159 }, prepSeconds = 4f, closingGraceSeconds = 15f },
            new NightLevelEntry { nightNumber = 2, durationSeconds = 660f, revenueGoal = 110, goalsByHumanCount = new[] { 110, 176, 236, 292 }, prepSeconds = 4f, closingGraceSeconds = 15f },
            new NightLevelEntry { nightNumber = 3, durationSeconds = 720f, revenueGoal = 170, goalsByHumanCount = new[] { 170, 272, 366, 450 }, prepSeconds = 4f, closingGraceSeconds = 15f },
        };

        public Dictionary<int, NightPresetRowData> BuildDomainRows()
        {
            var dict = new Dictionary<int, NightPresetRowData>();
            if (nights == null) return dict;
            for (int i = 0; i < nights.Count; i++)
            {
                var row = nights[i].ToDomainRow();
                dict[row.NightNumber] = row;
            }
            return dict;
        }

        public NightGoalParameters ResolveParameters(int nightNumber)
        {
            return ResolveEffectiveParameters(nightNumber, 1, RunDifficulty.Normal, null, 1f).ToNightGoalParameters();
        }

        public ResolvedNightParameters ResolveEffectiveParameters(
            int nightNumber,
            int connectedHumanCount,
            RunDifficulty difficulty,
            PizzeriaRunPreset activePreset,
            float goalModifierMultiplier,
            float tutorialDuration = 600f,
            int tutorialGoal = 15)
        {
            return NightParameterResolver.ResolveEffectiveParameters(
                nightNumber,
                connectedHumanCount,
                difficulty,
                activePreset != null ? activePreset.ToDomainData() : null,
                BuildDomainRows(),
                tutorialDuration,
                tutorialGoal,
                goalModifierMultiplier);
        }
    }
}

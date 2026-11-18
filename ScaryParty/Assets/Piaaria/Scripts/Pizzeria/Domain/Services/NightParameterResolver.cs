using System;
using System.Collections.Generic;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public enum GoalResolutionSource : byte
    {
        PresetTable = 0,
        NightConfigTable = 1,
        ExplicitFormulaFallback = 2,
        DefaultFallback = 3
    }

    public sealed class NightPresetRowData
    {
        public int NightNumber { get; }
        public float DurationSeconds { get; }
        public int[] GoalsByHumanCount { get; }
        public float PrepSeconds { get; }
        public float ClosingGraceSeconds { get; }
        public int ReputationMinGoal { get; }
        public int PizzasMinGoal { get; }
        public bool UseFormulaFallback { get; }

        public NightPresetRowData(
            int nightNumber,
            float durationSeconds,
            int[] goalsByHumanCount,
            float prepSeconds,
            float closingGraceSeconds,
            int reputationMinGoal = 0,
            int pizzasMinGoal = 0,
            bool useFormulaFallback = false)
        {
            NightNumber = nightNumber;
            DurationSeconds = durationSeconds;
            GoalsByHumanCount = goalsByHumanCount != null ? (int[])goalsByHumanCount.Clone() : Array.Empty<int>();
            PrepSeconds = prepSeconds;
            ClosingGraceSeconds = closingGraceSeconds;
            ReputationMinGoal = reputationMinGoal;
            PizzasMinGoal = pizzasMinGoal;
            UseFormulaFallback = useFormulaFallback;
        }
    }

    public sealed class RunPresetData
    {
        public string PresetId { get; }
        public int TotalNights { get; }
        public bool IncludeNight0Tutorial { get; }
        public bool AllowDebugPanel { get; }
        public RunDifficulty Difficulty { get; }
        public int StartingBudget { get; }
        public int StartingReputation { get; }
        public float Night0DurationSeconds { get; }
        public int[] Night0GoalsByHumanCount { get; }
        public float Night0PrepSeconds { get; }
        public float Night0ClosingGraceSeconds { get; }
        public bool Night0NonLethalDefeat { get; }
        public IReadOnlyList<NightPresetRowData> Nights { get; }

        public RunPresetData(
            string presetId,
            int totalNights,
            bool includeNight0Tutorial,
            bool allowDebugPanel,
            RunDifficulty difficulty,
            int startingBudget,
            int startingReputation,
            float night0DurationSeconds,
            int[] night0GoalsByHumanCount,
            float night0PrepSeconds,
            float night0ClosingGraceSeconds,
            bool night0NonLethalDefeat,
            IReadOnlyList<NightPresetRowData> nights)
        {
            PresetId = string.IsNullOrEmpty(presetId) ? "Custom" : presetId;
            TotalNights = Math.Clamp(totalNights, 1, 7);
            IncludeNight0Tutorial = includeNight0Tutorial;
            AllowDebugPanel = allowDebugPanel;
            Difficulty = difficulty;
            StartingBudget = Math.Max(0, startingBudget);
            StartingReputation = Math.Clamp(startingReputation, 1, 100);
            Night0DurationSeconds = Math.Max(30f, night0DurationSeconds);
            Night0GoalsByHumanCount = night0GoalsByHumanCount != null ? (int[])night0GoalsByHumanCount.Clone() : new[] { 15, 15, 15, 15 };
            Night0PrepSeconds = Math.Max(0f, night0PrepSeconds);
            Night0ClosingGraceSeconds = Math.Max(0f, night0ClosingGraceSeconds);
            Night0NonLethalDefeat = night0NonLethalDefeat;
            Nights = nights ?? Array.Empty<NightPresetRowData>();
        }
    }

    public readonly struct ResolvedNightParameters
    {
        public readonly int NightNumber;
        public readonly int FrozenHumanCount;
        public readonly float DurationSeconds;
        public readonly float PrepSeconds;
        public readonly float ClosingGraceSeconds;
        public readonly int BaseGoal;
        public readonly float HumanScaleFactor;
        public readonly float ModifierGoalMultiplier;
        public readonly int FinalGoal;
        public readonly int ReputationMinGoal;
        public readonly int PizzasMinGoal;
        public readonly bool NonLethalDefeat;
        public readonly GoalResolutionSource GoalSource;
        public readonly string PresetId;

        public ResolvedNightParameters(
            int nightNumber,
            int frozenHumanCount,
            float durationSeconds,
            float prepSeconds,
            float closingGraceSeconds,
            int baseGoal,
            float humanScaleFactor,
            float modifierGoalMultiplier,
            int finalGoal,
            int reputationMinGoal,
            int pizzasMinGoal,
            bool nonLethalDefeat,
            GoalResolutionSource goalSource,
            string presetId)
        {
            NightNumber = Math.Max(0, nightNumber);
            FrozenHumanCount = Math.Clamp(frozenHumanCount, 1, 4);
            DurationSeconds = Math.Max(30f, durationSeconds);
            PrepSeconds = Math.Max(0f, prepSeconds);
            ClosingGraceSeconds = Math.Max(0f, closingGraceSeconds);
            BaseGoal = Math.Max(1, baseGoal);
            HumanScaleFactor = humanScaleFactor > 0f ? humanScaleFactor : 1f;
            ModifierGoalMultiplier = modifierGoalMultiplier > 0f ? modifierGoalMultiplier : 1f;
            FinalGoal = Math.Max(1, finalGoal);
            ReputationMinGoal = Math.Max(0, reputationMinGoal);
            PizzasMinGoal = Math.Max(0, pizzasMinGoal);
            NonLethalDefeat = nonLethalDefeat;
            GoalSource = goalSource;
            PresetId = string.IsNullOrEmpty(presetId) ? "Default" : presetId;
        }

        public NightGoalParameters ToNightGoalParameters()
            => new NightGoalParameters(NightNumber, DurationSeconds, FinalGoal, PrepSeconds, ClosingGraceSeconds);
    }

    /// <summary>
    /// Fonte única e autoritativa de resolução de metas, durações e política de derrota para P3.
    /// Usada em startup, nova RUN, Noite 0, saída do tutorial, avanço inter-noites e retry.
    /// </summary>
    public static class NightParameterResolver
    {
        public static float GetDefaultHumanScale(int humanCount)
        {
            return Math.Clamp(humanCount, 1, 4) switch
            {
                1 => 1.0f,
                2 => 1.6f,
                3 => 2.15f,
                _ => 2.65f
            };
        }

        public static ResolvedNightParameters ResolveEffectiveParameters(
            int nightNumber,
            int connectedHumanCount,
            RunDifficulty difficulty,
            RunPresetData preset = null,
            IReadOnlyDictionary<int, NightPresetRowData> nightConfigRows = null,
            float tutorialDurationSeconds = 600f,
            int tutorialGoal = 15,
            float modifierGoalMultiplier = 1.0f)
        {
            int frozenHumans = Math.Clamp(connectedHumanCount, 1, 4);
            int idx = frozenHumans - 1;
            float modMult = modifierGoalMultiplier > 0.01f ? modifierGoalMultiplier : 1.0f;
            string presetId = preset != null ? preset.PresetId : "Config";

            // Noite 0 (Tutorial): política própria, nunca herda fórmula da Noite 1
            if (nightNumber <= 0)
            {
                float dur = preset != null ? preset.Night0DurationSeconds : Math.Max(30f, tutorialDurationSeconds);
                float prep = preset != null ? preset.Night0PrepSeconds : 2f;
                float grace = preset != null ? preset.Night0ClosingGraceSeconds : 0f;
                bool nonLethal = preset == null || preset.Night0NonLethalDefeat;

                int rawGoal = tutorialGoal > 0 ? tutorialGoal : 15;
                GoalResolutionSource src = GoalResolutionSource.NightConfigTable;
                if (preset != null && preset.Night0GoalsByHumanCount != null && preset.Night0GoalsByHumanCount.Length > 0)
                {
                    int entryGoal = idx < preset.Night0GoalsByHumanCount.Length
                        ? preset.Night0GoalsByHumanCount[idx]
                        : preset.Night0GoalsByHumanCount[preset.Night0GoalsByHumanCount.Length - 1];
                    if (entryGoal > 0)
                    {
                        rawGoal = entryGoal;
                        src = GoalResolutionSource.PresetTable;
                    }
                }

                return new ResolvedNightParameters(
                    0,
                    frozenHumans,
                    dur,
                    prep,
                    grace,
                    rawGoal,
                    1.0f,
                    1.0f,
                    rawGoal,
                    0,
                    0,
                    nonLethal,
                    src,
                    presetId);
            }

            // 1. Prioridade máxima: tabela editável do Preset ativo
            if (preset != null && preset.Nights != null)
            {
                for (int i = 0; i < preset.Nights.Count; i++)
                {
                    var row = preset.Nights[i];
                    if (row != null && row.NightNumber == nightNumber && !row.UseFormulaFallback)
                    {
                        if (TryResolveRowGoal(row, idx, difficulty, modMult, out int base1p, out float scale, out int finalGoal))
                        {
                            return new ResolvedNightParameters(
                                nightNumber,
                                frozenHumans,
                                row.DurationSeconds > 10f ? row.DurationSeconds : 360f,
                                row.PrepSeconds >= 0f ? row.PrepSeconds : 4f,
                                row.ClosingGraceSeconds >= 0f ? row.ClosingGraceSeconds : 15f,
                                base1p,
                                scale,
                                modMult,
                                finalGoal,
                                row.ReputationMinGoal,
                                row.PizzasMinGoal,
                                false,
                                GoalResolutionSource.PresetTable,
                                presetId);
                        }
                    }
                }
            }

            // 2. Segunda prioridade: tabela editável de NightConfig
            if (nightConfigRows != null && nightConfigRows.TryGetValue(nightNumber, out var cfgRow) && cfgRow != null && !cfgRow.UseFormulaFallback)
            {
                if (TryResolveRowGoal(cfgRow, idx, difficulty, modMult, out int base1p, out float scale, out int finalGoal))
                {
                    return new ResolvedNightParameters(
                        nightNumber,
                        frozenHumans,
                        cfgRow.DurationSeconds > 10f ? cfgRow.DurationSeconds : 360f,
                        cfgRow.PrepSeconds >= 0f ? cfgRow.PrepSeconds : 4f,
                        cfgRow.ClosingGraceSeconds >= 0f ? cfgRow.ClosingGraceSeconds : 20f,
                        base1p,
                        scale,
                        modMult,
                        finalGoal,
                        cfgRow.ReputationMinGoal,
                        cfgRow.PizzasMinGoal,
                        false,
                        GoalResolutionSource.NightConfigTable,
                        presetId);
                }
            }

            // 3. Fallback explícito (quando useFormulaFallback=true ou noite além da tabela configurada)
            int formulaBase1p = nightNumber switch
            {
                1 => 60,
                2 => 100,
                3 => 150,
                _ => 150 + (nightNumber - 3) * 45
            };
            float humanScale = GetDefaultHumanScale(frozenHumans);
            float diffScale = difficulty == RunDifficulty.Hard ? 1.2f : 1.0f;
            int formulaFinal = (int)Math.Max(1, Math.Round(formulaBase1p * humanScale * diffScale * modMult));

            float fallbackDur = 360f + Math.Max(0, nightNumber - 1) * 30f;
            if (nightConfigRows != null && nightConfigRows.TryGetValue(nightNumber, out var fRow) && fRow != null && fRow.DurationSeconds > 10f)
            {
                fallbackDur = fRow.DurationSeconds;
            }

            return new ResolvedNightParameters(
                nightNumber,
                frozenHumans,
                fallbackDur,
                4f,
                20f,
                formulaBase1p,
                humanScale * diffScale,
                modMult,
                formulaFinal,
                0,
                0,
                false,
                GoalResolutionSource.ExplicitFormulaFallback,
                presetId);
        }

        private static bool TryResolveRowGoal(
            NightPresetRowData row,
            int humanIdx,
            RunDifficulty difficulty,
            float modMult,
            out int baseGoal1P,
            out float scaleFactor,
            out int finalGoal)
        {
            baseGoal1P = 0;
            scaleFactor = 1f;
            finalGoal = 0;

            var table = row.GoalsByHumanCount;
            if (table == null || table.Length == 0) return false;

            int entry1P = table[0] > 0 ? table[0] : 0;
            int selected = humanIdx < table.Length && table[humanIdx] > 0
                ? table[humanIdx]
                : (entry1P > 0 ? (int)Math.Round(entry1P * GetDefaultHumanScale(humanIdx + 1)) : 0);

            if (selected <= 0) return false;

            baseGoal1P = entry1P > 0 ? entry1P : selected;
            float diffMult = difficulty == RunDifficulty.Hard ? 1.2f : 1.0f;
            // Se o autor configurou metas por 1/2/3/4 humanos na tabela, preserva o valor exato para Normal; aplica modMult uma única vez.
            scaleFactor = baseGoal1P > 0 ? (float)selected / baseGoal1P : 1f;
            if (difficulty == RunDifficulty.Hard && table.Length == 1)
            {
                scaleFactor *= diffMult;
                finalGoal = (int)Math.Max(1, Math.Round(selected * diffMult * modMult));
            }
            else
            {
                if (difficulty == RunDifficulty.Hard)
                {
                    scaleFactor *= diffMult;
                    finalGoal = (int)Math.Max(1, Math.Round(selected * diffMult * modMult));
                }
                else
                {
                    finalGoal = (int)Math.Max(1, Math.Round(selected * modMult));
                }
            }
            return true;
        }
    }
}

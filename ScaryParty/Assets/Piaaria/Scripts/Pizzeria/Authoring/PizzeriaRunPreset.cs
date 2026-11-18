using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public struct NightPresetEntry
    {
        public int nightNumber;
        public float durationSeconds;
        [Tooltip("Metas explícitas para 1, 2, 3 e 4 jogadores humanos.")]
        public int[] goalsByHumanCount;
        public float prepSeconds;
        public float closingGraceSeconds;
        public int reputationMinGoal;
        public int pizzasMinGoal;
        [Tooltip("Se marcado, permite calcular pela fórmula legado quando a tabela tiver valor <= 0.")]
        public bool useFormulaFallback;

        public NightPresetRowData ToDomainRow()
        {
            return new NightPresetRowData(
                nightNumber,
                durationSeconds,
                goalsByHumanCount,
                prepSeconds,
                closingGraceSeconds,
                reputationMinGoal,
                pizzasMinGoal,
                useFormulaFallback);
        }
    }

    /// <summary>
    /// Preset persistente de RUN configurável para P3/P6 (ex.: Entrega_3Noites, Playtest_Integracao, Tutorial_Noite0).
    /// </summary>
    [CreateAssetMenu(fileName = "PizzeriaRunPreset", menuName = "Scary Party/Pizzeria/Run Preset")]
    public class PizzeriaRunPreset : ScriptableObject
    {
        [Header("Identificação")]
        public string presetId = "Entrega_3Noites";
        public string displayName = "Entrega (3 Noites)";
        [TextArea(2, 4)]
        public string description = "RUN padrão de 3 noites para entrega (30-45 minutos), sem painel de diagnóstico.";

        [Header("Política da RUN")]
        [Range(1, 7)]
        public int totalNights = 3;
        public bool includeNight0Tutorial = false;
        public bool allowDebugPanel = false;
        public RunDifficulty difficulty = RunDifficulty.Normal;
        public int startingBudget = 500;
        [Range(1, 100)]
        public int startingReputation = 100;
        [Range(1, 8)]
        public int backpackInitialSlots = 2;
        [Range(1, 8)]
        public int backpackMaxCapacity = 4;

        [Header("Tutorial Dedicado (Noite 0 — Não consome as 3 noites)")]
        public float night0DurationSeconds = 600f;
        public int[] night0GoalsByHumanCount = new int[4] { 15, 15, 15, 15 };
        public float night0PrepSeconds = 2f;
        public float night0ClosingGraceSeconds = 0f;
        public bool night0NonLethalDefeat = true;

        [Header("Metas por Noite (1..N)")]
        public List<NightPresetEntry> nights = new List<NightPresetEntry>
        {
            new NightPresetEntry
            {
                nightNumber = 1,
                durationSeconds = 600f,
                goalsByHumanCount = new[] { 60, 96, 129, 159 },
                prepSeconds = 4f,
                closingGraceSeconds = 15f,
                reputationMinGoal = 0,
                pizzasMinGoal = 0,
                useFormulaFallback = false
            },
            new NightPresetEntry
            {
                nightNumber = 2,
                durationSeconds = 660f,
                goalsByHumanCount = new[] { 110, 176, 236, 292 },
                prepSeconds = 4f,
                closingGraceSeconds = 15f,
                reputationMinGoal = 0,
                pizzasMinGoal = 0,
                useFormulaFallback = false
            },
            new NightPresetEntry
            {
                nightNumber = 3,
                durationSeconds = 720f,
                goalsByHumanCount = new[] { 170, 272, 366, 450 },
                prepSeconds = 4f,
                closingGraceSeconds = 15f,
                reputationMinGoal = 0,
                pizzasMinGoal = 0,
                useFormulaFallback = false
            }
        };

        public RunPresetData ToDomainData()
        {
            var rows = new List<NightPresetRowData>(nights != null ? nights.Count : 0);
            if (nights != null)
            {
                for (int i = 0; i < nights.Count; i++)
                {
                    rows.Add(nights[i].ToDomainRow());
                }
            }

            return new RunPresetData(
                presetId,
                totalNights,
                includeNight0Tutorial,
                allowDebugPanel,
                difficulty,
                startingBudget,
                startingReputation,
                night0DurationSeconds,
                night0GoalsByHumanCount,
                night0PrepSeconds,
                night0ClosingGraceSeconds,
                night0NonLethalDefeat,
                rows);
        }
    }
}

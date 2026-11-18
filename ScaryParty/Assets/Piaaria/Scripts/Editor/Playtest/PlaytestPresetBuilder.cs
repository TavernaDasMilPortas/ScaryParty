using System;
using System.Collections.Generic;
using System.Text;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Services;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Editor.Playtest
{
    /// <summary>
    /// Construtor idempotente dos presets persistentes de RUN da Fase P6:
    /// - Entrega_3Noites (36 min totais, painel [F8] desativado, padrão de entrega)
    /// - Playtest_Integracao (noites curtas somente para testes, painel [F8] liberado)
    /// - Tutorial_Noite0 (Noite 0 educativa + 3 noites padrão, painel [F8] liberado)
    /// </summary>
    public static class PlaytestPresetBuilder
    {
        public const string PresetsDir = PlaytestPlaceholderAssetBuilder.PresetsDir;
        public const string EntregaPresetPath = PresetsDir + "/Entrega_3Noites.asset";
        public const string PlaytestPresetPath = PresetsDir + "/Playtest_Integracao.asset";
        public const string TutorialPresetPath = PresetsDir + "/Tutorial_Noite0.asset";
        public const string NoiteDeTestePresetPath = PresetsDir + "/NoiteDeTeste.asset";

        public static int EnsureAllPresets(PlaytestReferenceManifest manifest, StringBuilder sb)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Piaaria/Playtest"))
                AssetDatabase.CreateFolder("Assets/Piaaria", "Playtest");
            if (!AssetDatabase.IsValidFolder(PresetsDir))
                AssetDatabase.CreateFolder("Assets/Piaaria/Playtest", "Presets");

            int changes = 0;

            var entrega = EnsurePresetAsset(
                EntregaPresetPath,
                presetId: "Entrega_3Noites",
                displayName: "Entrega (3 Noites)",
                description: "RUN oficial de entrega com 3 noites completas (~36 minutos no total) e painel de diagnóstico desativado.",
                totalNights: 3,
                includeNight0Tutorial: false,
                allowDebugPanel: false,
                difficulty: RunDifficulty.Normal,
                startingBudget: 500,
                startingReputation: 100,
                night0Duration: 420f,
                night0Goals: new[] { 15, 15, 15, 15 },
                nightDurations: new[] { 660f, 720f, 780f },
                nightGoals: new[]
                {
                    new[] { 60, 96, 129, 159 },
                    new[] { 110, 176, 236, 292 },
                    new[] { 170, 272, 365, 450 }
                },
                prepSeconds: 4f,
                graceSeconds: 15f,
                unlimitedShiftTime: false,
                ref changes,
                sb);

            var playtest = EnsurePresetAsset(
                PlaytestPresetPath,
                presetId: "Playtest_Integracao",
                displayName: "Playtest Rápido (Integração)",
                description: "Preset curto exclusivo para integração e diagnóstico rápido com painel [F8] ativado; nunca altera o preset de entrega.",
                totalNights: 3,
                includeNight0Tutorial: false,
                allowDebugPanel: true,
                difficulty: RunDifficulty.Normal,
                startingBudget: 500,
                startingReputation: 100,
                night0Duration: 300f,
                night0Goals: new[] { 15, 15, 15, 15 },
                nightDurations: new[] { 90f, 105f, 120f },
                nightGoals: new[]
                {
                    new[] { 30, 48, 64, 80 },
                    new[] { 45, 72, 96, 120 },
                    new[] { 60, 96, 129, 159 }
                },
                prepSeconds: 3f,
                graceSeconds: 8f,
                unlimitedShiftTime: false,
                ref changes,
                sb);

            var tutorial = EnsurePresetAsset(
                TutorialPresetPath,
                presetId: "Tutorial_Noite0",
                displayName: "Tutorial + 3 Noites (Noite 0)",
                description: "Começa na Noite 0 educativa (não-letal, sem consumir as 3 noites principais) e segue para 3 noites padrão.",
                totalNights: 3,
                includeNight0Tutorial: true,
                allowDebugPanel: true,
                difficulty: RunDifficulty.Normal,
                startingBudget: 500,
                startingReputation: 100,
                night0Duration: 420f,
                night0Goals: new[] { 15, 15, 15, 15 },
                nightDurations: new[] { 660f, 720f, 780f },
                nightGoals: new[]
                {
                    new[] { 60, 96, 129, 159 },
                    new[] { 110, 176, 236, 292 },
                    new[] { 170, 272, 365, 450 }
                },
                prepSeconds: 4f,
                graceSeconds: 15f,
                unlimitedShiftTime: false,
                ref changes,
                sb);

            var noiteDeTeste = EnsurePresetAsset(
                NoiteDeTestePresetPath,
                presetId: "NoiteDeTeste",
                displayName: "Noite de Teste — sem limite de tempo",
                description: "Preset livre de QA e playtest sem encerramento automatico por relogio de turno. Todas as regras e operacoes continuam normais.",
                totalNights: 3,
                includeNight0Tutorial: false,
                allowDebugPanel: true,
                difficulty: RunDifficulty.Normal,
                startingBudget: 500,
                startingReputation: 100,
                night0Duration: 420f,
                night0Goals: new[] { 15, 15, 15, 15 },
                nightDurations: new[] { 600f, 660f, 720f },
                nightGoals: new[]
                {
                    new[] { 60, 96, 129, 159 },
                    new[] { 110, 176, 236, 292 },
                    new[] { 170, 272, 365, 450 }
                },
                prepSeconds: 4f,
                graceSeconds: 15f,
                unlimitedShiftTime: true,
                ref changes,
                sb);

            var cfg = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(PlaytestInstallerAndValidator.PizzeriaConfigPath);
            if (cfg != null)
            {
                bool cfgDirty = false;
                if (cfg.defaultPreset != entrega)
                {
                    cfg.defaultPreset = entrega;
                    cfgDirty = true;
                }

                var desiredList = new List<PizzeriaRunPreset> { entrega, playtest, tutorial, noiteDeTeste };
                if (!PresetListsMatch(cfg.availablePresets, desiredList))
                {
                    cfg.availablePresets = desiredList;
                    cfgDirty = true;
                }

                if (cfgDirty)
                {
                    EditorUtility.SetDirty(cfg);
                    changes++;
                    sb.AppendLine("[P6 Presets] Wired Entrega_3Noites (default), Playtest_Integracao, Tutorial_Noite0 and NoiteDeTeste in PizzeriaConfig.");
                }
            }

            if (manifest != null)
            {
                var desiredSo = new List<ScriptableObject> { entrega, playtest, tutorial, noiteDeTeste };
                if (!ScriptableListsMatch(manifest.runPresets, desiredSo))
                {
                    manifest.runPresets = desiredSo;
                    EditorUtility.SetDirty(manifest);
                    changes++;
                    sb.AppendLine("[P6 Presets] Updated PlaytestReferenceManifest.runPresets.");
                }
            }

            return changes;
        }

        private static PizzeriaRunPreset EnsurePresetAsset(
            string path,
            string presetId,
            string displayName,
            string description,
            int totalNights,
            bool includeNight0Tutorial,
            bool allowDebugPanel,
            RunDifficulty difficulty,
            int startingBudget,
            int startingReputation,
            float night0Duration,
            int[] night0Goals,
            float[] nightDurations,
            int[][] nightGoals,
            float prepSeconds,
            float graceSeconds,
            bool unlimitedShiftTime,
            ref int changes,
            StringBuilder sb)
        {
            var asset = AssetDatabase.LoadAssetAtPath<PizzeriaRunPreset>(path);
            bool created = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PizzeriaRunPreset>();
                AssetDatabase.CreateAsset(asset, path);
                created = true;
            }

            var desiredNights = new List<NightPresetEntry>(totalNights);
            for (int i = 0; i < totalNights; i++)
            {
                desiredNights.Add(new NightPresetEntry
                {
                    nightNumber = i + 1,
                    durationSeconds = nightDurations[i],
                    goalsByHumanCount = (int[])nightGoals[i].Clone(),
                    prepSeconds = prepSeconds,
                    closingGraceSeconds = graceSeconds,
                    reputationMinGoal = 0,
                    pizzasMinGoal = 0,
                    useFormulaFallback = false
                });
            }

            bool dirty = created;
            if (asset.presetId != presetId) { asset.presetId = presetId; dirty = true; }
            if (asset.displayName != displayName) { asset.displayName = displayName; dirty = true; }
            if (asset.description != description) { asset.description = description; dirty = true; }
            if (asset.totalNights != totalNights) { asset.totalNights = totalNights; dirty = true; }
            if (asset.includeNight0Tutorial != includeNight0Tutorial) { asset.includeNight0Tutorial = includeNight0Tutorial; dirty = true; }
            if (asset.unlimitedShiftTime != unlimitedShiftTime) { asset.unlimitedShiftTime = unlimitedShiftTime; dirty = true; }
            if (asset.allowDebugPanel != allowDebugPanel) { asset.allowDebugPanel = allowDebugPanel; dirty = true; }
            if (asset.difficulty != difficulty) { asset.difficulty = difficulty; dirty = true; }
            if (asset.startingBudget != startingBudget) { asset.startingBudget = startingBudget; dirty = true; }
            if (asset.startingReputation != startingReputation) { asset.startingReputation = startingReputation; dirty = true; }
            if (asset.backpackInitialSlots != 2) { asset.backpackInitialSlots = 2; dirty = true; }
            if (asset.backpackMaxCapacity != 4) { asset.backpackMaxCapacity = 4; dirty = true; }
            if (Mathf.Abs(asset.night0DurationSeconds - night0Duration) > 0.01f) { asset.night0DurationSeconds = night0Duration; dirty = true; }
            if (!IntArraysEqual(asset.night0GoalsByHumanCount, night0Goals)) { asset.night0GoalsByHumanCount = (int[])night0Goals.Clone(); dirty = true; }
            if (Mathf.Abs(asset.night0PrepSeconds - 2f) > 0.01f) { asset.night0PrepSeconds = 2f; dirty = true; }
            if (Mathf.Abs(asset.night0ClosingGraceSeconds - 0f) > 0.01f) { asset.night0ClosingGraceSeconds = 0f; dirty = true; }
            if (!asset.night0NonLethalDefeat) { asset.night0NonLethalDefeat = true; dirty = true; }
            if (!NightRowsMatch(asset.nights, desiredNights)) { asset.nights = desiredNights; dirty = true; }

            if (dirty)
            {
                EditorUtility.SetDirty(asset);
                changes++;
                sb.AppendLine(created ? $"[P6 Presets] Created {path}" : $"[P6 Presets] Updated {path}");
            }

            return asset;
        }

        private static bool IntArraysEqual(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static bool NightRowsMatch(List<NightPresetEntry> a, List<NightPresetEntry> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].nightNumber != b[i].nightNumber ||
                    Mathf.Abs(a[i].durationSeconds - b[i].durationSeconds) > 0.01f ||
                    Mathf.Abs(a[i].prepSeconds - b[i].prepSeconds) > 0.01f ||
                    Mathf.Abs(a[i].closingGraceSeconds - b[i].closingGraceSeconds) > 0.01f ||
                    a[i].reputationMinGoal != b[i].reputationMinGoal ||
                    a[i].pizzasMinGoal != b[i].pizzasMinGoal ||
                    a[i].useFormulaFallback != b[i].useFormulaFallback ||
                    !IntArraysEqual(a[i].goalsByHumanCount, b[i].goalsByHumanCount))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool PresetListsMatch(List<PizzeriaRunPreset> a, List<PizzeriaRunPreset> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static bool ScriptableListsMatch(List<ScriptableObject> a, List<ScriptableObject> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}

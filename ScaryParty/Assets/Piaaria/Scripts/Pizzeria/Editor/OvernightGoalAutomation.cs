#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Editor.Authoring;
using ScaryParty.Pizzeria.Editor.Tests;
using ScaryParty.VisualStyles;
using ScaryParty.VisualStyles.Editor;
using UnityEditor.TestTools.TestRunner.Api;

namespace ScaryParty.Pizzeria.Editor
{
    [InitializeOnLoad]
    public static class OvernightGoalAutomation
    {
        private const string TriggerFile = "Temp/overnight_trigger.txt";
        private const string LogFile = "Temp/overnight_execution.log";
        private const string BatchStateFile = "Temp/visualstyle_batch_state.txt";
        private const string EditModeResultsCopyPath = "Logs/visualstyle_editmode_results.txt";
        private const string PlayModeResultsCopyPath = "Logs/visualstyle_playmode_results.txt";

        private static int _batchStartupTickCount;

        static OvernightGoalAutomation()
        {
            EditorApplication.update -= ProcessQueue;
            EditorApplication.update += ProcessQueue;
            EditorApplication.delayCall += ProcessQueue;
        }

        /// <summary>
        /// Batch entry point (-executeMethod) that clears startup dirty scenes and runs
        /// both EditMode and PlayMode VisualStyles suites sequentially, exiting cleanly.
        /// </summary>
        public static void RunVisualStyleBatchVerification()
        {
            Directory.CreateDirectory("Temp");
            _batchStartupTickCount = 0;
            long readyUtcTicks = DateTime.UtcNow.AddSeconds(2).Ticks;
            File.WriteAllText(BatchStateFile, $"WAIT_STARTUP|{readyUtcTicks}");
            Debug.Log("[OvernightGoalAutomation] Batch verification scheduled (waiting for startup delayCalls).");
        }

        public static void ProcessQueue()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            AdvanceBatchVerification();

            if (!File.Exists(TriggerFile)) return;
            string command = "";
            try
            {
                command = File.ReadAllText(TriggerFile).Trim();
                File.Delete(TriggerFile);
            }
            catch
            {
                return;
            }

            string result = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Executing: {command}\n";
            try
            {
                result += ExecuteCommand(command);
            }
            catch (Exception ex)
            {
                result += $"ERROR: {ex}\n";
            }

            try
            {
                File.WriteAllText(LogFile, result);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to write {LogFile}: {ex.Message}");
            }

            Debug.Log($"[OvernightGoalAutomation] {result}");
        }

        private static string ExecuteCommand(string command)
        {
            switch (command.ToUpperInvariant())
            {
                case "PING":
                    return "PONG: Unity Editor is responsive and executing C#.\n";

                case "RUN_STUDIO_TESTS":
                    PizzeriaContentStudioTestRunner.RunAllStudioTests();
                    if (File.Exists("Temp/StudioTestsResult.txt"))
                    {
                        return File.ReadAllText("Temp/StudioTestsResult.txt");
                    }
                    return "Studio tests completed.\n";

                case "ENSURE_LAYOUTS":
                    StationLayoutPresetLibrary.EnsureDefaultLayoutsExist();
                    return "Default station layouts verified and created.\n";

                case "REPAIR_PHASE1":
                    PizzeriaPhase1Repair.Repair();
                    return "PizzeriaPhase1Repair executed successfully.\n";

                case "VALIDATE_SETUP":
                    PizzeriaValidator.ValidateSetup();
                    return "PizzeriaValidator.ValidateSetup executed successfully.\n";

                case "REFRESH":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return "AssetDatabase.Refresh completed.\n";

                case "SEED_PRESETS":
                    VisualStylePresetSeeder.EnsureAllDefaultsExist();
                    return "VisualStyle presets and default ruleset verified.\n";

                case "RUN_PHASE0_AUDIT":
                    VisualStylePresetSeeder.EnsureAllDefaultsExist();
                    var audit = VisualStyleAuditService.RunFullAudit(VisualStylePresetSeeder.EnsureDefaultRuleSet(), writeMarkdownReport: true);
                    return $"Phase0 Audit Complete. Renderers={audit.totalRenderersInspected}, Slots={audit.totalSlotsInspected}, UniqueMats={audit.uniqueMaterialsCount}, Convert={audit.convertibleCount}, Preserve={audit.preservedCount}, Review={audit.manualReviewCount}, Warnings={audit.warningCount}, Report={audit.reportPath}\n";

                case "RUN_VISUALSTYLE_TESTS":
                    DiscardDirtyEditorScenesForAutomation();
                    return VisualStyleTestRunnerBridge.RunFilteredTests(TestMode.EditMode, "ScaryParty\\.VisualStyles", "VisualStyles_EditMode");

                case "RUN_VISUALSTYLE_PLAYMODE":
                    DiscardDirtyEditorScenesForAutomation();
                    return VisualStyleTestRunnerBridge.RunFilteredTests(TestMode.PlayMode, "ScaryParty\\.VisualStyles", "VisualStyles_PlayMode");

                case "BUILD_VISUALSTYLE_ALL":
                {
                    VisualStylePresetSeeder.EnsureAllDefaultsExist();
                    var profile = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.SoftToonVibrant);
                    var ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();
                    bool applied = VisualStyleGameplayIntegrator.ApplyToGameplayVariant(profile, ruleSet);
                    string showcase = VisualStyleShowcaseBuilder.BuildOrUpdateShowcaseScene(profile, ruleSet);
                    int pngs = GlobalCelShadingWizard.CaptureAndExportABScreenshotsHeadless(profile, ruleSet);
                    return $"BUILD_VISUALSTYLE_ALL: applied={applied}, showcase={showcase}, pngs={pngs}\n";
                }

                default:
                    if (command.StartsWith("RUN_EDITMODE_FILTER:", StringComparison.OrdinalIgnoreCase))
                    {
                        DiscardDirtyEditorScenesForAutomation();
                        string pattern = command.Substring("RUN_EDITMODE_FILTER:".Length).Trim();
                        return VisualStyleTestRunnerBridge.RunFilteredTests(TestMode.EditMode, pattern, $"EditMode:{pattern}");
                    }
                    if (command.StartsWith("RUN_PLAYMODE_FILTER:", StringComparison.OrdinalIgnoreCase))
                    {
                        DiscardDirtyEditorScenesForAutomation();
                        string pattern = command.Substring("RUN_PLAYMODE_FILTER:".Length).Trim();
                        return VisualStyleTestRunnerBridge.RunFilteredTests(TestMode.PlayMode, pattern, $"PlayMode:{pattern}");
                    }
                    return $"Unknown command: {command}\n";
            }
        }

        private static void DiscardDirtyEditorScenesForAutomation()
        {
            bool anyDirty = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                {
                    anyDirty = true;
                    break;
                }
            }

            if (anyDirty)
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
            }
        }

        private static void AdvanceBatchVerification()
        {
            if (!File.Exists(BatchStateFile)) return;

            string raw;
            try
            {
                raw = File.ReadAllText(BatchStateFile).Trim();
            }
            catch
            {
                return;
            }

            string[] parts = raw.Split('|');
            if (parts.Length < 2 || !long.TryParse(parts[1], out long ticks))
            {
                File.Delete(BatchStateFile);
                EditorApplication.Exit(2);
                return;
            }

            string phase = parts[0];
            long nowTicks = DateTime.UtcNow.Ticks;

            if (phase == "WAIT_STARTUP")
            {
                _batchStartupTickCount++;
                if (_batchStartupTickCount < 20 || nowTicks < ticks) return;

                DiscardDirtyEditorScenesForAutomation();
                DeleteIfExists(VisualStyleTestRunnerBridge.StatusFilePath);
                DeleteIfExists(VisualStyleTestRunnerBridge.ResultsFilePath);
                DeleteIfExists(EditModeResultsCopyPath);
                DeleteIfExists(PlayModeResultsCopyPath);

                long deadline = DateTime.UtcNow.AddMinutes(8).Ticks;
                File.WriteAllText(BatchStateFile, $"EDITMODE_RUNNING|{deadline}");
                string msg = VisualStyleTestRunnerBridge.RunFilteredTests(TestMode.EditMode, "ScaryParty\\.VisualStyles", "VisualStyles_EditMode");
                Debug.Log($"[OvernightGoalAutomation] Batch Step 1/2: {msg.Trim()}");
                return;
            }

            if (phase == "WAIT_BEFORE_PLAYMODE")
            {
                if (nowTicks < ticks) return;

                DiscardDirtyEditorScenesForAutomation();
                long deadline = DateTime.UtcNow.AddMinutes(6).Ticks;
                File.WriteAllText(BatchStateFile, $"PLAYMODE_RUNNING|{deadline}");
                string msg = VisualStyleTestRunnerBridge.RunFilteredTests(TestMode.PlayMode, "ScaryParty\\.VisualStyles", "VisualStyles_PlayMode");
                Debug.Log($"[OvernightGoalAutomation] Batch Step 2/2: {msg.Trim()}");
                return;
            }

            if (phase == "PLAYMODE_COOLDOWN")
            {
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (Directory.GetFiles("Assets", "InitTestScene*.unity").Length > 0) return;
                if (nowTicks < ticks) return;

                CopyIfExists(VisualStyleTestRunnerBridge.ResultsFilePath, PlayModeResultsCopyPath);
                string finalStatus = File.Exists(VisualStyleTestRunnerBridge.StatusFilePath)
                    ? File.ReadAllText(VisualStyleTestRunnerBridge.StatusFilePath).Trim()
                    : string.Empty;
                bool passed = finalStatus.StartsWith("DONE:VisualStyles_PlayMode:PASSED:", StringComparison.Ordinal);
                File.Delete(BatchStateFile);
                Debug.Log($"[OvernightGoalAutomation] Batch verification finished. PlayMode passed={passed}.");
                EditorApplication.Exit(passed ? 0 : 1);
                return;
            }

            if (nowTicks > ticks)
            {
                Debug.LogError($"[OvernightGoalAutomation] Batch verification timed out in phase {phase}.");
                File.Delete(BatchStateFile);
                EditorApplication.Exit(2);
                return;
            }

            string status = File.Exists(VisualStyleTestRunnerBridge.StatusFilePath)
                ? File.ReadAllText(VisualStyleTestRunnerBridge.StatusFilePath).Trim()
                : string.Empty;

            if (phase == "EDITMODE_RUNNING")
            {
                if (!status.StartsWith("DONE:VisualStyles_EditMode:", StringComparison.Ordinal)) return;

                CopyIfExists(VisualStyleTestRunnerBridge.ResultsFilePath, EditModeResultsCopyPath);
                bool passed = status.StartsWith("DONE:VisualStyles_EditMode:PASSED:", StringComparison.Ordinal);
                if (!passed)
                {
                    Debug.LogError($"[OvernightGoalAutomation] EditMode suite failed: {status}");
                    File.Delete(BatchStateFile);
                    EditorApplication.Exit(1);
                    return;
                }

                // Allow Unity TestJobRunner post-cleanup/UnlockReloadAssembliesTask to finish before PlayMode starts.
                long waitUntil = DateTime.UtcNow.AddSeconds(2).Ticks;
                File.WriteAllText(BatchStateFile, $"WAIT_BEFORE_PLAYMODE|{waitUntil}");
                return;
            }

            if (phase == "PLAYMODE_RUNNING")
            {
                if (!status.StartsWith("DONE:VisualStyles_PlayMode:", StringComparison.Ordinal)) return;
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

                long waitUntil = DateTime.UtcNow.AddSeconds(2).Ticks;
                File.WriteAllText(BatchStateFile, $"PLAYMODE_COOLDOWN|{waitUntil}");
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static void CopyIfExists(string sourcePath, string destPath)
        {
            if (!File.Exists(sourcePath)) return;
            string dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(sourcePath, destPath, overwrite: true);
        }
    }
}
#endif

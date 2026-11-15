#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor
{
    /// <summary>
    /// Runs Unity EditMode and PlayMode NUnit suites via TestRunnerApi and persists deterministic
    /// results to Temp/visualstyle_test_results.txt for automated verification.
    /// Survives PlayMode Domain Reloads by re-registering ICallbacks/IErrorCallbacks on InitializeOnLoad.
    /// </summary>
    [InitializeOnLoad]
    public static class VisualStyleTestRunnerBridge
    {
        public const string StatusFilePath = "Temp/visualstyle_test_status.txt";
        public const string ResultsFilePath = "Temp/visualstyle_test_results.txt";
        private const string ActiveRunFilePath = "Temp/visualstyle_test_active.txt";

        private static TestRunnerApi _activeApi;
        private static ResultCollector _activeCollector;

        static VisualStyleTestRunnerBridge()
        {
            RestoreActiveCollectorIfNeeded();
        }

        private static void RestoreActiveCollectorIfNeeded()
        {
            if (_activeCollector != null || !File.Exists(ActiveRunFilePath))
            {
                return;
            }

            try
            {
                string suiteLabel = File.ReadAllText(ActiveRunFilePath).Trim();
                if (string.IsNullOrEmpty(suiteLabel))
                {
                    File.Delete(ActiveRunFilePath);
                    return;
                }

                _activeApi = ScriptableObject.CreateInstance<TestRunnerApi>();
                _activeCollector = new ResultCollector(_activeApi, suiteLabel);
                TestRunnerApi.RegisterTestCallback(_activeCollector);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VisualStyleTestRunner] Failed to restore test callbacks across domain reload: {ex.Message}");
            }
        }

        private sealed class ResultCollector : IErrorCallbacks
        {
            private readonly TestRunnerApi _api;
            private readonly string _suiteLabel;
            private readonly StringBuilder _failures = new StringBuilder();
            private int _total;
            private int _passed;
            private int _failed;
            private int _skipped;

            public ResultCollector(TestRunnerApi api, string suiteLabel)
            {
                _api = api;
                _suiteLabel = suiteLabel;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(StatusFilePath, $"RUNNING:{_suiteLabel}:{DateTime.UtcNow:O}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    CollectLeafResults(result);
                    bool passed = _failed == 0 && _total > 0;
                    var sb = new StringBuilder();
                    sb.AppendLine($"SUITE={_suiteLabel}");
                    sb.AppendLine($"STATUS={(passed ? "PASSED" : "FAILED")}");
                    sb.AppendLine($"TOTAL={_total}");
                    sb.AppendLine($"PASSED={_passed}");
                    sb.AppendLine($"FAILED={_failed}");
                    sb.AppendLine($"SKIPPED={_skipped}");
                    if (_failures.Length > 0)
                    {
                        sb.AppendLine("--- FAILURES ---");
                        sb.Append(_failures);
                    }

                    File.WriteAllText(ResultsFilePath, sb.ToString());
                    File.WriteAllText(StatusFilePath, $"DONE:{_suiteLabel}:{(passed ? "PASSED" : "FAILED")}:{DateTime.UtcNow:O}");
                    Debug.Log($"[VisualStyleTestRunner] {_suiteLabel} finished: {_passed}/{_total} passed, {_failed} failed, {_skipped} skipped.");
                }
                finally
                {
                    CleanupRegistration();
                }
            }

            public void OnError(string message)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"SUITE={_suiteLabel}");
                    sb.AppendLine("STATUS=FAILED");
                    sb.AppendLine("TOTAL=0");
                    sb.AppendLine("PASSED=0");
                    sb.AppendLine("FAILED=1");
                    sb.AppendLine("SKIPPED=0");
                    sb.AppendLine("--- FAILURES ---");
                    sb.AppendLine($"[ERROR] {message}");

                    Directory.CreateDirectory("Temp");
                    File.WriteAllText(ResultsFilePath, sb.ToString());
                    File.WriteAllText(StatusFilePath, $"DONE:{_suiteLabel}:FAILED:{DateTime.UtcNow:O}");
                    Debug.LogError($"[VisualStyleTestRunner] {_suiteLabel} failed with error: {message}");
                }
                finally
                {
                    CleanupRegistration();
                }
            }

            private void CleanupRegistration()
            {
                try
                {
                    if (File.Exists(ActiveRunFilePath))
                    {
                        File.Delete(ActiveRunFilePath);
                    }
                }
                catch { /* ignore temp cleanup failure */ }

                try
                {
                    TestRunnerApi.UnregisterTestCallback(this);
                }
                catch { /* ignore unregister failure */ }

                if (_activeCollector == this)
                {
                    _activeApi = null;
                    _activeCollector = null;
                }
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            private void CollectLeafResults(ITestResultAdaptor node)
            {
                if (node == null) return;
                if (!node.HasChildren)
                {
                    _total++;
                    switch (node.TestStatus)
                    {
                        case TestStatus.Passed:
                            _passed++;
                            break;
                        case TestStatus.Skipped:
                            _skipped++;
                            break;
                        default:
                            _failed++;
                            _failures.AppendLine($"[FAIL] {node.FullName}: {node.Message}");
                            if (!string.IsNullOrEmpty(node.StackTrace))
                            {
                                _failures.AppendLine(node.StackTrace);
                            }
                            break;
                    }
                    return;
                }

                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        CollectLeafResults(child);
                    }
                }
            }
        }

        public static string RunFilteredTests(TestMode mode, string filterRegex, string label)
        {
            if (_activeCollector != null)
            {
                try
                {
                    TestRunnerApi.UnregisterTestCallback(_activeCollector);
                }
                catch { /* ignore */ }
                _activeCollector = null;
                _activeApi = null;
            }

            Directory.CreateDirectory("Temp");
            File.WriteAllText(ActiveRunFilePath, label);
            File.WriteAllText(StatusFilePath, $"QUEUED:{label}:{DateTime.UtcNow:O}");

            _activeApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            _activeCollector = new ResultCollector(_activeApi, label);
            TestRunnerApi.RegisterTestCallback(_activeCollector);

            var filter = new Filter
            {
                testMode = mode
            };

            if (!string.IsNullOrWhiteSpace(filterRegex))
            {
                filter.groupNames = new[] { filterRegex };
            }

            _activeApi.Execute(new ExecutionSettings(filter));
            return $"Started {mode} test execution '{label}' with filter '{filterRegex}'. Check {ResultsFilePath}.\n";
        }
    }
}
#endif

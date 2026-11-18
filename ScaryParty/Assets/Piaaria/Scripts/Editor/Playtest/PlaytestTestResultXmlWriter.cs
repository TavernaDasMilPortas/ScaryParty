using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ScaryParty.Editor.Playtest
{
    /// <summary>
    /// Captura resultados reais do Unity Test Runner (EditMode e PlayMode) via TestRunnerApi.ICallbacks,
    /// preservando XML NUnit3 original em pasta local de evidências e resumo compacto JSON no repositório.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaytestTestResultXmlWriter
    {
        public const string EvidenceRepoDir = "Docs/Release/Reports/Evidence";
        public const string ArtifactResultsDir = @"C:\Users\Rubens\.gemini\antigravity\brain\0ec9f96e-b24b-47a6-96b0-3d4feba2667a\test-results";

        public static string NextRunTag { get; set; }
        private static readonly XmlCallback CallbackInstance = new XmlCallback();
        private static bool _registered;
        private static string _lastRunSummaryLine;

        static PlaytestTestResultXmlWriter()
        {
            EnsureRegistered();
        }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(CallbackInstance);
        }

        public static string RunEditModeRegexSuite(string tag, string groupRegex)
        {
            EnsureRegistered();
            NextRunTag = tag;
            _lastRunSummaryLine = null;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var settings = new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                groupNames = new[] { groupRegex }
            })
            {
                runSynchronously = true
            };
            api.Execute(settings);
            return _lastRunSummaryLine ?? "RunFinished callback was not invoked synchronously.";
        }

        private sealed class XmlCallback : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (result == null) return;
                try
                {
                    string customTag = NextRunTag;
                    NextRunTag = null;
                    string modeTag = !string.IsNullOrEmpty(customTag) ? customTag : DetermineModeTag(result);
                    Directory.CreateDirectory(ArtifactResultsDir);

                    string xmlFileName = $"Unity_{modeTag}_Results.xml";
                    string xmlPath = Path.Combine(ArtifactResultsDir, xmlFileName);
                    WriteNUnit3Xml(result, xmlPath);

                    string repoEvidencePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", EvidenceRepoDir));
                    Directory.CreateDirectory(repoEvidencePath);
                    string summaryPath = Path.Combine(repoEvidencePath, $"Unity_{modeTag}_Summary.json");
                    WriteCompactSummaryJson(result, modeTag, xmlFileName, summaryPath);

                    int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                    _lastRunSummaryLine = $"mode={modeTag} total={total} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} duration={result.Duration:0.###}s xml={xmlPath} summary={summaryPath}";
                    Debug.Log($"[PlaytestTestResultXmlWriter] {_lastRunSummaryLine}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlaytestTestResultXmlWriter] Failed writing test results: {ex.Message}");
                }
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }

        private static string DetermineModeTag(ITestResultAdaptor result)
        {
            if (result.Test != null)
            {
                if ((result.Test.TestMode & TestMode.PlayMode) != 0) return "PlayMode";
                if ((result.Test.TestMode & TestMode.EditMode) != 0) return "EditMode";
            }
            return result.FullName != null && result.FullName.IndexOf("PlayMode", StringComparison.OrdinalIgnoreCase) >= 0
                ? "PlayMode"
                : "EditMode";
        }

        private static void WriteNUnit3Xml(ITestResultAdaptor result, string filePath)
        {
            var rootNode = new TNode("test-run");
            int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
            rootNode.AddAttribute("id", "2");
            rootNode.AddAttribute("testcasecount", total.ToString());
            rootNode.AddAttribute("result", result.ResultState ?? "Unknown");
            rootNode.AddAttribute("total", total.ToString());
            rootNode.AddAttribute("passed", result.PassCount.ToString());
            rootNode.AddAttribute("failed", result.FailCount.ToString());
            rootNode.AddAttribute("inconclusive", result.InconclusiveCount.ToString());
            rootNode.AddAttribute("skipped", result.SkipCount.ToString());
            rootNode.AddAttribute("asserts", result.AssertCount.ToString());
            rootNode.AddAttribute("engine-version", "3.5.0.0");
            rootNode.AddAttribute("clr-version", Environment.Version.ToString());
            rootNode.AddAttribute("start-time", result.StartTime.ToString("u"));
            rootNode.AddAttribute("end-time", result.EndTime.ToString("u"));
            rootNode.AddAttribute("duration", result.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture));
            rootNode.ChildNodes.Add(result.ToXml());

            var settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineOnAttributes = false,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            };
            using (var sw = new StreamWriter(filePath, append: false, encoding: new UTF8Encoding(false)))
            using (var xw = XmlWriter.Create(sw, settings))
            {
                rootNode.WriteTo(xw);
            }
        }

        private static void WriteCompactSummaryJson(ITestResultAdaptor result, string modeTag, string xmlFileName, string summaryPath)
        {
            var failedNames = new List<string>();
            var caseEntries = new List<string>();
            CollectLeafResults(result, failedNames, caseEntries);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"mode\": \"{EscapeJson(modeTag)}\",");
            sb.AppendLine($"  \"unityVersion\": \"{EscapeJson(Application.unityVersion)}\",");
            sb.AppendLine($"  \"resultState\": \"{EscapeJson(result.ResultState ?? "")}\",");
            sb.AppendLine($"  \"total\": {result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount},");
            sb.AppendLine($"  \"passed\": {result.PassCount},");
            sb.AppendLine($"  \"failed\": {result.FailCount},");
            sb.AppendLine($"  \"skipped\": {result.SkipCount},");
            sb.AppendLine($"  \"inconclusive\": {result.InconclusiveCount},");
            sb.AppendLine($"  \"durationSeconds\": {result.Duration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"startTimeUtc\": \"{EscapeJson(result.StartTime.ToString("u"))}\",");
            sb.AppendLine($"  \"endTimeUtc\": \"{EscapeJson(result.EndTime.ToString("u"))}\",");
            sb.AppendLine($"  \"preservedXmlFile\": \"{EscapeJson(xmlFileName)}\",");
            sb.AppendLine("  \"failedTests\": [");
            for (int i = 0; i < failedNames.Count; i++)
                sb.AppendLine($"    \"{EscapeJson(failedNames[i])}\"{(i + 1 < failedNames.Count ? "," : "")}");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"cases\": [");
            for (int i = 0; i < caseEntries.Count; i++)
                sb.AppendLine($"    {caseEntries[i]}{(i + 1 < caseEntries.Count ? "," : "")}");
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(summaryPath, sb.ToString(), new UTF8Encoding(false));
        }

        private static void CollectLeafResults(ITestResultAdaptor node, List<string> failedNames, List<string> caseEntries)
        {
            if (node == null) return;
            if (!node.HasChildren)
            {
                string fullName = node.FullName ?? node.Name ?? "Unknown";
                string status = node.TestStatus.ToString();
                if (node.TestStatus == UnityEditor.TestTools.TestRunner.Api.TestStatus.Failed) failedNames.Add(fullName);
                caseEntries.Add($"{{\"name\":\"{EscapeJson(fullName)}\",\"status\":\"{EscapeJson(status)}\",\"duration\":{node.Duration.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}}}");
                return;
            }
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                    CollectLeafResults(child, failedNames, caseEntries);
            }
        }

        private static string EscapeJson(string s)
            => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
    }
}

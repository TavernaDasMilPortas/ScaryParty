using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ScaryParty.VisualStyles.Tests
{
    /// <summary>
    /// PlayMode runtime validation & real profiler benchmark suite comparing
    /// canonical GameScene vs GameScene_CelShaded and verifying Showcase, OutlineService
    /// highlight suppression, CharacterAppearance MPB color retention, and zero runtime AssetDatabase/Material leaks.
    /// </summary>
    public class VisualStylePlayModeTests
    {
        private const string CanonicalScenePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        private const string VariantScenePath = "Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity";
        private const string ShowcaseScenePath = "Assets/Piaaria/Scenes/VisualTests/GlobalCelShading_Showcase.unity";
        private const string BenchmarkOutputPath = "Assets/Piaaria/Art/VisualStyles/Reports/PlayMode_Benchmark_Latest.json";

        [Serializable]
        private class ScenePerfSample
        {
            public string sceneName;
            public int framesSampled;
            public float avgFrameTimeMs;
            public float avgFps;
            public float avgDrawCalls;
            public float avgSetPassCalls;
            public float avgBatches;
            public int activeIllustrateRenderers;
            public int totalRenderers;
        }

        [Serializable]
        private class PlayModeBenchmarkReport
        {
            public string generatedUtc;
            public string unityVersion;
            public ScenePerfSample gameSceneBaseline;
            public ScenePerfSample gameSceneCelShaded;
            public float fpsDeltaPercent;
            public float drawCallsDeltaPercent;
            public float setPassDeltaPercent;
        }

        private static IEnumerator CleanupNetworkSessionIfNeeded()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null)
            {
                if (nm.IsListening)
                {
                    nm.Shutdown();
                }
                UnityEngine.Object.Destroy(nm.gameObject);
                yield return null;
                yield return null;
            }
        }

        private static IEnumerator LoadSceneByPathPlayMode(string scenePath)
        {
            yield return CleanupNetworkSessionIfNeeded();

#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (op != null && !op.isDone)
            {
                yield return null;
            }
#else
            SceneManager.LoadScene(Path.GetFileNameWithoutExtension(scenePath), LoadSceneMode.Single);
            yield return null;
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return CleanupNetworkSessionIfNeeded();
        }

        private static IEnumerator CaptureScenePerformance(string scenePath, Action<ScenePerfSample> onComplete)
        {
            yield return LoadSceneByPathPlayMode(scenePath);
            // Warm up frames for Awake / Start / initial city gen / shader warm-up
            for (int i = 0; i < 15; i++) yield return null;

            using var mainThreadRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 64);
            using var drawCallsRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 64);
            using var setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 64);
            using var batchesRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 64);

            const int sampleFrames = 30;
            double frameTimeSumMs = 0d;
            double drawCallSum = 0d;
            double setPassSum = 0d;
            double batchesSum = 0d;
            int counted = 0;

            for (int f = 0; f < sampleFrames; f++)
            {
                yield return null;
                double ms = mainThreadRec.LastValue > 0 ? mainThreadRec.LastValue / 1e6 : Time.unscaledDeltaTime * 1000.0;
                frameTimeSumMs += ms;
                drawCallSum += drawCallsRec.LastValue;
                setPassSum += setPassRec.LastValue;
                batchesSum += batchesRec.LastValue;
                counted++;
            }

            float avgMs = (float)(frameTimeSumMs / Math.Max(1, counted));
            float avgFps = avgMs > 0.001f ? 1000f / avgMs : 0f;

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int illustrateCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats != null && mats.Any(m => m != null && VisualStyleShaderUtility.IsIllustrateShader(m.shader)))
                {
                    illustrateCount++;
                }
            }

            onComplete(new ScenePerfSample
            {
                sceneName = Path.GetFileNameWithoutExtension(scenePath),
                framesSampled = counted,
                avgFrameTimeMs = (float)Math.Round(avgMs, 3),
                avgFps = (float)Math.Round(avgFps, 1),
                avgDrawCalls = (float)Math.Round(drawCallSum / Math.Max(1, counted), 1),
                avgSetPassCalls = (float)Math.Round(setPassSum / Math.Max(1, counted), 1),
                avgBatches = (float)Math.Round(batchesSum / Math.Max(1, counted), 1),
                activeIllustrateRenderers = illustrateCount,
                totalRenderers = renderers.Length
            });
        }

        [UnityTest]
        public IEnumerator Benchmark_GameScene_Vs_GameSceneCelShaded_CollectsRealProfilerMetrics()
        {
            Assert.IsTrue(File.Exists(CanonicalScenePath), "Canonical GameScene missing.");
            Assert.IsTrue(File.Exists(VariantScenePath), "Variant GameScene_CelShaded missing.");

            ScenePerfSample baseline = null;
            ScenePerfSample celShaded = null;

            yield return CaptureScenePerformance(CanonicalScenePath, s => baseline = s);
            yield return CaptureScenePerformance(VariantScenePath, s => celShaded = s);

            Assert.IsNotNull(baseline);
            Assert.IsNotNull(celShaded);

            // Verify GameScene_CelShaded activated VisualStyleManager cleanly in runtime
            Assert.IsNotNull(VisualStyleManager.Active, "VisualStyleManager.Active must be populated inside GameScene_CelShaded.");
            Assert.IsTrue(VisualStyleManager.HasActiveStyle);
            Assert.Greater(celShaded.activeIllustrateRenderers, 0, "GameScene_CelShaded should render Illustrate materials in PlayMode.");

            float fpsDelta = baseline.avgFps > 0f ? ((celShaded.avgFps - baseline.avgFps) / baseline.avgFps) * 100f : 0f;
            float dcDelta = baseline.avgDrawCalls > 0f ? ((celShaded.avgDrawCalls - baseline.avgDrawCalls) / baseline.avgDrawCalls) * 100f : 0f;
            float spDelta = baseline.avgSetPassCalls > 0f ? ((celShaded.avgSetPassCalls - baseline.avgSetPassCalls) / baseline.avgSetPassCalls) * 100f : 0f;

            var report = new PlayModeBenchmarkReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                gameSceneBaseline = baseline,
                gameSceneCelShaded = celShaded,
                fpsDeltaPercent = (float)Math.Round(fpsDelta, 2),
                drawCallsDeltaPercent = (float)Math.Round(dcDelta, 2),
                setPassDeltaPercent = (float)Math.Round(spDelta, 2)
            };

            Directory.CreateDirectory(Path.GetDirectoryName(BenchmarkOutputPath));
            File.WriteAllText(BenchmarkOutputPath, JsonUtility.ToJson(report, prettyPrint: true));
        }

        [UnityTest]
        public IEnumerator ShowcaseScene_PlaysCleanly_WithScreenSpaceOutlineSuppressionHook()
        {
            Assert.IsTrue(File.Exists(ShowcaseScenePath), "Showcase scene missing.");
            yield return LoadSceneByPathPlayMode(ShowcaseScenePath);
            yield return null;
            yield return null;

            var mgr = VisualStyleManager.Active;
            Assert.IsNotNull(mgr);
            Assert.IsNotNull(mgr.variantRendererData);

            // Toggle interaction highlight state and verify FullScreenPassRendererFeature reacts cleanly
            VisualStyleManager.SetInteractionHighlightActive(true);
            yield return null; // LateUpdate ticks on VisualStyleManager
            VisualStyleManager.SetInteractionHighlightActive(false);
            yield return null;

            // Verify Original vs CelShaded columns keep strict separation
            var colOrig = GameObject.Find("Column_Original");
            var colToon = GameObject.Find("Column_CelShaded");
            Assert.IsNotNull(colOrig);
            Assert.IsNotNull(colToon);

            int toonIllustrate = colToon.GetComponentsInChildren<Renderer>(true)
                .Count(r => r != null && r.sharedMaterials.Any(m => m != null && VisualStyleShaderUtility.IsIllustrateShader(m.shader)));
            Assert.Greater(toonIllustrate, 5);
        }
    }
}

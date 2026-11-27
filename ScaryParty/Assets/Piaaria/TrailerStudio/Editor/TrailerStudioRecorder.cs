using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Piaaria.TrailerStudio.Editor
{
    public static class TrailerStudioRecorder
    {
        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;
        public const int DefaultFps = 60;
        public const uint DefaultBitrateBps = 24000000;

        private const string BatchActiveKey = "Piaaria.TrailerStudio.BatchActive";
        private const string BatchDoneKey = "Piaaria.TrailerStudio.BatchDone";
        private const string BatchStartKey = "Piaaria.TrailerStudio.BatchStart";

        [InitializeOnLoadMethod]
        private static void RestoreBatchHooksAfterDomainReload()
        {
            if (SessionState.GetBool(BatchActiveKey, false))
            {
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                Application.runInBackground = true;
                EditorApplication.update -= OnBatchEditorUpdate;
                EditorApplication.update += OnBatchEditorUpdate;
                EditorApplication.playModeStateChanged -= OnBatchPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnBatchPlayModeStateChanged;
            }
            else if (SessionState.GetBool(BatchDoneKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.EraseBool(BatchDoneKey);
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[TrailerStudioRecorder] Renderização concluída com sucesso. Encerrando Unity batchmode (0).");
                    EditorApplication.Exit(0);
                };
            }
        }

        /// <summary>
        /// Invocado via CLI: -batchmode -projectPath ... -executeMethod Piaaria.TrailerStudio.Editor.TrailerStudioRecorder.RunBatchRenderAllAndExit (SEM -quit)
        /// </summary>
        public static void RunBatchRenderAllAndExit()
        {
            try
            {
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                Application.runInBackground = true;

                Debug.Log("[TrailerStudioRecorder] Abrindo TrailerStudio.unity para renderização batch 1080p60...");
                EditorSceneManager.OpenScene("Assets/Piaaria/TrailerStudio/Scenes/TrailerStudio.unity", OpenSceneMode.Single);

                TrailerDirector director = UnityEngine.Object.FindFirstObjectByType<TrailerDirector>();
                if (director != null && director.profile != null)
                {
                    director.profile.ResetCamerasToCanonicalDefaults();
                    director.profile.EnsureRuntimeDefaults();
                    EditorUtility.SetDirty(director.profile);
                    AssetDatabase.SaveAssets();
                }

                SessionState.SetBool(BatchActiveKey, true);
                SessionState.SetBool(BatchDoneKey, false);
                SessionState.SetFloat(BatchStartKey, (float)EditorApplication.timeSinceStartup);
                EditorApplication.playModeStateChanged -= OnBatchPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnBatchPlayModeStateChanged;
                EditorApplication.update -= OnBatchEditorUpdate;
                EditorApplication.update += OnBatchEditorUpdate;
                EditorApplication.isPlaying = true;
            }
            catch (Exception ex)
            {
                SessionState.EraseBool(BatchActiveKey);
                Debug.LogError("[TrailerStudioRecorder] Falha fatal ao iniciar batch render: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void OnBatchEditorUpdate()
        {
            if (!SessionState.GetBool(BatchActiveKey, false)) return;

            float startedAt = SessionState.GetFloat(BatchStartKey, 0f);
            if (EditorApplication.timeSinceStartup - startedAt > 600.0)
            {
                SessionState.EraseBool(BatchActiveKey);
                EditorApplication.update -= OnBatchEditorUpdate;
                Debug.LogError("[TrailerStudioRecorder] Timeout aguardando TrailerDirector ficar pronto em Play Mode.");
                EditorApplication.Exit(2);
                return;
            }

            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;

            TrailerDirector director = UnityEngine.Object.FindFirstObjectByType<TrailerDirector>();
            if (director == null) return;

            if (director.state == TrailerDirectorState.Error)
            {
                SessionState.EraseBool(BatchActiveKey);
                EditorApplication.update -= OnBatchEditorUpdate;
                Debug.LogError("[TrailerStudioRecorder] TrailerDirector entrou em Error: " + director.lastValidationError);
                EditorApplication.Exit(3);
                return;
            }

            if (!director.environmentReady) return;

            SessionState.EraseBool(BatchActiveKey);
            EditorApplication.update -= OnBatchEditorUpdate;

            try
            {
                var manifestLines = new List<string>();
                manifestLines.Add($"# Trailer Studio Batch Render — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                manifestLines.Add(RunOperatorStressSequence(director));

                var clipResults = RecordAllTakesSync(director, includeAltCameras: true, saveValidationPngs: true);
                manifestLines.AddRange(clipResults);

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string manifestPath = Path.Combine(projectRoot, "Recordings", "Trailer", "RENDER_MANIFEST.txt");
                File.WriteAllLines(manifestPath, manifestLines);
                Debug.Log("[TrailerStudioRecorder] Manifesto salvo em " + manifestPath + "\n" + string.Join("\n", manifestLines));
            }
            catch (Exception ex)
            {
                Debug.LogError("[TrailerStudioRecorder] Erro durante gravação dos takes: " + ex);
                EditorApplication.Exit(4);
                return;
            }

            SessionState.SetBool(BatchDoneKey, true);
            EditorApplication.isPlaying = false;
        }

        private static void OnBatchPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(BatchDoneKey, false))
            {
                SessionState.EraseBool(BatchDoneKey);
                EditorApplication.playModeStateChanged -= OnBatchPlayModeStateChanged;
                Debug.Log("[TrailerStudioRecorder] Renderização concluída com sucesso. Encerrando Unity batchmode (0).");
                EditorApplication.Exit(0);
            }
        }

        private static string RunOperatorStressSequence(TrailerDirector d)
        {
            // 1. T03 duas vezes seguidas
            d.take = TrailerTakeId.T03_Dough;
            d.TryPrepareTake(); d.Play();
            for (int i = 0; i < 60; i++) d.StepSimulation(1f / 30f);
            d.TryPrepareTake(); d.Play();
            for (int i = 0; i < 60; i++) d.StepSimulation(1f / 30f);

            // 2. Interromper T07 no meio e reiniciar
            d.take = TrailerTakeId.T07_Packaging;
            d.TryPrepareTake(); d.Play();
            for (int i = 0; i < 105; i++) d.StepSimulation(1f / 30f);
            d.StopTake(); d.Play();
            for (int i = 0; i < 120; i++) d.StepSimulation(1f / 30f);

            // 3. Pausar T12 durante a partida e retomar
            d.take = TrailerTakeId.T12_MotorcycleStart;
            d.TryPrepareTake(); d.Play();
            for (int i = 0; i < 130; i++) d.StepSimulation(1f / 30f);
            d.PauseOrResume();
            float pausedTime = d.localTime;
            d.StepSimulation(1f / 30f);
            bool pauseHeld = Mathf.Approximately(d.localTime, pausedTime);
            d.PauseOrResume();
            for (int i = 0; i < 40; i++) d.StepSimulation(1f / 30f);

            // 4. Transições T12 -> T07, T15 -> T03, T08 -> T13
            var transitions = new[]
            {
                (TrailerTakeId.T12_MotorcycleStart, TrailerTakeId.T07_Packaging),
                (TrailerTakeId.T15_FinalGag, TrailerTakeId.T03_Dough),
                (TrailerTakeId.T08_Tickets, TrailerTakeId.T13_CityRide)
            };
            bool transOk = true;
            foreach (var (fromT, toT) in transitions)
            {
                d.take = fromT;
                transOk &= d.TryPrepareTake();
                d.Play();
                for (int i = 0; i < 90; i++) d.StepSimulation(1f / 30f);
                d.take = toT;
                transOk &= d.TryPrepareTake();
                d.Play();
                for (int i = 0; i < 90; i++) d.StepSimulation(1f / 30f);
            }

            return $"STRESS_TEST: pauseHeld={pauseHeld}, transitionsOk={transOk}, state={d.state}, err='{d.lastValidationError}'";
        }

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/Gravar Take Atual em MP4 (1080p60)", false, 194)]
        public static void RecordSelectedTakeMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[TrailerStudioRecorder] Entre em Play Mode na cena TrailerStudio.unity antes de gravar.");
                return;
            }

            TrailerDirector director = UnityEngine.Object.FindFirstObjectByType<TrailerDirector>();
            if (director == null)
            {
                Debug.LogError("[TrailerStudioRecorder] TrailerDirector não encontrado na cena.");
                return;
            }

            string stem = $"{director.take}_{(director.cameraMode == TrailerCameraMode.CameraB_Alt ? "B" : "A")}";
            string result = RecordTakeSync(director, director.take, director.cameraMode, stem, saveValidationPngs: true);
            Debug.Log("[TrailerStudioRecorder] " + result);
        }

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/Gravar Todos os Takes em MP4 (1080p60)", false, 195)]
        public static void RecordAllTakesMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[TrailerStudioRecorder] Entre em Play Mode na cena TrailerStudio.unity antes de gravar todos os takes.");
                return;
            }

            TrailerDirector director = UnityEngine.Object.FindFirstObjectByType<TrailerDirector>();
            if (director == null)
            {
                Debug.LogError("[TrailerStudioRecorder] TrailerDirector não encontrado na cena.");
                return;
            }

            var summary = RecordAllTakesSync(director, includeAltCameras: true, saveValidationPngs: true);
            Debug.Log("[TrailerStudioRecorder] Gravação em lote concluída:\n" + string.Join("\n", summary));
        }

        public static List<string> RecordAllTakesSync(
            TrailerDirector director,
            bool includeAltCameras = true,
            bool saveValidationPngs = true)
        {
            var results = new List<string>();
            if (director == null || director.profile == null)
            {
                results.Add("ERROR: TrailerDirector ou profile nulo.");
                return results;
            }

            DisablePointLightShadowsForCleanAtlas();

            foreach (TrailerTakeId id in Enum.GetValues(typeof(TrailerTakeId)))
            {
                string stemA = $"{id}_A";
                results.Add(RecordTakeSync(director, id, TrailerCameraMode.CameraA, stemA, saveValidationPngs));

                var def = director.profile.GetTake(id);
                if (includeAltCameras && def != null && def.hasCameraB)
                {
                    string stemB = $"{id}_B";
                    results.Add(RecordTakeSync(director, id, TrailerCameraMode.CameraB_Alt, stemB, saveValidationPngs));
                }
            }

            return results;
        }

        public static string RecordTakeSync(
            TrailerDirector director,
            TrailerTakeId takeId,
            TrailerCameraMode cameraMode,
            string fileStem,
            bool saveValidationPngs = true,
            int width = DefaultWidth,
            int height = DefaultHeight,
            int fps = DefaultFps)
        {
            Camera cam = Camera.main;
            if (cam == null && director.cinemachineBrain != null)
                cam = director.cinemachineBrain.GetComponent<Camera>();
            if (cam == null)
                return $"{fileStem}: ERROR Main Camera não encontrada.";

            if (director.cinemachineBrain != null)
                director.cinemachineBrain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(projectRoot, "Recordings", "Trailer");
            string framesDir = Path.Combine(outDir, "ValidationFrames");
            Directory.CreateDirectory(outDir);
            if (saveValidationPngs) Directory.CreateDirectory(framesDir);

            bool prevLoop = director.loopTake;
            bool prevPlayAll = director.playAll;
            float prevSpeed = director.speed;
            director.loopTake = false;
            director.playAll = false;
            director.speed = 1f;

            director.take = takeId;
            director.cameraMode = cameraMode;
            if (!director.TryPrepareTake())
            {
                director.loopTake = prevLoop;
                director.playAll = prevPlayAll;
                director.speed = prevSpeed;
                return $"{fileStem}: PREPARE FAILED -> {director.lastValidationError}";
            }

            TrailerTakeDefinition def = director.profile.GetTake(takeId);
            float totalDuration = def != null ? def.TotalDuration : 6f;
            int totalFrames = Mathf.Max(2, Mathf.CeilToInt(totalDuration * fps));
            int startSnapFrame = Mathf.Clamp(Mathf.RoundToInt(0.35f * fps), 0, totalFrames - 1);
            int midSnapFrame = Mathf.Clamp(Mathf.RoundToInt((def.preRoll + def.actionDuration * 0.58f) * fps), 0, totalFrames - 1);
            int endSnapFrame = Mathf.Clamp(totalFrames - Mathf.RoundToInt(0.25f * fps), 0, totalFrames - 1);

            string mp4Path = Path.Combine(outDir, fileStem + ".mp4");
            if (File.Exists(mp4Path)) File.Delete(mp4Path);

            var rt = new RenderTexture(width, height, 24, GraphicsFormat.R8G8B8A8_SRGB) { antiAliasing = 1 };
            rt.Create();
            var readTex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

            float dt = 1f / fps;
            int encoded = 0;

            try
            {
                var h264Attr = new H264EncoderAttributes
                {
                    gopSize = (uint)(fps / 2),
                    numConsecutiveBFrames = 2,
                    profile = VideoEncodingProfile.H264High
                };
                var videoAttr = new VideoTrackEncoderAttributes(h264Attr)
                {
                    frameRate = new MediaRational(fps, 1),
                    width = (uint)width,
                    height = (uint)height,
                    includeAlpha = false,
                    bitRateMode = VideoBitrateMode.High,
                    targetBitRate = DefaultBitrateBps
                };

                using (var encoder = new MediaEncoder(mp4Path, videoAttr))
                {
                    director.Play();

                    for (int f = 0; f < totalFrames; f++)
                    {
                        if (f > 0)
                        {
                            director.StepSimulation(dt, manualSubsystems: true);
                        }

                        var vcam = director.ActiveVirtualCamera;
                        if (vcam != null)
                        {
                            cam.transform.SetPositionAndRotation(vcam.transform.position, vcam.transform.rotation);
                            cam.fieldOfView = vcam.m_Lens.FieldOfView;
                        }

                        var prevTarget = cam.targetTexture;
                        cam.targetTexture = rt;
                        cam.Render();
                        cam.targetTexture = prevTarget;

                        var prevActive = RenderTexture.active;
                        RenderTexture.active = rt;
                        readTex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                        RenderTexture.active = prevActive;

                        encoder.AddFrame(readTex);
                        encoded++;

                        if (saveValidationPngs)
                        {
                            if (f == startSnapFrame)
                                File.WriteAllBytes(Path.Combine(framesDir, fileStem + "_1_start.png"), readTex.EncodeToPNG());
                            else if (f == midSnapFrame)
                                File.WriteAllBytes(Path.Combine(framesDir, fileStem + "_2_action.png"), readTex.EncodeToPNG());
                            else if (f == endSnapFrame)
                                File.WriteAllBytes(Path.Combine(framesDir, fileStem + "_3_end.png"), readTex.EncodeToPNG());
                        }

                        if (director.state == TrailerDirectorState.Finished && f >= endSnapFrame)
                            break;
                    }
                }
            }
            finally
            {
                director.loopTake = prevLoop;
                director.playAll = prevPlayAll;
                director.speed = prevSpeed;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(readTex);
            }

            var fi = new FileInfo(mp4Path);
            return $"{fileStem}: OK frames={encoded}, duration={(encoded / (float)fps):F2}s, sizeKB={fi.Length / 1024}, path={mp4Path}";
        }

        public static void DisablePointLightShadowsForCleanAtlas()
        {
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != null && l.type == LightType.Point && l.shadows != LightShadows.None)
                    l.shadows = LightShadows.None;
            }
        }
    }
}

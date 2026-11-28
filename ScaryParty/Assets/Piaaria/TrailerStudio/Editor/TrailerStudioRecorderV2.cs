using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Piaaria.TrailerStudio.Editor
{
    public enum TrailerRenderProfile
    {
        Preview = 0, // 960x540, 30 FPS, Medium
        Final = 1    // 1920x1080, 60 FPS, High
    }

    public enum TrailerQueueState
    {
        Pending = 0,
        Setup = 1,
        Warmup = 2,
        Validate = 3,
        Record = 4,
        Finalize = 5,
        Verify = 6,
        Done = 7,
        Failed = 8
    }

    [Serializable]
    public class TrailerTakeSpec
    {
        public TrailerTakeId takeId;
        public TrailerCameraMode cameraMode;
        public string fileName;

        public TrailerTakeSpec(TrailerTakeId id, TrailerCameraMode mode, string name)
        {
            takeId = id;
            cameraMode = mode;
            fileName = name;
        }
    }

    [Serializable]
    public class TrailerTakeManifestEntry
    {
        public string takeId;
        public string fileName;
        public string cameraMode;
        public string profile;
        public string state;
        public string outputPath;
        public float plannedDuration;
        public float actualDuration;
        public int width;
        public int height;
        public int fps;
        public long fileSizeBytes;
        public bool preflightPassed;
        public bool technicalVerified;
        public string visualReview = "visual review pending";
        public int attempts;
        public string lastError;
        public string updatedAt;
    }

    [Serializable]
    public class TrailerManifestData
    {
        public string generatedAt;
        public string unityVersion;
        public string recorderVersion = "5.1.8";
        public List<TrailerTakeManifestEntry> entries = new List<TrailerTakeManifestEntry>();
    }

    public static class TrailerStudioRecorderV2
    {
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string OutputRoot => Path.Combine(ProjectRoot, "TrailerOutput");
        public static string PreviewsDir => Path.Combine(OutputRoot, "Previews");
        public static string FinalDir => Path.Combine(OutputRoot, "Final");
        public static string ReportsDir => Path.Combine(OutputRoot, "Reports");
        public static string ManifestPath => Path.Combine(ReportsDir, "manifest.json");
        public static string ReportMdPath => Path.Combine(ReportsDir, "REPORT.md");

        private const string ScenePath = "Assets/Piaaria/TrailerStudio/Scenes/TrailerStudio.unity";
        private const string BatchModeKey = "Piaaria.TrailerStudioV2.BatchMode";
        private const string BatchActionKey = "Piaaria.TrailerStudioV2.BatchAction";
        private const string BatchProfileKey = "Piaaria.TrailerStudioV2.BatchProfile";

        private static RecorderController _activeController;
        private static RecorderControllerSettings _activeSettings;
        private static bool _isQueueRunning;
        private static bool _cancelRequested;

        // Estado da fila ativa
        private static List<TrailerTakeSpec> _queue = new List<TrailerTakeSpec>();
        private static int _queueIndex;
        private static TrailerRenderProfile _activeProfile;
        private static TrailerQueueState _currentState;
        private static int _warmupFrames;
        private static double _stateStartTime;
        private static string _stagingPathNoExt;
        private static string _finalTargetPath;
        private static TrailerManifestData _manifest;
        private static bool _exitOnQueueComplete;

        public static bool IsQueueRunning => _isQueueRunning;
        public static string ActiveStatusText => _isQueueRunning && _queueIndex < _queue.Count
            ? $"{_queue[_queueIndex].fileName} [{_currentState}] ({_queueIndex + 1}/{_queue.Count})"
            : "Idle";

        public static List<TrailerTakeSpec> GetCanonical16Takes()
        {
            return new List<TrailerTakeSpec>
            {
                new TrailerTakeSpec(TrailerTakeId.T01_CityReveal, TrailerCameraMode.CameraA, "T01_CityReveal_A"),
                new TrailerTakeSpec(TrailerTakeId.T02_KitchenTeam, TrailerCameraMode.CameraA, "T02_KitchenTeam_A"),
                new TrailerTakeSpec(TrailerTakeId.T02_KitchenTeam, TrailerCameraMode.CameraB_Alt, "T02_KitchenTeam_B"),
                new TrailerTakeSpec(TrailerTakeId.T03_Dough, TrailerCameraMode.CameraA, "T03_Dough_A"),
                new TrailerTakeSpec(TrailerTakeId.T03_Dough, TrailerCameraMode.CameraB_Alt, "T03_Dough_B"),
                new TrailerTakeSpec(TrailerTakeId.T04_Ingredients, TrailerCameraMode.CameraA, "T04_Ingredients_A"),
                new TrailerTakeSpec(TrailerTakeId.T05_Assembly, TrailerCameraMode.CameraA, "T05_Assembly_A"),
                new TrailerTakeSpec(TrailerTakeId.T05_Assembly, TrailerCameraMode.CameraB_Alt, "T05_Assembly_B"),
                new TrailerTakeSpec(TrailerTakeId.T06_Oven, TrailerCameraMode.CameraA, "T06_Oven_A"),
                new TrailerTakeSpec(TrailerTakeId.T07_Packaging, TrailerCameraMode.CameraA, "T07_Packaging_A"),
                new TrailerTakeSpec(TrailerTakeId.T07_Packaging, TrailerCameraMode.CameraB_Alt, "T07_Packaging_B"),
                new TrailerTakeSpec(TrailerTakeId.T08_Tickets, TrailerCameraMode.CameraA, "T08_Tickets_A"),
                new TrailerTakeSpec(TrailerTakeId.T09_KitchenChaos, TrailerCameraMode.CameraA, "T09_KitchenChaos_A"),
                new TrailerTakeSpec(TrailerTakeId.T09_KitchenChaos, TrailerCameraMode.CameraB_Alt, "T09_KitchenChaos_B"),
                new TrailerTakeSpec(TrailerTakeId.T10_BurntPizza, TrailerCameraMode.CameraA, "T10_BurntPizza_A"),
                new TrailerTakeSpec(TrailerTakeId.T11_Backpack, TrailerCameraMode.CameraA, "T11_Backpack_A"),
            };
        }

        public static List<TrailerTakeSpec> GetPilotTakes()
        {
            return new List<TrailerTakeSpec>
            {
                new TrailerTakeSpec(TrailerTakeId.T03_Dough, TrailerCameraMode.CameraA, "T03_Dough_A"),
                new TrailerTakeSpec(TrailerTakeId.T06_Oven, TrailerCameraMode.CameraA, "T06_Oven_A"),
                new TrailerTakeSpec(TrailerTakeId.T11_Backpack, TrailerCameraMode.CameraA, "T11_Backpack_A"),
            };
        }

        public static void EnsureOutputDirectories()
        {
            Directory.CreateDirectory(OutputRoot);
            Directory.CreateDirectory(PreviewsDir);
            Directory.CreateDirectory(FinalDir);
            Directory.CreateDirectory(ReportsDir);
        }

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/V2/Open Output Folder", false, 50)]
        public static void OpenOutputFolder()
        {
            EnsureOutputDirectories();
            EditorUtility.RevealInFinder(OutputRoot);
        }

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/V2/Smoke Test Recorder (2s)", false, 51)]
        public static void RunSmokeTestMenu() => StartBatchAction("SMOKE_TEST", TrailerRenderProfile.Preview, false);

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/V2/Render Pilot Takes (540p30)", false, 52)]
        public static void RenderPilotsMenu() => StartBatchAction("PILOTS", TrailerRenderProfile.Preview, false);

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/V2/Render All Previews (540p30)", false, 53)]
        public static void RenderAllPreviewsMenu() => StartBatchAction("ALL_PREVIEWS", TrailerRenderProfile.Preview, false);

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/V2/Render All Final Takes (1080p60)", false, 54)]
        public static void RenderAllFinalMenu() => StartBatchAction("ALL_FINAL", TrailerRenderProfile.Final, false);

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/V2/Cancel Render Queue", false, 55)]
        public static void CancelRenderQueue()
        {
            _cancelRequested = true;
            Debug.LogWarning("[TrailerStudioRecorderV2] Cancelamento da fila solicitado.");
        }

        // CLI Entrypoints
        public static void RunSmokeTestBatch() => StartBatchAction("SMOKE_TEST", TrailerRenderProfile.Preview, true);
        public static void RunPilotPreviewsBatch() => StartBatchAction("PILOTS", TrailerRenderProfile.Preview, true);
        public static void RunAllPreviewsBatch() => StartBatchAction("ALL_PREVIEWS", TrailerRenderProfile.Preview, true);
        public static void RunAllFinalBatch() => StartBatchAction("ALL_FINAL", TrailerRenderProfile.Final, true);

        public static void StartSingleTake(TrailerTakeId takeId, TrailerCameraMode camMode, TrailerRenderProfile profile)
        {
            var spec = new TrailerTakeSpec(takeId, camMode, GetCanonicalFileName(takeId, camMode));
            StartQueue(new List<TrailerTakeSpec> { spec }, profile, exitOnComplete: false);
        }

        private static void StartBatchAction(string action, TrailerRenderProfile profile, bool exitOnComplete)
        {
            EnsureOutputDirectories();
            Debug.Log($"[TrailerStudioRecorderV2] Iniciando ação {action} (profile={profile}, exit={exitOnComplete})");

            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetBool(BatchModeKey, exitOnComplete);
                SessionState.SetString(BatchActionKey, action);
                SessionState.SetInt(BatchProfileKey, (int)profile);
                EditorApplication.update -= OnEditorUpdateTick;
                EditorApplication.update += OnEditorUpdateTick;
                EditorApplication.isPlaying = true;
            }
            else
            {
                DispatchActionInPlayMode(action, profile, exitOnComplete);
            }
        }

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            string action = SessionState.GetString(BatchActionKey, string.Empty);
            if (!string.IsNullOrEmpty(action))
            {
                EditorApplication.update -= OnEditorUpdateTick;
                EditorApplication.update += OnEditorUpdateTick;
            }
        }

        private static void OnEditorUpdateTick()
        {
            string action = SessionState.GetString(BatchActionKey, string.Empty);
            if (string.IsNullOrEmpty(action))
            {
                EditorApplication.update -= OnEditorUpdateTick;
                return;
            }

            if (!EditorApplication.isPlaying) return;

            TrailerDirector director = UnityEngine.Object.FindFirstObjectByType<TrailerDirector>();
            if (director == null) return;

            if (director.state == TrailerDirectorState.Error)
            {
                bool exitOnComplete = SessionState.GetBool(BatchModeKey, false);
                SessionState.EraseString(BatchActionKey);
                SessionState.EraseBool(BatchModeKey);
                EditorApplication.update -= OnEditorUpdateTick;
                Debug.LogError("[TrailerStudioRecorderV2] TrailerDirector Error: " + director.lastValidationError);
                if (exitOnComplete) EditorApplication.Exit(2);
                return;
            }

            if (!director.environmentReady) return;

            bool exit = SessionState.GetBool(BatchModeKey, false);
            var profile = (TrailerRenderProfile)SessionState.GetInt(BatchProfileKey, 0);
            SessionState.EraseString(BatchActionKey);
            SessionState.EraseBool(BatchModeKey);
            EditorApplication.update -= OnEditorUpdateTick;

            DispatchActionInPlayMode(action, profile, exit);
        }

        private static void DispatchActionInPlayMode(string action, TrailerRenderProfile profile, bool exitOnComplete)
        {
            if (action == "SMOKE_TEST")
            {
                ExecuteSmokeTestInPlayMode(exitOnComplete);
            }
            else if (action == "PILOTS")
            {
                StartQueue(GetPilotTakes(), TrailerRenderProfile.Preview, exitOnComplete);
            }
            else if (action == "ALL_PREVIEWS")
            {
                StartQueue(GetCanonical16Takes(), TrailerRenderProfile.Preview, exitOnComplete);
            }
            else if (action == "ALL_FINAL")
            {
                StartQueue(GetCanonical16Takes(), TrailerRenderProfile.Final, exitOnComplete);
            }
        }

        public static void StartQueue(List<TrailerTakeSpec> specs, TrailerRenderProfile profile, bool exitOnComplete)
        {
            if (_isQueueRunning)
            {
                Debug.LogWarning("[TrailerStudioRecorderV2] Já existe uma fila em execução.");
                return;
            }

            EnsureOutputDirectories();
            _manifest = LoadOrCreateManifest();
            _queue = specs;
            _queueIndex = 0;
            _activeProfile = profile;
            _isQueueRunning = true;
            _cancelRequested = false;
            _exitOnQueueComplete = exitOnComplete;
            _currentState = TrailerQueueState.Setup;

            EditorApplication.update -= ProcessQueueTick;
            EditorApplication.update += ProcessQueueTick;
        }

        private static void ProcessQueueTick()
        {
            if (!_isQueueRunning)
            {
                EditorApplication.update -= ProcessQueueTick;
                return;
            }

            if (_cancelRequested || !EditorApplication.isPlaying)
            {
                CleanupActiveRecorder();
                _isQueueRunning = false;
                EditorApplication.update -= ProcessQueueTick;
                SaveManifest();
                Debug.LogWarning("[TrailerStudioRecorderV2] Fila interrompida.");
                if (_exitOnQueueComplete) EditorApplication.Exit(5);
                return;
            }

            if (_queueIndex >= _queue.Count)
            {
                CleanupActiveRecorder();
                _isQueueRunning = false;
                EditorApplication.update -= ProcessQueueTick;
                SaveManifest();
                WriteMarkdownReport();
                Debug.Log("[TrailerStudioRecorderV2] Fila concluída! Manifesto e relatório salvos em TrailerOutput/Reports/");
                if (_exitOnQueueComplete)
                {
                    EditorApplication.isPlaying = false;
                    EditorApplication.delayCall += () => EditorApplication.Exit(0);
                }
                return;
            }

            TrailerDirector director = UnityEngine.Object.FindFirstObjectByType<TrailerDirector>();
            if (director == null) return;

            var spec = _queue[_queueIndex];

            switch (_currentState)
            {
                case TrailerQueueState.Setup:
                {
                    director.loopTake = false;
                    director.playAll = false;
                    director.speed = 1f;
                    director.take = spec.takeId;
                    director.cameraMode = spec.cameraMode;
                    director.PrepareTake();
                    _warmupFrames = 0;
                    _stateStartTime = EditorApplication.timeSinceStartup;
                    _currentState = TrailerQueueState.Warmup;
                    break;
                }

                case TrailerQueueState.Warmup:
                {
                    _warmupFrames++;
                    if (_warmupFrames >= 10)
                    {
                        director.PrepareTake();
                        _currentState = TrailerQueueState.Validate;
                    }
                    break;
                }

                case TrailerQueueState.Validate:
                {
                    bool camOk = Camera.main != null;
                    string preflightErr = camOk ? string.Empty : "Camera.main é nula.";
                    if (camOk && Physics.CheckSphere(Camera.main.transform.position, 0.12f))
                    {
                        preflightErr = $"Câmera dentro de collider em {Camera.main.transform.position}";
                        Debug.LogWarning($"[TrailerStudioRecorderV2] Preflight aviso em {spec.fileName}: {preflightErr}");
                    }

                    UpdateManifestEntry(spec, _activeProfile, TrailerQueueState.Record, preflightPassed: camOk, error: preflightErr);

                    if (!camOk)
                    {
                        UpdateManifestEntry(spec, _activeProfile, TrailerQueueState.Failed, preflightPassed: false, error: preflightErr);
                        _queueIndex++;
                        _currentState = TrailerQueueState.Setup;
                        break;
                    }

                    // Iniciar gravação
                    int width = _activeProfile == TrailerRenderProfile.Final ? 1920 : 960;
                    int height = _activeProfile == TrailerRenderProfile.Final ? 1080 : 540;
                    int fps = _activeProfile == TrailerRenderProfile.Final ? 60 : 30;
                    var quality = _activeProfile == TrailerRenderProfile.Final
                        ? CoreEncoderSettings.VideoEncodingQuality.High
                        : CoreEncoderSettings.VideoEncodingQuality.Medium;

                    string targetDir = _activeProfile == TrailerRenderProfile.Final ? FinalDir : PreviewsDir;
                    _finalTargetPath = Path.Combine(targetDir, spec.fileName + ".mp4");
                    _stagingPathNoExt = Path.Combine(ReportsDir, spec.fileName + "_staging");
                    string stagingMp4 = _stagingPathNoExt + ".mp4";
                    if (File.Exists(stagingMp4)) File.Delete(stagingMp4);

                    float duration = GetTakeDuration(director, spec.takeId);
                    int totalFrames = Mathf.Max(fps, Mathf.RoundToInt(duration * fps));

                    var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                    var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                    movieSettings.name = spec.fileName;
                    movieSettings.Enabled = true;
                    movieSettings.EncoderSettings = new CoreEncoderSettings
                    {
                        Codec = CoreEncoderSettings.OutputCodec.MP4,
                        EncodingQuality = quality
                    };
                    movieSettings.ImageInputSettings = new CameraInputSettings
                    {
                        Source = ImageSource.MainCamera,
                        OutputWidth = width,
                        OutputHeight = height,
                        FlipFinalOutput = false
                    };
                    movieSettings.AudioInputSettings.PreserveAudio = false;
                    movieSettings.OutputFile = _stagingPathNoExt;

                    controllerSettings.AddRecorderSettings(movieSettings);
                    controllerSettings.SetRecordModeToFrameInterval(0, totalFrames - 1);
                    controllerSettings.FrameRate = fps;
                    controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
                    controllerSettings.CapFrameRate = true;

                    _activeSettings = controllerSettings;
                    _activeController = new RecorderController(controllerSettings);
                    _activeController.PrepareRecording();

                    director.Play();
                    _activeController.StartRecording();
                    _stateStartTime = EditorApplication.timeSinceStartup;
                    _currentState = TrailerQueueState.Record;
                    Debug.Log($"[TrailerStudioRecorderV2] Gravando {spec.fileName} ({width}x{height}@{fps}fps, {totalFrames} frames)...");
                    break;
                }

                case TrailerQueueState.Record:
                {
                    if (_activeController != null && _activeController.IsRecording())
                    {
                        if (EditorApplication.timeSinceStartup - _stateStartTime > 600.0)
                        {
                            Debug.LogError($"[TrailerStudioRecorderV2] Timeout gravando {spec.fileName}");
                            CleanupActiveRecorder();
                            UpdateManifestEntry(spec, _activeProfile, TrailerQueueState.Failed, error: "Timeout na gravação");
                            _queueIndex++;
                            _currentState = TrailerQueueState.Setup;
                        }
                        return;
                    }

                    CleanupActiveRecorder();
                    _stateStartTime = EditorApplication.timeSinceStartup;
                    _currentState = TrailerQueueState.Finalize;
                    break;
                }

                case TrailerQueueState.Finalize:
                {
                    if (EditorApplication.timeSinceStartup - _stateStartTime < 0.5)
                        return;
                    _currentState = TrailerQueueState.Verify;
                    break;
                }

                case TrailerQueueState.Verify:
                {
                    string stagingMp4 = _stagingPathNoExt + ".mp4";
                    bool exists = File.Exists(stagingMp4);
                    long size = exists ? new FileInfo(stagingMp4).Length : 0;

                    if (exists && size > 10240)
                    {
                        if (File.Exists(_finalTargetPath)) File.Delete(_finalTargetPath);
                        File.Move(stagingMp4, _finalTargetPath);
                        UpdateManifestEntry(spec, _activeProfile, TrailerQueueState.Done, preflightPassed: true, technicalVerified: true, fileSize: size, outputPath: _finalTargetPath);
                        Debug.Log($"[TrailerStudioRecorderV2] OK: {spec.fileName} -> {_finalTargetPath} ({size / 1024} KB)");
                    }
                    else
                    {
                        UpdateManifestEntry(spec, _activeProfile, TrailerQueueState.Failed, preflightPassed: true, technicalVerified: false, fileSize: size, error: $"Arquivo inválido (exists={exists}, size={size})");
                        Debug.LogError($"[TrailerStudioRecorderV2] FALHA: {spec.fileName} (exists={exists}, size={size})");
                    }

                    SaveManifest();
                    _queueIndex++;
                    _currentState = TrailerQueueState.Setup;
                    break;
                }
            }
        }

        private static float GetTakeDuration(TrailerDirector director, TrailerTakeId takeId)
        {
            if (director != null && director.profile != null && director.profile.takes != null)
            {
                var def = director.profile.takes.FirstOrDefault(t => t != null && t.id == takeId);
                if (def != null && def.TotalDuration > 0.5f)
                    return def.TotalDuration;
            }
            return 6.0f;
        }

        private static void ExecuteSmokeTestInPlayMode(bool exitOnComplete)
        {
            try
            {
                TrailerDirector director = UnityEngine.Object.FindFirstObjectByType<TrailerDirector>();
                if (director == null)
                    throw new Exception("TrailerDirector não encontrado na cena.");

                director.take = TrailerTakeId.T03_Dough;
                director.cameraMode = TrailerCameraMode.CameraA;
                director.PrepareTake();
                director.Play();

                string smokePathNoExt = Path.Combine(ReportsDir, "smoke_test");
                string smokeMp4 = smokePathNoExt + ".mp4";
                if (File.Exists(smokeMp4)) File.Delete(smokeMp4);

                var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movieSettings.name = "SmokeTestMovie";
                movieSettings.Enabled = true;
                movieSettings.EncoderSettings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Medium
                };
                movieSettings.ImageInputSettings = new CameraInputSettings
                {
                    Source = ImageSource.MainCamera,
                    OutputWidth = 960,
                    OutputHeight = 540,
                    FlipFinalOutput = false
                };
                movieSettings.AudioInputSettings.PreserveAudio = false;
                movieSettings.OutputFile = smokePathNoExt;

                controllerSettings.AddRecorderSettings(movieSettings);
                controllerSettings.SetRecordModeToFrameInterval(0, 59);
                controllerSettings.FrameRate = 30f;
                controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
                controllerSettings.CapFrameRate = true;

                var controller = new RecorderController(controllerSettings);
                controller.PrepareRecording();
                controller.StartRecording();

                _activeController = controller;
                _activeSettings = controllerSettings;

                double startTime = EditorApplication.timeSinceStartup;
                EditorApplication.CallbackFunction waitFinish = null;
                waitFinish = () =>
                {
                    if (_activeController != null && _activeController.IsRecording() &&
                        EditorApplication.timeSinceStartup - startTime < 30.0)
                    {
                        return;
                    }

                    EditorApplication.update -= waitFinish;
                    CleanupActiveRecorder();

                    EditorApplication.delayCall += () =>
                    {
                        bool exists = File.Exists(smokeMp4);
                        long size = exists ? new FileInfo(smokeMp4).Length : 0;
                        Debug.Log($"[TrailerStudioRecorderV2] Smoke Test concluído: exists={exists}, size={size} bytes, path={smokeMp4}");

                        if (exitOnComplete)
                        {
                            EditorApplication.isPlaying = false;
                            EditorApplication.delayCall += () => EditorApplication.Exit(exists && size > 1024 ? 0 : 3);
                        }
                    };
                };
                EditorApplication.update += waitFinish;
            }
            catch (Exception ex)
            {
                Debug.LogError("[TrailerStudioRecorderV2] Smoke Test falhou: " + ex);
                CleanupActiveRecorder();
                if (exitOnComplete)
                {
                    EditorApplication.isPlaying = false;
                    EditorApplication.delayCall += () => EditorApplication.Exit(4);
                }
            }
        }

        public static void CleanupActiveRecorder()
        {
            if (_activeController != null)
            {
                if (_activeController.IsRecording())
                    _activeController.StopRecording();
                _activeController = null;
            }

            if (_activeSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(_activeSettings);
                _activeSettings = null;
            }
        }

        private static TrailerManifestData LoadOrCreateManifest()
        {
            if (File.Exists(ManifestPath))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<TrailerManifestData>(File.ReadAllText(ManifestPath));
                    if (loaded != null && loaded.entries != null) return loaded;
                }
                catch { }
            }
            return new TrailerManifestData
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                unityVersion = Application.unityVersion
            };
        }

        private static void UpdateManifestEntry(
            TrailerTakeSpec spec,
            TrailerRenderProfile profile,
            TrailerQueueState state,
            bool preflightPassed = true,
            bool technicalVerified = false,
            long fileSize = 0,
            string outputPath = "",
            string error = "")
        {
            var entry = _manifest.entries.FirstOrDefault(e => e.fileName == spec.fileName && e.profile == profile.ToString());
            if (entry == null)
            {
                entry = new TrailerTakeManifestEntry
                {
                    takeId = spec.takeId.ToString(),
                    fileName = spec.fileName,
                    cameraMode = spec.cameraMode.ToString(),
                    profile = profile.ToString()
                };
                _manifest.entries.Add(entry);
            }

            if (state == TrailerQueueState.Record) entry.attempts++;
            entry.state = state.ToString();
            entry.width = profile == TrailerRenderProfile.Final ? 1920 : 960;
            entry.height = profile == TrailerRenderProfile.Final ? 1080 : 540;
            entry.fps = profile == TrailerRenderProfile.Final ? 60 : 30;
            entry.preflightPassed = preflightPassed;
            entry.technicalVerified = technicalVerified;
            entry.fileSizeBytes = fileSize;
            if (!string.IsNullOrEmpty(outputPath)) entry.outputPath = outputPath;
            entry.lastError = error;
            entry.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static void SaveManifest()
        {
            _manifest.generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(_manifest, true));
        }

        private static void WriteMarkdownReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Trailer Studio V2 — Relatório de Renderização");
            sb.AppendLine($"Gerado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("| Take | Perfil | Status | Resolução | FPS | Tamanho (KB) | Preflight | Técnico | Visual |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var e in _manifest.entries)
            {
                sb.AppendLine($"| {e.fileName} | {e.profile} | {e.state} | {e.width}x{e.height} | {e.fps} | {e.fileSizeBytes / 1024} | {e.preflightPassed} | {e.technicalVerified} | {e.visualReview} |");
            }
            File.WriteAllText(ReportMdPath, sb.ToString());
        }

        public static string GetCanonicalFileName(TrailerTakeId takeId, TrailerCameraMode camMode)
        {
            string suffix = camMode == TrailerCameraMode.CameraA ? "A" : "B";
            switch (takeId)
            {
                case TrailerTakeId.T01_CityReveal: return $"T01_CityReveal_{suffix}";
                case TrailerTakeId.T02_KitchenTeam: return $"T02_KitchenTeam_{suffix}";
                case TrailerTakeId.T03_Dough: return $"T03_Dough_{suffix}";
                case TrailerTakeId.T04_Ingredients: return $"T04_Ingredients_{suffix}";
                case TrailerTakeId.T05_Assembly: return $"T05_Assembly_{suffix}";
                case TrailerTakeId.T06_Oven: return $"T06_Oven_{suffix}";
                case TrailerTakeId.T07_Packaging: return $"T07_Packaging_{suffix}";
                case TrailerTakeId.T08_Tickets: return $"T08_Tickets_{suffix}";
                case TrailerTakeId.T09_KitchenChaos: return $"T09_KitchenChaos_{suffix}";
                case TrailerTakeId.T10_BurntPizza: return $"T10_BurntPizza_{suffix}";
                case TrailerTakeId.T11_Backpack: return $"T11_Backpack_{suffix}";
                default: return $"{takeId}_{suffix}";
            }
        }
    }
}

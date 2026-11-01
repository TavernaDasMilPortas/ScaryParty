using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.UI.Piaria.Presenters;
using ScaryParty.UI.Piaria.Exceptions;

namespace ScaryParty.UI.Piaria.Editor
{
    [InitializeOnLoad]
    public static class PiariaAutoRunner
    {
        private const string TriggerFile = "Temp/PiariaRunTrigger.txt";
        private const string ResultFile = "PiariaInstallationResult.txt";

        static PiariaAutoRunner()
        {
            EditorApplication.update += CheckAndRun;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                AutoConfigureActiveScene();
            }
        }

        [MenuItem("Tools/Scary Party/UI/Abrir LobbyScene", false, 1)]
        public static void OpenLobbyScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            AutoConfigureActiveScene();
        }

        [MenuItem("Tools/Scary Party/UI/Configurar Cena Atual", false, 13)]
        public static void ForceConfigureCurrentScene()
        {
            AutoConfigureActiveScene();
            Debug.Log("[PiariaAutoRunner] Cena atual configurada com sucesso!");
        }

        [MenuItem("Tools/Scary Party/UI/Executar Trigger", false, 14)]
        public static void ForceRun()
        {
            Execute();
        }

        private static void CheckAndRun()
        {
            if (!File.Exists(TriggerFile)) return;

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += CheckAndRun;
                return;
            }

            try
            {
                File.Delete(TriggerFile);
                Execute();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PiariaAutoRunner] Erro durante disparo: {ex}");
                File.WriteAllText(ResultFile, $"FAILED: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void Execute()
        {
            Debug.Log("[PiariaAutoRunner] Iniciando execução de RunInstallationBatch...");
            var window = ScriptableObject.CreateInstance<PiariaUIInstallerWindow>();
            window.ExecuteFullInstallation();
            UnityEngine.Object.DestroyImmediate(window);

            // Reabre a LobbyScene para o usuário poder testar imediatamente
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            AutoConfigureActiveScene();

            File.WriteAllText(ResultFile, $"SUCCESS: Installed at {DateTime.Now}\n");
            Debug.Log("[PiariaAutoRunner] Execução finalizada com sucesso!");
        }

        public static void AutoConfigureActiveScene()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.name)) return;

            string sceneName = activeScene.name;
            var catalog = AssetDatabase.LoadAssetAtPath<PiariaUIAssetCatalog>("Assets/UI/Piaria/Config/PiariaUIAssetCatalog.asset")
                          ?? AssetDatabase.LoadAssetAtPath<PiariaUIAssetCatalog>("Assets/Resources/PiariaUIAssetCatalog.asset");
            var panelSettings = catalog?.panelSettings ?? AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");

            if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 &&
                sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) < 0)
            {
                ConfigureLobbyScene(panelSettings);
            }
            else if (sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ConfigureReadyScene(panelSettings);
            }
            else if (sceneName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0 || sceneName.IndexOf("Sample", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ConfigureGameScene(panelSettings);
            }
        }

        private static void ConfigureLobbyScene(PanelSettings panelSettings)
        {
            var uiGo = GameObject.Find("--- UI ---");
            if (uiGo == null) uiGo = new GameObject("--- UI ---");

            var doc = uiGo.GetComponent<UIDocument>() ?? uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            var containerUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/LobbySceneContainer.uxml");
            if (containerUxml != null) doc.visualTreeAsset = containerUxml;
            EditorUtility.SetDirty(doc);

            EnsureComp<UIScreenRouter>(uiGo);
            EnsureComp<UIFeedbackService>(uiGo);
            EnsureComp<UIModalStack>(uiGo);

            var modal = EnsureComp<ModalPresenter>(uiGo);
            modal.uiDocument = doc;
            EditorUtility.SetDirty(modal);

            var settings = EnsureComp<SettingsPresenter>(uiGo);
            settings.uiDocument = doc;
            EditorUtility.SetDirty(settings);

            var menu = EnsureComp<MainMenuPresenter>(uiGo);
            menu.uiDocument = doc;
            EditorUtility.SetDirty(menu);

            var lobby = EnsureComp<LobbyPresenter>(uiGo);
            lobby.uiDocument = doc;
            lobby.lobbyManager = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
            EditorUtility.SetDirty(lobby);

            var toast = EnsureComp<ToastPresenter>(uiGo);
            toast.uiDocument = doc;
            EditorUtility.SetDirty(toast);

            EnsureComp<CharacterPreviewRig>(uiGo);
            EditorUtility.SetDirty(uiGo);

            // Remove presenters de outras cenas que possam ter sido arrastados por engano para o objeto raiz do Lobby
            RemoveComp<ReadyRoomPresenter>(uiGo);
            RemoveComp<HudPresenter>(uiGo);
            RemoveComp<ScoreboardPresenter>(uiGo);
            RemoveComp<StoragePresenter>(uiGo);
            RemoveComp<StagingPresenter>(uiGo);

            // Auto-conecta presenters em quaisquer outros GameObjects da cena para evitar campos None
            var strayPresenters = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in strayPresenters)
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Namespace != null && type.Namespace.StartsWith("ScaryParty.UI.Piaria.Presenters"))
                {
                    var docProp = type.GetField("uiDocument");
                    if (docProp != null && docProp.GetValue(mb) == null)
                    {
                        docProp.SetValue(mb, doc);
                        EditorUtility.SetDirty(mb);
                    }
                    if (mb is LobbyPresenter lp && lp.lobbyManager == null)
                    {
                        lp.lobbyManager = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
                        EditorUtility.SetDirty(lp);
                    }
                }
            }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        private static void ConfigureReadyScene(PanelSettings panelSettings)
        {
            var uiGo = GameObject.Find("--- UI ---");
            if (uiGo == null) uiGo = new GameObject("--- UI ---");

            var doc = uiGo.GetComponent<UIDocument>() ?? uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            var readyUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/ReadyRoomScreen.uxml");
            if (readyUxml != null) doc.visualTreeAsset = readyUxml;
            EditorUtility.SetDirty(doc);

            EnsureComp<UIModalStack>(uiGo);
            var modal = EnsureComp<ModalPresenter>(uiGo);
            modal.uiDocument = doc;
            EditorUtility.SetDirty(modal);

            var settings = EnsureComp<SettingsPresenter>(uiGo);
            settings.uiDocument = doc;
            EditorUtility.SetDirty(settings);

            var ready = EnsureComp<ReadyRoomPresenter>(uiGo);
            ready.uiDocument = doc;
            EditorUtility.SetDirty(ready);

            var toast = EnsureComp<ToastPresenter>(uiGo);
            toast.uiDocument = doc;
            EditorUtility.SetDirty(toast);

            EditorUtility.SetDirty(uiGo);
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        private static void ConfigureGameScene(PanelSettings panelSettings)
        {
            var mainUIGo = GameObject.Find("MainGameUI");
            if (mainUIGo == null) return;

            var doc = mainUIGo.GetComponent<UIDocument>() ?? mainUIGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            var hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/MainGameHUD.uxml");
            if (hudUxml != null) doc.visualTreeAsset = hudUxml;
            EditorUtility.SetDirty(doc);

            EnsureComp<UIModalStack>(mainUIGo);
            var modal = EnsureComp<ModalPresenter>(mainUIGo);
            modal.uiDocument = doc;
            EditorUtility.SetDirty(modal);

            var settings = EnsureComp<SettingsPresenter>(mainUIGo);
            settings.uiDocument = doc;
            EditorUtility.SetDirty(settings);

            var hud = EnsureComp<HudPresenter>(mainUIGo);
            hud.uiDocument = doc;
            EditorUtility.SetDirty(hud);

            var score = EnsureComp<ScoreboardPresenter>(mainUIGo);
            score.uiDocument = doc;
            EditorUtility.SetDirty(score);

            var storage = EnsureComp<StoragePresenter>(mainUIGo);
            storage.uiDocument = doc;
            EditorUtility.SetDirty(storage);

            var staging = EnsureComp<StagingPresenter>(mainUIGo);
            staging.uiDocument = doc;
            EditorUtility.SetDirty(staging);

            var toast = EnsureComp<ToastPresenter>(mainUIGo);
            toast.uiDocument = doc;
            EditorUtility.SetDirty(toast);

            EnsureComp<MinimapPresenter>(mainUIGo);
            EnsureComp<OrderBoardPresenter>(mainUIGo);

            EditorUtility.SetDirty(mainUIGo);
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        private static T EnsureComp<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static void RemoveComp<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp != null) UnityEngine.Object.DestroyImmediate(comp);
        }
    }
}

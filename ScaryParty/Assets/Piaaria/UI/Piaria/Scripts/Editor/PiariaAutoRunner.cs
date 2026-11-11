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

        [MenuItem("Tools/Scary Party/Uso Comum/UI/Abrir LobbyScene", false, 185)]
        public static void OpenLobbyScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Piaaria/Scenes/LobbyScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            AutoConfigureActiveScene();
        }

        [MenuItem("Tools/Scary Party/Uso Comum/UI/Configurar Cena Atual", false, 186)]
        public static void ForceConfigureCurrentScene()
        {
            AutoConfigureActiveScene();
            Debug.Log("[PiariaAutoRunner] Cena atual configurada com sucesso!");
        }

        [MenuItem("Tools/Scary Party/Setup e Uso Único/UI (P.I.A.R.I.A)/Executar Trigger de AutoRunner", false, 288)]
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
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Piaaria/Scenes/LobbyScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            AutoConfigureActiveScene();

            File.WriteAllText(ResultFile, $"SUCCESS: Installed at {DateTime.Now}\n");
            Debug.Log("[PiariaAutoRunner] Execução finalizada com sucesso!");
        }

        public static void AutoConfigureActiveScene()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.name)) return;

            PlayerPreviewPrefabGenerator.EnsurePreviewPrefabExists();

            string sceneName = activeScene.name;
            var catalog = AssetDatabase.LoadAssetAtPath<PiariaUIAssetCatalog>("Assets/Piaaria/UI/Piaria/Config/PiariaUIAssetCatalog.asset")
                          ?? AssetDatabase.LoadAssetAtPath<PiariaUIAssetCatalog>("Assets/Resources/PiariaUIAssetCatalog.asset");
            var panelSettings = catalog?.panelSettings ?? AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Piaaria/UI/PanelSettings.asset");
            if (panelSettings != null)
            {
                EnsurePanelSettingsConfigured(panelSettings);
            }

            if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 &&
                sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) < 0)
            {
                ConfigureLobbyScene(panelSettings);
            }
            else if (sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ConfigureReadyScene(panelSettings);
            }
            else if (sceneName.IndexOf("Demo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ConfigureDemoScene(panelSettings);
            }
            else if (sceneName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0 || sceneName.IndexOf("Sample", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ConfigureGameScene(panelSettings);
            }

            // Universal fallback para qualquer cena: conecta UIDocument e LobbyManager em todos os presenters
            var sceneDoc = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            var sceneLobbyMgr = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
            if (sceneDoc != null)
            {
                var allPresenters = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in allPresenters)
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    if (type.Namespace != null && type.Namespace.StartsWith("ScaryParty.UI.Piaria.Presenters"))
                    {
                        var docProp = type.GetField("uiDocument");
                        if (docProp != null && docProp.GetValue(mb) == null)
                        {
                            PiariaPresenterUtils.SetSerializedPropertyRef(mb, "uiDocument", sceneDoc);
                        }

                        if (mb is LobbyPresenter lp && lp.lobbyManager == null && sceneLobbyMgr != null)
                        {
                            PiariaPresenterUtils.SetSerializedPropertyRef(lp, "lobbyManager", sceneLobbyMgr);
                        }
                    }
                }
            }

            if (activeScene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        private static void ConfigureLobbyScene(PanelSettings panelSettings)
        {
            var uiGo = GameObject.Find("--- UI ---");
            if (uiGo == null) uiGo = new GameObject("--- UI ---");

            var doc = uiGo.GetComponent<UIDocument>() ?? uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            var containerUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/LobbySceneContainer.uxml");
            if (containerUxml != null) doc.visualTreeAsset = containerUxml;
            EditorUtility.SetDirty(doc);

            EnsureComp<UIScreenRouter>(uiGo);
            EnsureComp<UIFeedbackService>(uiGo);
            EnsureComp<UIModalStack>(uiGo);

            var modal = EnsureComp<ModalPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(modal, "uiDocument", doc);

            var settings = EnsureComp<SettingsPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(settings, "uiDocument", doc);

            var menu = EnsureComp<MainMenuPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(menu, "uiDocument", doc);

            var lobby = EnsureComp<LobbyPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(lobby, "uiDocument", doc);
            var lm = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
            if (lm != null) PiariaPresenterUtils.SetSerializedPropertyRef(lobby, "lobbyManager", lm);

            var toast = EnsureComp<ToastPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(toast, "uiDocument", doc);

            EnsureComp<CharacterPreviewRig>(uiGo);
            EditorUtility.SetDirty(uiGo);

            // Remove presenters de outras cenas que possam ter sido arrastados por engano para o objeto raiz do Lobby
            RemoveComp<ReadyRoomPresenter>(uiGo);
            RemoveComp<HudPresenter>(uiGo);
            RemoveComp<ScoreboardPresenter>(uiGo);
            RemoveComp<StoragePresenter>(uiGo);
            RemoveComp<StagingPresenter>(uiGo);

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        private static void ConfigureDemoScene(PanelSettings panelSettings)
        {
            var uiGo = GameObject.Find("--- PIARIA UI DEMO ---") ?? GameObject.Find("--- UI ---");
            if (uiGo == null) uiGo = new GameObject("--- PIARIA UI DEMO ---");

            var doc = uiGo.GetComponent<UIDocument>() ?? uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            var demoUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/LobbySceneContainer.uxml")
                           ?? AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/MainMenuScreen.uxml");
            if (demoUxml != null) doc.visualTreeAsset = demoUxml;
            EditorUtility.SetDirty(doc);

            EnsureComp<UIScreenRouter>(uiGo);
            EnsureComp<UIFeedbackService>(uiGo);
            EnsureComp<UIModalStack>(uiGo);

            var mPres = EnsureComp<ModalPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(mPres, "uiDocument", doc);
            var sPres = EnsureComp<SettingsPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(sPres, "uiDocument", doc);
            var mmPres = EnsureComp<MainMenuPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(mmPres, "uiDocument", doc);
            var lobbyPres = EnsureComp<LobbyPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(lobbyPres, "uiDocument", doc);
            var lMgr = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
            if (lMgr != null) PiariaPresenterUtils.SetSerializedPropertyRef(lobbyPres, "lobbyManager", lMgr);

            var rrPres = EnsureComp<ReadyRoomPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(rrPres, "uiDocument", doc);
            var hPres = EnsureComp<HudPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(hPres, "uiDocument", doc);
            var scPres = EnsureComp<ScoreboardPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(scPres, "uiDocument", doc);
            var stgPres = EnsureComp<StoragePresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(stgPres, "uiDocument", doc);
            var staPres = EnsureComp<StagingPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(staPres, "uiDocument", doc);
            var tPres = EnsureComp<ToastPresenter>(uiGo); PiariaPresenterUtils.SetSerializedPropertyRef(tPres, "uiDocument", doc);

            EnsureComp<CharacterPreviewRig>(uiGo);
            EnsureComp<PiariaDemoSceneController>(uiGo);

            EditorUtility.SetDirty(uiGo);
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
            var readyUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/ReadyRoomScreen.uxml");
            if (readyUxml != null) doc.visualTreeAsset = readyUxml;
            EditorUtility.SetDirty(doc);

            EnsureComp<UIScreenRouter>(uiGo);
            EnsureComp<UIFeedbackService>(uiGo);
            EnsureComp<UIModalStack>(uiGo);

            var modal = EnsureComp<ModalPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(modal, "uiDocument", doc);

            var settings = EnsureComp<SettingsPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(settings, "uiDocument", doc);

            var ready = EnsureComp<ReadyRoomPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(ready, "uiDocument", doc);

            var toast = EnsureComp<ToastPresenter>(uiGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(toast, "uiDocument", doc);

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
            var hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/MainGameHUD.uxml");
            if (hudUxml != null) doc.visualTreeAsset = hudUxml;
            EditorUtility.SetDirty(doc);

            EnsureComp<UIScreenRouter>(mainUIGo);
            EnsureComp<UIFeedbackService>(mainUIGo);
            EnsureComp<UIModalStack>(mainUIGo);

            var modal = EnsureComp<ModalPresenter>(mainUIGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(modal, "uiDocument", doc);

            var settings = EnsureComp<SettingsPresenter>(mainUIGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(settings, "uiDocument", doc);

            var hud = EnsureComp<HudPresenter>(mainUIGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(hud, "uiDocument", doc);

            var score = EnsureComp<ScoreboardPresenter>(mainUIGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(score, "uiDocument", doc);

            var storage = EnsureComp<StoragePresenter>(mainUIGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(storage, "uiDocument", doc);

            var staging = EnsureComp<StagingPresenter>(mainUIGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(staging, "uiDocument", doc);

            var toast = EnsureComp<ToastPresenter>(mainUIGo);
            PiariaPresenterUtils.SetSerializedPropertyRef(toast, "uiDocument", doc);

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

        public static void EnsurePanelSettingsConfigured(PanelSettings panelSettings)
        {
            if (panelSettings == null) return;
            var themePath = AssetDatabase.GUIDToAssetPath("d69ba10a5cfd9d748bd38f539b6646d9");
            if (string.IsNullOrEmpty(themePath))
                themePath = "Packages/com.unity.ui/PackageResources/StyleSheets/UnityDefaultRuntimeTheme.tss";
            var theme = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(themePath);

            var so = new SerializedObject(panelSettings);
            bool changed = false;

            var themeProp = so.FindProperty("themeUss") ?? so.FindProperty("m_ThemeUss");
            if (themeProp != null && theme != null && themeProp.objectReferenceValue == null)
            {
                themeProp.objectReferenceValue = theme;
                changed = true;
            }

            var scaleModeProp = so.FindProperty("m_ScaleMode");
            if (scaleModeProp != null && scaleModeProp.intValue != 1)
            {
                scaleModeProp.intValue = 1; // ScaleWithScreenSize
                changed = true;
            }

            var refResProp = so.FindProperty("m_ReferenceResolution");
            if (refResProp != null)
            {
                refResProp.vector2IntValue = new Vector2Int(1920, 1080);
                changed = true;
            }

            var matchProp = so.FindProperty("m_Match");
            if (matchProp != null)
            {
                matchProp.floatValue = 0.5f;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(panelSettings);
                AssetDatabase.SaveAssets();
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ScaryParty.UI.Piaria.Core
{
    public static class PiariaPresenterUtils
    {
        public static VisualTreeAsset ResolveTemplate(Func<PiariaUIAssetCatalog, VisualTreeAsset> catalogSelector, string editorFallbackPath = null)
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            VisualTreeAsset asset = (catalog != null && catalogSelector != null) ? catalogSelector(catalog) : null;
#if UNITY_EDITOR
            if (asset == null && !string.IsNullOrEmpty(editorFallbackPath))
            {
                asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(editorFallbackPath);
            }
#endif
            return asset;
        }

        public static void ConfigureHierarchyPicking(VisualElement root)
        {
            if (root == null) return;
            // Configura todos os TemplateContainers da hierarquia para ignorar raycasts (não bloquear elementos abaixo)
            var containers = root.Query<TemplateContainer>().ToList();
            foreach (var tc in containers)
            {
                tc.pickingMode = PickingMode.Ignore;
            }

            var fullscreens = root.Query(className: "fullscreen-root").ToList();
            foreach (var fs in fullscreens)
            {
                if (fs is not Button && fs is not TextField)
                {
                    fs.pickingMode = PickingMode.Ignore;
                }
            }

            var passthroughs = root.Query(className: "hud-pass-through").ToList();
            foreach (var pt in passthroughs)
            {
                pt.pickingMode = PickingMode.Ignore;
            }
        }

        public static void RegisterResponsiveLayout(VisualElement root)
        {
            if (root == null) return;

            void ApplyBreakpoints(float width, float height)
            {
                if (width <= 1f || height <= 1f) return;

                bool isCompact = width < 1450f || height < 840f;
                bool isWide = width >= 2250f;

                root.EnableInClassList("layout-compact", isCompact);
                root.EnableInClassList("layout-wide", !isCompact && isWide);
                root.EnableInClassList("layout-normal", !isCompact && !isWide);
            }

            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                ApplyBreakpoints(evt.newRect.width, evt.newRect.height);
            });

            if (root.resolvedStyle.width > 1f && root.resolvedStyle.height > 1f)
            {
                ApplyBreakpoints(root.resolvedStyle.width, root.resolvedStyle.height);
            }
            else
            {
                ApplyBreakpoints(Screen.width, Screen.height);
            }
        }

        public static void ResolveUIDocument(MonoBehaviour presenter, ref UIDocument uiDoc, string fallbackUxmlPath = null)
        {
            if (uiDoc == null)
            {
                uiDoc = presenter.GetComponent<UIDocument>() 
                     ?? presenter.GetComponentInParent<UIDocument>()
                     ?? UnityEngine.Object.FindFirstObjectByType<UIDocument>();

                if (uiDoc == null)
                {
                    var uiGo = GameObject.Find("--- UI ---") ?? GameObject.Find("MainGameUI") ?? GameObject.Find("--- PIARIA UI DEMO ---");
                    if (uiGo != null)
                    {
                        uiDoc = uiGo.GetComponent<UIDocument>();
                    }
                }
            }

            if (Application.isPlaying)
            {
                if (uiDoc == null)
                {
                    uiDoc = presenter.gameObject.AddComponent<UIDocument>();
                    var catalog = PiariaUIAssetCatalog.Instance;
                    if (catalog != null && catalog.panelSettings != null)
                    {
                        uiDoc.panelSettings = catalog.panelSettings;
                    }
                    else
                    {
                        var foundPs = Resources.FindObjectsOfTypeAll<PanelSettings>();
                        if (foundPs != null && foundPs.Length > 0)
                            uiDoc.panelSettings = foundPs[0];
                    }
                }

                if (uiDoc != null && uiDoc.visualTreeAsset == null && !string.IsNullOrEmpty(fallbackUxmlPath))
                {
                    var catalog = PiariaUIAssetCatalog.Instance;
                    if (catalog != null)
                    {
                        if (fallbackUxmlPath.Contains("LobbySceneContainer") && catalog.templateLobbySceneContainer != null)
                            uiDoc.visualTreeAsset = catalog.templateLobbySceneContainer;
                        else if (fallbackUxmlPath.Contains("Lobby") && catalog.templateLobby != null)
                            uiDoc.visualTreeAsset = catalog.templateLobby;
                        else if (fallbackUxmlPath.Contains("MainMenu") && catalog.templateMainMenu != null)
                            uiDoc.visualTreeAsset = catalog.templateMainMenu;
                        else if (fallbackUxmlPath.Contains("ReadyRoom") && catalog.templateReadyRoom != null)
                            uiDoc.visualTreeAsset = catalog.templateReadyRoom;
                        else if (fallbackUxmlPath.Contains("HUD") && catalog.templateMainGameHud != null)
                            uiDoc.visualTreeAsset = catalog.templateMainGameHud;
                    }

#if UNITY_EDITOR
                    if (uiDoc.visualTreeAsset == null)
                        uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fallbackUxmlPath);
#endif
                }
            }
#if UNITY_EDITOR
            else
            {
                if (presenter != null)
                {
                    var currentDoc = uiDoc;
                    EditorApplication.delayCall += () =>
                    {
                        if (presenter == null || presenter.gameObject == null) return;
                        var doc = currentDoc ?? presenter.GetComponent<UIDocument>() ?? presenter.GetComponentInParent<UIDocument>() ?? UnityEngine.Object.FindFirstObjectByType<UIDocument>();
                        if (doc == null)
                        {
                            var uiRoot = GameObject.Find("--- UI ---") ?? GameObject.Find("MainGameUI") ?? GameObject.Find("--- PIARIA UI DEMO ---");
                            if (uiRoot != null) doc = uiRoot.GetComponent<UIDocument>();
                        }

                        if (doc == null)
                        {
                            doc = presenter.gameObject.AddComponent<UIDocument>();
                            var catalog = PiariaUIAssetCatalog.Instance;
                            if (catalog != null && catalog.panelSettings != null)
                                doc.panelSettings = catalog.panelSettings;
                            else
                                doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Piaaria/UI/PanelSettings.asset");

                            if (!string.IsNullOrEmpty(fallbackUxmlPath))
                                doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fallbackUxmlPath);

                            EditorUtility.SetDirty(doc);
                        }

                        var so = new SerializedObject(presenter);
                        so.Update();
                        var prop = so.FindProperty("uiDocument");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = doc;
                            so.ApplyModifiedProperties();
                        }

                        var field = presenter.GetType().GetField("uiDocument");
                        if (field != null)
                        {
                            field.SetValue(presenter, doc);
                        }

                        EditorUtility.SetDirty(presenter);
                        if (!string.IsNullOrEmpty(presenter.gameObject.scene.path))
                        {
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
                        }
                    };
                }
            }
#endif
        }

        public static void ResolveLobbyManager(MonoBehaviour presenter, ref LobbyManager lobbyMgr)
        {
            if (lobbyMgr == null)
            {
                lobbyMgr = presenter.GetComponent<LobbyManager>()
                        ?? presenter.GetComponentInParent<LobbyManager>()
                        ?? UnityEngine.Object.FindFirstObjectByType<LobbyManager>();

                if (lobbyMgr == null)
                {
                    var lobbyGo = GameObject.Find("--- LOBBY ---") ?? GameObject.Find("LobbyManager");
                    if (lobbyGo != null)
                    {
                        lobbyMgr = lobbyGo.GetComponent<LobbyManager>();
                    }
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && presenter != null)
            {
                var currentMgr = lobbyMgr;
                EditorApplication.delayCall += () =>
                {
                    if (presenter == null) return;
                    var targetMgr = currentMgr ?? presenter.GetComponent<LobbyManager>() ?? presenter.GetComponentInParent<LobbyManager>() ?? UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
                    if (targetMgr != null)
                    {
                        var so = new SerializedObject(presenter);
                        so.Update();
                        var prop = so.FindProperty("lobbyManager");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = targetMgr;
                            so.ApplyModifiedProperties();
                        }

                        var field = presenter.GetType().GetField("lobbyManager");
                        if (field != null)
                        {
                            field.SetValue(presenter, targetMgr);
                        }

                        EditorUtility.SetDirty(presenter);
                        if (!string.IsNullOrEmpty(presenter.gameObject.scene.path))
                        {
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
                        }
                    }
                };
            }
#endif
        }

#if UNITY_EDITOR
        public static void SetSerializedPropertyRef(MonoBehaviour target, string propertyName, UnityEngine.Object refValue)
        {
            if (target == null || refValue == null) return;
            var so = new SerializedObject(target);
            so.Update();
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = refValue;
                so.ApplyModifiedProperties();
            }
            var field = target.GetType().GetField(propertyName);
            if (field != null)
            {
                field.SetValue(target, refValue);
            }
            EditorUtility.SetDirty(target);
        }
#endif
    }
}

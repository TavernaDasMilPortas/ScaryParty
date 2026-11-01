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
        public static void ResolveUIDocument(MonoBehaviour presenter, ref UIDocument uiDoc, string fallbackUxmlPath = null)
        {
            if (uiDoc != null) return;

            uiDoc = presenter.GetComponent<UIDocument>() ?? UnityEngine.Object.FindFirstObjectByType<UIDocument>();

            if (uiDoc == null)
            {
                var uiGo = GameObject.Find("--- UI ---") ?? GameObject.Find("MainGameUI") ?? GameObject.Find("--- PIARIA UI DEMO ---");
                if (uiGo != null)
                {
                    uiDoc = uiGo.GetComponent<UIDocument>();
                }
            }

            if (Application.isPlaying)
            {
                if (uiDoc == null)
                {
                    uiDoc = presenter.gameObject.AddComponent<UIDocument>();
                    var catalog = PiariaUIAssetCatalog.Instance;
                    if (catalog != null && catalog.panelSettings != null)
                        uiDoc.panelSettings = catalog.panelSettings;
                    else
                        uiDoc.panelSettings = Resources.Load<PanelSettings>("PanelSettings");
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
                if (uiDoc != null)
                {
                    EditorUtility.SetDirty(presenter);
                }
                else
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (presenter == null || presenter.gameObject == null) return;
                        var doc = presenter.GetComponent<UIDocument>() ?? UnityEngine.Object.FindFirstObjectByType<UIDocument>();
                        if (doc == null)
                        {
                            var uiRoot = GameObject.Find("--- UI ---") ?? GameObject.Find("MainGameUI");
                            if (uiRoot != null) doc = uiRoot.GetComponent<UIDocument>();
                        }

                        if (doc == null)
                        {
                            doc = presenter.gameObject.AddComponent<UIDocument>();
                            var catalog = PiariaUIAssetCatalog.Instance;
                            if (catalog != null && catalog.panelSettings != null)
                                doc.panelSettings = catalog.panelSettings;
                            else
                                doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");

                            if (!string.IsNullOrEmpty(fallbackUxmlPath))
                                doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fallbackUxmlPath);

                            EditorUtility.SetDirty(doc);
                        }

                        var prop = presenter.GetType().GetField("uiDocument");
                        if (prop != null)
                        {
                            prop.SetValue(presenter, doc);
                            EditorUtility.SetDirty(presenter);
                        }
                    };
                }
            }
#endif
        }

        public static void ResolveLobbyManager(MonoBehaviour presenter, ref LobbyManager lobbyMgr)
        {
            if (lobbyMgr != null) return;

            lobbyMgr = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
            if (lobbyMgr == null)
            {
                var lobbyGo = GameObject.Find("--- LOBBY ---") ?? GameObject.Find("LobbyManager");
                if (lobbyGo != null)
                {
                    lobbyMgr = lobbyGo.GetComponent<LobbyManager>();
                }
            }

#if UNITY_EDITOR
            if (lobbyMgr != null && !Application.isPlaying)
            {
                EditorUtility.SetDirty(presenter);
            }
#endif
        }
    }
}

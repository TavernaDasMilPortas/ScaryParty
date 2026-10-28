#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace ScaryParty.EditorScripts
{
    /// <summary>
    /// Builder e ferramenta de diagnóstico para remover sistemas legados do jogo:
    /// 1. Remove o sistema antigo de teste que gerava pizzas prontas na bancada (PizzariaManager e PizzaItem).
    /// 2. Desativa os controles de combate e troca de arma via scroll do mouse (WeaponInputHandler / WeaponController).
    /// 3. Limpa referências legadas de NetworkPrefabs (PizzaBoxPlaceholder).
    /// 
    /// Acessível via: Tools > Scary Party > Legacy Cleanup Builder
    /// </summary>
    public class LegacyCleanupBuilder : EditorWindow
    {
        private Vector2 _scrollPos;
        private string _lastReport = "Clique em 'Escanear Cena & Prefabs' para diagnosticar elementos legados.";
        private MessageType _reportType = MessageType.Info;

        private const string PlayerPrefabPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string LegacyPizzaBoxGuid = "4e439716a6912d04bba4581994280b23";

        [MenuItem("Tools/Scary Party/Legacy Cleanup Builder", false, 15)]
        public static void ShowWindow()
        {
            var win = GetWindow<LegacyCleanupBuilder>("Legacy Cleanup Builder");
            win.minSize = new Vector2(500, 600);
            win.Show();
        }

        [MenuItem("Tools/Scary Party/Clean Legacy Systems (1-Click)", false, 16)]
        public static void RunQuickCleanup()
        {
            string summary = RunAllCleanup(msg => Debug.Log($"[LegacyCleanup] {msg}"));
            EditorUtility.DisplayDialog("Limpeza de Legados Concluída", summary, "OK");
        }

        public static void ApplyFromCommandLine()
        {
            try
            {
                Debug.Log("[LegacyCleanup-CLI] Iniciando limpeza de legados em batchmode...");
                string summary = RunAllCleanup(msg => Debug.Log($"[LegacyCleanup-CLI] {msg}"));
                Debug.Log($"[LegacyCleanup-CLI] Sucesso:\n{summary}");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LegacyCleanup-CLI-ERROR] {ex}");
                EditorApplication.Exit(1);
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("🍕 Scary Party — Legacy Cleanup Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Esta ferramenta analisa a cena ativa (ex: GameScene) e os prefabs do projeto para:\n" +
                "1. Remover o sistema legado de testes (PizzariaManager que gerava pizzas prontas).\n" +
                "2. Desativar a rolagem/scroll e controles das armas por hora.\n" +
                "3. Limpar NetworkPrefabs antigos.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Seção de Diagnóstico da Cena
            DrawDiagnosticsSection();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Ações de Limpeza", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(1f, 0.5f, 0.2f);
            if (GUILayout.Button("1. 🧹 Remover Sistema Antigo de Pizzas (PizzariaManager)", GUILayout.Height(35)))
            {
                int removed = CleanLegacyPizzaSpawners(msg => Debug.Log($"[LegacyCleanup] {msg}"));
                _lastReport = $"Removidos {removed} componentes/objetos legados de pizza da cena.";
                _reportType = MessageType.Info;
            }

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("2. 🚫 Desativar Scroll e Controles de Armas (PlayerArmature)", GUILayout.Height(35)))
            {
                bool ok = DisableWeaponControlsOnPrefabsAndScene(msg => Debug.Log($"[LegacyCleanup] {msg}"));
                _lastReport = ok ? "Controles de scroll e combate das armas desativados com sucesso." : "Falha ao desativar armas no PlayerArmature.";
                _reportType = ok ? MessageType.Info : MessageType.Warning;
            }

            GUI.backgroundColor = new Color(0.7f, 0.5f, 0.9f);
            if (GUILayout.Button("3. 📦 Limpar PizzaBoxPlaceholder de DefaultNetworkPrefabs", GUILayout.Height(35)))
            {
                bool cleaned = CleanLegacyNetworkPrefabs(msg => Debug.Log($"[LegacyCleanup] {msg}"));
                _lastReport = cleaned ? "DefaultNetworkPrefabs limpo de referências legadas." : "Nenhuma referência legada encontrada em DefaultNetworkPrefabs.";
                _reportType = MessageType.Info;
            }

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("🚀 EXECUTAR LIMPEZA COMPLETA DE LEGADOS (1-Click)", GUILayout.Height(45)))
            {
                _lastReport = RunAllCleanup(msg => Debug.Log($"[LegacyCleanup] {msg}"));
                _reportType = MessageType.Info;
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Relatório / Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_lastReport, _reportType);

            EditorGUILayout.EndScrollView();
        }

        private void DrawDiagnosticsSection()
        {
            EditorGUILayout.LabelField("Diagnóstico da Cena Ativa (" + SceneManager.GetActiveScene().name + ")", EditorStyles.boldLabel);

            var pManagers = FindObjectsByType<PizzariaManager>(FindObjectsSortMode.None);
            bool hasLegacyPizzaManager = pManagers != null && pManagers.Length > 0;
            DrawStatusRow("PizzariaManager (Legado)", hasLegacyPizzaManager ? "⚠️ Presente na cena (Requer Remoção)" : "✅ Limpo (Não presente)", !hasLegacyPizzaManager);

            var pItems = FindObjectsByType<PizzaItem>(FindObjectsSortMode.None);
            bool hasLegacyPizzaItems = pItems != null && pItems.Length > 0;
            DrawStatusRow("PizzaItem na cena (Legado)", hasLegacyPizzaItems ? $"⚠️ {pItems.Length} instâncias (Requer Remoção)" : "✅ Limpo", !hasLegacyPizzaItems);

            // Verifica status do PlayerArmature
            bool weaponScrollDisabled = CheckWeaponScrollDisabledInPrefab();
            DrawStatusRow("Armas: Scroll Desativado", weaponScrollDisabled ? "✅ Desativado (Correto)" : "⚠️ Ativo (Requer Desativação)", weaponScrollDisabled);

            // Verifica NetworkPrefabs
            bool networkPrefabsClean = CheckNetworkPrefabsClean();
            DrawStatusRow("NetworkPrefabs: Sem Pizza Placeholder", networkPrefabsClean ? "✅ Limpo" : "⚠️ Contém PizzaBoxPlaceholder", networkPrefabsClean);
        }

        private void DrawStatusRow(string label, string status, bool ok)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(240));
            GUI.color = ok ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(status);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        public static string RunAllCleanup(Action<string> log)
        {
            int totalRemoved = 0;
            log?.Invoke("Iniciando limpeza completa de legados...");

            // 1. Limpa PizzariaManager e PizzaItems da cena
            int removedPizza = CleanLegacyPizzaSpawners(log);
            totalRemoved += removedPizza;

            // 2. Desativa armas no prefab e na cena
            bool weaponOk = DisableWeaponControlsOnPrefabsAndScene(log);

            // 3. Limpa NetworkPrefabs
            bool netClean = CleanLegacyNetworkPrefabs(log);

            // Salva a cena ativa
            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            AssetDatabase.SaveAssets();

            string report = $"Limpeza Concluída:\n" +
                            $"- Objetos legados de pizza removidos da cena: {removedPizza}\n" +
                            $"- Controles de armas/scroll desativados no PlayerArmature: {(weaponOk ? "SIM" : "NÃO")}\n" +
                            $"- NetworkPrefabs legado limpo: {(netClean ? "SIM" : "NÃO OU NÃO NECESSÁRIO")}\n" +
                            $"- Cena '{activeScene.name}' salva com sucesso.";

            log?.Invoke(report);
            return report;
        }

        public static int CleanLegacyPizzaSpawners(Action<string> log)
        {
            int removedCount = 0;

            // 1. Localiza PizzariaManager na cena
            var pManagers = UnityEngine.Object.FindObjectsByType<PizzariaManager>(FindObjectsSortMode.None);
            foreach (var mgr in pManagers)
            {
                if (mgr != null)
                {
                    string objName = mgr.gameObject.name;
                    log?.Invoke($"Removendo componente/GameObject legado: {objName}");
                    Undo.DestroyObjectImmediate(mgr.gameObject);
                    removedCount++;
                }
            }

            // 2. Localiza objetos com nome "PizzariaManager" mesmo sem componente
            GameObject namedObj = GameObject.Find("PizzariaManager");
            if (namedObj != null)
            {
                log?.Invoke($"Removendo GameObject chamado PizzariaManager");
                Undo.DestroyObjectImmediate(namedObj);
                removedCount++;
            }

            // 3. Localiza PizzaItem na cena
            var pItems = UnityEngine.Object.FindObjectsByType<PizzaItem>(FindObjectsSortMode.None);
            foreach (var item in pItems)
            {
                if (item != null)
                {
                    log?.Invoke($"Removendo PizzaItem legado na cena: {item.gameObject.name}");
                    Undo.DestroyObjectImmediate(item.gameObject);
                    removedCount++;
                }
            }

            // 4. Localiza objetos com nome PizzaBoxPlaceholder
            var allRoot = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in allRoot)
            {
                CleanPizzaBoxRecursive(root.transform, ref removedCount, log);
            }

            return removedCount;
        }

        private static void CleanPizzaBoxRecursive(Transform current, ref int removedCount, Action<string> log)
        {
            if (current == null) return;

            for (int i = current.childCount - 1; i >= 0; i--)
            {
                Transform child = current.GetChild(i);
                if (child.name.Contains("PizzaBoxPlaceholder"))
                {
                    log?.Invoke($"Removendo placeholder de caixa de pizza: {child.name}");
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedCount++;
                }
                else
                {
                    CleanPizzaBoxRecursive(child, ref removedCount, log);
                }
            }
        }

        public static bool DisableWeaponControlsOnPrefabsAndScene(Action<string> log)
        {
            bool success = true;

            // 1. Atualiza no PlayerArmature.prefab
            if (System.IO.File.Exists(PlayerPrefabPath))
            {
                GameObject prefabObj = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
                if (prefabObj != null)
                {
                    var inputHandler = prefabObj.GetComponent<WeaponInputHandler>();
                    if (inputHandler != null)
                    {
                        inputHandler.enableWeaponControls = false;
                        inputHandler.enableWeaponScroll = false;
                        log?.Invoke("PlayerArmature.prefab: WeaponInputHandler configurado com enableWeaponControls=false e enableWeaponScroll=false.");
                    }

                    var weaponCtrl = prefabObj.GetComponent<WeaponController>();
                    if (weaponCtrl != null)
                    {
                        weaponCtrl.SetWeaponMode(WeaponMode.Unarmed);
                        log?.Invoke("PlayerArmature.prefab: WeaponController ajustado para WeaponMode.Unarmed.");
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefabObj, PlayerPrefabPath);
                    PrefabUtility.UnloadPrefabContents(prefabObj);
                }
            }
            else
            {
                log?.Invoke($"Aviso: Prefab {PlayerPrefabPath} não encontrado.");
                success = false;
            }

            // 2. Atualiza instâncias na cena ativa se houver
            var handlersInScene = UnityEngine.Object.FindObjectsByType<WeaponInputHandler>(FindObjectsSortMode.None);
            foreach (var handler in handlersInScene)
            {
                if (handler != null)
                {
                    handler.enableWeaponControls = false;
                    handler.enableWeaponScroll = false;
                    EditorUtility.SetDirty(handler);
                    log?.Invoke($"Instância na cena '{handler.gameObject.name}': armas desativadas.");
                }
            }

            return success;
        }

        public static bool CleanLegacyNetworkPrefabs(Action<string> log)
        {
            NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (prefabList == null) return false;

            bool modified = false;

            // Remove using the public API if match found
            var toRemove = new List<NetworkPrefab>();
            foreach (var entry in prefabList.PrefabList)
            {
                if (entry.Prefab != null)
                {
                    string path = AssetDatabase.GetAssetPath(entry.Prefab);
                    if (path.Contains("PizzaBoxPlaceholder"))
                    {
                        toRemove.Add(entry);
                        log?.Invoke($"Marcado para remoção de DefaultNetworkPrefabs: {path}");
                    }
                }
            }

            foreach (var item in toRemove)
            {
                prefabList.Remove(item);
                modified = true;
            }

            // Also clean via SerializedObject in case of missing/null prefabs with matching GUID
            SerializedObject so = new SerializedObject(prefabList);
            SerializedProperty listProp = so.FindProperty("List");
            if (listProp != null && listProp.isArray)
            {
                for (int i = listProp.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty elem = listProp.GetArrayElementAtIndex(i);
                    SerializedProperty prefabProp = elem.FindPropertyRelative("Prefab");
                    if (prefabProp != null)
                    {
                        if (prefabProp.objectReferenceValue == null)
                        {
                            // Null/dangling reference
                            listProp.DeleteArrayElementAtIndex(i);
                            modified = true;
                        }
                        else
                        {
                            string path = AssetDatabase.GetAssetPath(prefabProp.objectReferenceValue);
                            if (path.Contains("PizzaBoxPlaceholder"))
                            {
                                listProp.DeleteArrayElementAtIndex(i);
                                modified = true;
                            }
                        }
                    }
                }
                if (modified)
                {
                    so.ApplyModifiedProperties();
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(prefabList);
                AssetDatabase.SaveAssets();
                log?.Invoke("DefaultNetworkPrefabs salvo com sucesso.");
            }

            return modified;
        }

        private static bool CheckWeaponScrollDisabledInPrefab()
        {
            if (!System.IO.File.Exists(PlayerPrefabPath)) return false;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null) return false;

            var handler = prefab.GetComponent<WeaponInputHandler>();
            if (handler == null) return true; // Sem handler, sem scroll

            return !handler.enableWeaponControls && !handler.enableWeaponScroll;
        }

        private static bool CheckNetworkPrefabsClean()
        {
            NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (prefabList == null) return true;

            foreach (var entry in prefabList.PrefabList)
            {
                if (entry.Prefab != null)
                {
                    string path = AssetDatabase.GetAssetPath(entry.Prefab);
                    if (path.Contains("PizzaBoxPlaceholder")) return false;
                }
            }

            return true;
        }
    }
}
#endif

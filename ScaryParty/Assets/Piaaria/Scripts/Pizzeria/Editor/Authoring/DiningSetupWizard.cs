using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Wizard e serviço não-destrutivo de configuração do Salão, Filas e Mesas (Fase 7):
    /// - Preserva integralmente PizzeriaAuthoringBindings, estações existentes e objetos 3D vinculados;
    /// - Configura/atualiza de forma idempotente as 2 filas (CustomerCounterStation 0 e 1),
    ///   as mesas do salão (DiningTableStation), CustomerSpawner, CustomerPatienceHudPresenter e DemandDirector
    ///   nos prefabs da pizzaria (PizzeriaModular_Manual.prefab e PizzeriaPrefab.prefab).
    /// </summary>
    [InitializeOnLoad]
    public class DiningSetupWizard : EditorWindow
    {
        public const string ManualPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
        public const string LegacyPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab";
        private const string SessionCheckKey = "ScaryParty_DiningSetupWizard_AutoChecked_v1";

        private Vector2 _scroll;
        private string _lastReport = string.Empty;

        static DiningSetupWizard()
        {
            EditorApplication.delayCall += AutoEnsurePrefabsIfMissingSilent;
        }

        [MenuItem("Tools/Scary Party/Pizzeria/Salão, Filas e Mesas (Dining Setup Wizard)", false, 15)]
        public static void ShowWindow()
        {
            var win = GetWindow<DiningSetupWizard>("Salão, Filas & Mesas");
            win.minSize = new Vector2(680f, 500f);
        }

        private static void AutoEnsurePrefabsIfMissingSilent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(SessionCheckKey, false)) return;
            SessionState.SetBool(SessionCheckKey, true);

            EnsureDiningOnPrefabAssetIfMissing(ManualPrefabPath);
            EnsureDiningOnPrefabAssetIfMissing(LegacyPrefabPath);
        }

        private static void EnsureDiningOnPrefabAssetIfMissing(string prefabPath)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null) return;

            var existingCounters = prefabAsset.GetComponentsInChildren<CustomerCounterStation>(true);
            var existingTables = prefabAsset.GetComponentsInChildren<DiningTableStation>(true);
            var existingSpawner = prefabAsset.GetComponent<CustomerSpawner>();
            var existingHud = prefabAsset.GetComponent<CustomerPatienceHudPresenter>();

            if (existingCounters != null && existingCounters.Length >= 2 &&
                existingTables != null && existingTables.Length >= 2 &&
                existingSpawner != null && existingHud != null)
            {
                return;
            }

            ApplyNonDestructiveDiningSetupToPrefab(prefabPath, out _);
        }

        public static bool ApplyNonDestructiveDiningSetupToPrefab(string prefabPath, out string report)
        {
            report = string.Empty;
            if (string.IsNullOrEmpty(prefabPath) || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                report = $"Prefab não encontrado em '{prefabPath}'.";
                return false;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                bool changed = ConfigureDiningOnHierarchy(root, out report);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                report = $"Erro ao configurar '{prefabPath}': {ex.Message}";
                Debug.LogError($"[DiningSetupWizard] {report}");
                return false;
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        /// <summary>
        /// Configura de forma não-destrutiva (preservando PizzeriaAuthoringBindings e todas as StationView existentes)
        /// os 2 pontos de fila de clientes (CustomerCounterStation), as mesas do salão (DiningTableStation),
        /// CustomerSpawner e CustomerPatienceHudPresenter.
        /// </summary>
        public static bool ConfigureDiningOnHierarchy(GameObject root, out string report)
        {
            bool changed = false;
            var notes = new List<string>();

            if (root == null)
            {
                report = "Raiz nula.";
                return false;
            }

            var pizzeriaRoot = root.GetComponent<PizzeriaRoot>();

            // 1. Garantir CustomerSpawner e CustomerPatienceHudPresenter na raiz
            var spawner = root.GetComponent<CustomerSpawner>();
            if (spawner == null)
            {
                spawner = root.AddComponent<CustomerSpawner>();
                changed = true;
                notes.Add("Adicionado CustomerSpawner na raiz.");
            }

            var charVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");
            if (charVisualPrefab != null && spawner.characterVisualPrefab != charVisualPrefab)
            {
                spawner.characterVisualPrefab = charVisualPrefab;
                changed = true;
            }

            var patienceHud = root.GetComponent<CustomerPatienceHudPresenter>();
            if (patienceHud == null)
            {
                patienceHud = root.AddComponent<CustomerPatienceHudPresenter>();
                changed = true;
                notes.Add("Adicionado CustomerPatienceHudPresenter na raiz.");
            }

            // 2. Garantir 2 CustomerCounterStation (Fila 1 = queueIndex 0, Fila 2 = queueIndex 1)
            // IMPORTANTE: Não alterar nenhuma CounterStation de cozinha existente!
            var counters = root.GetComponentsInChildren<CustomerCounterStation>(true);
            var byQueue = new Dictionary<int, CustomerCounterStation>();
            for (int i = 0; i < counters.Length; i++)
            {
                if (counters[i] != null)
                {
                    counters[i].EnsureQueueSlotAnchors(3);
                    byQueue[counters[i].queueIndex] = counters[i];
                }
            }

            Transform entrance = pizzeriaRoot != null && pizzeriaRoot.entranceAnchor != null
                ? pizzeriaRoot.entranceAnchor
                : root.transform.Find("Anchors/Entrance") ?? root.transform.Find("Entrance");

            for (int q = 0; q < 2; q++)
            {
                if (byQueue.ContainsKey(q)) continue;

                string nodeName = $"CustomerQueueCounter_{q}";
                Transform existingNode = root.transform.Find(nodeName);
                GameObject go = existingNode != null ? existingNode.gameObject : new GameObject(nodeName);
                if (existingNode == null)
                {
                    go.transform.SetParent(root.transform, false);
                    float xOffset = q == 0 ? -0.90f : 0.90f;
                    Vector3 localPos = entrance != null
                        ? root.transform.InverseTransformPoint(entrance.position) + new Vector3(xOffset, 0f, 2.2f)
                        : new Vector3(xOffset, 0f, -2.4f);
                    go.transform.localPosition = localPos;
                    go.transform.localRotation = Quaternion.identity;
                }

                if (go.GetComponent<Collider>() == null)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 1.0f, 0f);
                    box.size = new Vector3(0.85f, 1.8f, 0.85f);
                }

                var counter = go.GetComponent<CustomerCounterStation>() ?? go.AddComponent<CustomerCounterStation>();
                counter.queueIndex = q;
                counter.counterName = $"Balcão de Atendimento (Fila {q + 1})";
                counter.EnsureQueueSlotAnchors(3);
                byQueue[q] = counter;
                changed = true;
                notes.Add($"Configurado CustomerCounterStation para Fila {q + 1} (3 slots).");
            }

            // 3. Garantir DiningTableStation nas mesas do salão (sem tocar em bancadas que tenham StationView)
            var existingTables = root.GetComponentsInChildren<DiningTableStation>(true);
            var tableById = new Dictionary<int, DiningTableStation>();
            for (int i = 0; i < existingTables.Length; i++)
            {
                if (existingTables[i] != null && existingTables[i].tableId > 0)
                {
                    existingTables[i].EnsureAnchors();
                    tableById[existingTables[i].tableId] = existingTables[i];
                }
            }

            if (tableById.Count == 0)
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                int nextId = 1;
                for (int i = 0; i < allTransforms.Length && nextId <= 6; i++)
                {
                    var t = allTransforms[i];
                    if (t == null || t == root.transform) continue;
                    string lower = t.name.ToLowerInvariant();
                    bool looksLikeDiningTable =
                        (lower.StartsWith("mesa") || lower.Contains("diningtable") || lower.Contains("table_salao")) &&
                        !lower.Contains("corte") && !lower.Contains("preparo") && !lower.Contains("montagem");

                    if (looksLikeDiningTable &&
                        t.GetComponent<StationView>() == null &&
                        t.GetComponentInParent<StationView>() == null &&
                        t.GetComponent<DiningTableStation>() == null)
                    {
                        var dts = t.gameObject.AddComponent<DiningTableStation>();
                        dts.tableId = nextId;
                        dts.tableName = $"Mesa {nextId}";
                        if (t.GetComponentInChildren<Collider>() == null)
                        {
                            var bc = t.gameObject.AddComponent<BoxCollider>();
                            bc.center = new Vector3(0f, 0.45f, 0f);
                            bc.size = new Vector3(1.1f, 0.9f, 1.1f);
                        }
                        dts.EnsureAnchors();
                        tableById[nextId] = dts;
                        nextId++;
                        changed = true;
                    }
                }

                // Se o prefab ainda não tiver malhas de mesa identificadas, criar 4 mesas de salão dedicadas
                if (tableById.Count == 0)
                {
                    for (int id = 1; id <= 4; id++)
                    {
                        string tName = $"DiningTable_{id}";
                        Transform existingT = root.transform.Find(tName);
                        GameObject go = existingT != null ? existingT.gameObject : new GameObject(tName);
                        if (existingT == null)
                        {
                            go.transform.SetParent(root.transform, false);
                            float x = (id % 2 == 1) ? -2.4f : 2.4f;
                            float z = (id <= 2) ? -1.4f : -3.4f;
                            go.transform.localPosition = new Vector3(x, 0f, z);
                            go.transform.localRotation = Quaternion.identity;
                        }

                        if (go.GetComponent<Collider>() == null)
                        {
                            var bc = go.AddComponent<BoxCollider>();
                            bc.center = new Vector3(0f, 0.45f, 0f);
                            bc.size = new Vector3(1.1f, 0.9f, 1.1f);
                        }

                        var dts = go.GetComponent<DiningTableStation>() ?? go.AddComponent<DiningTableStation>();
                        dts.tableId = id;
                        dts.tableName = $"Mesa {id}";
                        dts.EnsureAnchors();
                        tableById[id] = dts;
                        changed = true;
                    }
                }

                notes.Add($"Configuradas {tableById.Count} mesas de salão (DiningTableStation).");
            }

            report = notes.Count > 0
                ? string.Join(" ", notes)
                : $"Salão já estava configurado ({byQueue.Count} filas, {tableById.Count} mesas). PizzeriaAuthoringBindings preservado.";
            return changed;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Configuração Não-Destrutiva do Salão, Filas e Mesas", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adiciona/valida as 2 filas de atendimento no balcão (CustomerCounterStation), as mesas do salão com âncoras de cadeira e prato (DiningTableStation), o CustomerSpawner e o CustomerPatienceHudPresenter sem sobrescrever PizzeriaAuthoringBindings ou bancadas da cozinha.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Configurar PizzeriaModular_Manual.prefab (Preservando Bindings)", GUILayout.Height(32)))
            {
                ApplyNonDestructiveDiningSetupToPrefab(ManualPrefabPath, out _lastReport);
                Debug.Log($"[DiningSetupWizard] {_lastReport}");
            }

            if (GUILayout.Button("Configurar PizzeriaPrefab.prefab (Legado / Compatibilidade)", GUILayout.Height(28)))
            {
                ApplyNonDestructiveDiningSetupToPrefab(LegacyPrefabPath, out _lastReport);
                Debug.Log($"[DiningSetupWizard] {_lastReport}");
            }

            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_lastReport, MessageType.None);
            }
        }
    }
}

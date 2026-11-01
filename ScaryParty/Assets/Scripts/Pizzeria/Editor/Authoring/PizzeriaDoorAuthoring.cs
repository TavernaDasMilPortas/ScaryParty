#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public static class PizzeriaDoorAuthoring
    {
        public const string ManualPrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";

        [MenuItem("Tools/Scary Party/Pizzeria/Configurar Portas de Geladeiras e Fornos no Prefab Manual")]
        public static void ConfigureManualPrefabMenu()
        {
            if (!EditorUtility.DisplayDialog("Configurar Portas",
                    $"Deseja configurar os pivôs vazios e portas animadas no prefab '{ManualPrefabPath}'?",
                    "Configurar", "Cancelar")) return;

            ConfigurePrefabDoors(ManualPrefabPath);
        }

        public static void BatchConfigureManualPrefab()
        {
            Debug.Log($"[PizzeriaDoorAuthoring] Iniciando configuração headless em '{ManualPrefabPath}'...");
            ConfigurePrefabDoors(ManualPrefabPath);
        }

        public static bool ConfigurePrefabDoors(string prefabPath)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    Debug.LogError($"[PizzeriaDoorAuthoring] Falha ao carregar prefab '{prefabPath}'.");
                    return false;
                }

                ConfigureAllStationDoorsInHierarchy(root, out int fridges, out int ovens, out var warnings);

                for (int i = 0; i < warnings.Count; i++)
                {
                    Debug.LogWarning($"[PizzeriaDoorAuthoring] {warnings[i]}");
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[PizzeriaDoorAuthoring] Sucesso ao configurar '{prefabPath}': {fridges} geladeira(s), {ovens} forno(s). Total avisos: {warnings.Count}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PizzeriaDoorAuthoring] Erro ao configurar '{prefabPath}': {ex}");
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

        public static void ConfigureAllStationDoorsInHierarchy(
            GameObject root,
            out int fridgesConfigured,
            out int ovensConfigured,
            out List<string> warnings)
        {
            fridgesConfigured = 0;
            ovensConfigured = 0;
            warnings = new List<string>();

            if (root == null) return;

            var stations = root.GetComponentsInChildren<StationView>(true);

            foreach (var station in stations)
            {
                string lowerName = station.gameObject.name.ToLowerInvariant();

                // 1. Geladeiras
                if (station is StorageStation || lowerName.Contains("geladeira"))
                {
                    if (lowerName.Contains("geladeira") || HasFridgeDoorMeshes(station.gameObject))
                    {
                        if (ConfigureFridgeDoors(station.gameObject, out string report))
                        {
                            fridgesConfigured++;
                        }
                        if (!string.IsNullOrEmpty(report))
                        {
                            warnings.Add(report);
                        }
                    }
                    else if (lowerName.Contains("armario"))
                    {
                        // Armários: verificar se tem geometria separada
                        warnings.Add($"O móvel '{station.stationName}' ({station.gameObject.name}) possui corpo e portas combinados em uma única malha no FBX. Um pivô vazio não separa geometria combinada. É necessário separar a geometria das folhas no modelo FBX para permitir animação independente.");
                    }
                }

                // 2. Fornos
                if (station is OvenStation || lowerName.Contains("forno"))
                {
                    if (ConfigureOvenDoor(station.gameObject, out string report))
                    {
                        ovensConfigured++;
                    }
                    if (!string.IsNullOrEmpty(report))
                    {
                        warnings.Add(report);
                    }
                }
            }
        }

        private static bool HasFridgeDoorMeshes(GameObject stationGo)
        {
            var renderers = stationGo.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                string name = r.gameObject.name.ToLowerInvariant();
                if (name.Contains("portamaior") || name.Contains("portapequena"))
                    return true;
            }
            return false;
        }

        public static bool ConfigureFridgeDoors(GameObject stationGo, out string report)
        {
            report = string.Empty;
            if (stationGo == null) return false;

            var stationView = stationGo.GetComponent<StationView>();
            var presenter = stationGo.GetComponent<StationDoorPresenter>();
            if (presenter == null)
            {
                presenter = stationGo.AddComponent<StationDoorPresenter>();
            }

            var renderers = stationGo.GetComponentsInChildren<MeshRenderer>(true);
            var doorViews = new List<TransformDoorView>();
            int createdPivots = 0;

            foreach (var r in renderers)
            {
                string name = r.gameObject.name.ToLowerInvariant();
                bool isUpper = name.Contains("portamaior");
                bool isLower = name.Contains("portapequena");

                if (!isUpper && !isLower) continue;

                GameObject doorObj = r.gameObject;
                Transform doorTrans = doorObj.transform;

                // Verificar se já está sob um DoorPivot
                Transform pivotTrans = doorTrans.parent;
                bool needsNewPivot = pivotTrans == null || pivotTrans == stationGo.transform || !pivotTrans.name.StartsWith("DoorPivot");

                if (needsNewPivot)
                {
                    // Determinar posição da dobradiça na borda da porta
                    Bounds bounds = r.bounds;
                    Vector3 hingeWorldPos;

                    // Na geladeira, a dobradiça fica na lateral direita ou esquerda da porta.
                    // Usamos a lateral x máxima em espaço local da estação para a dobradiça
                    Vector3 localCenter = stationGo.transform.InverseTransformPoint(bounds.center);
                    Vector3 localExtents = stationGo.transform.InverseTransformVector(bounds.extents);

                    // A dobradiça fica no canto lateral da porta (borda X positiva) e na face de junção/vedação da carcaça da geladeira.
                    // Posicionar o pivô na linha da junção (Z = -0.338m) elimina a fresta de 15cm quando aberta
                    // e preserva a folha exatamente fora da carcaça quando fechada.
                    float hingeLocalX = 0.412f;
                    float hingeLocalY = localCenter.y;
                    float hingeLocalZ = -0.338f;

                    hingeWorldPos = stationGo.transform.TransformPoint(new Vector3(hingeLocalX, hingeLocalY, hingeLocalZ));

                    string pivotName = isUpper ? "DoorPivot_Upper" : "DoorPivot_Lower";
                    var pivotGo = new GameObject(pivotName);
                    pivotTrans = pivotGo.transform;
                    pivotTrans.SetParent(stationGo.transform, false);
                    pivotTrans.position = hingeWorldPos;
                    pivotTrans.rotation = stationGo.transform.rotation;
                    pivotTrans.localScale = Vector3.one;

                    // Reparentalizar mantendo a pose mundial da mesh
                    doorTrans.SetParent(pivotTrans, true);
                    createdPivots++;
                }

                // Configurar TransformDoorView no pivot
                var doorView = pivotTrans.GetComponent<TransformDoorView>();
                if (doorView == null)
                {
                    doorView = pivotTrans.gameObject.AddComponent<TransformDoorView>();
                }

                doorView.motionMode = DoorMotionMode.Rotation;
                doorView.localAxis = Vector3.up;
                doorView.sign = -1f; // Sentido de abertura para fora (porta com dobradiça na direita abre com rotação negativa em Y)
                doorView.hoverAmount = 20f; // Entreabre 20 graus no hover (abertura perceptível)
                doorView.openAmount = 90f; // Abre 90 graus na interação
                doorView.durationOpen = 0.30f;
                doorView.durationClose = 0.25f;
                doorView.durationHover = 0.15f;
                doorView.CaptureClosedPose();

                if (!doorViews.Contains(doorView))
                {
                    doorViews.Add(doorView);
                }
            }

            if (doorViews.Count == 0)
            {
                report = $"Nenhuma malha de porta separada encontrada em '{stationGo.name}'.";
                return false;
            }

            presenter.SetDoors(doorViews.ToArray());
            return true;
        }

        public static bool ConfigureOvenDoor(GameObject ovenGo, out string report)
        {
            report = string.Empty;
            if (ovenGo == null) return false;

            var ovenStation = ovenGo.GetComponent<OvenStation>();
            var presenter = ovenGo.GetComponent<StationDoorPresenter>();
            if (presenter == null)
            {
                presenter = ovenGo.AddComponent<StationDoorPresenter>();
            }

            // Localizar porta vertical deslizante (portaforno)
            var renderers = ovenGo.GetComponentsInChildren<MeshRenderer>(true);
            TransformDoorView doorView = null;

            foreach (var r in renderers)
            {
                string name = r.gameObject.name.ToLowerInvariant();
                if (name.Contains("portaforno"))
                {
                    doorView = r.gameObject.GetComponent<TransformDoorView>();
                    if (doorView == null)
                    {
                        doorView = r.gameObject.AddComponent<TransformDoorView>();
                    }

                    doorView.motionMode = DoorMotionMode.Translation;
                    doorView.localAxis = Vector3.up;
                    doorView.sign = 1f;
                    doorView.hoverAmount = 0.08f; // Pequena subida de 8cm no hover
                    doorView.openAmount = 0.55f; // Curso total de 55cm suficiente para passagem da pizza
                    doorView.durationOpen = 0.25f;
                    doorView.durationClose = 0.25f;
                    doorView.durationHover = 0.15f;
                    doorView.CaptureClosedPose();
                    break;
                }
            }

            // Garantir EntryAnchor na boca do forno
            if (ovenStation != null)
            {
                ovenStation.GetEntryAnchor();
            }

            if (doorView == null)
            {
                report = $"Malha 'portaforno' não encontrada em '{ovenGo.name}'.";
                return false;
            }

            presenter.SetDoors(new[] { doorView });
            return true;
        }
    }
}
#endif

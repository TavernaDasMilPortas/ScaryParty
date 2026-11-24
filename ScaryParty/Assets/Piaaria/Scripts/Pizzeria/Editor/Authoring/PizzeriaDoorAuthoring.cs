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
        public const string ManualPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Pizzaria/Configurar Portas de Geladeiras e Fornos (Prefab Manual)", false, 244)]
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

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Pizzaria/Gerar Pontos de Trabalho de NPC (Prefab Manual)", false, 245)]
        public static void GenerateNpcWorkPointsManualPrefabMenu()
        {
            if (!EditorUtility.DisplayDialog("Gerar Pontos de Trabalho de NPC",
                    $"Deseja gerar/atualizar os NpcWorkPoints no prefab '{ManualPrefabPath}'?",
                    "Gerar", "Cancelar")) return;

            GenerateNpcWorkPointsInPrefab(ManualPrefabPath);
        }

        public static bool GenerateNpcWorkPointsInPrefab(string prefabPath)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    Debug.LogError($"[NpcWorkPointAuthoring] Falha ao carregar prefab '{prefabPath}'.");
                    return false;
                }

                int created = 0;
                int updated = 0;
                float floorY = EstimatePizzeriaFloorY(root.transform);

                var stations = root.GetComponentsInChildren<StationView>(true);
                for (int i = 0; i < stations.Length; i++)
                {
                    var st = stations[i];
                    if (st == null) continue;
                    NpcWorkPointRole role = ResolveRoleForStation(st);

                    if (st.slotAnchors != null && st.slotAnchors.Length > 0)
                    {
                        for (int s = 0; s < st.slotAnchors.Length; s++)
                        {
                            var anchor = st.slotAnchors[s];
                            if (anchor == null) continue;
                            Vector3 targetPos = anchor.position;
                            Vector3 dirOut = ResolveOutwardDirection(st.transform, targetPos);
                            Vector3 workPos = new Vector3(targetPos.x + dirOut.x * 0.65f, floorY, targetPos.z + dirOut.z * 0.65f);
                            UpsertWorkPoint(st.transform, st, s, role, $"NpcWorkPoint_Slot{s}", workPos, targetPos, ref created, ref updated);
                        }
                    }
                    else
                    {
                        Vector3 targetPos = st.transform.position;
                        Vector3 dirOut = ResolveOutwardDirection(st.transform, targetPos);
                        Vector3 workPos = new Vector3(targetPos.x + dirOut.x * 0.75f, floorY, targetPos.z + dirOut.z * 0.75f);
                        UpsertWorkPoint(st.transform, st, -1, role, "NpcWorkPoint_Main", workPos, targetPos, ref created, ref updated);
                    }
                }

                var counters = root.GetComponentsInChildren<CustomerCounterStation>(true);
                for (int i = 0; i < counters.Length; i++)
                {
                    var counter = counters[i];
                    if (counter == null) continue;
                    // Fila fica em -forward; lado de dentro do balcão fica em +forward
                    Vector3 targetPos = counter.transform.position;
                    Vector3 insidePos = targetPos + counter.transform.forward * 1.20f;
                    insidePos.y = floorY;
                    UpsertWorkPoint(counter.transform, counter, -1, NpcWorkPointRole.CounterInside, "NpcWorkPoint_CounterInside", insidePos, targetPos, ref created, ref updated);
                }

                var tables = root.GetComponentsInChildren<DiningTableStation>(true);
                for (int i = 0; i < tables.Length; i++)
                {
                    var table = tables[i];
                    if (table == null) continue;
                    Vector3 targetPos = table.transform.position;
                    Vector3 servePos = table.GetApproachPosition();
                    servePos.y = floorY;
                    UpsertWorkPoint(table.transform, table, -1, NpcWorkPointRole.TableServe, "NpcWorkPoint_TableServe", servePos, targetPos, ref created, ref updated);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[NpcWorkPointAuthoring] Pontos de trabalho atualizados em '{prefabPath}': {created} criado(s), {updated} atualizado(s).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcWorkPointAuthoring] Erro ao gerar NpcWorkPoints: {ex}");
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

        private static NpcWorkPointRole ResolveRoleForStation(StationView st)
        {
            if (st is PhoneStation) return NpcWorkPointRole.PhoneUse;
            if (st is TicketBoardStation) return NpcWorkPointRole.TicketBoard;
            if (st is StorageStation) return NpcWorkPointRole.StorageAccess;
            if (st is OvenStation) return NpcWorkPointRole.OvenFront;
            return NpcWorkPointRole.Generic;
        }

        private static float EstimatePizzeriaFloorY(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                string lower = r.gameObject.name.ToLowerInvariant();
                if (lower.Contains("floor") || lower.Contains("piso") || lower.Contains("chao"))
                {
                    return r.bounds.max.y;
                }
            }
            return root.position.y - 1.60f;
        }

        private static Vector3 ResolveOutwardDirection(Transform stationTransform, Vector3 anchorPos)
        {
            Vector3 fwd = stationTransform != null ? stationTransform.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            return fwd.normalized;
        }

        private static void UpsertWorkPoint(
            Transform parent,
            Component targetStation,
            int slotIndex,
            NpcWorkPointRole role,
            string childName,
            Vector3 worldPos,
            Vector3 lookAtWorldPos,
            ref int created,
            ref int updated)
        {
            Transform existingT = parent.Find(childName);
            NpcWorkPoint wp;
            if (existingT == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(parent, false);
                wp = go.AddComponent<NpcWorkPoint>();
                created++;
            }
            else
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(existingT.gameObject);
                wp = existingT.GetComponent<NpcWorkPoint>() ?? existingT.gameObject.AddComponent<NpcWorkPoint>();
                if (wp.manualLocked) return;
                updated++;
            }

            wp.targetStation = targetStation;
            wp.slotIndex = slotIndex;
            wp.role = role;
            wp.standRadius = 0.25f;

            wp.transform.position = worldPos;
            Vector3 lookDir = lookAtWorldPos - worldPos;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                wp.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
        }
    }
}
#endif

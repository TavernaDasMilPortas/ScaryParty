#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Interaction;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    public static class PizzeriaFunctionalPrefabBuilder
    {
        public const string CanonicalPrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";
        public const string BackupFolder = "Assets/Prefabs/Pizzeria/Backup";
        public const string ConfigPath = "Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset";
        public const string FloorMaterialPath = "Assets/Prefabs/Pizzeria/Materials/Mat_PizzeriaFloor.mat";
        public const string WallMaterialPath = "Assets/Prefabs/Pizzeria/Materials/Mat_PizzeriaWall.mat";

        public static GameObject BuildOrUpdateModularPrefab(PizzeriaFbxImportProfile profile = null)
        {
            if (profile == null)
            {
                profile = AssetDatabase.LoadAssetAtPath<PizzeriaFbxImportProfile>("Assets/ScriptableObjects/Pizzeria/Import/PizzeriaFbxImportProfile.asset");
                if (profile == null)
                {
                    profile = PizzeriaFbxImportProfile.CreateDefaultProfile();
                    if (!Directory.Exists("Assets/ScriptableObjects/Pizzeria/Import"))
                        Directory.CreateDirectory("Assets/ScriptableObjects/Pizzeria/Import");
                    AssetDatabase.CreateAsset(profile, "Assets/ScriptableObjects/Pizzeria/Import/PizzeriaFbxImportProfile.asset");
                    AssetDatabase.SaveAssets();
                }
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(profile.fbxAssetPath);
            if (fbx == null)
            {
                throw new FileNotFoundException($"FBX da Pizzaria não encontrado em: {profile.fbxAssetPath}");
            }

            // 1. Cria backup prévio do prefab canônico se ele existir
            BackupExistingPrefab();

            // 2. Extrai ou atualiza prefabs visuais reutilizáveis
            PizzeriaModelPrefabBuilder.BuildVisualPrefabs(profile);
            PizzeriaModelPrefabBuilder.LinkVisualsToPresentationProfiles();

            // 3. Constrói a hierarquia modular em memória
            var root = new GameObject("PizzeriaPrefab");
            try
            {
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;

                // Componentes de Rede e Domínio
                var netObj = BuilderToolkit.GetOrAddComponent<NetworkObject>(root);
                var rootComp = BuilderToolkit.GetOrAddComponent<PizzeriaRoot>(root);
                var footprint = BuilderToolkit.GetOrAddComponent<PizzeriaFootprint>(root);
                BuilderToolkit.GetOrAddComponent<PizzeriaNetworkState>(root);
                BuilderToolkit.GetOrAddComponent<PizzeriaCommandHandler>(root);
                BuilderToolkit.GetOrAddComponent<KitchenCommands>(root);
                BuilderToolkit.GetOrAddComponent<OrderCommands>(root);
                BuilderToolkit.GetOrAddComponent<LogisticsCommands>(root);
                BuilderToolkit.GetOrAddComponent<PizzeriaNotifier>(root);
                BuilderToolkit.GetOrAddComponent<PizzeriaDebugCommands>(root);
                BuilderToolkit.GetOrAddComponent<PlayerLifecycleBridge>(root);
                BuilderToolkit.GetOrAddComponent<PizzeriaHudBuilder>(root);
                BuilderToolkit.GetOrAddComponent<PizzeriaItemVisualizer>(root);
                BuilderToolkit.GetOrAddComponent<TicketModalPresenter>(root);
                BuilderToolkit.GetOrAddComponent<Night1Bootstrapper>(root);

                // Configura PizzeriaConfig no PizzeriaRoot
                var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(ConfigPath);
                if (config != null)
                {
                    var so = new SerializedObject(rootComp);
                    so.FindProperty("_config").objectReferenceValue = config;
                    so.ApplyModifiedProperties();
                }

                // 4. Constrói Arquitetura e Pisos L
                var buildingRoot = new GameObject("Building");
                buildingRoot.transform.SetParent(root.transform, false);

                BuildProceduralFloor(buildingRoot.transform, profile);
                BuildArchitectureFromFbx(buildingRoot.transform, fbx, profile);

                // 5. Constrói Mobília do FBX
                var furnitureRoot = new GameObject("Furniture");
                furnitureRoot.transform.SetParent(buildingRoot.transform, false);
                BuildFurnitureFromFbx(furnitureRoot.transform, fbx, profile);

                // 6. Constrói Estações Canônicas (1 a 18) com slots explícitos
                var stationsRoot = new GameObject("Stations");
                stationsRoot.transform.SetParent(root.transform, false);
                var stationViews = BuildCanonicalStations(stationsRoot.transform, fbx, profile);

                // 7. Constrói Âncoras do Prédio
                var anchorsRoot = new GameObject("Anchors");
                anchorsRoot.transform.SetParent(root.transform, false);
                BuildAnchors(anchorsRoot.transform, rootComp, footprint, stationViews, profile);

                // 8. Salva o Prefab Canônico preservando seu GUID
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, CanonicalPrefabPath);
                Debug.Log($"[PizzeriaFunctionalPrefabBuilder] ✅ Prefab canônico salvo com sucesso em: {CanonicalPrefabPath}");

                // 9. Atualiza NetworkPrefabsList
                RegisterCanonicalPrefabInNetworkList(savedPrefab);

                return savedPrefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void BackupExistingPrefab()
        {
            if (File.Exists(CanonicalPrefabPath))
            {
                if (!Directory.Exists(BackupFolder)) Directory.CreateDirectory(BackupFolder);
                string backupFile = $"{BackupFolder}/PizzeriaPrefab_Backup.prefab";
                AssetDatabase.CopyAsset(CanonicalPrefabPath, backupFile);
                Debug.Log($"[PizzeriaFunctionalPrefabBuilder] Backup do prefab canônico criado em: {backupFile}");
            }
        }

        public static bool RestoreBackup()
        {
            string backupFile = $"{BackupFolder}/PizzeriaPrefab_Backup.prefab";
            if (!File.Exists(backupFile))
            {
                Debug.LogError($"[PizzeriaFunctionalPrefabBuilder] Arquivo de backup não encontrado em: {backupFile}");
                return false;
            }

            AssetDatabase.CopyAsset(backupFile, CanonicalPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PizzeriaFunctionalPrefabBuilder] ✅ Prefab restaurado com sucesso a partir do backup!");
            return true;
        }

        private static void BuildProceduralFloor(Transform parent, PizzeriaFbxImportProfile profile)
        {
            var floorParent = new GameObject("Floor");
            floorParent.transform.SetParent(parent, false);

            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (floorMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                floorMat = new Material(shader) { color = new Color(0.35f, 0.35f, 0.38f) };
            }

            // Slab 1: Salão de Atendimento / Entrada (16.4m x 22.4m)
            // diningHallRect: Min (0, -13.2), Max (16.4, 9.2)
            Vector2 dMin = profile.diningHallRect.min;
            Vector2 dMax = profile.diningHallRect.max;
            Vector3 dCenter = new Vector3((dMin.x + dMax.x) * 0.5f, -profile.floorThickness * 0.5f, (dMin.y + dMax.y) * 0.5f);
            Vector3 dSize = new Vector3(dMax.x - dMin.x, profile.floorThickness, dMax.y - dMin.y);

            var diningFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diningFloor.name = "Floor_Dining";
            diningFloor.transform.SetParent(floorParent.transform, false);
            diningFloor.transform.localPosition = dCenter;
            diningFloor.transform.localScale = dSize;
            diningFloor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // Slab 2: Cozinha / Área Operacional (16.6m x 36.8m)
            // kitchenRect: Min (16.4, -27.6), Max (33.0, 9.2)
            Vector2 kMin = profile.kitchenRect.min;
            Vector2 kMax = profile.kitchenRect.max;
            Vector3 kCenter = new Vector3((kMin.x + kMax.x) * 0.5f, -profile.floorThickness * 0.5f, (kMin.y + kMax.y) * 0.5f);
            Vector3 kSize = new Vector3(kMax.x - kMin.x, profile.floorThickness, kMax.y - kMin.y);

            var kitchenFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kitchenFloor.name = "Floor_Kitchen";
            kitchenFloor.transform.SetParent(floorParent.transform, false);
            kitchenFloor.transform.localPosition = kCenter;
            kitchenFloor.transform.localScale = kSize;
            kitchenFloor.GetComponent<Renderer>().sharedMaterial = floorMat;
        }

        private static void BuildArchitectureFromFbx(Transform parent, GameObject fbx, PizzeriaFbxImportProfile profile)
        {
            var archRoot = new GameObject("Architecture");
            archRoot.transform.SetParent(parent, false);

            var ignoredSet = new HashSet<string>(profile.ignoredNodeNames, StringComparer.OrdinalIgnoreCase);

            foreach (Transform child in fbx.transform)
            {
                string name = child.name;
                if (ignoredSet.Contains(name)) continue;

                // Ignora itens móveis dinâmicos
                if (IsDynamicItem(name)) continue;

                // Ignora estações específicas que serão tratadas em Stations
                if (IsStationNode(name, profile)) continue;

                // Ignora mobília decorativa que vai para Furniture
                if (IsFurnitureNode(name)) continue;

                // Nós de paredes, pilastras, telhados, portas e batentes
                var go = new GameObject(name);
                go.transform.SetParent(archRoot.transform, false);
                go.transform.localPosition = child.localPosition + new Vector3(0f, profile.floorElevationY, 0f);
                go.transform.localRotation = child.localRotation;
                go.transform.localScale = child.localScale;

                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var newMf = go.AddComponent<MeshFilter>();
                    newMf.sharedMesh = mf.sharedMesh;

                    var newMr = go.AddComponent<MeshRenderer>();
                    newMr.sharedMaterials = mr != null ? mr.sharedMaterials : new Material[0];

                    // Adiciona BoxCollider se for parede, pilastra, batente ou balcão fixo
                    if (name.StartsWith("parede", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("pilastra", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("balcao", StringComparison.OrdinalIgnoreCase))
                    {
                        var col = go.AddComponent<BoxCollider>();
                        col.center = mf.sharedMesh.bounds.center;
                        col.size = mf.sharedMesh.bounds.size;
                    }
                }
            }
        }

        private static void BuildFurnitureFromFbx(Transform parent, GameObject fbx, PizzeriaFbxImportProfile profile)
        {
            foreach (Transform child in fbx.transform)
            {
                string name = child.name;
                if (!IsFurnitureNode(name)) continue;
                if (IsStationNode(name, profile)) continue;

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = child.localPosition + new Vector3(0f, profile.floorElevationY, 0f);
                go.transform.localRotation = child.localRotation;
                go.transform.localScale = child.localScale;

                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var newMf = go.AddComponent<MeshFilter>();
                    newMf.sharedMesh = mf.sharedMesh;

                    var newMr = go.AddComponent<MeshRenderer>();
                    newMr.sharedMaterials = mr != null ? mr.sharedMaterials : new Material[0];

                    // Adiciona colisor a cadeiras e mesas
                    if (name.StartsWith("cadeira", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("mesa", StringComparison.OrdinalIgnoreCase))
                    {
                        var col = go.AddComponent<BoxCollider>();
                        col.center = mf.sharedMesh.bounds.center;
                        col.size = mf.sharedMesh.bounds.size;
                    }
                }
            }
        }

        private static Dictionary<int, StationView> BuildCanonicalStations(Transform parent, GameObject fbx, PizzeriaFbxImportProfile profile)
        {
            var views = new Dictionary<int, StationView>();

            foreach (var mapping in profile.stationMappings)
            {
                var stGo = new GameObject(mapping.stationName);
                stGo.transform.SetParent(parent, false);
                stGo.transform.localPosition = mapping.localPosition;
                stGo.transform.localRotation = Quaternion.Euler(mapping.localEulerAngles);

                // Encontra e copia modelo do FBX se existir
                Transform fbxModel = FindChildRecursive(fbx.transform, mapping.primaryNodeName);
                if (fbxModel != null)
                {
                    var modelGo = new GameObject("Model");
                    modelGo.transform.SetParent(stGo.transform, false);
                    modelGo.transform.localPosition = Vector3.zero;
                    modelGo.transform.localRotation = Quaternion.identity;
                    modelGo.transform.localScale = fbxModel.localScale;

                    var mf = fbxModel.GetComponent<MeshFilter>();
                    var mr = fbxModel.GetComponent<MeshRenderer>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var newMf = modelGo.AddComponent<MeshFilter>();
                        newMf.sharedMesh = mf.sharedMesh;

                        var newMr = modelGo.AddComponent<MeshRenderer>();
                        newMr.sharedMaterials = mr != null ? mr.sharedMaterials : new Material[0];

                        // Colisor na bancada para raycast do jogador
                        var col = stGo.AddComponent<BoxCollider>();
                        col.center = mf.sharedMesh.bounds.center;
                        col.size = mf.sharedMesh.bounds.size;
                    }
                }
                else
                {
                    // Fallback para colisor de interação
                    var col = stGo.AddComponent<BoxCollider>();
                    col.size = new Vector3(1.2f, 1f, 1.2f);
                    col.center = new Vector3(0f, 0.5f, 0f);
                }

                // Adiciona componente de estação correspondente
                StationView stView = AttachStationComponent(stGo, mapping.category);
                stView.stationId = mapping.stationId;
                stView.stationName = mapping.stationName;

                // Cria âncoras de slots explícitas
                var slotsParent = new GameObject("Slots");
                slotsParent.transform.SetParent(stGo.transform, false);

                var slotTransforms = new List<Transform>();
                for (int s = 0; s < mapping.slotOffsets.Count; s++)
                {
                    var slotGo = new GameObject($"SlotAnchor_{s}");
                    slotGo.transform.SetParent(slotsParent.transform, false);
                    slotGo.transform.localPosition = mapping.slotOffsets[s];
                    slotGo.transform.localRotation = Quaternion.identity;
                    slotTransforms.Add(slotGo.transform);
                }
                stView.slotAnchors = slotTransforms.ToArray();

                views[mapping.stationId] = stView;
            }

            return views;
        }

        private static StationView AttachStationComponent(GameObject target, MigrationStationCategory cat)
        {
            switch (cat)
            {
                case MigrationStationCategory.StorageFridge:
                case MigrationStationCategory.StorageCupboard:
                    return target.AddComponent<StorageStation>();
                case MigrationStationCategory.ToolRack:
                    return target.AddComponent<ToolRackStation>();
                case MigrationStationCategory.Counter:
                    return target.AddComponent<CounterStation>();
                case MigrationStationCategory.Prep:
                    return target.AddComponent<PrepStation>();
                case MigrationStationCategory.Assembly:
                    return target.AddComponent<AssemblyStation>();
                case MigrationStationCategory.Oven:
                    return target.AddComponent<OvenStation>();
                case MigrationStationCategory.Packaging:
                    return target.AddComponent<PackagingStation>();
                case MigrationStationCategory.Staging:
                    return target.AddComponent<StagingStation>();
                case MigrationStationCategory.SupplierTerminal:
                    return target.AddComponent<SupplierTerminal>();
                case MigrationStationCategory.Phone:
                    var phone = target.AddComponent<PhoneStation>();
                    if (target.GetComponent<AudioSource>() == null)
                        target.AddComponent<AudioSource>();
                    target.AddComponent<PhoneRinger>();
                    return phone;
                case MigrationStationCategory.Receiving:
                    return target.AddComponent<ReceivingStation>();
                case MigrationStationCategory.Trash:
                    return target.AddComponent<TrashStation>();
                case MigrationStationCategory.TicketBoard:
                    return target.AddComponent<TicketBoardStation>();
                case MigrationStationCategory.Stack:
                    return target.AddComponent<StackStation>();
                default:
                    return target.AddComponent<StationView>();
            }
        }

        private static void BuildAnchors(
            Transform parent,
            PizzeriaRoot rootComp,
            PizzeriaFootprint footprint,
            Dictionary<int, StationView> stations,
            PizzeriaFbxImportProfile profile)
        {
            // Spawns de Jogador (0 a 3)
            var spawnList = new List<Transform>();
            for (int i = 0; i < profile.playerSpawnPositions.Length; i++)
            {
                var spGo = new GameObject($"PlayerSpawn_{i}");
                spGo.transform.SetParent(parent, false);
                spGo.transform.localPosition = profile.playerSpawnPositions[i];
                spGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f); // Olhando para a entrada / rua
                spawnList.Add(spGo.transform);
            }
            rootComp.playerSpawnAnchors = spawnList.ToArray();

            // Entrada
            var entGo = new GameObject("EntranceAnchor");
            entGo.transform.SetParent(parent, false);
            entGo.transform.localPosition = profile.entranceAnchorPos;
            entGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            rootComp.entranceAnchor = entGo.transform;
            footprint.entranceAnchor = entGo.transform;

            // Recebimento
            var recGo = new GameObject("ReceivingAnchor");
            recGo.transform.SetParent(parent, false);
            recGo.transform.localPosition = profile.receivingAnchorPos;
            recGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            rootComp.receivingAnchor = recGo.transform;
            footprint.receivingAnchor = recGo.transform;

            // Recuperação
            var recovGo = new GameObject("RecoveryAnchor");
            recovGo.transform.SetParent(parent, false);
            recovGo.transform.localPosition = profile.recoveryAnchorPos;
            recovGo.transform.localRotation = Quaternion.identity;
            rootComp.recoveryAnchor = recovGo.transform;

            // Suporte de Utensílios
            if (stations.TryGetValue(3, out var toolRack))
            {
                rootComp.toolRackAnchor = toolRack.transform;
            }

            // Metadados explícitos de Footprint
            footprint.localBounds = new Bounds(new Vector3(16.5f, 1.5f, -9.2f), new Vector3(34.5f, 6.0f, 38.5f));
            footprint.footprintSize = new Vector2(34.5f, 38.5f);
            footprint.footprintCenter = new Vector2(16.5f, -9.2f);
            footprint.entranceDirectionLocal = new Vector2(-1f, 0f);
            footprint.diningHallRect = profile.diningHallRect;
            footprint.kitchenRect = profile.kitchenRect;
        }

        private static bool IsDynamicItem(string name)
        {
            return name.StartsWith("faca", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("cortador de pizza", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("prato/forma", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("deliverybag", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFurnitureNode(string name)
        {
            return name.StartsWith("cadeira", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("molhos", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("prancheta", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("prateleira", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("cardapio", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStationNode(string name, PizzeriaFbxImportProfile profile)
        {
            foreach (var m in profile.stationMappings)
            {
                if (m.primaryNodeName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName)) return null;
            foreach (Transform child in root)
            {
                if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase)) return child;
                var sub = FindChildRecursive(child, childName);
                if (sub != null) return sub;
            }
            return null;
        }

        private static void RegisterCanonicalPrefabInNetworkList(GameObject prefab)
        {
            string listPath = "Assets/DefaultNetworkPrefabs.asset";
            var netList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
            if (netList == null) return;

            bool found = false;
            foreach (var entry in netList.PrefabList)
            {
                if (entry.Prefab == prefab)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                netList.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(netList);
                AssetDatabase.SaveAssets();
                Debug.Log("[PizzeriaFunctionalPrefabBuilder] ✅ PizzeriaPrefab registrado em DefaultNetworkPrefabs.asset!");
            }
        }
    }
}
#endif

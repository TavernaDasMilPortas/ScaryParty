using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public class PizzeriaRebuildPreviewInfo
    {
        public string targetPrefabPath;
        public int existingConfiguredStationsCount;
        public List<string> existingConfiguredStationNames = new List<string>();
        public int manualLocksCount;
        public bool hasCustomAnchors;
        public bool hasGeneratedFloors;
    }

    public static class PizzeriaPrefabAuthoringService
    {
        /// <summary>
        /// Obtém a raiz editável atual se o Prefab Stage estiver aberto para o targetPrefab,
        /// ou null se não estiver aberto em Prefab Mode.
        /// </summary>
        public static GameObject GetOpenPrefabStageRoot(GameObject targetPrefabAsset)
        {
            if (targetPrefabAsset == null) return null;
            string assetPath = AssetDatabase.GetAssetPath(targetPrefabAsset);
            if (string.IsNullOrEmpty(assetPath)) return null;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && string.Equals(stage.assetPath, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                return stage.prefabContentsRoot;
            }
            return null;
        }

        public static string ResolveTargetPrefabPath(PizzeriaAuthoringSettings settings)
        {
            if (settings == null) return "Assets/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
            if (settings.targetPrefab != null)
            {
                string existingPath = AssetDatabase.GetAssetPath(settings.targetPrefab);
                if (!string.IsNullOrEmpty(existingPath))
                    return existingPath;
            }

            string folder = string.IsNullOrWhiteSpace(settings.outputFolder) ? "Assets/Prefabs/Pizzeria" : settings.outputFolder.Trim().TrimEnd('/', '\\');
            string name = string.IsNullOrWhiteSpace(settings.outputPrefabName) ? "PizzeriaModular_Manual" : settings.outputPrefabName.Trim();
            if (!name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                name += ".prefab";

            return $"{folder}/{name}";
        }

        /// <summary>
        /// Remove recursivamente quaisquer componentes com Missing Script na hierarquia informada,
        /// garantindo que o Unity permita salvar o Prefab sem erros.
        /// </summary>
        public static int RemoveMissingScriptsRecursively(GameObject root, bool ensureNodeIdentities = false)
        {
            if (root == null) return 0;
            int removedTotal = 0;
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t == null || t.gameObject == null) continue;
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (count > 0)
                {
                    Undo.RegisterCompleteObjectUndo(t.gameObject, "Remover Missing Scripts");
                    removedTotal += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }

                if (ensureNodeIdentities && t != root.transform && t.name != "Anchors" && !t.IsChildOf(root.transform.Find("Anchors") ?? root.transform))
                {
                    // Se o nó tinha um PizzeriaNodeIdentity que ficou Missing antes da separação do arquivo .cs, recriamos
                    bool isCategoryContainer = t.parent == root.transform && (
                        t.name == "Arquitetura" || t.name == "Equipamentos" || t.name == "Mobiliario" ||
                        t.name == "Utensilios" || t.name == "Decoracao" || t.name == "Revisar" || t.name == "Anchors");
                    bool isSubArchContainer = t.parent != null && t.parent.name == "Arquitetura" && (
                        t.name == "Paredes" || t.name == "Pisos" || t.name == "Tetos" ||
                        t.name == "Estrutura" || t.name == "Aberturas");

                    if (!isCategoryContainer && !isSubArchContainer)
                    {
                        var id = t.GetComponent<PizzeriaNodeIdentity>();
                        if (id == null)
                        {
                            id = t.gameObject.AddComponent<PizzeriaNodeIdentity>();
                            id.EnsureGuid();
                            id.originalNodeName = t.name;
                        }
                    }
                }
            }
            return removedTotal;
        }

        /// <summary>
        /// Limpa Missing Scripts diretamente no asset do Prefab salvo em disco, se houver.
        /// </summary>
        public static int SanitizePrefabAssetOnDisk(string prefabAssetPath)
        {
            if (string.IsNullOrEmpty(prefabAssetPath) || !File.Exists(prefabAssetPath))
                return 0;

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabAssetPath);
            if (contents == null) return 0;

            try
            {
                int removed = RemoveMissingScriptsRecursively(contents, ensureNodeIdentities: true);
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabAssetPath);
                }
                return removed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Etapa 1: Cria um novo prefab editável a partir de settings.sourceModel, ou abre o existente sem sobrescrever silenciosamente.
        /// </summary>
        public static GameObject CreateOrOpenEditablePrefab(
            PizzeriaAuthoringSettings settings,
            out PizzeriaHierarchySummaryReport hierarchyReport,
            out bool createdNew,
            out string statusMessage)
        {
            hierarchyReport = null;
            createdNew = false;

            if (settings == null)
            {
                statusMessage = "Configuração do Builder (PizzeriaAuthoringSettings) nula.";
                return null;
            }

            string targetPath = ResolveTargetPrefabPath(settings);

            // Se já existe um prefab nesse caminho ou referenciado em targetPrefab, NÃO sobrescrever silenciosamente!
            var existingPrefab = settings.targetPrefab != null
                ? settings.targetPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);

            if (existingPrefab != null)
            {
                string existingAssetPath = AssetDatabase.GetAssetPath(existingPrefab);
                int cleanedOnDisk = SanitizePrefabAssetOnDisk(existingAssetPath);
                GameObject openStageRoot = GetOpenPrefabStageRoot(existingPrefab);
                if (openStageRoot != null)
                {
                    cleanedOnDisk += RemoveMissingScriptsRecursively(openStageRoot, ensureNodeIdentities: true);
                }

                if (settings.targetPrefab != existingPrefab)
                {
                    Undo.RecordObject(settings, "Vincular Prefab Alvo Existente");
                    settings.targetPrefab = existingPrefab;
                    EditorUtility.SetDirty(settings);
                }

                statusMessage = cleanedOnDisk > 0
                    ? $"Prefab '{targetPath}' já existe ({cleanedOnDisk} scripts inválidos/missing limpos automaticamente). Pronto para edição."
                    : $"Prefab '{targetPath}' já existe. Abrindo para continuar edição sem sobrescrever.";
                return existingPrefab;
            }

            if (settings.sourceModel == null)
            {
                statusMessage = "Modelo de origem não atribuído na configuração.";
                return null;
            }

            EnsureFolderExists(Path.GetDirectoryName(targetPath)?.Replace('\\', '/'));

            string sourcePath = AssetDatabase.GetAssetPath(settings.sourceModel);
            string sourceGuid = !string.IsNullOrEmpty(sourcePath) ? AssetDatabase.AssetPathToGUID(sourcePath) : string.Empty;

            string rootName = Path.GetFileNameWithoutExtension(targetPath);
            GameObject rootGo = new GameObject(rootName);

            try
            {
                // Instanciar o modelo de origem e desvincular de forma controlada mantendo meshes e materiais
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(settings.sourceModel);
                if (modelInstance == null)
                {
                    modelInstance = UnityEngine.Object.Instantiate(settings.sourceModel);
                }

                if (PrefabUtility.IsPartOfPrefabInstance(modelInstance))
                {
                    PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }

                // Mover os filhos diretos do modelo para a raiz estável (sem duplicar o prédio)
                var directChildren = new List<Transform>();
                for (int i = 0; i < modelInstance.transform.childCount; i++)
                {
                    directChildren.Add(modelInstance.transform.GetChild(i));
                }

                if (directChildren.Count > 0)
                {
                    foreach (var child in directChildren)
                    {
                        Vector3 worldPos = child.position;
                        Quaternion worldRot = child.rotation;
                        Vector3 worldScale = child.lossyScale;

                        // Registrar identidade original antes de mover
                        var id = child.GetComponent<PizzeriaNodeIdentity>();
                        if (id == null) id = child.gameObject.AddComponent<PizzeriaNodeIdentity>();
                        id.EnsureGuid();
                        id.originalNodeName = child.name;
                        id.originalRelativePath = child.name;

                        child.SetParent(rootGo.transform, true);
                        child.position = worldPos;
                        child.rotation = worldRot;
                        child.localScale = worldScale;
                    }
                    UnityEngine.Object.DestroyImmediate(modelInstance);
                }
                else
                {
                    modelInstance.name = settings.sourceModel.name;
                    modelInstance.transform.SetParent(rootGo.transform, true);
                }

                // Adicionar componente de bindings persistentes na raiz
                var bindings = rootGo.AddComponent<PizzeriaAuthoringBindings>();
                bindings.isManualAuthoring = true;
                bindings.toolVersion = PizzeriaAuthoringBindings.CurrentToolVersion;
                bindings.sourceModelAssetGuid = sourceGuid;
                bindings.sourceModelAssetPath = sourcePath;
                bindings.appliedScaleFactor = 1.0f;

                // Aplicar escala explícita se for diferente de 1.0
                if (Mathf.Abs(settings.explicitScaleFactor - 1.0f) > 0.0001f && settings.explicitScaleFactor > 0.001f)
                {
                    ApplyExplicitScaleToRoot(rootGo, settings.explicitScaleFactor);
                }

                // Organizar hierarquia automaticamente se habilitado (padrão = true)
                if (settings.organizeHierarchyOnCreate)
                {
                    hierarchyReport = PizzeriaHierarchyOrganizer.OrganizeHierarchy(rootGo, settings, respectManualLocks: true);
                }

                RemoveMissingScriptsRecursively(rootGo, ensureNodeIdentities: false);

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, targetPath, out bool success);
                if (!success || savedPrefab == null)
                {
                    statusMessage = $"Falha ao salvar prefab em '{targetPath}'.";
                    return null;
                }

                Undo.RecordObject(settings, "Definir Prefab Alvo Criado");
                settings.targetPrefab = savedPrefab;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                createdNew = true;
                statusMessage = $"Prefab '{targetPath}' criado com sucesso a partir de '{settings.sourceModel.name}'.";
                return savedPrefab;
            }
            finally
            {
                if (rootGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(rootGo);
                }
            }
        }

        /// <summary>
        /// Gera um preview do que será afetado caso o usuário escolha reconstruir a partir da fonte.
        /// </summary>
        public static PizzeriaRebuildPreviewInfo BuildRebuildPreview(PizzeriaAuthoringSettings settings)
        {
            var info = new PizzeriaRebuildPreviewInfo();
            if (settings == null || settings.targetPrefab == null) return info;

            info.targetPrefabPath = AssetDatabase.GetAssetPath(settings.targetPrefab);
            var root = GetOpenPrefabStageRoot(settings.targetPrefab) ?? settings.targetPrefab;

            var stations = root.GetComponentsInChildren<StationView>(true);
            info.existingConfiguredStationsCount = stations.Length;
            foreach (var st in stations)
            {
                if (st != null)
                    info.existingConfiguredStationNames.Add($"{st.name} ({st.GetType().Name} #{st.stationId})");
            }

            var identities = root.GetComponentsInChildren<PizzeriaNodeIdentity>(true);
            info.manualLocksCount = identities.Count(i => i != null && i.manualAssociationLocked);

            var bindings = root.GetComponent<PizzeriaAuthoringBindings>();
            info.hasCustomAnchors = bindings != null && bindings.anchorsVerifiedByUser;
            info.hasGeneratedFloors = root.transform.Find("Arquitetura/Pisos") != null;

            return info;
        }

        /// <summary>
        /// Operação explícita e separada para reconstruir a estrutura visual a partir do sourceModel,
        /// preservando Anchors, pisos gerados e os componentes raiz quando existentes.
        /// </summary>
        public static bool RebuildFromSourceModel(
            PizzeriaAuthoringSettings settings,
            out PizzeriaHierarchySummaryReport hierarchyReport,
            out string statusMessage)
        {
            hierarchyReport = null;
            if (settings == null || settings.sourceModel == null || settings.targetPrefab == null)
            {
                statusMessage = "Modelo de origem e Prefab-alvo precisam estar selecionados para reconstruir.";
                return false;
            }

            string targetPath = AssetDatabase.GetAssetPath(settings.targetPrefab);
            if (string.IsNullOrEmpty(targetPath))
            {
                statusMessage = "Caminho do prefab-alvo inválido.";
                return false;
            }

            // Se estiver aberto em Prefab Stage, fechar ou operar no conteúdo
            GameObject contentsRoot = PrefabUtility.LoadPrefabContents(targetPath);
            try
            {
                // Preservar Anchors e Pisos gerados se existirem
                Transform anchorsT = contentsRoot.transform.Find("Anchors");
                Transform generatedFloorsT = contentsRoot.transform.Find("Arquitetura/Pisos/PisosGerados");
                if (generatedFloorsT != null)
                {
                    generatedFloorsT.SetParent(contentsRoot.transform, true);
                }

                // Remover grupos visuais antigos exceto Anchors e PisosGerados
                for (int i = contentsRoot.transform.childCount - 1; i >= 0; i--)
                {
                    var ch = contentsRoot.transform.GetChild(i);
                    if (ch == anchorsT || ch == generatedFloorsT) continue;
                    UnityEngine.Object.DestroyImmediate(ch.gameObject);
                }

                // Instanciar novamente do sourceModel
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(settings.sourceModel);
                if (modelInstance == null)
                    modelInstance = UnityEngine.Object.Instantiate(settings.sourceModel);

                if (PrefabUtility.IsPartOfPrefabInstance(modelInstance))
                {
                    PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }

                var directChildren = new List<Transform>();
                for (int i = 0; i < modelInstance.transform.childCount; i++)
                    directChildren.Add(modelInstance.transform.GetChild(i));

                foreach (var child in directChildren)
                {
                    Vector3 worldPos = child.position;
                    Quaternion worldRot = child.rotation;
                    Vector3 worldScale = child.lossyScale;

                    var id = child.GetComponent<PizzeriaNodeIdentity>();
                    if (id == null) id = child.gameObject.AddComponent<PizzeriaNodeIdentity>();
                    id.EnsureGuid();
                    id.originalNodeName = child.name;
                    id.originalRelativePath = child.name;

                    child.SetParent(contentsRoot.transform, true);
                    child.position = worldPos;
                    child.rotation = worldRot;
                    child.localScale = worldScale;
                }
                UnityEngine.Object.DestroyImmediate(modelInstance);

                var bindings = GetOrAddComponent<PizzeriaAuthoringBindings>(contentsRoot);
                bindings.isManualAuthoring = true;
                bindings.sourceModelAssetPath = AssetDatabase.GetAssetPath(settings.sourceModel);
                bindings.sourceModelAssetGuid = AssetDatabase.AssetPathToGUID(bindings.sourceModelAssetPath);
                bindings.stationRecords.Clear();

                if (settings.organizeHierarchyOnCreate)
                {
                    hierarchyReport = PizzeriaHierarchyOrganizer.OrganizeHierarchy(contentsRoot, settings, respectManualLocks: false);
                }

                if (generatedFloorsT != null)
                {
                    Transform pisosContainer = PizzeriaHierarchyOrganizer.GetOrCreateCategoryContainer(contentsRoot.transform, PizzeriaHierarchyCategory.Architecture_Floors);
                    generatedFloorsT.SetParent(pisosContainer, true);
                }

                RemoveMissingScriptsRecursively(contentsRoot, ensureNodeIdentities: false);

                PrefabUtility.SaveAsPrefabAsset(contentsRoot, targetPath);
                statusMessage = $"Prefab '{targetPath}' reconstruído a partir de '{settings.sourceModel.name}'.";
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contentsRoot);
            }
        }

        /// <summary>
        /// Verifica se o prefab possui malhas de piso em Arquitetura/Pisos.
        /// </summary>
        public static bool HasFloorGeometry(GameObject root)
        {
            if (root == null) return false;
            Transform pisos = root.transform.Find("Arquitetura/Pisos");
            if (pisos != null && pisos.GetComponentsInChildren<Renderer>(true).Length > 0)
                return true;

            var identities = root.GetComponentsInChildren<PizzeriaNodeIdentity>(true);
            foreach (var id in identities)
            {
                if (id != null && id.category == PizzeriaHierarchyCategory.Architecture_Floors)
                {
                    if (id.GetComponent<Renderer>() != null || id.GetComponentInChildren<Renderer>() != null)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Cria ou atualiza o piso procedural editável (com suporte a regiões em L) sob Arquitetura/Pisos/PisosGerados.
        /// </summary>
        public static int CreateOrUpdateEditableFloor(
            GameObject editableRoot,
            PizzeriaAuthoringSettings settings)
        {
            if (editableRoot == null || settings == null || settings.floorRegions == null)
                return 0;

            Undo.RegisterFullObjectHierarchyUndo(editableRoot, "Criar/Atualizar Piso da Pizzaria");

            Transform pisosContainer = PizzeriaHierarchyOrganizer.GetOrCreateCategoryContainer(
                editableRoot.transform,
                PizzeriaHierarchyCategory.Architecture_Floors);

            Transform generatedRoot = pisosContainer.Find("PisosGerados");
            if (generatedRoot == null)
            {
                var go = new GameObject("PisosGerados");
                Undo.RegisterCreatedObjectUndo(go, "Criar PisosGerados");
                generatedRoot = go.transform;
                generatedRoot.SetParent(pisosContainer, false);
                generatedRoot.localPosition = Vector3.zero;
                generatedRoot.localRotation = Quaternion.identity;
                generatedRoot.localScale = Vector3.one;
            }

            // Limpar regiões antigas geradas
            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(generatedRoot.GetChild(i).gameObject);
            }

            Material mat = settings.floorMaterial;
            if (mat == null)
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Pizzeria/Materials/Mat_PizzeriaFloor.mat");
            }

            int createdCount = 0;
            for (int i = 0; i < settings.floorRegions.Count; i++)
            {
                var reg = settings.floorRegions[i];
                if (reg == null) continue;

                float sizeX = Mathf.Abs(reg.maxXZ.x - reg.minXZ.x);
                float sizeZ = Mathf.Abs(reg.maxXZ.y - reg.minXZ.y);
                if (sizeX < 0.1f || sizeZ < 0.1f) continue;

                float centerX = (reg.minXZ.x + reg.maxXZ.x) * 0.5f;
                float centerZ = (reg.minXZ.y + reg.maxXZ.y) * 0.5f;
                float thickness = Mathf.Max(0.05f, reg.thickness);

                GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(slab, "Criar Região de Piso");
                slab.name = string.IsNullOrWhiteSpace(reg.regionName) ? $"Piso_Regiao_{i + 1}" : reg.regionName;
                slab.transform.SetParent(generatedRoot, false);
                slab.transform.localPosition = new Vector3(centerX, reg.floorY - thickness * 0.5f, centerZ);
                slab.transform.localRotation = Quaternion.identity;
                slab.transform.localScale = new Vector3(sizeX, thickness, sizeZ);

                if (mat != null)
                {
                    var rend = slab.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = mat;
                }

                var id = slab.AddComponent<PizzeriaNodeIdentity>();
                id.EnsureGuid();
                id.originalNodeName = slab.name;
                id.category = PizzeriaHierarchyCategory.Architecture_Floors;
                id.manualAssociationLocked = true;

                createdCount++;
            }

            EditorUtility.SetDirty(editableRoot);
            return createdCount;
        }

        /// <summary>
        /// Aplica um fator de escala explícito nos filhos do modelo, preservando a escala unitária dos contêineres.
        /// </summary>
        public static void ApplyExplicitScaleToRoot(GameObject editableRoot, float newScaleFactor)
        {
            if (editableRoot == null || newScaleFactor <= 0.001f) return;

            Undo.RegisterFullObjectHierarchyUndo(editableRoot, "Ajustar Escala do Modelo");
            var bindings = GetOrAddComponent<PizzeriaAuthoringBindings>(editableRoot);
            float previousScale = bindings.appliedScaleFactor > 0.001f ? bindings.appliedScaleFactor : 1.0f;
            float ratio = newScaleFactor / previousScale;
            if (Mathf.Abs(ratio - 1.0f) < 0.0001f) return;

            var allIdentities = editableRoot.GetComponentsInChildren<PizzeriaNodeIdentity>(true);
            foreach (var id in allIdentities)
            {
                if (id == null || id.transform == editableRoot.transform) continue;
                var mf = id.GetComponent<MeshFilter>();
                var rend = id.GetComponent<Renderer>();
                if (mf == null && rend == null) continue;

                Undo.RecordObject(id.transform, "Escalar Nó de Modelo");
                Vector3 localPosFromRoot = editableRoot.transform.InverseTransformPoint(id.transform.position);
                id.transform.position = editableRoot.transform.TransformPoint(localPosFromRoot * ratio);
                id.transform.localScale *= ratio;
            }

            bindings.appliedScaleFactor = newScaleFactor;
            EditorUtility.SetDirty(bindings);
            EditorUtility.SetDirty(editableRoot);
        }

        /// <summary>
        /// Etapa 2: Injeta ou atualiza de forma idempotente todos os componentes de funcionamento base na raiz do prefab,
        /// além de PizzeriaConfig, ItemPresentationCatalog, PizzeriaFootprint e Anchors.
        /// </summary>
        public static bool InjectOrUpdateBaseFunctioning(
            GameObject editableRoot,
            PizzeriaAuthoringSettings settings,
            out string statusMessage)
        {
            if (editableRoot == null)
            {
                statusMessage = "Raiz do prefab nula.";
                return false;
            }

            Undo.RegisterFullObjectHierarchyUndo(editableRoot, "Injetar/Atualizar Funcionamento Base");

            // 1. Resolver PizzeriaConfig (clonando se solicitado e ainda estiver apontando para o asset compartilhado)
            PizzeriaConfig configToUse = ResolveOrClonePizzeriaConfig(editableRoot, settings);
            ItemPresentationCatalog catalogToUse = ResolvePresentationCatalog(settings);

            // 2. Componentes obrigatórios na raiz (GetOrAddComponent idempotente)
            var netObj = GetOrAddComponent<NetworkObject>(editableRoot);
            var pizzeriaRoot = GetOrAddComponent<PizzeriaRoot>(editableRoot);
            var footprint = GetOrAddComponent<PizzeriaFootprint>(editableRoot);
            var netState = GetOrAddComponent<PizzeriaNetworkState>(editableRoot);
            var cmdHandler = GetOrAddComponent<PizzeriaCommandHandler>(editableRoot);
            var kitchenCmds = GetOrAddComponent<KitchenCommands>(editableRoot);
            var orderCmds = GetOrAddComponent<OrderCommands>(editableRoot);
            var logisticsCmds = GetOrAddComponent<LogisticsCommands>(editableRoot);
            var notifier = GetOrAddComponent<PizzeriaNotifier>(editableRoot);
            var lifecycleBridge = GetOrAddComponent<PlayerLifecycleBridge>(editableRoot);
            var hudBuilder = GetOrAddComponent<PizzeriaHudBuilder>(editableRoot);
            var itemVisualizer = GetOrAddComponent<PizzeriaItemVisualizer>(editableRoot);
            var ticketModal = GetOrAddComponent<TicketModalPresenter>(editableRoot);
            var nightBootstrapper = GetOrAddComponent<Night1Bootstrapper>(editableRoot);
            var bindings = GetOrAddComponent<PizzeriaAuthoringBindings>(editableRoot);

            bindings.isManualAuthoring = true;
            bindings.toolVersion = PizzeriaAuthoringBindings.CurrentToolVersion;
            if (settings != null && settings.sourceModel != null)
            {
                bindings.sourceModelAssetPath = AssetDatabase.GetAssetPath(settings.sourceModel);
                bindings.sourceModelAssetGuid = AssetDatabase.AssetPathToGUID(bindings.sourceModelAssetPath);
            }

            // 3. Serializar PizzeriaConfig no PizzeriaRoot
            if (configToUse != null)
            {
                var soRoot = new SerializedObject(pizzeriaRoot);
                var cfgProp = soRoot.FindProperty("_config");
                if (cfgProp != null)
                {
                    cfgProp.objectReferenceValue = configToUse;
                    soRoot.ApplyModifiedProperties();
                }
            }

            // 4. Serializar ItemPresentationCatalog e prefabs de fallback no PizzeriaItemVisualizer
            if (catalogToUse != null)
            {
                Undo.RecordObject(itemVisualizer, "Atribuir Catálogo ao PizzeriaItemVisualizer");
                itemVisualizer.catalog = catalogToUse;
            }
            if (itemVisualizer.pizzaBoxPrefab == null)
                itemVisualizer.pizzaBoxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pizzeria/Visuals/Visual_PizzaBox.prefab")
                                             ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pizzeria/PizzaBox.prefab");
            if (itemVisualizer.pizzaBasePrefab == null)
                itemVisualizer.pizzaBasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pizzeria/Visuals/Visual_PizzaPan.prefab")
                                              ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pizzeria/PizzaBase.prefab");
            if (itemVisualizer.doughPrefab == null)
                itemVisualizer.doughPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pizzeria/Ingredient_Dough.prefab");
            EditorUtility.SetDirty(itemVisualizer);

            // 5. Configurar PizzeriaFootprint preservando valores já ajustados pelo usuário
            Undo.RecordObject(footprint, "Configurar PizzeriaFootprint");
            if (footprint.footprintSize.x <= 0.1f || footprint.footprintSize.y <= 0.1f || (Mathf.Approximately(footprint.footprintSize.x, 16f) && Mathf.Approximately(footprint.footprintSize.y, 14f)))
            {
                if (settings != null)
                {
                    footprint.footprintSize = settings.defaultFootprintSize;
                    footprint.footprintCenter = new Vector2(settings.defaultFootprintCenterOffset.x, settings.defaultFootprintCenterOffset.z);
                    footprint.localBounds = new Bounds(settings.defaultFootprintCenterOffset, new Vector3(settings.defaultFootprintSize.x, 6.0f, settings.defaultFootprintSize.y));
                    footprint.entranceDirectionLocal = settings.defaultEntranceDirectionXZ;
                }
            }

            // 6. Garantir hierarquia de Anchors (EntranceAnchor, ReceivingAnchor, RecoveryAnchor, PlayerSpawn_0..3)
            EnsureAnchorsOnRoot(editableRoot, pizzeriaRoot, footprint, bindings);

            bindings.SyncPrimaryStationsFromRecords();
            pizzeriaRoot.EnsureAnchorsResolved();

            EditorUtility.SetDirty(pizzeriaRoot);
            EditorUtility.SetDirty(footprint);
            EditorUtility.SetDirty(bindings);
            EditorUtility.SetDirty(editableRoot);

            statusMessage = "Funcionamento base (NetworkObject, PizzeriaRoot, Serviços, Config, Catálogo, Footprint e Anchors) injetado/atualizado com sucesso.";
            return true;
        }

        private static void EnsureAnchorsOnRoot(
            GameObject editableRoot,
            PizzeriaRoot pizzeriaRoot,
            PizzeriaFootprint footprint,
            PizzeriaAuthoringBindings bindings)
        {
            Transform anchorsGroup = editableRoot.transform.Find("Anchors");
            if (anchorsGroup == null)
            {
                var go = new GameObject("Anchors");
                Undo.RegisterCreatedObjectUndo(go, "Criar Grupo Anchors");
                anchorsGroup = go.transform;
                anchorsGroup.SetParent(editableRoot.transform, false);
                anchorsGroup.localPosition = Vector3.zero;
                anchorsGroup.localRotation = Quaternion.identity;
                anchorsGroup.localScale = Vector3.one;
            }

            // EntranceAnchor
            if (pizzeriaRoot.entranceAnchor == null)
            {
                Transform existing = anchorsGroup.Find("EntranceAnchor");
                if (existing == null)
                {
                    var go = new GameObject("EntranceAnchor");
                    Undo.RegisterCreatedObjectUndo(go, "Criar EntranceAnchor");
                    existing = go.transform;
                    existing.SetParent(anchorsGroup, false);
                    // Posição inicial próxima à fachada (x=0.5, y=-1.4, z=-1.98)
                    existing.localPosition = new Vector3(0.5f, -1.4f, -1.98f);
                    existing.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    bindings.entranceAnchorProvisional = true;
                }
                pizzeriaRoot.entranceAnchor = existing;
            }

            // ReceivingAnchor
            if (pizzeriaRoot.receivingAnchor == null)
            {
                Transform existing = anchorsGroup.Find("ReceivingAnchor");
                if (existing == null)
                {
                    var go = new GameObject("ReceivingAnchor");
                    Undo.RegisterCreatedObjectUndo(go, "Criar ReceivingAnchor");
                    existing = go.transform;
                    existing.SetParent(anchorsGroup, false);
                    existing.localPosition = new Vector3(18.0f, -1.4f, -24.0f);
                    existing.localRotation = Quaternion.identity;
                    bindings.receivingAnchorProvisional = true;
                }
                pizzeriaRoot.receivingAnchor = existing;
            }

            // RecoveryAnchor
            if (pizzeriaRoot.recoveryAnchor == null)
            {
                Transform existing = anchorsGroup.Find("RecoveryAnchor");
                if (existing == null)
                {
                    var go = new GameObject("RecoveryAnchor");
                    Undo.RegisterCreatedObjectUndo(go, "Criar RecoveryAnchor");
                    existing = go.transform;
                    existing.SetParent(anchorsGroup, false);
                    existing.localPosition = new Vector3(13.5f, -1.4f, -2.0f);
                    existing.localRotation = Quaternion.identity;
                    bindings.recoveryAnchorProvisional = true;
                }
                pizzeriaRoot.recoveryAnchor = existing;
            }

            // PlayerSpawns (mínimo 4)
            var spawnList = new List<Transform>();
            if (pizzeriaRoot.playerSpawnAnchors != null)
            {
                foreach (var sp in pizzeriaRoot.playerSpawnAnchors)
                {
                    if (sp != null) spawnList.Add(sp);
                }
            }

            Vector3[] defaultSpawnOffsets = new[]
            {
                new Vector3(2.5f, -1.4f, -1.0f),
                new Vector3(2.5f, -1.4f, -3.0f),
                new Vector3(4.5f, -1.4f, -1.0f),
                new Vector3(4.5f, -1.4f, -3.0f)
            };

            for (int i = 0; i < 4; i++)
            {
                string spawnName = $"PlayerSpawn_{i}";
                Transform existing = anchorsGroup.Find(spawnName);
                if (existing == null && i < spawnList.Count && spawnList[i] != null)
                {
                    existing = spawnList[i];
                }
                if (existing == null)
                {
                    var go = new GameObject(spawnName);
                    Undo.RegisterCreatedObjectUndo(go, $"Criar {spawnName}");
                    existing = go.transform;
                    existing.SetParent(anchorsGroup, false);
                    existing.localPosition = defaultSpawnOffsets[i];
                    existing.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    bindings.playerSpawnsProvisional = true;
                }
                if (!spawnList.Contains(existing))
                    spawnList.Add(existing);
            }

            pizzeriaRoot.playerSpawnAnchors = spawnList.ToArray();
            if (footprint != null)
            {
                footprint.entranceAnchor = pizzeriaRoot.entranceAnchor;
                footprint.receivingAnchor = pizzeriaRoot.receivingAnchor;
            }
        }

        public static PizzeriaConfig ResolveOrClonePizzeriaConfig(GameObject editableRoot, PizzeriaAuthoringSettings settings)
        {
            PizzeriaConfig baseConfig = settings != null ? settings.pizzeriaConfig : null;
            if (baseConfig == null)
            {
                baseConfig = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset")
                          ?? AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/Resources/Pizzeria/PizzeriaConfig.asset");
            }

            if (baseConfig == null)
            {
                EnsureFolderExists("Assets/ScriptableObjects/Pizzeria");
                baseConfig = ScriptableObject.CreateInstance<PizzeriaConfig>();
                AssetDatabase.CreateAsset(baseConfig, "Assets/ScriptableObjects/Pizzeria/PizzeriaConfig_Modular.asset");
                AssetDatabase.SaveAssets();
            }

            if (settings != null && settings.cloneConfigForNewPrefab)
            {
                string basePath = AssetDatabase.GetAssetPath(baseConfig);
                // Se o config atual é o PizzeriaConfig.asset global compartilhado, criar um clone dedicado para este prefab
                if (basePath.EndsWith("/PizzeriaConfig.asset", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureFolderExists("Assets/ScriptableObjects/Pizzeria");
                    string prefabName = editableRoot != null ? editableRoot.name : (settings.outputPrefabName ?? "PizzeriaModular");
                    string clonePath = $"Assets/ScriptableObjects/Pizzeria/PizzeriaConfig_{prefabName}.asset";
                    var existingClone = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(clonePath);
                    if (existingClone == null)
                    {
                        AssetDatabase.CopyAsset(basePath, clonePath);
                        AssetDatabase.SaveAssets();
                        existingClone = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(clonePath);
                    }
                    if (existingClone != null)
                    {
                        baseConfig = existingClone;
                        Undo.RecordObject(settings, "Atribuir PizzeriaConfig Clonado");
                        settings.pizzeriaConfig = existingClone;
                        EditorUtility.SetDirty(settings);
                    }
                }
            }

            return baseConfig;
        }

        public static ItemPresentationCatalog ResolvePresentationCatalog(PizzeriaAuthoringSettings settings)
        {
            ItemPresentationCatalog cat = settings != null ? settings.presentationCatalog : null;
            if (cat == null)
            {
                cat = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset")
                   ?? Resources.Load<ItemPresentationCatalog>("ItemPresentationCatalog");
            }
            if (cat != null && settings != null && settings.presentationCatalog == null)
            {
                settings.presentationCatalog = cat;
                EditorUtility.SetDirty(settings);
            }
            return cat;
        }

        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return Undo.AddComponent<T>(go);
        }

        public static void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}

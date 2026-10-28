using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using ScaryParty.Interaction;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public class PizzeriaSelectionValidationResult
    {
        public bool isValid;
        public string blockReason;
        public GameObject editablePrefabRoot;
        public List<GameObject> validTargets = new List<GameObject>();
        public StationView existingStationOnPrimaryTarget;
        public PizzeriaStationKind existingStationKind = PizzeriaStationKind.None;
    }

    public static class PizzeriaSelectionConverter
    {
        /// <summary>
        /// Valida se a seleção atual pertence ao Prefab Stage aberto do targetPrefab (ou hierarquia editável em testes).
        /// Bloqueia seleção de assets FBX, outros prefabs ou objetos soltos na cena quando um targetPrefab está definido.
        /// </summary>
        public static PizzeriaSelectionValidationResult ValidateSelection(
            GameObject[] rawSelection,
            PizzeriaAuthoringSettings settings,
            GameObject explicitEditableRootOverride = null)
        {
            var res = new PizzeriaSelectionValidationResult();

            if (rawSelection == null || rawSelection.Length == 0)
            {
                res.isValid = false;
                res.blockReason = "Nenhum objeto selecionado na Hierarchy.";
                return res;
            }

            GameObject editableRoot = explicitEditableRootOverride;
            if (editableRoot == null && settings != null && settings.targetPrefab != null)
            {
                editableRoot = PizzeriaPrefabAuthoringService.GetOpenPrefabStageRoot(settings.targetPrefab);
                if (editableRoot == null)
                {
                    res.isValid = false;
                    res.blockReason = $"O prefab-alvo '{settings.targetPrefab.name}' não está aberto em Prefab Mode. Clique em 'Abrir prefab para editar' primeiro.";
                    return res;
                }
            }

            if (editableRoot == null)
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                {
                    editableRoot = stage.prefabContentsRoot;
                }
            }

            if (editableRoot == null)
            {
                res.isValid = false;
                res.blockReason = "Nenhum Prefab editável aberto em Prefab Mode.";
                return res;
            }

            res.editablePrefabRoot = editableRoot;

            for (int i = 0; i < rawSelection.Length; i++)
            {
                var go = rawSelection[i];
                if (go == null) continue;

                // Bloquear assets do Project (ex: FBX importado ou prefab fechado)
                if (EditorUtility.IsPersistent(go))
                {
                    string assetPath = AssetDatabase.GetAssetPath(go);
                    res.isValid = false;
                    res.blockReason = $"O objeto selecionado '{go.name}' ({assetPath}) é um asset de projeto (ex: FBX/Prefab no disco). Abra o prefab-alvo em Prefab Mode e selecione os objetos na Hierarchy.";
                    return res;
                }

                if (go == editableRoot)
                {
                    res.isValid = false;
                    res.blockReason = "A raiz inteira da pizzaria está selecionada. Selecione apenas as peças internas (ex: corpo/porta do forno, mesa, geladeira).";
                    return res;
                }

                if (!go.transform.IsChildOf(editableRoot.transform))
                {
                    res.isValid = false;
                    res.blockReason = $"O objeto '{go.name}' não pertence ao prefab-alvo '{editableRoot.name}'.";
                    return res;
                }

                res.validTargets.Add(go);
            }

            if (res.validTargets.Count == 0)
            {
                res.isValid = false;
                res.blockReason = "Nenhum objeto válido do prefab selecionado.";
                return res;
            }

            // Detectar se já existe StationView no alvo principal ou pai imediato de unidade
            var bindings = editableRoot.GetComponent<PizzeriaAuthoringBindings>();
            var primary = ResolveUnitRootForSelection(res.validTargets, editableRoot.transform, createIfMultiple: false);
            if (primary != null)
            {
                var st = primary.GetComponent<StationView>() ?? primary.GetComponentInParent<StationView>();
                if (st != null && st.transform != editableRoot.transform)
                {
                    res.existingStationOnPrimaryTarget = st;
                    res.existingStationKind = bindings != null ? bindings.GetStationKind(st) : InferKindFromComponent(st);
                }
            }

            res.isValid = true;
            return res;
        }

        private static PizzeriaStationKind InferKindFromComponent(StationView st)
        {
            if (st == null) return PizzeriaStationKind.None;
            if (st is PrepStation) return PizzeriaStationKind.Prep;
            if (st is AssemblyStation) return PizzeriaStationKind.Assembly;
            if (st is OvenStation) return PizzeriaStationKind.Oven;
            if (st is PackagingStation) return PizzeriaStationKind.Packaging;
            if (st is StagingStation) return PizzeriaStationKind.Staging;
            if (st is StorageStation) return st.stationId == 1 ? PizzeriaStationKind.Fridge : PizzeriaStationKind.Pantry;
            if (st is ToolRackStation) return PizzeriaStationKind.ToolRack;
            if (st is PhoneStation) return PizzeriaStationKind.Phone;
            if (st is TicketBoardStation) return PizzeriaStationKind.TicketBoard;
            if (st is SupplierTerminal) return PizzeriaStationKind.SupplierTerminal;
            if (st is ReceivingStation) return PizzeriaStationKind.Receiving;
            if (st is TrashStation) return PizzeriaStationKind.Trash;
            if (st is StackStation) return PizzeriaStationKind.BoxStack;
            if (st is CounterStation) return PizzeriaStationKind.Counter;
            return PizzeriaStationKind.None;
        }

        /// <summary>
        /// Converte os objetos selecionados na função escolhida (Etapa 3), respeitando o modo de seleção múltipla:
        /// - SingleEquipmentFromSelection: reúne as peças (ex: corpo + porta) em uma única unidade funcional.
        /// - OneEquipmentPerObject: cria uma estação independente por objeto selecionado (normalizando pai/filho para não duplicar).
        /// </summary>
        public static List<StationView> ConvertSelectionToStation(
            GameObject editableRoot,
            IReadOnlyList<GameObject> selectedObjects,
            PizzeriaStationConversionOptions options,
            PizzeriaAuthoringSettings settings,
            bool allowReplaceDifferentKind,
            out string statusMessage)
        {
            var createdOrUpdated = new List<StationView>();
            if (editableRoot == null || selectedObjects == null || selectedObjects.Count == 0 || options == null)
            {
                statusMessage = "Seleção ou parâmetros inválidos para conversão.";
                return createdOrUpdated;
            }

            Undo.RegisterFullObjectHierarchyUndo(editableRoot, $"Configurar {options.kind}");

            // Garantir que a raiz possui PizzeriaRoot, PizzeriaConfig e PizzeriaAuthoringBindings
            var pizzeriaRoot = PizzeriaPrefabAuthoringService.GetOrAddComponent<PizzeriaRoot>(editableRoot);
            var bindings = PizzeriaPrefabAuthoringService.GetOrAddComponent<PizzeriaAuthoringBindings>(editableRoot);
            PizzeriaConfig config = pizzeriaRoot.Config;
            if (config == null)
            {
                config = PizzeriaPrefabAuthoringService.ResolveOrClonePizzeriaConfig(editableRoot, settings);
                var soRoot = new SerializedObject(pizzeriaRoot);
                var cfgProp = soRoot.FindProperty("_config");
                if (cfgProp != null)
                {
                    cfgProp.objectReferenceValue = config;
                    soRoot.ApplyModifiedProperties();
                }
            }

            PizzeriaMultiSelectionMode mode = settings != null
                ? settings.multiSelectionMode
                : PizzeriaMultiSelectionMode.OneEquipmentPerObject;

            // Só juntamos múltiplos objetos num único equipamento se o usuário escolheu explicitamente SingleEquipmentFromSelection
            // E se os objetos selecionados não forem unidades já separadas (como Lixeira_01 e Lixeira_02).
            bool allAlreadyShareSameUnitRoot = AreAllInSameEquipmentUnit(selectedObjects, editableRoot.transform);

            if (selectedObjects.Count == 1 || allAlreadyShareSameUnitRoot || mode == PizzeriaMultiSelectionMode.SingleEquipmentFromSelection)
            {
                if (selectedObjects.Count > 1 && !allAlreadyShareSameUnitRoot && mode == PizzeriaMultiSelectionMode.OneEquipmentPerObject)
                {
                    // Fallback de segurança
                }
                else
                {
                    GameObject unitRoot = ResolveUnitRootForSelection(selectedObjects, editableRoot.transform, createIfMultiple: true, options.kind.ToString());
                    if (unitRoot == null)
                    {
                        statusMessage = "Não foi possível determinar a raiz do equipamento para a seleção.";
                        return createdOrUpdated;
                    }

                    var st = ApplyStationRecipeToUnit(unitRoot, editableRoot, pizzeriaRoot, bindings, config, options, settings, allowReplaceDifferentKind, out string msg);
                    if (st != null)
                    {
                        createdOrUpdated.Add(st);
                    }
                    statusMessage = msg;

                    bindings.SyncPrimaryStationsFromRecords();
                    pizzeriaRoot.EnsureAnchorsResolved();
                    EditorUtility.SetDirty(bindings);
                    EditorUtility.SetDirty(pizzeriaRoot);
                    if (config != null) EditorUtility.SetDirty(config);
                    EditorUtility.SetDirty(editableRoot);
                    return createdOrUpdated;
                }
            }

            // Modo Padrão ("Um equipamento independente por objeto selecionado"):
            // Normaliza cada objeto selecionado para sua própria raiz de equipamento (ex: se clicou em lata de lixo.001 e lata de lixo.002,
            // resolve Lixeira_01 e Lixeira_02 individualmente; se clicou em balcao.001, balcao.002, balcao.003, cria 3 bancadas individuais no lugar de cada uma!)
            var normalizedRoots = NormalizeIndependentRoots(selectedObjects, editableRoot.transform);
            var messages = new List<string>();

            foreach (var targetGo in normalizedRoots)
            {
                var st = ApplyStationRecipeToUnit(targetGo, editableRoot, pizzeriaRoot, bindings, config, options, settings, allowReplaceDifferentKind, out string msg);
                if (st != null)
                    createdOrUpdated.Add(st);
                if (!string.IsNullOrEmpty(msg))
                    messages.Add(msg);
            }

            statusMessage = $"Criados/atualizados {createdOrUpdated.Count} equipamento(s) individuais ({options.kind}): " + string.Join(" | ", messages);

            bindings.SyncPrimaryStationsFromRecords();
            pizzeriaRoot.EnsureAnchorsResolved();
            EditorUtility.SetDirty(bindings);
            EditorUtility.SetDirty(pizzeriaRoot);
            if (config != null) EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(editableRoot);

            return createdOrUpdated;
        }

        private static bool AreAllInSameEquipmentUnit(IReadOnlyList<GameObject> selection, Transform prefabRoot)
        {
            if (selection == null || selection.Count <= 1) return true;
            GameObject firstUnit = ResolveSingleObjectUnitRoot(selection[0], prefabRoot);
            if (firstUnit == null) return false;

            for (int i = 1; i < selection.Count; i++)
            {
                if (ResolveSingleObjectUnitRoot(selection[i], prefabRoot) != firstUnit)
                    return false;
            }
            return true;
        }

        private static GameObject ResolveSingleObjectUnitRoot(GameObject go, Transform prefabRoot)
        {
            if (go == null) return null;
            Transform t = go.transform;
            if (t.parent != null && t.parent != prefabRoot && IsUnitGroupTransform(t.parent, prefabRoot))
            {
                return t.parent.gameObject;
            }
            return go;
        }

        private static List<GameObject> NormalizeIndependentRoots(IReadOnlyList<GameObject> selection, Transform prefabRoot)
        {
            // Primeiro, elevar cada objeto selecionado para sua raiz de unidade (ex: forno.001 -> Forno_01, lata de lixo.001 -> Lixeira_01),
            // mantendo bancadas/mesas individuais (balcao.001 -> balcao.001).
            var promoted = new List<GameObject>();
            var seen = new HashSet<GameObject>();

            foreach (var go in selection)
            {
                if (go == null) continue;
                GameObject unitGo = ResolveSingleObjectUnitRoot(go, prefabRoot);
                if (unitGo != null && seen.Add(unitGo))
                {
                    promoted.Add(unitGo);
                }
            }

            var set = new HashSet<Transform>(promoted.Select(g => g.transform));
            var result = new List<GameObject>();

            foreach (var go in promoted)
            {
                if (go == null) continue;
                bool hasSelectedAncestor = false;
                Transform p = go.transform.parent;
                while (p != null && p != prefabRoot)
                {
                    if (set.Contains(p))
                    {
                        hasSelectedAncestor = true;
                        break;
                    }
                    p = p.parent;
                }
                if (!hasSelectedAncestor)
                {
                    result.Add(go);
                }
            }

            return result;
        }

        public static GameObject ResolveUnitRootForSelection(
            IReadOnlyList<GameObject> selectedObjects,
            Transform prefabRoot,
            bool createIfMultiple,
            string groupPrefix = "Equipamento")
        {
            var valid = selectedObjects.Where(g => g != null && g.transform != prefabRoot && g.transform.IsChildOf(prefabRoot)).ToList();
            if (valid.Count == 0) return null;

            if (valid.Count == 1)
            {
                Transform t = valid[0].transform;
                // Se o objeto selecionado é uma peça filha de uma raiz de unidade (ex: Forno_01 -> forno.001), usar a raiz de unidade Forno_01!
                if (t.parent != null && t.parent != prefabRoot && IsUnitGroupTransform(t.parent, prefabRoot))
                {
                    return t.parent.gameObject;
                }
                return t.gameObject;
            }

            // Verificar se todos os selecionados já compartilham a mesma raiz de unidade (ou um é pai dos demais)
            foreach (var candidate in valid)
            {
                bool isAncestorOfAll = valid.All(other => other == candidate || other.transform.IsChildOf(candidate.transform));
                if (isAncestorOfAll)
                    return candidate;
            }

            Transform sharedParent = valid[0].transform.parent;
            if (sharedParent != null && sharedParent != prefabRoot && IsUnitGroupTransform(sharedParent, prefabRoot))
            {
                bool allShareUnit = valid.All(v => v.transform.IsChildOf(sharedParent));
                if (allShareUnit)
                    return sharedParent.gameObject;
            }

            if (!createIfMultiple)
                return valid[0];

            // Criar uma raiz de unidade sob Equipamentos (ou sob o pai comum) reunindo as peças selecionadas
            Bounds combinedBounds = PizzeriaHierarchyOrganizer.ComputeWorldBounds(valid[0].transform, out _);
            for (int i = 1; i < valid.Count; i++)
            {
                var b = PizzeriaHierarchyOrganizer.ComputeWorldBounds(valid[i].transform, out bool hasMesh);
                if (hasMesh) combinedBounds.Encapsulate(b);
            }

            Transform equipContainer = PizzeriaHierarchyOrganizer.GetOrCreateCategoryContainer(prefabRoot, PizzeriaHierarchyCategory.Equipment);
            string unitName = $"{groupPrefix}_{valid[0].name}";
            var unitGo = new GameObject(unitName);
            Undo.RegisterCreatedObjectUndo(unitGo, "Agrupar Peças do Equipamento");
            unitGo.transform.SetParent(equipContainer, false);
            unitGo.transform.position = combinedBounds.center;
            unitGo.transform.rotation = Quaternion.identity;
            unitGo.transform.localScale = Vector3.one;

            var unitId = unitGo.AddComponent<PizzeriaNodeIdentity>();
            unitId.EnsureGuid();
            unitId.category = PizzeriaHierarchyCategory.Equipment;
            unitId.manualAssociationLocked = true;

            foreach (var part in valid)
            {
                if (part.transform.IsChildOf(unitGo.transform)) continue;
                PizzeriaHierarchyOrganizer.SafeReparentPreservingWorldTransform(part.transform, unitGo.transform, out _);
            }

            return unitGo;
        }

        private static bool IsUnitGroupTransform(Transform t, Transform prefabRoot)
        {
            if (t == null || t == prefabRoot) return false;
            string n = t.name;
            if (n == "Arquitetura" || n == "Paredes" || n == "Internas" || n == "Externas" ||
                n == "Pisos" || n == "Tetos" || n == "Estrutura" || n == "Aberturas" ||
                n == "Equipamentos" || n == "Mobiliario" || n == "Utensilios" ||
                n == "Decoracao" || n == "Revisar" || n == "Anchors" || n == "Model")
            {
                return false;
            }
            return true;
        }

        private static StationView ApplyStationRecipeToUnit(
            GameObject unitGo,
            GameObject editableRoot,
            PizzeriaRoot pizzeriaRoot,
            PizzeriaAuthoringBindings bindings,
            PizzeriaConfig config,
            PizzeriaStationConversionOptions options,
            PizzeriaAuthoringSettings settings,
            bool allowReplaceDifferentKind,
            out string statusMessage)
        {
            var descriptor = PizzeriaStationRecipeRegistry.GetDescriptor(options.kind);

            // Garantir que NÃO existe NetworkObject aninhado no filho
            var nestedNetObj = unitGo.GetComponent<NetworkObject>();
            if (nestedNetObj != null && unitGo != editableRoot)
            {
                Undo.DestroyObjectImmediate(nestedNetObj);
            }

            // Verificar se já existe um StationView no unitGo
            var existingStation = unitGo.GetComponent<StationView>();
            int preservedStationId = existingStation != null && existingStation.stationId > 0 ? existingStation.stationId : 0;

            if (existingStation != null)
            {
                PizzeriaStationKind existingKind = bindings.GetStationKind(existingStation);
                bool sameComponentType = existingStation.GetType() == descriptor.runtimeComponentType;

                if (!sameComponentType || (existingKind != PizzeriaStationKind.None && existingKind != options.kind))
                {
                    if (!allowReplaceDifferentKind)
                    {
                        statusMessage = $"O objeto '{unitGo.name}' já possui a função '{existingKind}' ({existingStation.GetType().Name}). Use 'Trocar função' para substituir.";
                        return null;
                    }

                    // Trocar função de forma limpa sem destruir o objeto do modelo!
                    RemoveStationComponentsCleanly(unitGo, bindings, config, existingStation);
                    existingStation = null;
                }
            }

            // Adicionar ou reutilizar componente concreto da estação
            StationView stationComp = existingStation;
            if (stationComp == null)
            {
                stationComp = (StationView)Undo.AddComponent(unitGo, descriptor.runtimeComponentType);
            }

            // Atribuir stationId único (preservando ID existente se for atualização/troca ou alocando próximo ID livre)
            int assignedId = preservedStationId > 0
                ? preservedStationId
                : AllocateUniqueStationId(editableRoot, config, options.kind);

            Undo.RecordObject(stationComp, "Configurar StationView");
            stationComp.stationId = assignedId;
            stationComp.stationName = !string.IsNullOrWhiteSpace(options.customDisplayName)
                ? options.customDisplayName
                : descriptor.displayName;

            // Garantir PizzeriaNodeIdentity
            var nodeId = PizzeriaPrefabAuthoringService.GetOrAddComponent<PizzeriaNodeIdentity>(unitGo);
            nodeId.EnsureGuid();
            nodeId.category = PizzeriaHierarchyCategory.Equipment;
            nodeId.pendingReview = false;
            nodeId.reviewReason = string.Empty;

            // Garantir OutlineTarget para highlight visual de foco
            PizzeriaPrefabAuthoringService.GetOrAddComponent<OutlineTarget>(unitGo);

            // Garantir Colliders apropriados para Raycast de interação (separando corpo e porta quando houver partes móveis)
            EnsureStationColliders(unitGo, options.kind);

            // Criar ou atualizar Slots (slotAnchors)
            int desiredSlots = Mathf.Max(1, options.slotCount);
            if (options.kind == PizzeriaStationKind.TicketBoard)
                desiredSlots = 1; // TicketBoard usa ticketSlots separados

            EnsureStationSlots(unitGo, stationComp, desiredSlots, options.kind, options.operationalForwardLocal);

            // Configurações específicas de cada um dos 15 tipos de estação
            ConfigureSpecificStationType(unitGo, stationComp, pizzeriaRoot, bindings, config, options, settings);

            // Registrar StationDef e StationInstanceConfig no PizzeriaConfig
            StationDef stationDef = RegisterOrUpdateStationInConfig(unitGo, stationComp, config, options, settings);

            // Registrar no PizzeriaAuthoringBindings
            var record = bindings.FindRecordByStation(stationComp);
            if (record == null)
            {
                record = new PizzeriaStationBindingRecord
                {
                    bindingGuid = Guid.NewGuid().ToString("N")
                };
                bindings.stationRecords.Add(record);
            }
            record.nodeGuid = nodeId.nodeGuid;
            record.stationId = stationComp.stationId;
            record.stationKind = options.kind;
            record.storagePreset = options.storagePreset;
            record.stationComponent = stationComp;
            record.stationDef = stationDef;
            record.operationalForwardLocal = options.operationalForwardLocal.sqrMagnitude > 0.001f ? options.operationalForwardLocal.normalized : Vector3.forward;
            record.maxInteractionDistance = Mathf.Max(1.0f, options.maxInteractionDistance);
            record.configuredIngredientIds = options.storageIngredientIds != null ? new List<int>(options.storageIngredientIds) : new List<int>();
            record.configuredProcessIds = ResolveAcceptedProcessIds(options);
            record.configuredToolDefIds = options.toolDefIds != null ? new List<int>(options.toolDefIds) : new List<int>();

            if (settings != null && !settings.configuredBindingGuids.Contains(record.bindingGuid))
            {
                Undo.RecordObject(settings, "Registrar Binding GUID");
                settings.configuredBindingGuids.Add(record.bindingGuid);
                EditorUtility.SetDirty(settings);
            }

            EditorUtility.SetDirty(stationComp);
            statusMessage = $"'{unitGo.name}' configurado como {descriptor.displayName} (stationId={stationComp.stationId}, slots={stationComp.slotAnchors?.Length ?? 0}).";
            return stationComp;
        }

        /// <summary>
        /// Remove componentes e artefatos de uma função de estação anterior sem destruir a malha/hierarquia do modelo.
        /// </summary>
        public static void RemoveStationFunctionFromObject(
            GameObject targetGo,
            GameObject editableRoot,
            PizzeriaAuthoringSettings settings)
        {
            if (targetGo == null || editableRoot == null) return;
            var st = targetGo.GetComponent<StationView>();
            if (st == null) return;

            Undo.RegisterFullObjectHierarchyUndo(editableRoot, "Remover Função da Estação");
            var bindings = editableRoot.GetComponent<PizzeriaAuthoringBindings>();
            var rootComp = editableRoot.GetComponent<PizzeriaRoot>();
            PizzeriaConfig config = rootComp != null ? rootComp.Config : (settings != null ? settings.pizzeriaConfig : null);

            RemoveStationComponentsCleanly(targetGo, bindings, config, st);
            if (bindings != null) EditorUtility.SetDirty(bindings);
            if (config != null) EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(editableRoot);
        }

        private static void RemoveStationComponentsCleanly(
            GameObject unitGo,
            PizzeriaAuthoringBindings bindings,
            PizzeriaConfig config,
            StationView oldStation)
        {
            int oldId = oldStation.stationId;

            // Remover componentes auxiliares específicos de estação (PhoneRinger)
            var ringer = unitGo.GetComponent<PhoneRinger>();
            if (ringer != null) Undo.DestroyObjectImmediate(ringer);

            // Remover TicketSlots se era TicketBoard
            var ticketSlots = unitGo.transform.Find("TicketSlots");
            if (ticketSlots != null) Undo.DestroyObjectImmediate(ticketSlots.gameObject);

            // Remover StationView concreto
            Undo.DestroyObjectImmediate(oldStation);

            if (bindings != null)
            {
                bindings.stationRecords.RemoveAll(r => r == null || r.stationComponent == oldStation || r.stationId == oldId);
                if (bindings.primaryPantryStation == oldStation) bindings.primaryPantryStation = null;
                if (bindings.primaryFridgeStation == oldStation) bindings.primaryFridgeStation = null;
                if (bindings.primaryToolRackStation == oldStation) bindings.primaryToolRackStation = null;
                if (bindings.primaryStagingStation == oldStation) bindings.primaryStagingStation = null;
                if (bindings.primaryBoxStackStation == oldStation) bindings.primaryBoxStackStation = null;
                if (bindings.primaryPhoneStation == oldStation) bindings.primaryPhoneStation = null;
                if (bindings.primaryTicketBoardStation == oldStation) bindings.primaryTicketBoardStation = null;
                if (bindings.primarySupplierTerminal == oldStation) bindings.primarySupplierTerminal = null;
                if (bindings.primaryReceivingStation == oldStation) bindings.primaryReceivingStation = null;
            }

            if (config != null && oldId > 0)
            {
                if (config.stationInstances != null)
                    config.stationInstances.RemoveAll(s => s != null && s.instanceId == oldId);
                if (config.initialStorageSlots != null)
                    config.initialStorageSlots.RemoveAll(s => s != null && s.storageId == oldId);
            }
        }

        private static int AllocateUniqueStationId(GameObject editableRoot, PizzeriaConfig config, PizzeriaStationKind kind)
        {
            var usedIds = new HashSet<int>();
            var allStations = editableRoot.GetComponentsInChildren<StationView>(true);
            for (int i = 0; i < allStations.Length; i++)
            {
                if (allStations[i] != null && allStations[i].stationId > 0)
                    usedIds.Add(allStations[i].stationId);
            }

            if (config != null && config.stationInstances != null)
            {
                foreach (var inst in config.stationInstances)
                {
                    if (inst != null && inst.instanceId > 0)
                        usedIds.Add(inst.instanceId);
                }
            }

            // Preferir IDs canônicos legados para a primeira unidade de cada tipo se estiverem livres
            int preferredId = GetPreferredCanonicalId(kind);
            if (preferredId > 0 && !usedIds.Contains(preferredId))
                return preferredId;

            int candidate = 1;
            while (usedIds.Contains(candidate))
                candidate++;
            return candidate;
        }

        private static int GetPreferredCanonicalId(PizzeriaStationKind kind)
        {
            switch (kind)
            {
                case PizzeriaStationKind.Fridge: return 1;
                case PizzeriaStationKind.Pantry: return 2;
                case PizzeriaStationKind.ToolRack: return 3;
                case PizzeriaStationKind.Prep: return 4;
                case PizzeriaStationKind.Assembly: return 6;
                case PizzeriaStationKind.Oven: return 8;
                case PizzeriaStationKind.Packaging: return 9;
                case PizzeriaStationKind.Staging: return 10;
                case PizzeriaStationKind.Counter: return 11;
                case PizzeriaStationKind.Phone: return 12;
                case PizzeriaStationKind.SupplierTerminal: return 13;
                case PizzeriaStationKind.Receiving: return 14;
                case PizzeriaStationKind.TicketBoard: return 15;
                case PizzeriaStationKind.BoxStack: return 16;
                case PizzeriaStationKind.Trash: return 19;
                default: return 0;
            }
        }

        private static void EnsureStationColliders(GameObject unitGo, PizzeriaStationKind kind)
        {
            // Se o unitGo tem filhos separados (ex: corpo e porta), garantir colliders individuais nas peças com MeshFilter
            // para não criar um único BoxCollider gigante que tampe a abertura do forno ou geladeira!
            var childMeshFilters = unitGo.GetComponentsInChildren<MeshFilter>(true);
            if (childMeshFilters.Length > 1)
            {
                // Se havia um BoxCollider gigante na raiz de agrupamento vazia, removê-lo
                if (unitGo.GetComponent<MeshFilter>() == null)
                {
                    var rootCol = unitGo.GetComponent<BoxCollider>();
                    if (rootCol != null) Undo.DestroyObjectImmediate(rootCol);
                }

                foreach (var mf in childMeshFilters)
                {
                    if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) continue;
                    var existingCol = mf.GetComponent<Collider>();
                    if (existingCol == null)
                    {
                        var box = Undo.AddComponent<BoxCollider>(mf.gameObject);
                        box.center = mf.sharedMesh.bounds.center;
                        box.size = mf.sharedMesh.bounds.size;
                    }
                    else if (!existingCol.enabled)
                    {
                        Undo.RecordObject(existingCol, "Habilitar Collider");
                        existingCol.enabled = true;
                    }
                }
                return;
            }

            // Caso de objeto único
            var anyCol = unitGo.GetComponentInChildren<Collider>(true);
            if (anyCol == null)
            {
                var mf = unitGo.GetComponent<MeshFilter>();
                var box = Undo.AddComponent<BoxCollider>(unitGo);
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0)
                {
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                }
                else
                {
                    Bounds b = PizzeriaHierarchyOrganizer.ComputeWorldBounds(unitGo.transform, out bool hasMesh);
                    if (hasMesh)
                    {
                        box.center = unitGo.transform.InverseTransformPoint(b.center);
                        box.size = b.size;
                    }
                    else
                    {
                        box.size = new Vector3(1f, 1f, 1f);
                    }
                }
            }
            else if (!anyCol.enabled)
            {
                Undo.RecordObject(anyCol, "Habilitar Collider");
                anyCol.enabled = true;
            }
        }

        public static void EnsureStationSlots(
            GameObject unitGo,
            StationView station,
            int desiredSlotCount,
            PizzeriaStationKind kind,
            Vector3 operationalForwardLocal)
        {
            desiredSlotCount = Mathf.Max(1, desiredSlotCount);
            Transform slotsContainer = unitGo.transform.Find("Slots");
            if (slotsContainer == null)
            {
                var go = new GameObject("Slots");
                Undo.RegisterCreatedObjectUndo(go, "Criar Contêiner de Slots");
                slotsContainer = go.transform;
                slotsContainer.SetParent(unitGo.transform, false);
                slotsContainer.localPosition = Vector3.zero;
                slotsContainer.localRotation = Quaternion.identity;
                slotsContainer.localScale = Vector3.one;
            }

            Bounds worldBounds = PizzeriaHierarchyOrganizer.ComputeWorldBounds(unitGo.transform, out bool hasMesh);
            Vector3 forwardWorld = unitGo.transform.TransformDirection(
                operationalForwardLocal.sqrMagnitude > 0.001f ? operationalForwardLocal.normalized : Vector3.forward);
            Vector3 rightWorld = Vector3.Cross(Vector3.up, forwardWorld).normalized;
            if (rightWorld.sqrMagnitude < 0.001f) rightWorld = unitGo.transform.right;

            // Determinar eixo maior da bancada para distribuir múltiplos slots
            float extentAlongRight = Mathf.Abs(Vector3.Dot(worldBounds.size, rightWorld));
            float extentAlongForward = Mathf.Abs(Vector3.Dot(worldBounds.size, forwardWorld));
            Vector3 spreadAxis = extentAlongRight >= extentAlongForward ? rightWorld : forwardWorld;
            float spreadLength = Mathf.Max(extentAlongRight, extentAlongForward);

            // Altura de trabalho: topo da bancada para bancadas/mesas, ou altura de câmara acessível (~1.05m do chão) para fornos altos
            float slotY = hasMesh ? worldBounds.max.y + 0.03f : unitGo.transform.position.y + 0.85f;
            if (kind == PizzeriaStationKind.Oven && hasMesh)
            {
                // Fornos têm chaminé alta (bounds.size.y ~ 6m). Posicionar o slot na boca do forno (~min.y + 1.15m) e levemente à frente
                slotY = worldBounds.min.y + 1.15f;
            }
            else if ((kind == PizzeriaStationKind.Fridge || kind == PizzeriaStationKind.Pantry) && hasMesh && worldBounds.size.y > 1.5f)
            {
                slotY = worldBounds.min.y + 1.1f;
            }
            else if (kind == PizzeriaStationKind.ToolRack && hasMesh)
            {
                slotY = worldBounds.center.y + 0.05f;
            }

            var newAnchors = new Transform[desiredSlotCount];
            for (int i = 0; i < desiredSlotCount; i++)
            {
                string slotName = $"Slot_{i}";
                Transform slotT = null;

                if (station.slotAnchors != null && i < station.slotAnchors.Length && station.slotAnchors[i] != null)
                {
                    slotT = station.slotAnchors[i];
                }
                if (slotT == null)
                {
                    slotT = slotsContainer.Find(slotName);
                }

                if (slotT == null)
                {
                    var sGo = new GameObject(slotName);
                    Undo.RegisterCreatedObjectUndo(sGo, $"Criar {slotName}");
                    slotT = sGo.transform;
                    slotT.SetParent(slotsContainer, false);

                    float tNorm = desiredSlotCount == 1 ? 0f : ((float)i / (desiredSlotCount - 1)) - 0.5f;
                    float usableSpan = Mathf.Clamp(spreadLength * 0.65f, 0.4f, Mathf.Max(0.5f, spreadLength - 0.5f));
                    Vector3 worldPos = new Vector3(worldBounds.center.x, slotY, worldBounds.center.z) + spreadAxis * (tNorm * usableSpan);

                    if (kind == PizzeriaStationKind.Oven && hasMesh)
                    {
                        // Trazer o slot do forno para perto da boca frontal para ficar dentro do alcance de interação
                        float frontExtent = Mathf.Abs(Vector3.Dot(worldBounds.extents, forwardWorld));
                        worldPos += forwardWorld * Mathf.Max(0f, frontExtent - 0.35f);
                    }

                    slotT.position = worldPos;
                    slotT.rotation = Quaternion.LookRotation(forwardWorld, Vector3.up);
                }

                newAnchors[i] = slotT;
            }

            station.slotAnchors = newAnchors;
        }

        private static void ConfigureSpecificStationType(
            GameObject unitGo,
            StationView stationComp,
            PizzeriaRoot pizzeriaRoot,
            PizzeriaAuthoringBindings bindings,
            PizzeriaConfig config,
            PizzeriaStationConversionOptions options,
            PizzeriaAuthoringSettings settings)
        {
            switch (options.kind)
            {
                case PizzeriaStationKind.Prep:
                    if (stationComp is PrepStation prep)
                    {
                        var procs = ResolveAcceptedProcessIds(options);
                        prep.defaultProcessId = procs.Count > 0 ? procs[0] : options.defaultProcessId;
                    }
                    break;

                case PizzeriaStationKind.Oven:
                    if (stationComp is OvenStation oven)
                    {
                        EnsureOvenEffects(unitGo, oven);
                        if (config != null)
                        {
                            Undo.RecordObject(config, "Atualizar Parâmetros de Forno");
                            if (options.defaultBakeDuration > 0f) config.defaultBakeDuration = options.defaultBakeDuration;
                            if (options.burnGraceDuration > 0f) config.ovenBurnGraceDuration = options.burnGraceDuration;
                        }
                    }
                    break;

                case PizzeriaStationKind.Pantry:
                case PizzeriaStationKind.Fridge:
                    if (stationComp is StorageStation storage)
                    {
                        if (options.kind == PizzeriaStationKind.Pantry && bindings.primaryPantryStation == null)
                            bindings.primaryPantryStation = storage;
                        if (options.kind == PizzeriaStationKind.Fridge && bindings.primaryFridgeStation == null)
                            bindings.primaryFridgeStation = storage;

                        SyncStorageSlotsInConfig(storage.stationId, config, options);
                    }
                    break;

                case PizzeriaStationKind.ToolRack:
                    if (stationComp is ToolRackStation rack)
                    {
                        if (bindings.primaryToolRackStation == null)
                            bindings.primaryToolRackStation = rack;
                        if (pizzeriaRoot != null && pizzeriaRoot.toolRackAnchor == null)
                            pizzeriaRoot.toolRackAnchor = rack.transform;
                    }
                    break;

                case PizzeriaStationKind.Staging:
                    if (stationComp is StagingStation stg)
                    {
                        if (bindings.primaryStagingStation == null)
                            bindings.primaryStagingStation = stg;
                    }
                    break;

                case PizzeriaStationKind.Phone:
                    if (stationComp is PhoneStation phone)
                    {
                        if (bindings.primaryPhoneStation == null)
                            bindings.primaryPhoneStation = phone;
                        ConfigurePhoneComponentsOnTarget(unitGo, phone);
                    }
                    break;

                case PizzeriaStationKind.TicketBoard:
                    if (stationComp is TicketBoardStation board)
                    {
                        if (bindings.primaryTicketBoardStation == null)
                            bindings.primaryTicketBoardStation = board;
                        ConfigureTicketBoardComponentsOnTarget(unitGo, board, Mathf.Max(1, options.ticketSlotCount));
                    }
                    break;

                case PizzeriaStationKind.SupplierTerminal:
                    if (stationComp is SupplierTerminal terminal)
                    {
                        if (bindings.primarySupplierTerminal == null)
                            bindings.primarySupplierTerminal = terminal;
                    }
                    break;

                case PizzeriaStationKind.Receiving:
                    if (stationComp is ReceivingStation receiving)
                    {
                        if (bindings.primaryReceivingStation == null)
                            bindings.primaryReceivingStation = receiving;
                        if (pizzeriaRoot != null && pizzeriaRoot.receivingAnchor == null)
                        {
                            pizzeriaRoot.receivingAnchor = receiving.slotAnchors != null && receiving.slotAnchors.Length > 0
                                ? receiving.slotAnchors[0]
                                : receiving.transform;
                        }
                    }
                    break;

                case PizzeriaStationKind.BoxStack:
                    if (stationComp is StackStation stack)
                    {
                        if (bindings.primaryBoxStackStation == null)
                            bindings.primaryBoxStackStation = stack;
                        ConfigureStackStationComponentsOnTarget(unitGo, stack, options.stackId, options.stackMaxCapacity);
                    }
                    break;
            }
        }

        /// <summary>
        /// Rotina reutilizável para configurar componentes de telefone (PhoneStation + PhoneRinger + AudioSource + Beacon)
        /// no objeto selecionado sem recriar primitivas nem alterar posição/escala do modelo.
        /// </summary>
        public static void ConfigurePhoneComponentsOnTarget(GameObject unitGo, PhoneStation phone)
        {
            if (unitGo == null || phone == null) return;

            var audioSource = PizzeriaPrefabAuthoringService.GetOrAddComponent<AudioSource>(unitGo);
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = 2.0f;
            audioSource.maxDistance = 18.0f;

            var ringer = PizzeriaPrefabAuthoringService.GetOrAddComponent<PhoneRinger>(unitGo);
            Undo.RecordObject(ringer, "Configurar PhoneRinger");
            ringer.audioSource = audioSource;

            Undo.RecordObject(phone, "Vincular PhoneRinger ao PhoneStation");
            phone.ringer = ringer;
            EditorUtility.SetDirty(phone);

            Transform beaconT = unitGo.transform.Find("PhoneBeaconLight");
            if (beaconT == null)
            {
                var beaconGo = new GameObject("PhoneBeaconLight");
                Undo.RegisterCreatedObjectUndo(beaconGo, "Criar PhoneBeaconLight");
                beaconT = beaconGo.transform;
                beaconT.SetParent(unitGo.transform, false);
                Bounds b = PizzeriaHierarchyOrganizer.ComputeWorldBounds(unitGo.transform, out bool hasMesh);
                beaconT.position = hasMesh ? new Vector3(b.center.x, b.max.y + 0.25f, b.center.z) : unitGo.transform.position + Vector3.up * 0.5f;
            }

            var beaconLight = PizzeriaPrefabAuthoringService.GetOrAddComponent<Light>(beaconT.gameObject);
            beaconLight.type = LightType.Point;
            beaconLight.color = Color.red;
            beaconLight.range = 2.5f;
            beaconLight.intensity = 0f;
            beaconLight.enabled = false;
            ringer.beaconLight = beaconLight;

            if (ringer.beaconRenderer == null)
            {
                ringer.beaconRenderer = unitGo.GetComponentInChildren<Renderer>(true);
            }
            EditorUtility.SetDirty(ringer);
        }

        /// <summary>
        /// Rotina reutilizável para configurar TicketBoardStation + TicketSlots + TicketSheetView
        /// no objeto selecionado sem mover o modelo original.
        /// </summary>
        public static void ConfigureTicketBoardComponentsOnTarget(GameObject unitGo, TicketBoardStation board, int slotCount = 6)
        {
            if (unitGo == null || board == null) return;
            slotCount = Mathf.Clamp(slotCount, 1, 12);

            Transform slotsRoot = unitGo.transform.Find("TicketSlots");
            if (slotsRoot == null)
            {
                var go = new GameObject("TicketSlots");
                Undo.RegisterCreatedObjectUndo(go, "Criar TicketSlots");
                slotsRoot = go.transform;
                slotsRoot.SetParent(unitGo.transform, false);
                slotsRoot.localPosition = Vector3.zero;
                slotsRoot.localRotation = Quaternion.identity;
                slotsRoot.localScale = Vector3.one;
            }

            Bounds b = PizzeriaHierarchyOrganizer.ComputeWorldBounds(unitGo.transform, out bool hasMesh);
            var ticketAnchors = new Transform[slotCount];

            int cols = Mathf.Min(3, slotCount);
            int rows = Mathf.CeilToInt(slotCount / (float)cols);

            for (int i = 0; i < slotCount; i++)
            {
                string slotName = $"TicketSlot_{i}";
                Transform slotT = slotsRoot.Find(slotName);
                if (slotT == null)
                {
                    var sGo = new GameObject(slotName);
                    Undo.RegisterCreatedObjectUndo(sGo, $"Criar {slotName}");
                    slotT = sGo.transform;
                    slotT.SetParent(slotsRoot, false);

                    int col = i % cols;
                    int row = i / cols;
                    float xOffset = (col - (cols - 1) * 0.5f) * 0.28f;
                    float yOffset = ((rows - 1) * 0.5f - row) * 0.28f;
                    slotT.position = (hasMesh ? b.center : unitGo.transform.position) + unitGo.transform.right * xOffset + Vector3.up * yOffset + unitGo.transform.forward * 0.06f;
                    slotT.rotation = unitGo.transform.rotation;
                }

                var sheet = slotT.GetComponentInChildren<TicketSheetView>(true);
                if (sheet == null)
                {
                    sheet = PizzeriaPrefabAuthoringService.GetOrAddComponent<TicketSheetView>(slotT.gameObject);
                    var sheetCol = slotT.GetComponent<BoxCollider>();
                    if (sheetCol == null)
                    {
                        sheetCol = Undo.AddComponent<BoxCollider>(slotT.gameObject);
                        sheetCol.size = new Vector3(0.38f, 0.55f, 0.08f);
                    }
                    Transform textT = slotT.Find("SheetText");
                    if (textT == null)
                    {
                        var textGo = new GameObject("SheetText");
                        Undo.RegisterCreatedObjectUndo(textGo, "Criar SheetText");
                        textT = textGo.transform;
                        textT.SetParent(slotT, false);
                        textT.localPosition = new Vector3(0f, 0f, -0.015f);
                        textT.localScale = Vector3.one * 0.14f;
                    }
                    var tmp = PizzeriaPrefabAuthoringService.GetOrAddComponent<TMPro.TextMeshPro>(textT.gameObject);
                    tmp.alignment = TMPro.TextAlignmentOptions.Center;
                    tmp.fontSize = 2.4f;
                    tmp.color = Color.black;
                    sheet.textMesh = tmp;
                }

                ticketAnchors[i] = slotT;
            }

            Undo.RecordObject(board, "Configurar TicketBoardStation");
            board.ticketSlots = ticketAnchors;
            board.maxVisibleTickets = slotCount;
            EditorUtility.SetDirty(board);
        }

        /// <summary>
        /// Rotina reutilizável para configurar StackStation no objeto selecionado sem recriar primitivas.
        /// </summary>
        public static void ConfigureStackStationComponentsOnTarget(GameObject unitGo, StackStation stack, ulong stackId, int maxCapacity)
        {
            if (unitGo == null || stack == null) return;
            Undo.RecordObject(stack, "Configurar StackStation");

            if (stackId == 0)
            {
                // Garantir stackId único dentro do prefab
                var root = unitGo.transform.root;
                var allStacks = root.GetComponentsInChildren<StackStation>(true);
                ulong nextId = 1;
                while (allStacks.Any(s => s != null && s != stack && s.stackId == nextId))
                    nextId++;
                stackId = nextId;
            }

            stack.stackId = stackId;
            stack.capacity = Mathf.Max(1, maxCapacity);
            EditorUtility.SetDirty(stack);
        }

        private static void EnsureOvenEffects(GameObject unitGo, OvenStation oven)
        {
            Bounds b = PizzeriaHierarchyOrganizer.ComputeWorldBounds(unitGo.transform, out bool hasMesh);
            float mouthY = hasMesh ? b.min.y + 1.15f : unitGo.transform.position.y + 0.8f;

            if (oven.ovenFireLight == null)
            {
                Transform lightT = unitGo.transform.Find("OvenFireLight");
                if (lightT == null)
                {
                    var lightGo = new GameObject("OvenFireLight");
                    Undo.RegisterCreatedObjectUndo(lightGo, "Criar OvenFireLight");
                    lightT = lightGo.transform;
                    lightT.SetParent(unitGo.transform, false);
                    lightT.position = new Vector3(hasMesh ? b.center.x : unitGo.transform.position.x, mouthY + 0.15f, hasMesh ? b.center.z : unitGo.transform.position.z);
                }
                var light = PizzeriaPrefabAuthoringService.GetOrAddComponent<Light>(lightT.gameObject);
                light.type = LightType.Point;
                light.color = new Color(1.0f, 0.45f, 0.05f);
                light.range = 3.5f;
                light.intensity = 0f;
                light.enabled = false;
                oven.ovenFireLight = light;
            }

            if (oven.smokeParticles == null)
            {
                Transform smokeT = unitGo.transform.Find("OvenSmokeParticles");
                if (smokeT == null)
                {
                    var smokeGo = new GameObject("OvenSmokeParticles");
                    Undo.RegisterCreatedObjectUndo(smokeGo, "Criar OvenSmokeParticles");
                    smokeT = smokeGo.transform;
                    smokeT.SetParent(unitGo.transform, false);
                    smokeT.position = new Vector3(hasMesh ? b.center.x : unitGo.transform.position.x, mouthY + 0.6f, hasMesh ? b.center.z : unitGo.transform.position.z);
                }
                var ps = PizzeriaPrefabAuthoringService.GetOrAddComponent<ParticleSystem>(smokeT.gameObject);
                var main = ps.main;
                main.playOnAwake = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                oven.smokeParticles = ps;
            }

            if (oven.audioSource == null)
            {
                var audio = PizzeriaPrefabAuthoringService.GetOrAddComponent<AudioSource>(unitGo);
                audio.playOnAwake = false;
                audio.spatialBlend = 1.0f;
                oven.audioSource = audio;
            }
        }

        private static List<int> ResolveAcceptedProcessIds(PizzeriaStationConversionOptions options)
        {
            if (options.kind != PizzeriaStationKind.Prep)
                return new List<int>();

            switch (options.prepPreset)
            {
                case PizzeriaPrepPreset.RollDoughOnly:
                    return new List<int> { 1 };
                case PizzeriaPrepPreset.GrateCheeseOnly:
                    return new List<int> { 2 };
                case PizzeriaPrepPreset.SliceToppingsOnly:
                    return new List<int> { 3, 4 };
                case PizzeriaPrepPreset.Custom:
                    return options.customAcceptedProcessIds != null && options.customAcceptedProcessIds.Count > 0
                        ? new List<int>(options.customAcceptedProcessIds)
                        : new List<int> { 1, 2, 3, 4 };
                case PizzeriaPrepPreset.AllProcesses:
                default:
                    return new List<int> { 1, 2, 3, 4 };
            }
        }

        private static void SyncStorageSlotsInConfig(
            int storageStationId,
            PizzeriaConfig config,
            PizzeriaStationConversionOptions options)
        {
            if (config == null) return;
            Undo.RecordObject(config, "Atualizar StorageSlots no PizzeriaConfig");
            if (config.initialStorageSlots == null)
                config.initialStorageSlots = new List<StorageSlotConfig>();

            // Remover slots antigos deste mesmo storageStationId antes de recriar
            config.initialStorageSlots.RemoveAll(s => s != null && s.storageId == storageStationId);

            List<int> ingIds = options.storageIngredientIds;
            if (ingIds == null || ingIds.Count == 0)
            {
                ingIds = options.kind == PizzeriaStationKind.Fridge
                    ? new List<int> { 3, 4, 6, 8 }
                    : new List<int> { 1 };
            }

            foreach (int ingId in ingIds)
            {
                IngredientDef ingDef = null;
                if (config.ingredients != null)
                {
                    ingDef = config.ingredients.FirstOrDefault(i => i != null && i.id == ingId);
                }

                config.initialStorageSlots.Add(new StorageSlotConfig
                {
                    storageId = storageStationId,
                    ingredient = ingDef,
                    ingredientId = ingId,
                    initialStock = Mathf.Max(0, options.initialStockPerIngredient),
                    capacity = Mathf.Max(1, options.capacityPerIngredient)
                });
            }

            EditorUtility.SetDirty(config);
        }

        private static StationDef RegisterOrUpdateStationInConfig(
            GameObject unitGo,
            StationView stationComp,
            PizzeriaConfig config,
            PizzeriaStationConversionOptions options,
            PizzeriaAuthoringSettings settings)
        {
            if (config == null) return null;

            string folder = settings != null && !string.IsNullOrWhiteSpace(settings.stationDefOutputFolder)
                ? settings.stationDefOutputFolder.TrimEnd('/', '\\')
                : "Assets/ScriptableObjects/Pizzeria/Stations";
            PizzeriaPrefabAuthoringService.EnsureFolderExists(folder);

            string assetPath = $"{folder}/StationDef_{stationComp.stationId}_{options.kind}.asset";
            StationDef def = AssetDatabase.LoadAssetAtPath<StationDef>(assetPath);
            if (def == null && config.stations != null)
            {
                def = config.stations.FirstOrDefault(s => s != null && s.id == stationComp.stationId);
            }

            if (def == null)
            {
                def = ScriptableObject.CreateInstance<StationDef>();
                AssetDatabase.CreateAsset(def, assetPath);
            }

            Undo.RecordObject(def, "Atualizar StationDef");
            def.id = stationComp.stationId;
            def.stationName = stationComp.stationName;
            def.slotCount = stationComp.slotAnchors != null && stationComp.slotAnchors.Length > 0 ? stationComp.slotAnchors.Length : 1;

            var acceptedProcIds = ResolveAcceptedProcessIds(options);
            def.acceptedProcessIds = new List<int>(acceptedProcIds);
            if (config.processes != null && acceptedProcIds.Count > 0)
            {
                def.acceptedProcesses = config.processes.Where(p => p != null && acceptedProcIds.Contains(p.id)).ToList();
            }
            else
            {
                def.acceptedProcesses.Clear();
            }
            EditorUtility.SetDirty(def);

            Undo.RecordObject(config, "Registrar Estação no PizzeriaConfig");
            if (config.stations == null) config.stations = new List<StationDef>();
            if (!config.stations.Contains(def))
                config.stations.Add(def);

            if (config.stationInstances == null) config.stationInstances = new List<StationInstanceConfig>();
            var inst = config.stationInstances.FirstOrDefault(s => s != null && s.instanceId == stationComp.stationId);
            if (inst == null)
            {
                inst = new StationInstanceConfig { instanceId = stationComp.stationId };
                config.stationInstances.Add(inst);
            }
            inst.definition = def;
            inst.displayName = stationComp.stationName;
            inst.overrideSlotCount = def.slotCount;
            EditorUtility.SetDirty(config);

            return def;
        }

        /// <summary>
        /// Adiciona um posto de trabalho extra (Slot + StationWorkpostTarget collider) a uma bancada longa.
        /// </summary>
        public static Transform AddWorkpostToStation(
            StationView station,
            out string statusMessage)
        {
            if (station == null)
            {
                statusMessage = "Estação nula.";
                return null;
            }

            Undo.RegisterFullObjectHierarchyUndo(station.gameObject, "Adicionar Posto de Trabalho");

            var existingList = station.slotAnchors != null ? station.slotAnchors.Where(t => t != null).ToList() : new List<Transform>();
            int newIndex = existingList.Count;

            Transform slotsContainer = station.transform.Find("Slots");
            if (slotsContainer == null)
            {
                var go = new GameObject("Slots");
                Undo.RegisterCreatedObjectUndo(go, "Criar Slots");
                slotsContainer = go.transform;
                slotsContainer.SetParent(station.transform, false);
            }

            var slotGo = new GameObject($"Slot_{newIndex}");
            Undo.RegisterCreatedObjectUndo(slotGo, "Criar Novo Slot");
            Transform slotT = slotGo.transform;
            slotT.SetParent(slotsContainer, false);

            if (existingList.Count > 0)
            {
                Transform last = existingList[existingList.Count - 1];
                slotT.position = last.position + station.transform.right * 0.9f;
                slotT.rotation = last.rotation;
            }
            else
            {
                Bounds b = PizzeriaHierarchyOrganizer.ComputeWorldBounds(station.transform, out bool hasMesh);
                slotT.position = hasMesh ? new Vector3(b.center.x, b.max.y + 0.03f, b.center.z) : station.transform.position + Vector3.up * 0.85f;
                slotT.rotation = station.transform.rotation;
            }

            existingList.Add(slotT);
            station.slotAnchors = existingList.ToArray();

            // Criar sub-alvo de interação (StationWorkpostTarget) com BoxCollider dedicado ao redor do slot
            Transform workpostsRoot = station.transform.Find("Workposts");
            if (workpostsRoot == null)
            {
                var wpRootGo = new GameObject("Workposts");
                Undo.RegisterCreatedObjectUndo(wpRootGo, "Criar Workposts");
                workpostsRoot = wpRootGo.transform;
                workpostsRoot.SetParent(station.transform, false);
                workpostsRoot.localPosition = Vector3.zero;
                workpostsRoot.localRotation = Quaternion.identity;
                workpostsRoot.localScale = Vector3.one;
            }

            var wpGo = new GameObject($"Workpost_{newIndex}");
            Undo.RegisterCreatedObjectUndo(wpGo, "Criar Posto de Trabalho");
            wpGo.transform.SetParent(workpostsRoot, false);
            wpGo.transform.position = slotT.position - Vector3.up * 0.25f;
            wpGo.transform.rotation = slotT.rotation;

            var col = wpGo.AddComponent<BoxCollider>();
            col.size = new Vector3(0.85f, 0.7f, 0.85f);

            var wpTarget = wpGo.AddComponent<StationWorkpostTarget>();
            wpTarget.targetStation = station;
            wpTarget.slotIndex = newIndex;
            wpTarget.slotAnchor = slotT;

            // Atualizar slotCount no StationDef / StationInstanceConfig se existir
            var pizzeriaRoot = station.GetComponentInParent<PizzeriaRoot>();
            if (pizzeriaRoot != null && pizzeriaRoot.Config != null)
            {
                var cfg = pizzeriaRoot.Config;
                var inst = cfg.stationInstances?.FirstOrDefault(s => s != null && s.instanceId == station.stationId);
                if (inst != null)
                {
                    Undo.RecordObject(cfg, "Atualizar SlotCount da Estação");
                    inst.overrideSlotCount = station.slotAnchors.Length;
                    if (inst.definition != null)
                    {
                        Undo.RecordObject(inst.definition, "Atualizar SlotCount no StationDef");
                        inst.definition.slotCount = station.slotAnchors.Length;
                        EditorUtility.SetDirty(inst.definition);
                    }
                    EditorUtility.SetDirty(cfg);
                }
            }

            EditorUtility.SetDirty(station);
            statusMessage = $"Posto de trabalho #{newIndex} adicionado em '{station.name}' (total de slots: {station.slotAnchors.Length}).";
            return slotT;
        }

        /// <summary>
        /// Resolve a estação e os objetos físicos a partir de uma seleção mista
        /// (ex: mesa de montagem + pratos). A estação é o objeto com StationView selecionado,
        /// ou o StationView pai do primeiro objeto selecionado.
        /// </summary>
        public static StationView ResolveStationForSlotBinding(GameObject[] selection, out List<GameObject> physicalObjects)
        {
            physicalObjects = new List<GameObject>();
            if (selection == null || selection.Length == 0) return null;

            StationView station = selection.Select(g => g != null ? g.GetComponent<StationView>() : null).FirstOrDefault(s => s != null);
            if (station == null)
            {
                foreach (var g in selection)
                {
                    if (g == null) continue;
                    station = g.GetComponentInParent<StationView>();
                    if (station != null) break;
                }
            }
            if (station == null) return null;

            foreach (var g in selection)
            {
                if (g == null || g == station.gameObject || g.GetComponent<StationView>() != null) continue;
                physicalObjects.Add(g);
            }
            // Ordem estável: esquerda → direita no espaço local da estação
            physicalObjects.Sort((a, b) =>
                station.transform.InverseTransformPoint(a.transform.position).x
                    .CompareTo(station.transform.InverseTransformPoint(b.transform.position).x));
            return station;
        }

        /// <summary>
        /// Vincula os slots de uma estação a objetos físicos (ex: pratos na mesa de montagem).
        /// Para cada objeto: cria um SlotAnchor filho no topo do objeto (item fica em cima dele),
        /// usa os colliders do próprio objeto como área de interação (gera um BoxCollider só se não houver)
        /// e um OutlineTarget, de modo que a interação e o contorno partem do slot e não da mesa inteira.
        /// </summary>
        /// <param name="replaceExisting">Se true, os slots/postos genéricos anteriores são removidos.</param>
        public static int BindSlotsToPhysicalObjects(
            StationView station,
            IList<GameObject> physicalObjects,
            bool replaceExisting,
            out string statusMessage)
        {
            if (station == null || physicalObjects == null || physicalObjects.Count == 0)
            {
                statusMessage = "Selecione a estação (ou um filho dela) e os objetos físicos (pratos) que serão os slots.";
                return 0;
            }

            Undo.RegisterFullObjectHierarchyUndo(station.gameObject, "Vincular Slots a Objetos");

            var newAnchors = new List<Transform>();
            var keptObjects = new HashSet<GameObject>(physicalObjects.Where(o => o != null));

            if (!replaceExisting && station.slotAnchors != null)
                newAnchors.AddRange(station.slotAnchors.Where(t => t != null));

            if (replaceExisting)
            {
                // Remove anchors/postos antigos que não pertencem aos objetos vinculados
                var oldAnchors = station.slotAnchors != null ? station.slotAnchors.Where(t => t != null).ToList() : new List<Transform>();
                foreach (var old in oldAnchors)
                {
                    var owner = old.parent != null ? old.parent.gameObject : null;
                    if (owner != null && keptObjects.Contains(owner)) continue;
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
                var wpRoot = station.transform.Find("Workposts");
                if (wpRoot != null) Undo.DestroyObjectImmediate(wpRoot.gameObject);
                foreach (var oldWp in station.GetComponentsInChildren<StationWorkpostTarget>(true))
                {
                    if (oldWp != null && !keptObjects.Contains(oldWp.gameObject))
                        Undo.DestroyObjectImmediate(oldWp);
                }
            }

            // Usa a layer de um collider real da estação (a raiz pode estar em Default e não entrar no interactableLayer)
            int interactLayer = station.gameObject.layer;
            var stationCol = station.GetComponentsInChildren<Collider>(true).FirstOrDefault(cl => cl != null
                && cl.GetComponentInParent<StationWorkpostTarget>() == null
                && !keptObjects.Any(k => cl.transform.IsChildOf(k.transform)));
            if (stationCol != null) interactLayer = stationCol.gameObject.layer;
            int bound = 0;

            foreach (var obj in physicalObjects)
            {
                if (obj == null) continue;

                // Garante que o objeto se move junto com a estação (prefab)
                if (!obj.transform.IsChildOf(station.transform))
                    Undo.SetTransformParent(obj.transform, station.transform, "Parentar objeto de slot à estação");

                Bounds b = PizzeriaHierarchyOrganizer.ComputeWorldBounds(obj.transform, out bool hasMesh);
                if (!hasMesh) b = new Bounds(obj.transform.position, new Vector3(0.3f, 0.02f, 0.3f));

                // 1) Âncora do slot no topo do objeto
                Transform anchor = obj.transform.Find("SlotAnchor");
                if (anchor == null)
                {
                    var aGo = new GameObject("SlotAnchor");
                    Undo.RegisterCreatedObjectUndo(aGo, "Criar SlotAnchor");
                    anchor = aGo.transform;
                    anchor.SetParent(obj.transform, false);
                }
                anchor.position = new Vector3(b.center.x, b.max.y + 0.005f, b.center.z);
                anchor.rotation = station.transform.rotation;

                int slotIndex = newAnchors.IndexOf(anchor);
                if (slotIndex < 0)
                {
                    slotIndex = newAnchors.Count;
                    newAnchors.Add(anchor);
                }

                // 2) Área de interação = colliders do próprio objeto. Se não tiver nenhum, gera um ajustado à malha.
                Transform legacyVol = obj.transform.Find("SlotInteractionVolume");
                if (legacyVol != null) Undo.DestroyObjectImmediate(legacyVol.gameObject);

                var ownColliders = obj.GetComponentsInChildren<Collider>(true);
                if (ownColliders.Length == 0)
                {
                    var box = Undo.AddComponent<BoxCollider>(obj);
                    Vector3 ls = obj.transform.lossyScale;
                    box.center = obj.transform.InverseTransformPoint(b.center);
                    box.size = new Vector3(
                        Mathf.Max(b.size.x, 0.05f) / Mathf.Max(Mathf.Abs(ls.x), 0.0001f),
                        Mathf.Max(b.size.y, 0.02f) / Mathf.Max(Mathf.Abs(ls.y), 0.0001f),
                        Mathf.Max(b.size.z, 0.05f) / Mathf.Max(Mathf.Abs(ls.z), 0.0001f));
                    ownColliders = new Collider[] { box };
                }
                // Garante que o raycast de interação enxergue os colliders do slot
                foreach (var oc in ownColliders)
                {
                    if (oc != null && oc.gameObject.layer != interactLayer)
                    {
                        Undo.RecordObject(oc.gameObject, "Ajustar layer do slot");
                        oc.gameObject.layer = interactLayer;
                    }
                }

                // 3) Outline e sub-alvo
                var outline = obj.GetComponent<OutlineTarget>(); if (outline == null) outline = Undo.AddComponent<OutlineTarget>(obj);
                var wp = obj.GetComponent<StationWorkpostTarget>(); if (wp == null) wp = Undo.AddComponent<StationWorkpostTarget>(obj);
                Undo.RecordObject(wp, "Configurar Slot");
                wp.targetStation = station;
                wp.slotIndex = slotIndex;
                wp.slotAnchor = anchor;
                wp.outlineTarget = outline;
                EditorUtility.SetDirty(wp);
                bound++;
            }

            Undo.RecordObject(station, "Atualizar Slots da Estação");
            station.slotAnchors = newAnchors.ToArray();
            station.slotsBoundToObjects = true;

            // Reindexa postos para refletir a ordem final do array
            foreach (var wp in station.GetComponentsInChildren<StationWorkpostTarget>(true))
            {
                if (wp == null || wp.targetStation != station || wp.slotAnchor == null) continue;
                int idx = Array.IndexOf(station.slotAnchors, wp.slotAnchor);
                if (idx >= 0 && idx != wp.slotIndex)
                {
                    Undo.RecordObject(wp, "Reindexar Slot");
                    wp.slotIndex = idx;
                    EditorUtility.SetDirty(wp);
                }
            }

            SyncSlotCountToConfig(station);
            EditorUtility.SetDirty(station);

            statusMessage = $"{bound} objeto(s) vinculados como slots em '{station.name}' (total de slots: {station.slotAnchors.Length}).";
            return bound;
        }

        private static void SyncSlotCountToConfig(StationView station)
        {
            var pizzeriaRoot = station.GetComponentInParent<PizzeriaRoot>();
            if (pizzeriaRoot == null || pizzeriaRoot.Config == null) return;
            var cfg = pizzeriaRoot.Config;
            var inst = cfg.stationInstances?.FirstOrDefault(s => s != null && s.instanceId == station.stationId);
            if (inst == null) return;
            Undo.RecordObject(cfg, "Atualizar SlotCount da Estação");
            inst.overrideSlotCount = station.slotAnchors.Length;
            if (inst.definition != null)
            {
                Undo.RecordObject(inst.definition, "Atualizar SlotCount no StationDef");
                inst.definition.slotCount = station.slotAnchors.Length;
                EditorUtility.SetDirty(inst.definition);
            }
            EditorUtility.SetDirty(cfg);
        }

        [MenuItem("Tools/Scary Party/Pizzeria/Vincular Slots aos Objetos Selecionados (substituir)", false, 30)]
        private static void MenuBindSlotsReplace() => MenuBindSlots(true);

        [MenuItem("Tools/Scary Party/Pizzeria/Vincular Slots aos Objetos Selecionados (adicionar)", false, 31)]
        private static void MenuBindSlotsAppend() => MenuBindSlots(false);

        private static void MenuBindSlots(bool replace)
        {
            var station = ResolveStationForSlotBinding(Selection.gameObjects, out var objs);
            BindSlotsToPhysicalObjects(station, objs, replace, out string msg);
            Debug.Log("[Pizzeria] " + msg);
            if (station != null)
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
                else EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
            }
        }

        /// <summary>
        /// Extrai um prefab visual limpo (sem Colliders e sem NetworkObject) a partir do objeto selecionado
        /// e vincula ao ItemPresentationProfile / ItemPresentationCatalog ou BackpackVisualizer.
        /// </summary>
        public static GameObject ExtractVisualAndBindRole(
            GameObject editableRoot,
            GameObject selectedObject,
            PizzeriaVisualRole role,
            int definitionId,
            PizzeriaAuthoringSettings settings,
            out string statusMessage)
        {
            if (selectedObject == null || editableRoot == null)
            {
                statusMessage = "Objeto selecionado ou raiz nula.";
                return null;
            }

            var nodeId = PizzeriaPrefabAuthoringService.GetOrAddComponent<PizzeriaNodeIdentity>(selectedObject);
            nodeId.EnsureGuid();

            if (role == PizzeriaVisualRole.DecorationOnly)
            {
                Undo.RecordObject(nodeId, "Marcar como Decoração");
                nodeId.visualRole = PizzeriaVisualRole.DecorationOnly;
                nodeId.boundDefinitionId = 0;
                EditorUtility.SetDirty(nodeId);
                statusMessage = $"'{selectedObject.name}' marcado como somente decorativo.";
                return null;
            }

            string folder = settings != null && !string.IsNullOrWhiteSpace(settings.extractedVisualsOutputFolder)
                ? settings.extractedVisualsOutputFolder.TrimEnd('/', '\\')
                : "Assets/Prefabs/Pizzeria/Visuals";
            PizzeriaPrefabAuthoringService.EnsureFolderExists(folder);

            string safeName = selectedObject.name.Replace('/', '_').Replace('\\', '_').Replace(' ', '_').Replace('.', '_');
            string prefabPath = $"{folder}/Visual_{role}_{safeName}.prefab";

            GameObject clone = UnityEngine.Object.Instantiate(selectedObject);
            try
            {
                clone.name = $"Visual_{safeName}";
                clone.transform.position = Vector3.zero;
                clone.transform.rotation = Quaternion.identity;

                // Remover colliders, NetworkObjects, StationViews e NodeIdentity do prefab visual
                foreach (var c in clone.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(c);
                foreach (var no in clone.GetComponentsInChildren<NetworkObject>(true))
                    UnityEngine.Object.DestroyImmediate(no);
                foreach (var sv in clone.GetComponentsInChildren<StationView>(true))
                    UnityEngine.Object.DestroyImmediate(sv);
                foreach (var ni in clone.GetComponentsInChildren<PizzeriaNodeIdentity>(true))
                    UnityEngine.Object.DestroyImmediate(ni);

                // Garantir âncoras auxiliares de apresentação (PivotGrip, PivotSupport, BoxLabelAnchor, IngredientLayerAnchor, SmokeAnchor)
                EnsureVisualWrapperSockets(clone, role);

                GameObject savedVisualPrefab = PrefabUtility.SaveAsPrefabAsset(clone, prefabPath, out bool ok);
                if (!ok || savedVisualPrefab == null)
                {
                    statusMessage = $"Falha ao salvar prefab visual em '{prefabPath}'.";
                    return null;
                }

                Undo.RecordObject(nodeId, "Registrar Papel Visual");
                nodeId.visualRole = role;
                nodeId.boundDefinitionId = definitionId;
                EditorUtility.SetDirty(nodeId);

                // Atualizar ItemPresentationCatalog / PizzeriaItemVisualizer / BackpackVisualizer
                BindExtractedVisualToCatalog(editableRoot, savedVisualPrefab, role, definitionId, settings);

                statusMessage = $"Visual '{savedVisualPrefab.name}' extraído em '{prefabPath}' e vinculado ao papel '{role}'.";
                return savedVisualPrefab;
            }
            finally
            {
                if (clone != null)
                    UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private static void EnsureVisualWrapperSockets(GameObject visualRoot, PizzeriaVisualRole role)
        {
            if (visualRoot.transform.Find("PivotGrip") == null)
            {
                var g = new GameObject("PivotGrip");
                g.transform.SetParent(visualRoot.transform, false);
            }
            if (visualRoot.transform.Find("PivotSupport") == null)
            {
                var s = new GameObject("PivotSupport");
                s.transform.SetParent(visualRoot.transform, false);
            }

            if (role == PizzeriaVisualRole.BoxVisual && visualRoot.transform.Find("BoxLabelAnchor") == null)
            {
                var lbl = new GameObject("BoxLabelAnchor");
                lbl.transform.SetParent(visualRoot.transform, false);
                lbl.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                lbl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if ((role == PizzeriaVisualRole.PanVisual || role == PizzeriaVisualRole.PizzaVisual) && visualRoot.transform.Find("IngredientLayerAnchor") == null)
            {
                var ing = new GameObject("IngredientLayerAnchor");
                ing.transform.SetParent(visualRoot.transform, false);
                ing.transform.localPosition = new Vector3(0f, 0.02f, 0f);

                var smk = new GameObject("SmokeAnchor");
                smk.transform.SetParent(visualRoot.transform, false);
                smk.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            }
        }

        private static void BindExtractedVisualToCatalog(
            GameObject editableRoot,
            GameObject visualPrefab,
            PizzeriaVisualRole role,
            int definitionId,
            PizzeriaAuthoringSettings settings)
        {
            var visualizer = editableRoot != null ? editableRoot.GetComponent<PizzeriaItemVisualizer>() : null;
            ItemPresentationCatalog catalog = visualizer != null && visualizer.catalog != null
                ? visualizer.catalog
                : PizzeriaPrefabAuthoringService.ResolvePresentationCatalog(settings);

            if (visualizer != null)
            {
                Undo.RecordObject(visualizer, "Atualizar Prefab Visual no Visualizer");
                if (role == PizzeriaVisualRole.BoxVisual) visualizer.pizzaBoxPrefab = visualPrefab;
                else if (role == PizzeriaVisualRole.PanVisual || role == PizzeriaVisualRole.PizzaVisual) visualizer.pizzaBasePrefab = visualPrefab;
                EditorUtility.SetDirty(visualizer);
            }

            if (role == PizzeriaVisualRole.BackpackVisual)
            {
                // Atualizar no PlayerArmature se existir
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab");
                if (playerPrefab != null)
                {
                    var bpVis = playerPrefab.GetComponent<BackpackVisualizer>();
                    if (bpVis != null)
                    {
                        Undo.RecordObject(bpVis, "Atualizar BackpackVisualPrefab");
                        bpVis.deliveryBagPrefab = visualPrefab;
                        EditorUtility.SetDirty(bpVis);
                    }
                }
                return;
            }

            if (catalog == null) return;

            bool isTool = role == PizzeriaVisualRole.UtensilVisual;
            bool isBoxed = role == PizzeriaVisualRole.BoxVisual;
            ItemCategory cat = ItemCategory.Other;
            if (role == PizzeriaVisualRole.IngredientVisual) cat = ItemCategory.Ingredient;
            else if (role == PizzeriaVisualRole.PizzaVisual || role == PizzeriaVisualRole.PanVisual) cat = ItemCategory.Pizza;
            else if (role == PizzeriaVisualRole.BoxVisual) cat = ItemCategory.Box;

            var profile = catalog.FindProfile(cat, definitionId, isTool, isBoxed);
            if (profile != null)
            {
                Undo.RecordObject(profile, "Atualizar VisualPrefab no Profile");
                profile.visualPrefab = visualPrefab;
                EditorUtility.SetDirty(profile);
            }
        }
    }
}

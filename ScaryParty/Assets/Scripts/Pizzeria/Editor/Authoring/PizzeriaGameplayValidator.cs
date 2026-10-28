using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using ScaryParty.Interaction;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Integration;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public enum PizzeriaValidationSeverity
    {
        Ok = 0,
        Warning = 1,
        BlockingError = 2,
        NotVerifiedInEditMode = 3
    }

    public class PizzeriaStationCoverageRow
    {
        public PizzeriaStationKind kind;
        public string displayName;
        public int totalFound;
        public int validCount;
        public int minimumRequired;
        public PizzeriaValidationSeverity status;
        public string statusDetail;
        public List<StationView> instances = new List<StationView>();
    }

    public class PizzeriaValidationIssue
    {
        public PizzeriaValidationSeverity severity;
        public string category;
        public string title;
        public string description;
        public UnityEngine.Object targetObject;
        public PizzeriaStationKind relatedStationKind = PizzeriaStationKind.None;
    }

    public class PizzeriaGameplayValidationReport
    {
        public bool isPrefabReadyForGameplay;
        public bool areSceneDependenciesReady;
        public int blockingErrorsCount;
        public int warningsCount;
        public int okCount;
        public List<PizzeriaStationCoverageRow> coverageTable = new List<PizzeriaStationCoverageRow>();
        public List<PizzeriaValidationIssue> prefabIssues = new List<PizzeriaValidationIssue>();
        public List<PizzeriaValidationIssue> cycleIssues = new List<PizzeriaValidationIssue>();
        public List<PizzeriaValidationIssue> sceneDependencyIssues = new List<PizzeriaValidationIssue>();
    }

    public static class PizzeriaGameplayValidator
    {
        /// <summary>
        /// Validação SOMENTE LEITURA do prefab-alvo e suas configurações referenciadas.
        /// NUNCA modifica o prefab durante a validação e NUNCA busca estações soltas na cena fora do prefab-alvo.
        /// </summary>
        public static PizzeriaGameplayValidationReport ValidatePrefabAndCycle(
            GameObject targetRootOrPrefab,
            PizzeriaAuthoringSettings settings,
            bool alsoValidateActiveSceneDependencies = true)
        {
            var report = new PizzeriaGameplayValidationReport();

            if (targetRootOrPrefab == null && settings != null)
            {
                targetRootOrPrefab = PizzeriaPrefabAuthoringService.GetOpenPrefabStageRoot(settings.targetPrefab) ?? settings.targetPrefab;
            }

            if (targetRootOrPrefab == null)
            {
                AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Prefab",
                    "Prefab-alvo ausente",
                    "Nenhum prefab-alvo selecionado ou aberto para validação.", null);
                FinalizeReportSummary(report);
                return report;
            }

            // Resolver componentes raiz exclusivamente dentro do targetRootOrPrefab
            var pizzeriaRoot = targetRootOrPrefab.GetComponent<PizzeriaRoot>();
            var bindings = targetRootOrPrefab.GetComponent<PizzeriaAuthoringBindings>();
            var footprint = targetRootOrPrefab.GetComponent<PizzeriaFootprint>();
            var itemVisualizer = targetRootOrPrefab.GetComponent<PizzeriaItemVisualizer>();

            PizzeriaConfig config = pizzeriaRoot != null && pizzeriaRoot.Config != null
                ? pizzeriaRoot.Config
                : (settings != null ? settings.pizzeriaConfig : null);

            // 0. Validar Componentes Raiz (Etapa 2)
            ValidateRootComponents(targetRootOrPrefab, pizzeriaRoot, bindings, footprint, itemVisualizer, config, report);

            // Coletar todas as estações EXCLUSIVAMENTE dentro de targetRootOrPrefab
            var allStations = targetRootOrPrefab.GetComponentsInChildren<StationView>(true);

            // A & B. Validar cada equipamento individualmente e montar tabela de cobertura por tipo (15 tipos)
            ValidateStationsAndBuildCoverage(targetRootOrPrefab, allStations, bindings, config, settings, report);

            // Validar integridade estrutural de conjuntos (portas/tampas soltas ou objetos pendentes em Revisar)
            ValidateStructuralHierarchyIntegrity(targetRootOrPrefab, report);

            // C. Validar Completude do Ciclo de Gameplay de ponta a ponta por receita ativa
            ValidateGameplayCycleCompleteness(targetRootOrPrefab, allStations, bindings, pizzeriaRoot, footprint, itemVisualizer, config, settings, report);

            // Validar dependências externas de cena separadamente
            if (alsoValidateActiveSceneDependencies)
            {
                ValidateSceneDependencies(settings != null && settings.targetPrefab != null ? settings.targetPrefab : targetRootOrPrefab, report);
            }

            FinalizeReportSummary(report);
            return report;
        }

        private static void ValidateRootComponents(
            GameObject root,
            PizzeriaRoot pizzeriaRoot,
            PizzeriaAuthoringBindings bindings,
            PizzeriaFootprint footprint,
            PizzeriaItemVisualizer itemVisualizer,
            PizzeriaConfig config,
            PizzeriaGameplayValidationReport report)
        {
            Type[] requiredRootTypes = new[]
            {
                typeof(NetworkObject),
                typeof(PizzeriaRoot),
                typeof(PizzeriaFootprint),
                typeof(PizzeriaNetworkState),
                typeof(PizzeriaCommandHandler),
                typeof(KitchenCommands),
                typeof(OrderCommands),
                typeof(LogisticsCommands),
                typeof(PizzeriaNotifier),
                typeof(PlayerLifecycleBridge),
                typeof(PizzeriaHudBuilder),
                typeof(PizzeriaItemVisualizer),
                typeof(TicketModalPresenter),
                typeof(Night1Bootstrapper),
                typeof(PizzeriaAuthoringBindings)
            };

            foreach (var t in requiredRootTypes)
            {
                if (root.GetComponent(t) == null)
                {
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Base (Raiz)",
                        $"Componente raiz ausente: {t.Name}",
                        $"A raiz '{root.name}' não possui o componente obrigatório '{t.Name}'. Use a aba '2. Funcionamento base' → 'Injetar/atualizar base'.",
                        root);
                }
            }

            // Verificar se existem NetworkObjects aninhados indevidos nos filhos
            var allNetObjs = root.GetComponentsInChildren<NetworkObject>(true);
            foreach (var no in allNetObjs)
            {
                if (no != null && no.gameObject != root)
                {
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Rede",
                        $"NetworkObject aninhado inválido em '{no.name}'",
                        $"Apenas a raiz do prefab deve possuir NetworkObject. Remova o NetworkObject do filho '{no.name}'.",
                        no.gameObject);
                }
            }

            if (config == null)
            {
                AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Base (Config)",
                    "PizzeriaConfig não atribuído",
                    "O PizzeriaRoot não possui um PizzeriaConfig serializado.",
                    pizzeriaRoot != null ? (UnityEngine.Object)pizzeriaRoot : root);
            }
            else
            {
                AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.Ok, "Base (Config)",
                    $"PizzeriaConfig atribuído ({config.name})",
                    "Configuração de domínio vinculada à raiz.", config);
            }

            if (itemVisualizer != null)
            {
                if (itemVisualizer.catalog == null)
                {
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Apresentação",
                        "ItemPresentationCatalog ausente no PizzeriaItemVisualizer",
                        "O campo 'catalog' do PizzeriaItemVisualizer deve estar serializado diretamente no prefab (não depender de AssetDatabase em runtime).",
                        itemVisualizer);
                }
                else
                {
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.Ok, "Apresentação",
                        $"Catálogo de apresentação serializado ({itemVisualizer.catalog.name})",
                        "PizzeriaItemVisualizer possui referência direta ao catálogo.", itemVisualizer);
                }
            }
        }

        private static void ValidateStationsAndBuildCoverage(
            GameObject root,
            StationView[] allStations,
            PizzeriaAuthoringBindings bindings,
            PizzeriaConfig config,
            PizzeriaAuthoringSettings settings,
            PizzeriaGameplayValidationReport report)
        {
            var stationsByKind = new Dictionary<PizzeriaStationKind, List<StationView>>();
            var validStationsByKind = new Dictionary<PizzeriaStationKind, List<StationView>>();

            foreach (PizzeriaStationKind k in Enum.GetValues(typeof(PizzeriaStationKind)))
            {
                if (k == PizzeriaStationKind.None) continue;
                stationsByKind[k] = new List<StationView>();
                validStationsByKind[k] = new List<StationView>();
            }

            // Mapa de IDs para detectar duplicatas
            var idToStations = new Dictionary<int, List<StationView>>();
            var stackIdToStations = new Dictionary<ulong, List<StackStation>>();

            for (int i = 0; i < allStations.Length; i++)
            {
                var st = allStations[i];
                if (st == null) continue;
                if (st.stationId > 0)
                {
                    if (!idToStations.TryGetValue(st.stationId, out var list))
                    {
                        list = new List<StationView>();
                        idToStations[st.stationId] = list;
                    }
                    list.Add(st);
                }
                if (st is StackStation stk && stk.stackId > 0)
                {
                    if (!stackIdToStations.TryGetValue(stk.stackId, out var sList))
                    {
                        sList = new List<StackStation>();
                        stackIdToStations[stk.stackId] = sList;
                    }
                    sList.Add(stk);
                }
            }

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            float maxReach = config != null && config.interactDistance > 0f ? config.interactDistance : 3.0f;

            // Validar cada estação encontrada
            for (int i = 0; i < allStations.Length; i++)
            {
                var st = allStations[i];
                if (st == null) continue;

                PizzeriaStationKind kind = ResolveStationKindForValidation(st, bindings, config);
                if (kind != PizzeriaStationKind.None && stationsByKind.ContainsKey(kind))
                {
                    stationsByKind[kind].Add(st);
                }

                bool isStationValid = true;

                // 1. Ativa na hierarquia e componente habilitado
                if (!st.gameObject.activeInHierarchy || !st.enabled)
                {
                    isStationValid = false;
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Estação",
                        $"Estação desativada: '{st.name}'",
                        $"O GameObject ou componente {st.GetType().Name} está desativado.", st);
                }

                // 2. stationId > 0 e único
                if (st.stationId <= 0)
                {
                    isStationValid = false;
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Estação",
                        $"stationId inválido ({st.stationId}) em '{st.name}'",
                        "Toda estação deve possuir stationId > 0.", st, kind);
                }
                else if (idToStations.TryGetValue(st.stationId, out var sameIdList) && sameIdList.Count > 1)
                {
                    isStationValid = false;
                    string others = string.Join(", ", sameIdList.Select(x => x.name));
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Estação",
                        $"stationId duplicado #{st.stationId} em '{st.name}'",
                        $"O ID #{st.stationId} está compartilhado entre: {others}. Cada estação deve ter ID único.", st, kind);
                }

                // 3. Registro no PizzeriaConfig (StationDef / StationInstanceConfig)
                if (config != null && st.stationId > 0)
                {
                    bool registeredInInstances = config.stationInstances != null && config.stationInstances.Any(inst => inst != null && inst.instanceId == st.stationId && inst.definition != null);
                    bool registeredInDefs = config.stations != null && config.stations.Any(d => d != null && d.id == st.stationId);
                    if (!registeredInInstances && !registeredInDefs)
                    {
                        // Para estações que usam catálogo de domínio (Prep, Oven, Counter, Assembly, Packaging, Staging, ToolRack), exigir registro
                        if (kind == PizzeriaStationKind.Prep || kind == PizzeriaStationKind.Oven || kind == PizzeriaStationKind.Assembly ||
                            kind == PizzeriaStationKind.Packaging || kind == PizzeriaStationKind.Staging || kind == PizzeriaStationKind.Counter ||
                            kind == PizzeriaStationKind.ToolRack)
                        {
                            isStationValid = false;
                            AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Estação",
                                $"Estação '{st.name}' (#{st.stationId}) não registrada no PizzeriaConfig",
                                $"Falta entrada correspondente em stationInstances/stations do PizzeriaConfig.", st, kind);
                        }
                    }
                }

                // 4. Colliders e Layer de Raycast
                var colliders = st.GetComponentsInChildren<Collider>(true);
                bool hasEnabledCollider = false;
                bool allOnIgnoreRaycast = true;
                for (int c = 0; c < colliders.Length; c++)
                {
                    var col = colliders[c];
                    if (col != null && col.enabled && col.gameObject.activeInHierarchy)
                    {
                        hasEnabledCollider = true;
                        if (col.gameObject.layer != ignoreRaycastLayer)
                            allOnIgnoreRaycast = false;
                    }
                }

                if (!hasEnabledCollider)
                {
                    isStationValid = false;
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Interação",
                        $"Estação '{st.name}' sem Collider ativo",
                        "O jogador não conseguirá focar ou interagir por raycast sem um Collider habilitado.", st, kind);
                }
                else if (allOnIgnoreRaycast)
                {
                    isStationValid = false;
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Interação",
                        $"Estação '{st.name}' na layer 'Ignore Raycast'",
                        "Todos os colliders ativos estão na layer Ignore Raycast, impedindo a interação do jogador.", st, kind);
                }

                // 5. OutlineTarget e InteractPrompt
                if (st.GetComponent<OutlineTarget>() == null && st.GetComponentInParent<OutlineTarget>() == null)
                {
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.Warning, "Apresentação",
                        $"OutlineTarget ausente em '{st.name}'",
                        "Recomendado adicionar OutlineTarget para highlight visual ao mirar.", st, kind);
                }

                // 6. Validação de slotAnchors (pertencentes ao mesmo prefab, não nulos e dentro do alcance de interação)
                bool requiresOperationalSlots = kind == PizzeriaStationKind.Counter ||
                                                kind == PizzeriaStationKind.Prep ||
                                                kind == PizzeriaStationKind.Assembly ||
                                                kind == PizzeriaStationKind.Oven ||
                                                kind == PizzeriaStationKind.Packaging ||
                                                kind == PizzeriaStationKind.Staging ||
                                                kind == PizzeriaStationKind.ToolRack ||
                                                kind == PizzeriaStationKind.BoxStack;

                if (requiresOperationalSlots)
                {
                    if (st.slotAnchors == null || st.slotAnchors.Length == 0)
                    {
                        isStationValid = false;
                        AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Slots",
                            $"Estação '{st.name}' sem slotAnchors configurados",
                            "A estação precisa de pelo menos 1 slotAnchor válido.", st, kind);
                    }
                    else
                    {
                        Bounds stationBounds = PizzeriaHierarchyOrganizer.ComputeWorldBounds(st.transform, out bool hasMesh);
                        for (int s = 0; s < st.slotAnchors.Length; s++)
                        {
                            var slotT = st.slotAnchors[s];
                            if (slotT == null)
                            {
                                isStationValid = false;
                                AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Slots",
                                    $"Slot nulo (índice {s}) em '{st.name}'",
                                    $"O elemento slotAnchors[{s}] está nulo.", st, kind);
                                continue;
                            }

                            if (!slotT.IsChildOf(root.transform))
                            {
                                isStationValid = false;
                                AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Slots",
                                    $"Slot '{slotT.name}' fora da hierarquia do prefab em '{st.name}'",
                                    "O slotAnchor deve ser descendente da raiz do próprio prefab.", slotT, kind);
                            }

                            // Verificar se o slot está em posição alcançável em relação aos colliders da estação
                            Vector3 closestColliderPoint = stationBounds.ClosestPoint(slotT.position);
                            float distFromSurface = Vector3.Distance(closestColliderPoint, slotT.position);
                            if (distFromSurface > maxReach)
                            {
                                isStationValid = false;
                                AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Slots",
                                    $"Slot '{slotT.name}' fora do alcance de interação ({distFromSurface:F2}m > {maxReach:F2}m) em '{st.name}'",
                                    "Posicione o slotAnchor próximo à superfície frontal/superior da bancada.", slotT, kind);
                            }
                        }
                    }
                }

                // 7. Validações específicas por tipo de estação
                if (kind == PizzeriaStationKind.Prep)
                {
                    bool hasValidProcess = false;
                    if (config != null)
                    {
                        var def = config.stations?.FirstOrDefault(d => d != null && d.id == st.stationId)
                               ?? config.stationInstances?.FirstOrDefault(inst => inst != null && inst.instanceId == st.stationId)?.definition;
                        if (def != null)
                        {
                            var procs = def.GetEffectiveProcessIds();
                            hasValidProcess = procs != null && procs.Count > 0;
                        }
                    }
                    if (!hasValidProcess)
                    {
                        isStationValid = false;
                        AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Preparo",
                            $"Bancada de preparo '{st.name}' sem processos aceitos",
                            "O StationDef da bancada de preparo precisa aceitar pelo menos 1 ProcessDef válido.", st, kind);
                    }
                }
                else if (kind == PizzeriaStationKind.Pantry || kind == PizzeriaStationKind.Fridge)
                {
                    bool hasStockConfigured = false;
                    if (config != null && config.initialStorageSlots != null && config.initialStorageSlots.Count > 0)
                    {
                        hasStockConfigured = config.initialStorageSlots.Any(s =>
                            s != null && s.storageId == st.stationId && s.EffectiveIngredientId > 0 && s.capacity > 0);
                    }
                    else if (config != null)
                    {
                        // Fallback legado IDs 1 e 2 ou primários
                        int fridgeId = bindings != null ? bindings.ResolvePrimaryFridgeId(1) : 1;
                        int pantryId = bindings != null ? bindings.ResolvePrimaryPantryId(2) : 2;
                        if ((kind == PizzeriaStationKind.Fridge && st.stationId == fridgeId) ||
                            (kind == PizzeriaStationKind.Pantry && st.stationId == pantryId))
                        {
                            hasStockConfigured = true;
                        }
                    }

                    if (!hasStockConfigured)
                    {
                        isStationValid = false;
                        AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Estoque",
                            $"{(kind == PizzeriaStationKind.Fridge ? "Geladeira" : "Dispensa")} '{st.name}' (#{st.stationId}) sem ingredientes configurados",
                            "Configure pelo menos 1 ingrediente com capacidade > 0 em initialStorageSlots para este storageId.", st, kind);
                    }
                }
                else if (kind == PizzeriaStationKind.Phone && st is PhoneStation phone)
                {
                    var ringer = phone.ringer != null ? phone.ringer : (st.GetComponent<PhoneRinger>() ?? st.GetComponentInChildren<PhoneRinger>(true));
                    if (ringer == null || (ringer.audioSource == null && st.GetComponent<AudioSource>() == null))
                    {
                        isStationValid = false;
                        AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Telefone",
                            $"Telefone '{st.name}' com PhoneRinger/AudioSource incompleto",
                            "O telefone requer PhoneRinger vinculado ao PhoneStation e um AudioSource válido.", st, kind);
                    }
                }
                else if (kind == PizzeriaStationKind.TicketBoard && st is TicketBoardStation board)
                {
                    bool hasValidSlots = board.ticketSlots != null && board.ticketSlots.Length > 0 &&
                                         board.ticketSlots.All(t => t != null && t.GetComponentInChildren<TicketSheetView>(true) != null);
                    if (!hasValidSlots)
                    {
                        isStationValid = false;
                        AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Comandas",
                            $"Quadro de comandas '{st.name}' sem TicketSlots/TicketSheetView válidos",
                            "O TicketBoardStation precisa de ticketSlots preenchidos com TicketSheetView em cada slot.", st, kind);
                    }
                }
                else if (kind == PizzeriaStationKind.Receiving && st is ReceivingStation rec)
                {
                    var pizzeriaRoot = root.GetComponent<PizzeriaRoot>();
                    bool hasReceivingAnchor = (rec.slotAnchors != null && rec.slotAnchors.Length > 0 && rec.slotAnchors[0] != null) ||
                                              (pizzeriaRoot != null && pizzeriaRoot.receivingAnchor != null);
                    if (!hasReceivingAnchor)
                    {
                        isStationValid = false;
                        AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Logística",
                            $"Área de recebimento '{st.name}' sem slotAnchor/receivingAnchor",
                            "Configure um slotAnchor na ReceivingStation ou receivingAnchor no PizzeriaRoot.", st, kind);
                    }
                }
                else if (kind == PizzeriaStationKind.BoxStack && st is StackStation stk)
                {
                    if (stk.stackId == 0 || stk.capacity <= 0)
                    {
                        isStationValid = false;
                        AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Pilha",
                            $"Pilha de caixas '{st.name}' com stackId={stk.stackId} ou capacidade={stk.capacity} inválida",
                            "StackStation requer stackId > 0 único e capacity > 0.", st, kind);
                    }
                    else if (stackIdToStations.TryGetValue(stk.stackId, out var sList) && sList.Count > 1)
                    {
                        isStationValid = false;
                        AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Pilha",
                            $"stackId duplicado #{stk.stackId} em '{st.name}'",
                            "Cada StackStation deve ter um stackId único.", st, kind);
                    }
                }

                if (isStationValid && kind != PizzeriaStationKind.None && validStationsByKind.ContainsKey(kind))
                {
                    validStationsByKind[kind].Add(st);
                }
            }

            // Construir Tabela de Cobertura Mínima por Tipo (A)
            foreach (var desc in PizzeriaStationRecipeRegistry.AllRecipes)
            {
                var kind = desc.kind;
                int minReq = settings != null ? settings.GetMinimumRequiredForKind(kind) : 1;
                int total = stationsByKind[kind].Count;
                int valid = validStationsByKind[kind].Count;

                PizzeriaValidationSeverity rowStatus;
                string detail;
                if (valid >= minReq && total == valid)
                {
                    rowStatus = PizzeriaValidationSeverity.Ok;
                    detail = valid > 0 ? $"{valid} unidade(s) válida(s)" : "Opcional neste perfil";
                }
                else if (valid < minReq)
                {
                    rowStatus = PizzeriaValidationSeverity.BlockingError;
                    detail = $"Faltam {minReq - valid} unidade(s) válida(s) (encontradas: {total}, válidas: {valid})";
                    AddIssue(report.prefabIssues, report, PizzeriaValidationSeverity.BlockingError, "Cobertura Mínima",
                        $"Tipo faltante ou inválido: {desc.displayName}",
                        $"Necessário pelo menos {minReq} unidade(s) válida(s) de '{desc.displayName}', mas há {valid} válida(s) de {total} encontrada(s).",
                        stationsByKind[kind].FirstOrDefault(), kind);
                }
                else
                {
                    rowStatus = PizzeriaValidationSeverity.Warning;
                    detail = $"Cobertura mínima atendida ({valid}/{minReq}), mas há {total - valid} instância(s) com erro.";
                }

                report.coverageTable.Add(new PizzeriaStationCoverageRow
                {
                    kind = kind,
                    displayName = desc.displayName,
                    totalFound = total,
                    validCount = valid,
                    minimumRequired = minReq,
                    status = rowStatus,
                    statusDetail = detail,
                    instances = new List<StationView>(stationsByKind[kind])
                });
            }
        }

        /// <summary>
        /// Determina o tipo funcional de uma estação usando os metadados persistidos no PizzeriaAuthoringBindings
        /// ou configuração de estoque (Dispensa vs Geladeira), NUNCA dependendo apenas do nome do GameObject.
        /// </summary>
        public static PizzeriaStationKind ResolveStationKindForValidation(
            StationView st,
            PizzeriaAuthoringBindings bindings,
            PizzeriaConfig config)
        {
            if (st == null) return PizzeriaStationKind.None;

            if (bindings != null)
            {
                var rec = bindings.FindRecordByStation(st);
                if (rec != null && rec.stationKind != PizzeriaStationKind.None)
                    return rec.stationKind;
            }

            if (st is PrepStation) return PizzeriaStationKind.Prep;
            if (st is AssemblyStation) return PizzeriaStationKind.Assembly;
            if (st is OvenStation) return PizzeriaStationKind.Oven;
            if (st is PackagingStation) return PizzeriaStationKind.Packaging;
            if (st is StagingStation) return PizzeriaStationKind.Staging;
            if (st is ToolRackStation) return PizzeriaStationKind.ToolRack;
            if (st is PhoneStation) return PizzeriaStationKind.Phone;
            if (st is TicketBoardStation) return PizzeriaStationKind.TicketBoard;
            if (st is SupplierTerminal) return PizzeriaStationKind.SupplierTerminal;
            if (st is ReceivingStation) return PizzeriaStationKind.Receiving;
            if (st is TrashStation) return PizzeriaStationKind.Trash;
            if (st is StackStation) return PizzeriaStationKind.BoxStack;
            if (st is CounterStation) return PizzeriaStationKind.Counter;

            if (st is StorageStation storage)
            {
                if (bindings != null)
                {
                    if (bindings.primaryFridgeStation == storage) return PizzeriaStationKind.Fridge;
                    if (bindings.primaryPantryStation == storage) return PizzeriaStationKind.Pantry;
                }

                // Inspecionar ingredientes configurados para este storageId no PizzeriaConfig
                if (config != null && config.initialStorageSlots != null)
                {
                    var slotsForStation = config.initialStorageSlots.Where(s => s != null && s.storageId == st.stationId).ToList();
                    if (slotsForStation.Count > 0)
                    {
                        bool hasDoughOnly = slotsForStation.All(s => s.EffectiveIngredientId == 1);
                        if (hasDoughOnly) return PizzeriaStationKind.Pantry;
                        return PizzeriaStationKind.Fridge;
                    }
                }

                if (st.stationId == 1) return PizzeriaStationKind.Fridge;
                if (st.stationId == 2) return PizzeriaStationKind.Pantry;
                return PizzeriaStationKind.Pantry;
            }

            return PizzeriaStationKind.None;
        }

        private static void ValidateStructuralHierarchyIntegrity(
            GameObject root,
            PizzeriaGameplayValidationReport report)
        {
            var identities = root.GetComponentsInChildren<PizzeriaNodeIdentity>(true);
            foreach (var id in identities)
            {
                if (id == null) continue;

                if (id.pendingReview)
                {
                    string norm = PizzeriaHierarchyOrganizer.NormalizeNodeNameForMatch(id.originalNodeName ?? id.name, out _);
                    // Se for uma porta ou tampa solta não associada, é um aviso/erro estrutural importante
                    bool isOrphanDoorOrLid = norm.StartsWith("porta") || norm.StartsWith("tampa") || norm.StartsWith("gaveta");
                    var sev = isOrphanDoorOrLid ? PizzeriaValidationSeverity.Warning : PizzeriaValidationSeverity.Warning;

                    AddIssue(report.prefabIssues, report, sev, "Hierarquia / Revisar",
                        $"Objeto pendente em Revisar: '{id.name}'",
                        string.IsNullOrEmpty(id.reviewReason)
                            ? "Objeto aguarda revisão ou fixação manual de categoria/pai."
                            : id.reviewReason,
                        id.gameObject);
                }
            }
        }

        private static void ValidateGameplayCycleCompleteness(
            GameObject root,
            StationView[] allStations,
            PizzeriaAuthoringBindings bindings,
            PizzeriaRoot pizzeriaRoot,
            PizzeriaFootprint footprint,
            PizzeriaItemVisualizer itemVisualizer,
            PizzeriaConfig config,
            PizzeriaAuthoringSettings settings,
            PizzeriaGameplayValidationReport report)
        {
            bool isFullProfile = settings == null || settings.validationProfile == PizzeriaValidationProfileMode.FullPizzeria;
            if (!isFullProfile && (config == null || allStations.Length == 0))
                return;

            if (config == null)
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Ciclo de Gameplay",
                    "Impossível validar cadeia produtiva sem PizzeriaConfig",
                    "Atribua um PizzeriaConfig válido na Etapa 2.", root);
                return;
            }

            DefinitionCatalog catalog = config.BuildCatalog();

            // 1. Verificar Receitas Ativas
            if (catalog.Recipes.Count == 0)
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Ciclo de Gameplay",
                    "Nenhuma receita ativa no catálogo",
                    "O PizzeriaConfig precisa ter pelo menos 1 RecipeDef válida.", config);
                return;
            }

            // 2. Detectar cadeias de processos circulares ou quebradas
            foreach (var proc in catalog.Processes.Values)
            {
                if (proc.InputIngredientId == proc.OutputIngredientId && proc.InputStage == proc.OutputStage)
                {
                    AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Ciclo (Processos)",
                        $"Processo circular detectado: '{proc.Name}' (#{proc.Id})",
                        $"O processo #{proc.Id} tem a mesma entrada e saída (ingrediente {proc.InputIngredientId}, estágio {proc.InputStage}).", config);
                }
            }

            // Coletar quais ingredientes estão disponíveis nas dispensas e geladeiras ativas do prefab
            var availableIngredientIdsInPrefabStorages = new HashSet<int>();
            var activeStorages = allStations.OfType<StorageStation>().Where(s => s != null && s.gameObject.activeInHierarchy && s.enabled).ToList();

            if (config.initialStorageSlots != null && config.initialStorageSlots.Count > 0)
            {
                foreach (var slotCfg in config.initialStorageSlots)
                {
                    if (slotCfg == null || slotCfg.capacity <= 0 || slotCfg.initialStock <= 0) continue;
                    if (activeStorages.Any(st => st.stationId == slotCfg.storageId))
                    {
                        availableIngredientIdsInPrefabStorages.Add(slotCfg.EffectiveIngredientId);
                    }
                }
            }
            else
            {
                int fridgeId = bindings != null ? bindings.ResolvePrimaryFridgeId(1) : 1;
                int pantryId = bindings != null ? bindings.ResolvePrimaryPantryId(2) : 2;
                if (activeStorages.Any(s => s.stationId == fridgeId))
                {
                    if (config.initialSauceStock > 0) availableIngredientIdsInPrefabStorages.Add(3);
                    if (config.initialCheeseStock > 0) availableIngredientIdsInPrefabStorages.Add(4);
                    if (config.initialCalabresaStock > 0) availableIngredientIdsInPrefabStorages.Add(6);
                    if (config.initialMushroomStock > 0) availableIngredientIdsInPrefabStorages.Add(8);
                }
                if (activeStorages.Any(s => s.stationId == pantryId))
                {
                    if (config.initialDoughStock > 0) availableIngredientIdsInPrefabStorages.Add(1);
                }
            }

            // Coletar processos aceitos pelas PrepStations ativas no prefab
            var supportedProcessIdsInPrefab = new HashSet<int>();
            var activePreps = allStations.OfType<PrepStation>().Where(p => p != null && p.gameObject.activeInHierarchy && p.enabled).ToList();
            foreach (var prep in activePreps)
            {
                if (catalog.Stations.TryGetValue(prep.stationId, out var stData) && stData.AcceptedProcessIds != null)
                {
                    foreach (int pid in stData.AcceptedProcessIds)
                        supportedProcessIdsInPrefab.Add(pid);
                }
            }

            // Coletar capacidades de ferramentas disponíveis no ToolRack ativo do prefab
            var availableToolCapabilities = new HashSet<ToolCapability> { ToolCapability.None };
            var activeToolRacks = allStations.OfType<ToolRackStation>().Where(r => r != null && r.gameObject.activeInHierarchy && r.enabled).ToList();
            int totalInitialToolsCount = 0;
            foreach (var tool in catalog.Tools.Values)
            {
                if (tool.InitialCount > 0)
                {
                    totalInitialToolsCount += tool.InitialCount;
                    if (activeToolRacks.Count > 0)
                    {
                        availableToolCapabilities.Add(tool.Capabilities);
                    }
                }
            }

            // Verificar se o ToolRack primário comporta a contagem inicial de ferramentas
            if (activeToolRacks.Count > 0)
            {
                int primaryRackId = bindings != null ? bindings.ResolvePrimaryToolRackId(3) : activeToolRacks[0].stationId;
                var primaryRack = activeToolRacks.FirstOrDefault(r => r.stationId == primaryRackId) ?? activeToolRacks[0];
                int rackSlots = primaryRack.slotAnchors != null ? primaryRack.slotAnchors.Length : 0;
                if (rackSlots < totalInitialToolsCount)
                {
                    AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Ciclo (Utensílios)",
                        $"Suporte de utensílios '{primaryRack.name}' tem menos slots ({rackSlots}) que o total de utensílios iniciais ({totalInitialToolsCount})",
                        $"Adicione pelo menos {totalInitialToolsCount} slots ao ToolRackStation para comportar todos os utensílios iniciais.",
                        primaryRack, PizzeriaStationKind.ToolRack);
                }
            }

            // 3. Validar cada Receita Ativa de ponta a ponta
            foreach (var recipe in catalog.Recipes.Values)
            {
                ValidateSingleRecipeChain(
                    recipe,
                    catalog,
                    availableIngredientIdsInPrefabStorages,
                    supportedProcessIdsInPrefab,
                    availableToolCapabilities,
                    config,
                    report);
            }

            // 4. Validar estações de fluxo (Montagem -> Forno -> Embalagem -> Retirada / Pilha / Mochila -> Entrega)
            bool hasAssembly = allStations.OfType<AssemblyStation>().Any(s => s != null && s.gameObject.activeInHierarchy && s.enabled);
            bool hasOven = allStations.OfType<OvenStation>().Any(s => s != null && s.gameObject.activeInHierarchy && s.enabled);
            bool hasPackaging = allStations.OfType<PackagingStation>().Any(s => s != null && s.gameObject.activeInHierarchy && s.enabled);
            bool hasStaging = allStations.OfType<StagingStation>().Any(s => s != null && s.gameObject.activeInHierarchy && s.enabled);
            bool hasPhone = allStations.OfType<PhoneStation>().Any(s => s != null && s.gameObject.activeInHierarchy && s.enabled);
            bool hasTicketBoard = allStations.OfType<TicketBoardStation>().Any(s => s != null && s.gameObject.activeInHierarchy && s.enabled);

            if (!hasPhone || !hasTicketBoard || !hasAssembly || !hasOven || !hasPackaging || !hasStaging)
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Ciclo de Gameplay",
                    "Fluxo principal de produção incompleto no prefab",
                    $"O ciclo exige Telefone ({hasPhone}), Quadro de Comandas ({hasTicketBoard}), Montagem ({hasAssembly}), Forno ({hasOven}), Embalagem ({hasPackaging}) e Retirada ({hasStaging}).",
                    root);
            }
            else
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.Ok, "Ciclo de Gameplay",
                    "Fluxo principal de estações (Pedido -> Montagem -> Forno -> Embalagem -> Retirada) presente",
                    "Todas as etapas operacionais do ciclo possuem estações ativas no prefab.", root);
            }

            // 5. Validar Mochila Térmica e Apresentação de Itens Críticos
            if (config.backpackInitialSlots <= 0)
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Ciclo (Entrega)",
                    "Capacidade inicial da mochila inválida (backpackInitialSlots <= 0)",
                    "O PizzeriaConfig precisa ter backpackInitialSlots >= 1 para entregas com mochila.", config);
            }

            if (itemVisualizer != null && itemVisualizer.catalog != null)
            {
                var cat = itemVisualizer.catalog;
                var boxProf = cat.FindProfile(ItemCategory.Box, 0, false, true);
                var pizzaProf = cat.FindProfile(ItemCategory.Pizza, 2, false, false);
                if ((boxProf == null || boxProf.visualPrefab == null) && itemVisualizer.pizzaBoxPrefab == null)
                {
                    AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.Warning, "Apresentação",
                        "Visual de caixa de pizza não configurado no catálogo nem no fallback",
                        "Configure o visual de caixa na Etapa 3 ou no ItemPresentationCatalog.", itemVisualizer);
                }
                if ((pizzaProf == null || pizzaProf.visualPrefab == null) && itemVisualizer.pizzaBasePrefab == null)
                {
                    AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.Warning, "Apresentação",
                        "Visual de pizza/forma não configurado no catálogo nem no fallback",
                        "Configure o visual de pizza/forma na Etapa 3 ou no ItemPresentationCatalog.", itemVisualizer);
                }
            }

            // 6. Validar Âncoras e Footprint
            ValidateAnchorsAndFootprint(root, pizzeriaRoot, footprint, bindings, report);
        }

        private static void ValidateSingleRecipeChain(
            RecipeData recipe,
            DefinitionCatalog catalog,
            HashSet<int> availableRawIngredients,
            HashSet<int> supportedProcesses,
            HashSet<ToolCapability> availableToolCapabilities,
            PizzeriaConfig config,
            PizzeriaGameplayValidationReport report)
        {
            // Verificar BaseIngredientId (ex: Massa Aberta = 2)
            if (!CanProduceIngredientStage(
                recipe.BaseIngredientId,
                ProcessingStage.Prepared,
                catalog,
                availableRawIngredients,
                supportedProcesses,
                availableToolCapabilities,
                out string baseFailReason))
            {
                // Também verificar se a base já é fornecida diretamente como Raw ou Prepared
                if (!CanProduceIngredientStage(
                    recipe.BaseIngredientId,
                    ProcessingStage.Raw,
                    catalog,
                    availableRawIngredients,
                    supportedProcesses,
                    availableToolCapabilities,
                    out _))
                {
                    AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, $"Receita: {recipe.Name}",
                        $"Base da receita '{recipe.Name}' (ID {recipe.BaseIngredientId}) não pode ser produzida",
                        baseFailReason, config);
                }
            }

            // Verificar cada ingrediente requerido pela receita
            if (recipe.Ingredients != null)
            {
                foreach (var req in recipe.Ingredients)
                {
                    if (!CanProduceIngredientStage(
                        req.IngredientDefId,
                        req.Stage,
                        catalog,
                        availableRawIngredients,
                        supportedProcesses,
                        availableToolCapabilities,
                        out string ingFailReason))
                    {
                        string ingName = catalog.Ingredients.TryGetValue(req.IngredientDefId, out var ingData)
                            ? ingData.Name
                            : $"ID {req.IngredientDefId}";
                        AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, $"Receita: {recipe.Name}",
                            $"Ingrediente '{ingName}' ({req.Stage}) da receita '{recipe.Name}' indisponível no ciclo",
                            ingFailReason, config);
                    }
                }
            }
        }

        private static bool CanProduceIngredientStage(
            int targetIngredientId,
            ProcessingStage targetStage,
            DefinitionCatalog catalog,
            HashSet<int> availableRawIngredients,
            HashSet<int> supportedProcesses,
            HashSet<ToolCapability> availableToolCapabilities,
            out string failureReason,
            HashSet<(int, ProcessingStage)> visited = null)
        {
            failureReason = string.Empty;

            // Caso 1: É um ingrediente bruto disponível diretamente em alguma Dispensa/Geladeira ativa
            if (targetStage == ProcessingStage.Raw && availableRawIngredients.Contains(targetIngredientId))
                return true;

            // Alguns ingredientes transformados (ex: Massa Aberta ID 2, Queijo Ralado ID 5, Calabresa Fatiada ID 7)
            // também podem estar estocados diretamente se o usuário configurou assim
            if (availableRawIngredients.Contains(targetIngredientId) && catalog.Ingredients.TryGetValue(targetIngredientId, out var directIng) && !directIng.RequiresPrep)
                return true;

            if (visited == null)
                visited = new HashSet<(int, ProcessingStage)>();

            if (!visited.Add((targetIngredientId, targetStage)))
            {
                failureReason = $"Dependência circular detectada ao tentar produzir o ingrediente ID {targetIngredientId}.";
                return false;
            }

            // Procurar processo no catálogo que produza targetIngredientId (no targetStage ou Prepared)
            var matchingProcs = catalog.Processes.Values.Where(p =>
                p.OutputIngredientId == targetIngredientId &&
                (p.OutputStage == targetStage || p.OutputStage == ProcessingStage.Prepared)).ToList();

            if (matchingProcs.Count == 0)
            {
                string ingName = catalog.Ingredients.TryGetValue(targetIngredientId, out var d) ? d.Name : $"#{targetIngredientId}";
                failureReason = $"O ingrediente '{ingName}' (ID {targetIngredientId}) não está disponível em nenhuma Dispensa/Geladeira com estoque > 0 e não possui processo de produção.";
                return false;
            }

            var reasons = new List<string>();
            foreach (var proc in matchingProcs)
            {
                if (!supportedProcesses.Contains(proc.Id))
                {
                    reasons.Add($"O processo '{proc.Name}' (#{proc.Id}) não é aceito por nenhuma Bancada de Preparo ativa no prefab.");
                    continue;
                }

                if (proc.RequiredCapability != ToolCapability.None)
                {
                    bool hasCap = availableToolCapabilities.Any(cap => cap.HasFlag(proc.RequiredCapability));
                    if (!hasCap)
                    {
                        reasons.Add($"O processo '{proc.Name}' exige ferramenta com capacidade '{proc.RequiredCapability}', mas não há suporte de utensílios ativo ou ferramenta inicial configurada.");
                        continue;
                    }
                }

                if (CanProduceIngredientStage(
                    proc.InputIngredientId,
                    proc.InputStage,
                    catalog,
                    availableRawIngredients,
                    supportedProcesses,
                    availableToolCapabilities,
                    out string inputReason,
                    visited))
                {
                    return true;
                }
                else
                {
                    reasons.Add(inputReason);
                }
            }

            failureReason = string.Join(" ", reasons.Distinct());
            return false;
        }

        private static void ValidateAnchorsAndFootprint(
            GameObject root,
            PizzeriaRoot pizzeriaRoot,
            PizzeriaFootprint footprint,
            PizzeriaAuthoringBindings bindings,
            PizzeriaGameplayValidationReport report)
        {
            if (pizzeriaRoot == null) return;

            if (pizzeriaRoot.entranceAnchor == null)
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Âncoras",
                    "EntranceAnchor ausente",
                    "PizzeriaRoot.entranceAnchor não está atribuído.", pizzeriaRoot);
            }
            if (pizzeriaRoot.receivingAnchor == null)
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Âncoras",
                    "ReceivingAnchor ausente",
                    "PizzeriaRoot.receivingAnchor não está atribuído.", pizzeriaRoot);
            }
            if (pizzeriaRoot.recoveryAnchor == null)
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Âncoras",
                    "RecoveryAnchor ausente",
                    "PizzeriaRoot.recoveryAnchor não está atribuído.", pizzeriaRoot);
            }

            int validSpawns = pizzeriaRoot.playerSpawnAnchors != null
                ? pizzeriaRoot.playerSpawnAnchors.Count(t => t != null)
                : 0;
            if (validSpawns < 4)
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Âncoras",
                    $"PlayerSpawns insuficientes ({validSpawns}/4)",
                    "O PizzeriaRoot requer pelo menos 4 pontos de spawn (PlayerSpawn_0 a PlayerSpawn_3).", pizzeriaRoot);
            }

            if (bindings != null && !bindings.anchorsVerifiedByUser &&
                (bindings.entranceAnchorProvisional || bindings.receivingAnchorProvisional || bindings.recoveryAnchorProvisional || bindings.playerSpawnsProvisional))
            {
                AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.Warning, "Âncoras",
                    "Âncoras automáticas ainda marcadas como provisórias",
                    "Verifique as posições de EntranceAnchor, ReceivingAnchor, RecoveryAnchor e PlayerSpawns na cena/prefab e clique em 'Marcar âncoras como verificadas' na Etapa 2.",
                    bindings);
            }

            if (footprint != null)
            {
                if (footprint.footprintSize.x < 4f || footprint.footprintSize.y < 4f)
                {
                    AddIssue(report.cycleIssues, report, PizzeriaValidationSeverity.BlockingError, "Footprint",
                        $"Dimensões do PizzeriaFootprint inválidas ({footprint.footprintSize.x:F1}m x {footprint.footprintSize.y:F1}m)",
                        "As dimensões footprintSize do PizzeriaFootprint devem cobrir a área da pizzaria.", footprint);
                }
            }
        }

        private static void ValidateSceneDependencies(
            GameObject targetPrefabAsset,
            PizzeriaGameplayValidationReport report)
        {
            bool sceneOk = true;

            // 1. DefaultNetworkPrefabs.asset
            var netPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (netPrefabs == null)
            {
                sceneOk = false;
                AddIssue(report.sceneDependencyIssues, report, PizzeriaValidationSeverity.Warning, "Dependências de Cena/Rede",
                    "DefaultNetworkPrefabs.asset não encontrado",
                    "O arquivo Assets/DefaultNetworkPrefabs.asset não foi localizado.", null);
            }
            else if (targetPrefabAsset != null && EditorUtility.IsPersistent(targetPrefabAsset))
            {
                bool isRegistered = netPrefabs.PrefabList != null && netPrefabs.PrefabList.Any(p => p != null && p.Prefab == targetPrefabAsset);
                if (!isRegistered)
                {
                    sceneOk = false;
                    AddIssue(report.sceneDependencyIssues, report, PizzeriaValidationSeverity.Warning, "Dependências de Cena/Rede",
                        $"Prefab '{targetPrefabAsset.name}' ainda não registrado no DefaultNetworkPrefabs.asset",
                        "Clique em 'Usar esta pizzaria no jogo' na aba 4 para registrar o prefab na rede e atualizar geradores/spawners.",
                        netPrefabs);
                }
                else
                {
                    AddIssue(report.sceneDependencyIssues, report, PizzeriaValidationSeverity.Ok, "Dependências de Cena/Rede",
                        $"Prefab '{targetPrefabAsset.name}' registrado em DefaultNetworkPrefabs.asset",
                        "Registro de rede Netcode confirmado.", netPrefabs);
                }
            }

            // 2. Verificar cena aberta (se não estivermos apenas em uma cena vazia de teste)
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
            {
                var cityGen = UnityEngine.Object.FindFirstObjectByType<CityGenerator>();
                var spawner = UnityEngine.Object.FindFirstObjectByType<PizzeriaSpawner>();
                var netMgr = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();

                if (cityGen == null && spawner == null)
                {
                    AddIssue(report.sceneDependencyIssues, report, PizzeriaValidationSeverity.NotVerifiedInEditMode, "Dependências de Cena",
                        $"Cena atual '{activeScene.name}' não contém CityGenerator nem PizzeriaSpawner",
                        "Abra a GameScene para verificar integração com a cidade ou use 'Usar esta pizzaria no jogo'.", null);
                }
                else
                {
                    if (cityGen != null && targetPrefabAsset != null && EditorUtility.IsPersistent(targetPrefabAsset) && cityGen.pizzeriaPrefab != targetPrefabAsset)
                    {
                        sceneOk = false;
                        AddIssue(report.sceneDependencyIssues, report, PizzeriaValidationSeverity.Warning, "Dependências de Cena",
                            $"CityGenerator aponta para outro prefab ('{(cityGen.pizzeriaPrefab != null ? cityGen.pizzeriaPrefab.name : "null")}')",
                            "Clique em 'Usar esta pizzaria no jogo' para atualizar CityGenerator.pizzeriaPrefab.", cityGen);
                    }

                    if (spawner != null && targetPrefabAsset != null && EditorUtility.IsPersistent(targetPrefabAsset))
                    {
                        var soSpawner = new SerializedObject(spawner);
                        var prop = soSpawner.FindProperty("_pizzeriaPrefab");
                        if (prop != null && prop.objectReferenceValue != targetPrefabAsset)
                        {
                            sceneOk = false;
                            AddIssue(report.sceneDependencyIssues, report, PizzeriaValidationSeverity.Warning, "Dependências de Cena",
                                $"PizzeriaSpawner aponta para outro prefab ('{(prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null")}')",
                                "Clique em 'Usar esta pizzaria no jogo' para atualizar PizzeriaSpawner.", spawner);
                        }
                    }
                }

                if (netMgr == null)
                {
                    AddIssue(report.sceneDependencyIssues, report, PizzeriaValidationSeverity.NotVerifiedInEditMode, "Dependências de Cena",
                        "NetworkManager não encontrado na cena aberta",
                        "O NetworkManager pode residir na cena de menu/bootstrap ou na GameScene.", null);
                }
            }

            report.areSceneDependenciesReady = sceneOk;
        }

        private static void AddIssue(
            List<PizzeriaValidationIssue> targetList,
            PizzeriaGameplayValidationReport report,
            PizzeriaValidationSeverity severity,
            string category,
            string title,
            string description,
            UnityEngine.Object targetObj,
            PizzeriaStationKind relatedKind = PizzeriaStationKind.None)
        {
            targetList.Add(new PizzeriaValidationIssue
            {
                severity = severity,
                category = category,
                title = title,
                description = description,
                targetObject = targetObj,
                relatedStationKind = relatedKind
            });
        }

        private static void FinalizeReportSummary(PizzeriaGameplayValidationReport report)
        {
            report.blockingErrorsCount = 0;
            report.warningsCount = 0;
            report.okCount = 0;

            foreach (var issue in report.prefabIssues.Concat(report.cycleIssues))
            {
                if (issue.severity == PizzeriaValidationSeverity.BlockingError) report.blockingErrorsCount++;
                else if (issue.severity == PizzeriaValidationSeverity.Warning) report.warningsCount++;
                else if (issue.severity == PizzeriaValidationSeverity.Ok) report.okCount++;
            }

            report.isPrefabReadyForGameplay = report.blockingErrorsCount == 0 &&
                                              report.coverageTable.Count > 0 &&
                                              report.coverageTable.All(r => r.status != PizzeriaValidationSeverity.BlockingError);
        }

        /// <summary>
        /// Comando explícito "Usar esta pizzaria no jogo":
        /// Atualiza CityGenerator.pizzeriaPrefab, PizzeriaSpawner._pizzeriaPrefab e DefaultNetworkPrefabs.asset
        /// mesmo quando as referências anteriores não são nulas, preservando o GUID do asset e relatando tudo o que foi alterado.
        /// </summary>
        public static List<string> ApplyPrefabToGameIntegration(
            GameObject targetPrefabAsset,
            bool updateGameSceneIfClosed = true)
        {
            var changes = new List<string>();
            if (targetPrefabAsset == null || !EditorUtility.IsPersistent(targetPrefabAsset))
            {
                changes.Add("Erro: O prefab-alvo precisa ser um asset salvo no projeto antes de integrá-lo ao jogo.");
                return changes;
            }

            string prefabPath = AssetDatabase.GetAssetPath(targetPrefabAsset);
            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);

            // 1. Registrar em DefaultNetworkPrefabs.asset
            var netPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (netPrefabs != null)
            {
                bool alreadyPresent = netPrefabs.PrefabList != null && netPrefabs.PrefabList.Any(p => p != null && p.Prefab == targetPrefabAsset);
                if (!alreadyPresent)
                {
                    Undo.RecordObject(netPrefabs, "Registrar Pizzaria em DefaultNetworkPrefabs");
                    netPrefabs.Add(new NetworkPrefab { Prefab = targetPrefabAsset });
                    EditorUtility.SetDirty(netPrefabs);
                    changes.Add($"Registrado '{targetPrefabAsset.name}' (GUID {prefabGuid}) em Assets/DefaultNetworkPrefabs.asset.");
                }
                else
                {
                    changes.Add($"'{targetPrefabAsset.name}' já estava registrado em Assets/DefaultNetworkPrefabs.asset.");
                }
            }

            // 2. Atualizar CityGenerator e PizzeriaSpawner nas cenas abertas
            int updatedInOpenScenes = UpdateGeneratorsAndSpawnersInLoadedScenes(targetPrefabAsset, changes);

            // 3. Se GameScene.unity não estiver aberta e updateGameSceneIfClosed for verdadeiro, atualizar se o usuário estiver integrando ao projeto
            string gameScenePath = "Assets/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
            var activeScene = SceneManager.GetActiveScene();
            if (updateGameSceneIfClosed && updatedInOpenScenes == 0 && File.Exists(gameScenePath) &&
                !string.Equals(activeScene.path, gameScenePath, StringComparison.OrdinalIgnoreCase))
            {
                var openedScene = EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Additive);
                try
                {
                    UpdateGeneratorsAndSpawnersInLoadedScenes(targetPrefabAsset, changes);
                    EditorSceneManager.SaveScene(openedScene);
                }
                finally
                {
                    EditorSceneManager.CloseScene(openedScene, true);
                }
            }

            AssetDatabase.SaveAssets();
            return changes;
        }

        private static int UpdateGeneratorsAndSpawnersInLoadedScenes(GameObject targetPrefabAsset, List<string> changes)
        {
            int count = 0;

            var generators = UnityEngine.Object.FindObjectsByType<CityGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var gen in generators)
            {
                if (gen == null) continue;
                Undo.RecordObject(gen, "Atualizar CityGenerator.pizzeriaPrefab");
                string oldName = gen.pizzeriaPrefab != null ? gen.pizzeriaPrefab.name : "null";
                gen.pizzeriaPrefab = targetPrefabAsset;
                EditorUtility.SetDirty(gen);
                if (gen.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
                changes.Add($"CityGenerator '{gen.name}' (cena '{gen.gameObject.scene.name}'): pizzeriaPrefab alterado de '{oldName}' para '{targetPrefabAsset.name}'.");
                count++;
            }

            var spawners = UnityEngine.Object.FindObjectsByType<PizzeriaSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                if (spawner == null) continue;
                var so = new SerializedObject(spawner);
                var prop = so.FindProperty("_pizzeriaPrefab");
                if (prop != null)
                {
                    string oldName = prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                    prop.objectReferenceValue = targetPrefabAsset;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(spawner);
                    if (spawner.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
                    changes.Add($"PizzeriaSpawner '{spawner.name}' (cena '{spawner.gameObject.scene.name}'): _pizzeriaPrefab alterado de '{oldName}' para '{targetPrefabAsset.name}'.");
                    count++;
                }
            }

            return count;
        }
    }
}

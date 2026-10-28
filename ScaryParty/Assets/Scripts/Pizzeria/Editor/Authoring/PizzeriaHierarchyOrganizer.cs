using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public class PizzeriaHierarchySummaryReport
    {
        public int totalAnalyzedNodes;
        public int emptyMeshNodesCount;
        public List<string> emptyMeshNodeNames = new List<string>();
        public Dictionary<PizzeriaHierarchyCategory, int> countsByCategory = new Dictionary<PizzeriaHierarchyCategory, int>();
        public Dictionary<string, int> countsByRoom = new Dictionary<string, int>();
        public List<string> assembledUnits = new List<string>();
        public List<string> surfaceParentedItems = new List<string>();
        public List<string> reviewItems = new List<string>();
        public List<string> transformWarnings = new List<string>();
        public int preservedManualLocks;
    }

    public static class PizzeriaHierarchyOrganizer
    {
        private static readonly string[] ManagedGroupRoots = new[]
        {
            "Comodo_01_Salao",
            "Comodo_02_Cozinha",
            "Comodo_03_Logistica",
            "Arquitetura",
            "Equipamentos",
            "Mobiliario",
            "Utensilios",
            "Decoracao",
            "Revisar",
            "Anchors",
            "Model"
        };

        /// <summary>
        /// Normaliza um nome de nó para comparação determinística:
        /// remove acentos, converte para minúsculas, remove sufixos numéricos (.001, _01, (1)) e caracteres não alfanuméricos.
        /// Nomes originais nos GameObjects são SEMPRE preservados.
        /// </summary>
        public static string NormalizeNodeNameForMatch(string rawName, out int numericSuffix)
        {
            numericSuffix = -1;
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            string trimmed = rawName.Trim();

            // Extrair sufixo numérico final se existir (ex: .001, _02, (1))
            var match = Regex.Match(trimmed, @"(?:[\._\-\s]|\()(\d+)\)?$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedNum))
            {
                numericSuffix = parsedNum;
                trimmed = trimmed.Substring(0, match.Index);
            }

            // Remover diacríticos (acentos)
            string normalizedD = trimmed.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalizedD.Length);
            for (int i = 0; i < normalizedD.Length; i++)
            {
                char c = normalizedD[i];
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        public static Bounds ComputeWorldBounds(Transform t, out bool hasValidMesh)
        {
            hasValidMesh = false;
            if (t == null) return new Bounds(Vector3.zero, Vector3.zero);

            var renderers = t.GetComponentsInChildren<Renderer>(true);
            Bounds combined = default;
            bool initialized = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && (mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0))
                    continue;

                hasValidMesh = true;
                if (!initialized)
                {
                    combined = r.bounds;
                    initialized = true;
                }
                else
                {
                    combined.Encapsulate(r.bounds);
                }
            }

            if (!initialized)
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0)
                {
                    hasValidMesh = true;
                    var localB = mf.sharedMesh.bounds;
                    Vector3 worldCenter = t.TransformPoint(localB.center);
                    Vector3 worldSize = Vector3.Scale(localB.size, AbsVector(t.lossyScale));
                    return new Bounds(worldCenter, worldSize);
                }
                return new Bounds(t.position, Vector3.zero);
            }

            return combined;
        }

        /// <summary>
        /// Calcula o bounds apenas da própria mesh do Transform (sem somar filhos), útil para testar apoio sobre bancadas/mesas.
        /// </summary>
        public static Bounds ComputeSelfWorldBounds(Transform t)
        {
            if (t == null) return new Bounds(Vector3.zero, Vector3.zero);
            var r = t.GetComponent<Renderer>();
            if (r != null) return r.bounds;
            return ComputeWorldBounds(t, out _);
        }

        private static Vector3 AbsVector(Vector3 v)
            => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        public static float ComputeSurfaceDistanceBetweenBounds(Bounds a, Bounds b)
        {
            Vector3 closestOnA = a.ClosestPoint(b.center);
            Vector3 closestOnB = b.ClosestPoint(closestOnA);
            closestOnA = a.ClosestPoint(closestOnB);
            return Vector3.Distance(closestOnA, closestOnB);
        }

        public static bool HasRigOrAnimationProtection(Transform root, Transform node)
        {
            if (node == null) return false;
            if (node.GetComponent<SkinnedMeshRenderer>() != null || node.GetComponent<Animator>() != null || node.GetComponent<Animation>() != null)
                return true;

            Transform curr = node.parent;
            while (curr != null && curr != root)
            {
                var anim = curr.GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null) return true;
                var legacyAnim = curr.GetComponent<Animation>();
                if (legacyAnim != null && legacyAnim.GetClipCount() > 0) return true;
                curr = curr.parent;
            }
            return false;
        }

        public static bool SafeReparentPreservingWorldTransform(
            Transform child,
            Transform newParent,
            out string warningMessage)
        {
            warningMessage = null;
            if (child == null || newParent == null || child.parent == newParent)
                return true;

            Vector3 oldPos = child.position;
            Quaternion oldRot = child.rotation;
            Vector3 oldLossy = child.lossyScale;
            Bounds oldBounds = ComputeSelfWorldBounds(child);

            Vector3 parentScale = newParent.lossyScale;
            bool parentNonUniform = Mathf.Abs(parentScale.x - parentScale.y) > 0.001f ||
                                    Mathf.Abs(parentScale.y - parentScale.z) > 0.001f ||
                                    parentScale.x < 0f || parentScale.y < 0f || parentScale.z < 0f;

            if (parentNonUniform && Quaternion.Angle(oldRot, newParent.rotation) > 0.5f)
            {
                warningMessage = $"Nó '{child.name}' não pôde ser anexado diretamente a '{newParent.name}' porque o pai possui escala não-uniforme/negativa ({parentScale}) com rotação divergente.";
                return false;
            }

            Undo.SetTransformParent(child, newParent, "Organizar Hierarquia da Pizzaria");
            child.position = oldPos;
            child.rotation = oldRot;

            if (Mathf.Abs(parentScale.x) > 0.0001f && Mathf.Abs(parentScale.y) > 0.0001f && Mathf.Abs(parentScale.z) > 0.0001f)
            {
                child.localScale = new Vector3(
                    oldLossy.x / parentScale.x,
                    oldLossy.y / parentScale.y,
                    oldLossy.z / parentScale.z);
            }

            if (oldBounds.size.sqrMagnitude > 0.0001f)
            {
                Bounds newBounds = ComputeSelfWorldBounds(child);
                float posDelta = Vector3.Distance(oldBounds.center, newBounds.center);
                float sizeDelta = Vector3.Distance(oldBounds.size, newBounds.size);
                if (posDelta > 0.005f || sizeDelta > 0.005f)
                {
                    warningMessage = $"Pequena variação geométrica detectada em '{child.name}' (delta centro={posDelta:F4}m, tamanho={sizeDelta:F4}m).";
                }
            }

            return true;
        }

        private class CandidateNodeInfo
        {
            public Transform transform;
            public PizzeriaNodeIdentity identity;
            public string rawName;
            public string normalizedName;
            public int suffixNumber;
            public Bounds bounds;
            public bool hasValidMesh;
            public bool isEmptyMesh;
            public PizzeriaHierarchyCategory assignedCategory = PizzeriaHierarchyCategory.Unclassified;
            public string assignedRoomName;
            public Transform unitRootOrSelf;
            public bool consumedInAssembly;
        }

        /// <summary>
        /// Determina a qual cômodo pertence uma posição local da raiz.
        /// Paredes divisórias exatamente na fronteira ficam em apenas 1 cômodo (avaliado por ordem de prioridade).
        /// </summary>
        public static string ResolveRoomNameForLocalPosition(Vector3 rootLocalPos, PizzeriaAuthoringSettings settings)
        {
            if (settings == null || !settings.groupByRooms || settings.roomRegions == null || settings.roomRegions.Count == 0)
                return null;

            var sortedRooms = settings.roomRegions
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.roomName))
                .OrderBy(r => r.priority)
                .ToList();

            if (sortedRooms.Count == 0)
                return null;

            // 1. Verificar se está estritamente contido nos limites XZ de algum cômodo (na ordem de prioridade, sem duplicar fronteira)
            for (int i = 0; i < sortedRooms.Count; i++)
            {
                var room = sortedRooms[i];
                float minX = Mathf.Min(room.minXZ.x, room.maxXZ.x);
                float maxX = Mathf.Max(room.minXZ.x, room.maxXZ.x);
                float minZ = Mathf.Min(room.minXZ.y, room.maxXZ.y);
                float maxZ = Mathf.Max(room.minXZ.y, room.maxXZ.y);

                if (rootLocalPos.x >= minX && rootLocalPos.x <= maxX &&
                    rootLocalPos.z >= minZ && rootLocalPos.z <= maxZ)
                {
                    return room.roomName.Trim();
                }
            }

            // 2. Caso esteja ligeiramente fora do perímetro externo (ex: beiral de parede externa a -0.34m ou 33.12m),
            // associar ao cômodo cujo retângulo XZ esteja mais próximo.
            string closestRoom = sortedRooms[0].roomName.Trim();
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < sortedRooms.Count; i++)
            {
                var room = sortedRooms[i];
                float minX = Mathf.Min(room.minXZ.x, room.maxXZ.x);
                float maxX = Mathf.Max(room.minXZ.x, room.maxXZ.x);
                float minZ = Mathf.Min(room.minXZ.y, room.maxXZ.y);
                float maxZ = Mathf.Max(room.minXZ.y, room.maxXZ.y);

                float clampedX = Mathf.Clamp(rootLocalPos.x, minX, maxX);
                float clampedZ = Mathf.Clamp(rootLocalPos.z, minZ, maxZ);
                float dx = rootLocalPos.x - clampedX;
                float dz = rootLocalPos.z - clampedZ;
                float distSq = dx * dx + dz * dz;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    closestRoom = room.roomName.Trim();
                }
            }

            return closestRoom;
        }

        /// <summary>
        /// Executa ou reexecuta a organização estrutural da hierarquia na raiz do prefab editável:
        /// - Divide por cômodo (quando groupByRooms = true), garantindo que paredes divisórias fiquem em apenas 1 cômodo;
        /// - Monta equipamentos multi-peça (Forno+Porta, Geladeira+Portas, Lixeira+Tampa, Registradora+Gaveta, Porta+Batente);
        /// - Parentaliza objetos apoiados em cima de bancadas/mesas/prateleiras e facas nos suportes (quando parentItemsOnSurfaces = true);
        /// - Parentaliza cadeiras às mesas correspondentes (quando parentChairsToTables = true).
        /// </summary>
        public static PizzeriaHierarchySummaryReport OrganizeHierarchy(
            GameObject prefabRoot,
            PizzeriaAuthoringSettings settings,
            bool respectManualLocks = true)
        {
            var report = new PizzeriaHierarchySummaryReport();
            if (prefabRoot == null) return report;

            if (settings != null)
            {
                if (settings.classificationRules == null || settings.classificationRules.Count == 0)
                    settings.InitializeDefaultFbxPreset();
                if (settings.roomRegions == null || settings.roomRegions.Count == 0)
                    settings.InitializeDefaultRooms();
            }

            Undo.RegisterFullObjectHierarchyUndo(prefabRoot, "Organizar Hierarquia da Pizzaria");

            Transform rootT = prefabRoot.transform;

            // Coletar todos os nós de modelo (folhas ou objetos com MeshFilter/Renderer/PizzeriaNodeIdentity)
            var candidateNodes = CollectOrganizableNodes(rootT);
            report.totalAnalyzedNodes = candidateNodes.Count;

            var rules = settings != null ? settings.classificationRules : new List<PizzeriaHierarchyClassificationRule>();
            var sortedRules = rules.Where(r => r != null && r.enabled).OrderBy(r => r.priority).ToList();

            // Desaninhar temporariamente filhos não travados que estavam em outras bancadas/mesas/grupos para recalcular limpo
            foreach (var node in candidateNodes)
            {
                node.unitRootOrSelf = node.transform;
                Vector3 localInRoot = rootT.InverseTransformPoint(node.bounds.center);
                node.assignedRoomName = ResolveRoomNameForLocalPosition(localInRoot, settings);
            }

            // Passo 1: Garantir PizzeriaNodeIdentity em cada nó e classificar categoria base
            foreach (var node in candidateNodes)
            {
                if (node.identity == null)
                {
                    node.identity = Undo.AddComponent<PizzeriaNodeIdentity>(node.transform.gameObject);
                    node.identity.originalNodeName = node.rawName;
                    node.identity.originalRelativePath = AnimationUtility.CalculateTransformPath(node.transform, rootT);
                }
                node.identity.EnsureGuid();
                if (string.IsNullOrEmpty(node.identity.originalNodeName))
                    node.identity.originalNodeName = node.rawName;

                if (node.isEmptyMesh)
                {
                    report.emptyMeshNodesCount++;
                    report.emptyMeshNodeNames.Add(node.rawName);
                    node.identity.pendingReview = true;
                    node.identity.reviewReason = "MeshFilter presente mas com mesh vazia ou ausente.";
                    node.assignedCategory = PizzeriaHierarchyCategory.Review;
                    node.identity.category = PizzeriaHierarchyCategory.Review;
                    report.reviewItems.Add($"{node.rawName}: {node.identity.reviewReason}");
                    continue;
                }

                // Se tiver trava manual, respeitar categoria e pai atuais
                if (respectManualLocks && node.identity.manualAssociationLocked)
                {
                    report.preservedManualLocks++;
                    node.assignedCategory = node.identity.category != PizzeriaHierarchyCategory.Unclassified
                        ? node.identity.category
                        : InferCategoryFromParentPath(node.transform, rootT);
                    node.consumedInAssembly = true;
                    continue;
                }

                // Se já estiver dentro de um conjunto montado que é uma estação funcional (StationView no pai), preservar!
                var parentStation = node.transform.parent != null && node.transform.parent != rootT
                    ? node.transform.parent.GetComponentInParent<StationView>()
                    : null;
                if (parentStation != null && parentStation.transform != node.transform)
                {
                    node.assignedCategory = PizzeriaHierarchyCategory.Equipment;
                    node.identity.category = PizzeriaHierarchyCategory.Equipment;
                    node.consumedInAssembly = true;
                    continue;
                }

                node.assignedCategory = ClassifyNode(node.normalizedName, sortedRules, out _);
                node.identity.category = node.assignedCategory;

                if (node.assignedCategory == PizzeriaHierarchyCategory.Review || node.assignedCategory == PizzeriaHierarchyCategory.Unclassified)
                {
                    node.assignedCategory = PizzeriaHierarchyCategory.Review;
                    node.identity.category = PizzeriaHierarchyCategory.Review;
                    node.identity.pendingReview = true;
                    node.identity.reviewReason = node.normalizedName == "player"
                        ? "Objeto de referência de escala humana ('player')."
                        : $"Objeto não classificado ou ambíguo ('{node.rawName}').";
                    report.reviewItems.Add($"{node.rawName}: {node.identity.reviewReason}");
                }
                else
                {
                    node.identity.pendingReview = false;
                    node.identity.reviewReason = string.Empty;
                }
            }

            // Passo 2: Montagem determinística de conjuntos pai/filho (Lixeiras, Fornos, Geladeiras, Caixa Registradora, Aberturas)
            var assemblyRules = settings != null ? settings.assemblyRules : new List<PizzeriaEquipmentAssemblyRule>();
            foreach (var asmRule in assemblyRules)
            {
                if (asmRule == null || !asmRule.enabled) continue;
                AssembleEquipmentSets(rootT, candidateNodes, asmRule, settings, report);
            }

            // Passo 3: Mover bancadas, equipamentos, paredes e estruturas para os contêineres do seu Cômodo (ou raiz)
            foreach (var node in candidateNodes)
            {
                if (node.consumedInAssembly) continue;
                if (HasRigOrAnimationProtection(rootT, node.transform))
                {
                    report.transformWarnings.Add($"Nó '{node.rawName}' mantido no local original por possuir rig/animação ativa.");
                    continue;
                }

                string roomForContainer = (node.assignedCategory == PizzeriaHierarchyCategory.Review || node.assignedCategory == PizzeriaHierarchyCategory.Anchors)
                    ? null
                    : node.assignedRoomName;

                Transform targetContainer = GetOrCreateCategoryContainer(rootT, node.assignedCategory, roomForContainer);
                if (targetContainer != null && node.transform.parent != targetContainer)
                {
                    if (!SafeReparentPreservingWorldTransform(node.transform, targetContainer, out string warn))
                    {
                        Transform reviewContainer = GetOrCreateCategoryContainer(rootT, PizzeriaHierarchyCategory.Review, null);
                        SafeReparentPreservingWorldTransform(node.transform, reviewContainer, out _);
                        node.identity.category = PizzeriaHierarchyCategory.Review;
                        node.identity.pendingReview = true;
                        node.identity.reviewReason = warn;
                        report.reviewItems.Add($"{node.rawName}: {warn}");
                    }
                    else if (!string.IsNullOrEmpty(warn))
                    {
                        report.transformWarnings.Add(warn);
                    }
                }
            }

            // Passo 4: Parentalização espacial de objetos apoiados sobre superfícies (bancadas, mesas, prateleiras, suportes de faca) e cadeiras nas mesas!
            if (settings == null || settings.parentItemsOnSurfaces)
            {
                ParentItemsOnSurfacesAndHolders(rootT, candidateNodes, report);
            }

            if (settings == null || settings.parentChairsToTables)
            {
                ParentChairsToNearestTables(rootT, candidateNodes, report);
            }

            // Passo 5: Limpar contêineres organizacionais vazios (exceto Anchors se tiver âncoras)
            CleanupEmptyOrganizationalContainers(rootT, settings);

            // Passo 6: Contabilizar resumo por categoria e por cômodo
            foreach (var node in candidateNodes)
            {
                var cat = node.identity != null ? node.identity.category : node.assignedCategory;
                if (!report.countsByCategory.ContainsKey(cat))
                    report.countsByCategory[cat] = 0;
                report.countsByCategory[cat]++;

                string rName = string.IsNullOrEmpty(node.assignedRoomName) ? "(Raiz / Revisar)" : node.assignedRoomName;
                if (!report.countsByRoom.ContainsKey(rName))
                    report.countsByRoom[rName] = 0;
                report.countsByRoom[rName]++;
            }

            EditorUtility.SetDirty(prefabRoot);
            return report;
        }

        /// <summary>
        /// Parentaliza automaticamente objetos que estão apoiados em cima de bancadas, mesas, prateleiras ou suportes:
        /// - Facas -> Suporte de faca (ou bancada abaixo)
        /// - Tábuas de cortar, formas/pratos, panelas, cortadores, espátulas, pranchetas, molhos, cardápio, caixas, mochilas -> Bancada/Mesa/Prateleira abaixo
        /// - CaixaRegistradora_01 e suportefaca -> Bancada/Mesa onde estão apoiados
        /// Assim, ao mover uma bancada/mesa/prateleira na Scene View, tudo que está em cima dela move junto!
        /// </summary>
        private static void ParentItemsOnSurfacesAndHolders(
            Transform rootT,
            List<CandidateNodeInfo> allNodes,
            PizzeriaHierarchySummaryReport report)
        {
            // Identificar todas as superfícies receptoras (bancadas, mesas, prateleiras)
            var surfaces = new List<CandidateNodeInfo>();
            var knifeHolders = new List<CandidateNodeInfo>();

            foreach (var n in allNodes)
            {
                if (n.isEmptyMesh || n.transform == null) continue;
                if (n.identity != null && n.identity.manualAssociationLocked) continue;

                if (IsSurfaceBearer(n.normalizedName))
                {
                    surfaces.Add(n);
                }
                else if (IsKnifeHolder(n.normalizedName))
                {
                    knifeHolders.Add(n);
                }
            }

            // 4.1 Primeiro: Facas vão para o Suporte de Faca correspondente (se estiverem encaixadas/próximas a um suportefaca)
            foreach (var n in allNodes)
            {
                if (n.isEmptyMesh || n.transform == null) continue;
                if (n.identity != null && n.identity.manualAssociationLocked) continue;
                if (!n.normalizedName.StartsWith("faca", StringComparison.Ordinal)) continue;

                CandidateNodeInfo bestHolder = null;
                float bestDist = 0.65f; // distância máxima entre faca e suporte de faca
                foreach (var holder in knifeHolders)
                {
                    float d = Vector3.Distance(n.bounds.center, holder.bounds.center);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestHolder = holder;
                    }
                }

                if (bestHolder != null)
                {
                    if (SafeReparentPreservingWorldTransform(n.transform, bestHolder.transform, out _))
                    {
                        report.surfaceParentedItems.Add($"'{n.rawName}' → filho de '{bestHolder.rawName}'");
                    }
                }
            }

            // 4.2 Segundo: Objetos apoiados em cima de Bancadas / Mesas / Prateleiras (incluindo Suporte de Faca, Caixa Registradora, Utensílios, Molhos, Mochilas, Caixas)
            foreach (var item in allNodes)
            {
                if (item.isEmptyMesh || item.transform == null) continue;
                if (item.identity != null && item.identity.manualAssociationLocked) continue;

                // Se já foi parentalizado a um suporte de faca no passo 4.1, o próprio suporte de faca será parentalizado à bancada abaixo!
                if (item.transform.parent != null && IsKnifeHolder(NormalizeNodeNameForMatch(item.transform.parent.name, out _)))
                    continue;

                if (!CanRestOnSurface(item.normalizedName))
                    continue;

                // Se o item faz parte de uma unidade montada (ex: caixaregistradora.001 dentro de CaixaRegistradora_01),
                // devemos mover a raiz da unidade (CaixaRegistradora_01) para cima da bancada, e não arrancar a peça de dentro da unidade!
                Transform targetToMove = GetMovableUnitOrSelf(item.transform, rootT);
                if (targetToMove == null) continue;

                // Se targetToMove já é filho de uma bancada/mesa/prateleira, verificar ou reavaliar
                Bounds itemBounds = ComputeSelfWorldBounds(item.transform);

                CandidateNodeInfo bestSurface = FindSupportingSurface(itemBounds, surfaces, targetToMove);
                if (bestSurface != null && targetToMove != bestSurface.transform && !bestSurface.transform.IsChildOf(targetToMove))
                {
                    if (targetToMove.parent != bestSurface.transform)
                    {
                        if (SafeReparentPreservingWorldTransform(targetToMove, bestSurface.transform, out _))
                        {
                            report.surfaceParentedItems.Add($"'{targetToMove.name}' → em cima de '{bestSurface.rawName}'");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parentaliza cada cadeira à mesa mais próxima dentro do mesmo cômodo (até 1.8m),
        /// facilitando mover a mesa inteira com suas cadeiras.
        /// </summary>
        private static void ParentChairsToNearestTables(
            Transform rootT,
            List<CandidateNodeInfo> allNodes,
            PizzeriaHierarchySummaryReport report)
        {
            var tables = allNodes.Where(n => !n.isEmptyMesh && IsTable(n.normalizedName)).ToList();
            if (tables.Count == 0) return;

            foreach (var chair in allNodes)
            {
                if (chair.isEmptyMesh || chair.transform == null) continue;
                if (chair.identity != null && chair.identity.manualAssociationLocked) continue;
                if (!chair.normalizedName.StartsWith("cadeira", StringComparison.Ordinal)) continue;

                CandidateNodeInfo bestTable = null;
                float bestHorizDist = 1.85f; // alcance máximo cadeira <-> centro da mesa

                Vector2 chairXZ = new Vector2(chair.bounds.center.x, chair.bounds.center.z);
                foreach (var table in tables)
                {
                    Vector2 tableXZ = new Vector2(table.bounds.center.x, table.bounds.center.z);
                    float d = Vector2.Distance(chairXZ, tableXZ);
                    if (d < bestHorizDist)
                    {
                        bestHorizDist = d;
                        bestTable = table;
                    }
                }

                if (bestTable != null && chair.transform.parent != bestTable.transform)
                {
                    if (SafeReparentPreservingWorldTransform(chair.transform, bestTable.transform, out _))
                    {
                        report.surfaceParentedItems.Add($"'{chair.rawName}' → cadeira da '{bestTable.rawName}'");
                    }
                }
            }
        }

        private static CandidateNodeInfo FindSupportingSurface(
            Bounds itemBounds,
            List<CandidateNodeInfo> surfaces,
            Transform itemTransform)
        {
            CandidateNodeInfo best = null;
            float bestScore = float.MinValue;

            Vector2 itemXZ = new Vector2(itemBounds.center.x, itemBounds.center.z);

            foreach (var surf in surfaces)
            {
                if (surf.transform == itemTransform) continue;
                Bounds sb = ComputeSelfWorldBounds(surf.transform);

                // Margem horizontal generosa para objetos apoiados na borda da bancada/mesa/prateleira
                float marginXZ = 0.55f;
                bool insideX = itemXZ.x >= (sb.min.x - marginXZ) && itemXZ.x <= (sb.max.x + marginXZ);
                bool insideZ = itemXZ.y >= (sb.min.z - marginXZ) && itemXZ.y <= (sb.max.z + marginXZ);
                if (!insideX || !insideZ)
                    continue;

                // Verificar altura compatível: o item deve estar na altura da superfície/prateleira (não no teto muito acima nem enterrado longe)
                // Prateleiras têm múltiplos níveis internos (entre sb.min.y e sb.max.y + 0.6f), bancadas/mesas têm itens perto do topo.
                bool verticalMatch = itemBounds.center.y >= (sb.min.y - 0.15f) && itemBounds.min.y <= (sb.max.y + 0.75f);
                if (!verticalMatch)
                    continue;

                Vector2 surfXZ = new Vector2(sb.center.x, sb.center.z);
                float horizDist = Vector2.Distance(itemXZ, surfXZ);
                float vertDistToTop = Mathf.Abs(itemBounds.min.y - sb.max.y);

                // Quanto mais alinhado horizontalmente e próximo da superfície, maior a pontuação
                float score = 10f - horizDist * 2.5f - vertDistToTop * 1.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = surf;
                }
            }

            return best;
        }

        private static Transform GetMovableUnitOrSelf(Transform node, Transform rootT)
        {
            if (node == null) return null;
            if (node.parent != null && node.parent != rootT)
            {
                // Se o pai imediato é uma raiz de conjunto montado (ex: CaixaRegistradora_01), movemos o conjunto inteiro
                string pName = node.parent.name;
                if (pName.StartsWith("CaixaRegistradora_", StringComparison.OrdinalIgnoreCase))
                {
                    return node.parent;
                }
                // Se for a gaveta dentro da CaixaRegistradora_01, não mover separadamente (a própria caixaregistradora.001 já move a raiz)
                if (NormalizeNodeNameForMatch(node.name, out _) == "gavetacaixa")
                {
                    return null;
                }
            }
            return node;
        }

        private static bool IsSurfaceBearer(string norm)
        {
            if (string.IsNullOrEmpty(norm)) return false;
            return norm.StartsWith("balcao", StringComparison.Ordinal) ||
                   norm.StartsWith("quinabalcao", StringComparison.Ordinal) ||
                   norm.StartsWith("mesacomum", StringComparison.Ordinal) ||
                   norm.StartsWith("mesamedia", StringComparison.Ordinal) ||
                   norm.StartsWith("mesaempacotamento", StringComparison.Ordinal) ||
                   norm.StartsWith("mesa", StringComparison.Ordinal) ||
                   norm.StartsWith("prateleira", StringComparison.Ordinal) ||
                   norm.StartsWith("armario", StringComparison.Ordinal);
        }

        private static bool IsTable(string norm)
        {
            if (string.IsNullOrEmpty(norm)) return false;
            return norm.StartsWith("mesacomum", StringComparison.Ordinal) ||
                   norm.StartsWith("mesamedia", StringComparison.Ordinal) ||
                   norm.StartsWith("mesaempacotamento", StringComparison.Ordinal) ||
                   norm.StartsWith("mesa", StringComparison.Ordinal);
        }

        private static bool IsKnifeHolder(string norm)
        {
            if (string.IsNullOrEmpty(norm)) return false;
            return norm.StartsWith("suportefaca", StringComparison.Ordinal) ||
                   norm.StartsWith("suportedefaca", StringComparison.Ordinal) ||
                   norm.StartsWith("rackfaca", StringComparison.Ordinal);
        }

        private static bool CanRestOnSurface(string norm)
        {
            if (string.IsNullOrEmpty(norm)) return false;
            // Bancadas, mesas, cadeiras, paredes, fornos, geladeiras e lixeiras NÃO sobem em bancadas
            if (IsSurfaceBearer(norm) ||
                norm.StartsWith("cadeira", StringComparison.Ordinal) ||
                norm.StartsWith("parede", StringComparison.Ordinal) ||
                norm.StartsWith("pilastra", StringComparison.Ordinal) ||
                norm.StartsWith("forno", StringComparison.Ordinal) ||
                norm.StartsWith("portaforno", StringComparison.Ordinal) ||
                norm.StartsWith("geladeira", StringComparison.Ordinal) ||
                norm.StartsWith("portamaior", StringComparison.Ordinal) ||
                norm.StartsWith("portapequena", StringComparison.Ordinal) ||
                norm.StartsWith("batente", StringComparison.Ordinal) ||
                norm.StartsWith("latadelixo", StringComparison.Ordinal) ||
                norm.StartsWith("tampadolixo", StringComparison.Ordinal) ||
                norm.StartsWith("lixeira", StringComparison.Ordinal) ||
                norm == "player" ||
                norm == "coiso")
            {
                return false;
            }

            // Utensílios, tábuas, formas, panelas, cortadores, espátulas, suportes de faca, caixa registradora, molhos, prancheta, cardápio, mochilas, caixas
            return norm.StartsWith("faca", StringComparison.Ordinal) ||
                   norm.StartsWith("suportefaca", StringComparison.Ordinal) ||
                   norm.StartsWith("tabuadecortar", StringComparison.Ordinal) ||
                   norm.StartsWith("pratoforma", StringComparison.Ordinal) ||
                   norm.StartsWith("cortadordepizza", StringComparison.Ordinal) ||
                   norm.StartsWith("panelaeconcha", StringComparison.Ordinal) ||
                   norm.StartsWith("espatula", StringComparison.Ordinal) ||
                   norm.StartsWith("deliverybag", StringComparison.Ordinal) ||
                   norm.StartsWith("caixa", StringComparison.Ordinal) ||
                   norm.StartsWith("caixaregistradora", StringComparison.Ordinal) ||
                   norm.StartsWith("prancheta", StringComparison.Ordinal) ||
                   norm.StartsWith("molhos", StringComparison.Ordinal) ||
                   norm.StartsWith("cardapio", StringComparison.Ordinal) ||
                   norm.StartsWith("abertura", StringComparison.Ordinal);
        }

        private static void AssembleEquipmentSets(
            Transform rootT,
            List<CandidateNodeInfo> allNodes,
            PizzeriaEquipmentAssemblyRule rule,
            PizzeriaAuthoringSettings settings,
            PizzeriaHierarchySummaryReport report)
        {
            var parents = new List<CandidateNodeInfo>();
            var children = new List<CandidateNodeInfo>();

            for (int i = 0; i < allNodes.Count; i++)
            {
                var n = allNodes[i];
                if (n.consumedInAssembly || n.isEmptyMesh) continue;

                if (MatchesAnyPrefix(n.normalizedName, rule.childPrefixes))
                {
                    children.Add(n);
                }
                else if (MatchesAnyPrefix(n.normalizedName, rule.parentPrefixes))
                {
                    parents.Add(n);
                }
            }

            if (parents.Count == 0 && children.Count == 0) return;

            parents.Sort((a, b) =>
            {
                int cmp = a.suffixNumber.CompareTo(b.suffixNumber);
                if (cmp != 0 && a.suffixNumber > 0 && b.suffixNumber > 0) return cmp;
                return string.CompareOrdinal(a.rawName, b.rawName);
            });

            var parentAssignedChildren = new Dictionary<CandidateNodeInfo, List<CandidateNodeInfo>>();
            foreach (var p in parents)
                parentAssignedChildren[p] = new List<CandidateNodeInfo>();

            children.Sort((a, b) => string.CompareOrdinal(a.rawName, b.rawName));

            foreach (var child in children)
            {
                child.identity.candidates.Clear();

                var scoredCandidates = new List<(CandidateNodeInfo parent, float score, float dist, string detail)>();
                foreach (var parent in parents)
                {
                    float surfDist = ComputeSurfaceDistanceBetweenBounds(parent.bounds, child.bounds);
                    float horizDist = Vector2.Distance(
                        new Vector2(parent.bounds.center.x, parent.bounds.center.z),
                        new Vector2(child.bounds.center.x, child.bounds.center.z));

                    if (surfDist > rule.maxBoundsDistance && horizDist > rule.maxBoundsDistance * 1.25f)
                        continue;

                    float proximityScore = Mathf.Clamp01(1f - (surfDist / Mathf.Max(0.01f, rule.maxBoundsDistance)));
                    float horizAlignmentScore = Mathf.Clamp01(1f - (horizDist / Mathf.Max(0.01f, parent.bounds.size.magnitude))) * 0.45f;
                    float suffixBonus = (parent.suffixNumber > 0 && parent.suffixNumber == child.suffixNumber) ? 0.12f : 0f;

                    float sizeRatio = child.bounds.size.magnitude / Mathf.Max(0.05f, parent.bounds.size.magnitude);
                    float sizePenalty = sizeRatio > 1.35f ? -0.5f : 0f;

                    float totalScore = proximityScore + horizAlignmentScore + suffixBonus + sizePenalty;
                    if (totalScore > 0.1f)
                    {
                        scoredCandidates.Add((parent, totalScore, surfDist, $"dist={surfDist:F2}m, horiz={horizDist:F2}m"));
                    }
                }

                scoredCandidates.Sort((a, b) => b.score.CompareTo(a.score));

                for (int c = 0; c < Mathf.Min(3, scoredCandidates.Count); c++)
                {
                    child.identity.candidates.Add(new PizzeriaCandidateAssociation
                    {
                        candidateNodeGuid = scoredCandidates[c].parent.identity.nodeGuid,
                        candidateName = scoredCandidates[c].parent.rawName,
                        score = scoredCandidates[c].score,
                        reason = scoredCandidates[c].detail
                    });
                }

                if (scoredCandidates.Count == 0)
                {
                    child.assignedCategory = PizzeriaHierarchyCategory.Review;
                    child.identity.category = PizzeriaHierarchyCategory.Review;
                    child.identity.pendingReview = true;
                    child.identity.reviewReason = $"Parte móvel '{child.rawName}' ({rule.setRuleName}) sem corpo principal compatível até {rule.maxBoundsDistance:F2}m.";
                    report.reviewItems.Add($"{child.rawName}: {child.identity.reviewReason}");
                    continue;
                }

                var best = scoredCandidates[0];
                bool ambiguous = false;
                if (scoredCandidates.Count > 1)
                {
                    var second = scoredCandidates[1];
                    float gap = best.score - second.score;
                    if (gap < rule.minScoreGapForAutoAssign)
                    {
                        ambiguous = true;
                    }
                }

                if (ambiguous || parentAssignedChildren[best.parent].Count >= rule.maxChildrenPerParent)
                {
                    child.assignedCategory = PizzeriaHierarchyCategory.Review;
                    child.identity.category = PizzeriaHierarchyCategory.Review;
                    child.identity.pendingReview = true;
                    child.identity.reviewReason = ambiguous
                        ? $"Associação ambígua entre '{best.parent.rawName}' (score {best.score:F2}) e '{scoredCandidates[1].parent.rawName}' (score {scoredCandidates[1].score:F2})."
                        : $"Corpo '{best.parent.rawName}' já atingiu o limite de {rule.maxChildrenPerParent} peças associadas.";
                    report.reviewItems.Add($"{child.rawName}: {child.identity.reviewReason}");
                    continue;
                }

                parentAssignedChildren[best.parent].Add(child);
                child.identity.pendingReview = false;
                child.identity.reviewReason = string.Empty;
            }

            int unitIndex = 1;

            foreach (var parent in parents)
            {
                var matchedChildren = parentAssignedChildren[parent];
                string unitName = $"{rule.groupNamePrefix}_{unitIndex:00}";
                unitIndex++;

                Transform categoryContainer = GetOrCreateCategoryContainer(rootT, rule.targetCategory, parent.assignedRoomName);

                if (rule.createNormalizedUnitRoot)
                {
                    Transform unitRoot = null;
                    if (parent.transform.parent != null &&
                        parent.transform.parent != rootT &&
                        parent.transform.parent.name.StartsWith(rule.groupNamePrefix + "_", StringComparison.OrdinalIgnoreCase) &&
                        parent.transform.parent.GetComponent<MeshFilter>() == null &&
                        parent.transform.parent.GetComponent<Renderer>() == null)
                    {
                        unitRoot = parent.transform.parent;
                        unitRoot.name = unitName;
                        if (unitRoot.parent != categoryContainer)
                        {
                            Undo.SetTransformParent(unitRoot, categoryContainer, "Mover Conjunto para Categoria");
                        }
                    }
                    else
                    {
                        Transform existingUnit = categoryContainer.Find(unitName);
                        if (existingUnit != null && existingUnit.GetComponent<MeshFilter>() == null && existingUnit.childCount == 0)
                        {
                            unitRoot = existingUnit;
                        }
                        else
                        {
                            var unitGo = new GameObject(unitName);
                            Undo.RegisterCreatedObjectUndo(unitGo, "Criar Raiz de Conjunto");
                            unitRoot = unitGo.transform;
                            unitRoot.SetParent(categoryContainer, false);
                        }
                        unitRoot.position = parent.bounds.center;
                        unitRoot.rotation = Quaternion.identity;
                        unitRoot.localScale = Vector3.one;

                        var unitIdentity = unitRoot.GetComponent<PizzeriaNodeIdentity>();
                        if (unitIdentity == null)
                            unitIdentity = Undo.AddComponent<PizzeriaNodeIdentity>(unitRoot.gameObject);
                        unitIdentity.EnsureGuid();
                        unitIdentity.category = rule.targetCategory;
                    }

                    SafeReparentPreservingWorldTransform(parent.transform, unitRoot, out _);
                    parent.consumedInAssembly = true;
                    parent.unitRootOrSelf = unitRoot;
                    parent.assignedCategory = rule.targetCategory;
                    parent.identity.category = rule.targetCategory;

                    foreach (var ch in matchedChildren)
                    {
                        SafeReparentPreservingWorldTransform(ch.transform, unitRoot, out _);
                        ch.consumedInAssembly = true;
                        ch.unitRootOrSelf = unitRoot;
                        ch.assignedRoomName = parent.assignedRoomName;
                        ch.assignedCategory = rule.targetCategory;
                        ch.identity.category = rule.targetCategory;
                    }

                    string childNames = matchedChildren.Count > 0
                        ? string.Join(", ", matchedChildren.Select(c => c.rawName))
                        : "sem partes móveis";
                    report.assembledUnits.Add($"{unitName} ({parent.assignedRoomName ?? "Geral"}): corpo='{parent.rawName}' + [{childNames}]");
                }
                else
                {
                    SafeReparentPreservingWorldTransform(parent.transform, categoryContainer, out _);
                    parent.consumedInAssembly = true;
                    parent.assignedCategory = rule.targetCategory;
                    parent.identity.category = rule.targetCategory;

                    foreach (var ch in matchedChildren)
                    {
                        if (!SafeReparentPreservingWorldTransform(ch.transform, parent.transform, out _))
                        {
                            var unitGo = new GameObject(unitName);
                            Undo.RegisterCreatedObjectUndo(unitGo, "Criar Raiz Intermediária");
                            unitGo.transform.SetParent(categoryContainer, false);
                            unitGo.transform.position = parent.bounds.center;
                            unitGo.transform.rotation = Quaternion.identity;
                            unitGo.transform.localScale = Vector3.one;

                            SafeReparentPreservingWorldTransform(parent.transform, unitGo.transform, out _);
                            SafeReparentPreservingWorldTransform(ch.transform, unitGo.transform, out _);
                        }
                        ch.consumedInAssembly = true;
                        ch.assignedRoomName = parent.assignedRoomName;
                        ch.assignedCategory = rule.targetCategory;
                        ch.identity.category = rule.targetCategory;
                    }

                    string childNames = matchedChildren.Count > 0
                        ? string.Join(", ", matchedChildren.Select(c => c.rawName))
                        : "sem partes móveis";
                    report.assembledUnits.Add($"{parent.rawName}: [{childNames}]");
                }
            }
        }

        private static List<CandidateNodeInfo> CollectOrganizableNodes(Transform rootT)
        {
            var result = new List<CandidateNodeInfo>();
            var allTransforms = rootT.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < allTransforms.Length; i++)
            {
                var t = allTransforms[i];
                if (t == null || t == rootT) continue;

                if (IsBuilderGeneratedSubNode(t, rootT))
                    continue;

                var mf = t.GetComponent<MeshFilter>();
                var rend = t.GetComponent<Renderer>();
                var skinned = t.GetComponent<SkinnedMeshRenderer>();

                if (mf == null && rend == null && skinned == null)
                    continue;

                bool isEmptyMesh = mf != null && (mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) && skinned == null;
                Bounds b = ComputeSelfWorldBounds(t);
                bool hasValidMesh = !isEmptyMesh && (rend != null || skinned != null || mf != null);

                var id = t.GetComponent<PizzeriaNodeIdentity>();
                string rawName = id != null && !string.IsNullOrEmpty(id.originalNodeName) ? id.originalNodeName : t.name;
                string norm = NormalizeNodeNameForMatch(rawName, out int suffix);

                result.Add(new CandidateNodeInfo
                {
                    transform = t,
                    identity = id,
                    rawName = rawName,
                    normalizedName = norm,
                    suffixNumber = suffix,
                    bounds = b,
                    hasValidMesh = hasValidMesh,
                    isEmptyMesh = isEmptyMesh
                });
            }

            return result;
        }

        private static bool IsBuilderGeneratedSubNode(Transform t, Transform rootT)
        {
            Transform curr = t;
            while (curr != null && curr != rootT)
            {
                string n = curr.name;
                if (n == "Anchors" || n == "Slots" || n == "TicketSlots" || n == "Workposts" || n == "PisosGerados")
                    return true;
                if (n.StartsWith("Slot_", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("TicketSlot_", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Workpost_", StringComparison.OrdinalIgnoreCase) ||
                    n == "OvenFireLight" || n == "OvenSmokeParticles" || n == "PhoneBeaconLight")
                    return true;
                if (curr.GetComponent<StationWorkpostTarget>() != null || curr.GetComponent<TicketSheetView>() != null)
                    return true;
                curr = curr.parent;
            }
            return false;
        }

        public static PizzeriaHierarchyCategory ClassifyNode(
            string normalizedName,
            List<PizzeriaHierarchyClassificationRule> sortedRules,
            out string matchedRuleName)
        {
            matchedRuleName = null;
            if (string.IsNullOrEmpty(normalizedName))
                return PizzeriaHierarchyCategory.Review;

            for (int i = 0; i < sortedRules.Count; i++)
            {
                var rule = sortedRules[i];
                if (rule == null || !rule.enabled || rule.exactOrPrefixAliases == null) continue;

                if (MatchesAnyPrefix(normalizedName, rule.exactOrPrefixAliases))
                {
                    matchedRuleName = rule.ruleName;
                    return rule.targetCategory;
                }
            }

            return PizzeriaHierarchyCategory.Review;
        }

        private static bool MatchesAnyPrefix(string normalizedName, List<string> aliases)
        {
            if (string.IsNullOrEmpty(normalizedName) || aliases == null) return false;
            for (int i = 0; i < aliases.Count; i++)
            {
                string rawAlias = aliases[i];
                if (string.IsNullOrWhiteSpace(rawAlias)) continue;
                string alias = NormalizeNodeNameForMatch(rawAlias, out _);
                if (string.IsNullOrEmpty(alias)) continue;

                if (alias.Length < 3)
                {
                    if (normalizedName == alias) return true;
                    continue;
                }

                if (normalizedName == alias || normalizedName.StartsWith(alias, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static string GetContainerRelativePath(PizzeriaHierarchyCategory category, string roomName = null)
        {
            string basePath;
            switch (category)
            {
                case PizzeriaHierarchyCategory.Architecture_Walls:
                    basePath = "Arquitetura/Paredes";
                    break;
                case PizzeriaHierarchyCategory.Architecture_Walls_Internal:
                    basePath = "Arquitetura/Paredes/Divisorias_Internas";
                    break;
                case PizzeriaHierarchyCategory.Architecture_Walls_External:
                    basePath = "Arquitetura/Paredes/Externas";
                    break;
                case PizzeriaHierarchyCategory.Architecture_Floors:
                    basePath = "Arquitetura/Pisos";
                    break;
                case PizzeriaHierarchyCategory.Architecture_Ceilings:
                    basePath = "Arquitetura/Tetos";
                    break;
                case PizzeriaHierarchyCategory.Architecture_Structure:
                    basePath = "Arquitetura/Estrutura";
                    break;
                case PizzeriaHierarchyCategory.Architecture_Openings:
                    basePath = "Arquitetura/Aberturas";
                    break;
                case PizzeriaHierarchyCategory.Equipment:
                    basePath = "Equipamentos";
                    break;
                case PizzeriaHierarchyCategory.Furniture:
                    basePath = "Mobiliario";
                    break;
                case PizzeriaHierarchyCategory.Utensils:
                    basePath = "Utensilios";
                    break;
                case PizzeriaHierarchyCategory.Decoration:
                    basePath = "Decoracao";
                    break;
                case PizzeriaHierarchyCategory.Anchors:
                    return "Anchors";
                case PizzeriaHierarchyCategory.Review:
                case PizzeriaHierarchyCategory.Unclassified:
                default:
                    return "Revisar";
            }

            if (!string.IsNullOrWhiteSpace(roomName))
            {
                return $"{roomName.Trim()}/{basePath}";
            }
            return basePath;
        }

        public static Transform GetOrCreateCategoryContainer(Transform rootT, PizzeriaHierarchyCategory category, string roomName = null)
        {
            string relPath = GetContainerRelativePath(category, roomName);
            string[] parts = relPath.Split('/');
            Transform current = rootT;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                Transform next = current.Find(part);
                if (next == null)
                {
                    var go = new GameObject(part);
                    Undo.RegisterCreatedObjectUndo(go, $"Criar Grupo {part}");
                    next = go.transform;
                    next.SetParent(current, false);
                    next.localPosition = Vector3.zero;
                    next.localRotation = Quaternion.identity;
                    next.localScale = Vector3.one;
                }
                else
                {
                    if (next.GetComponent<MeshFilter>() == null && next.GetComponent<Renderer>() == null)
                    {
                        next.localScale = Vector3.one;
                    }
                }
                current = next;
            }

            return current;
        }

        private static PizzeriaHierarchyCategory InferCategoryFromParentPath(Transform t, Transform rootT)
        {
            string path = AnimationUtility.CalculateTransformPath(t, rootT);
            if (path.IndexOf("Arquitetura/Paredes/Internas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Arquitetura/Paredes/Divisorias_Internas", StringComparison.OrdinalIgnoreCase) >= 0)
                return PizzeriaHierarchyCategory.Architecture_Walls_Internal;
            if (path.IndexOf("Arquitetura/Paredes/Externas", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Architecture_Walls_External;
            if (path.IndexOf("Arquitetura/Paredes", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Architecture_Walls;
            if (path.IndexOf("Arquitetura/Pisos", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Architecture_Floors;
            if (path.IndexOf("Arquitetura/Tetos", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Architecture_Ceilings;
            if (path.IndexOf("Arquitetura/Estrutura", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Architecture_Structure;
            if (path.IndexOf("Arquitetura/Aberturas", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Architecture_Openings;
            if (path.IndexOf("Equipamentos", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Equipment;
            if (path.IndexOf("Mobiliario", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Furniture;
            if (path.IndexOf("Utensilios", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Utensils;
            if (path.IndexOf("Decoracao", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Decoration;
            if (path.IndexOf("Anchors", StringComparison.OrdinalIgnoreCase) >= 0) return PizzeriaHierarchyCategory.Anchors;
            return PizzeriaHierarchyCategory.Review;
        }

        private static bool IsManagedRootContainer(string name, PizzeriaAuthoringSettings settings)
        {
            if (ManagedGroupRoots.Contains(name)) return true;
            if (name.StartsWith("Comodo_", StringComparison.OrdinalIgnoreCase)) return true;
            if (settings != null && settings.roomRegions != null)
            {
                for (int i = 0; i < settings.roomRegions.Count; i++)
                {
                    var r = settings.roomRegions[i];
                    if (r != null && string.Equals(r.roomName?.Trim(), name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private static void CleanupEmptyOrganizationalContainers(Transform rootT, PizzeriaAuthoringSettings settings = null)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = rootT.childCount - 1; i >= 0; i--)
                {
                    var child = rootT.GetChild(i);
                    if (!IsManagedRootContainer(child.name, settings)) continue;
                    CleanupEmptySubContainersRecursive(child);
                    if (child.childCount == 0 && child.GetComponent<MeshFilter>() == null && child.GetComponent<Renderer>() == null)
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                    }
                }
            }
        }

        private static void CleanupEmptySubContainersRecursive(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                CleanupEmptySubContainersRecursive(child);
                if (child.childCount == 0 &&
                    child.GetComponent<MeshFilter>() == null &&
                    child.GetComponent<Renderer>() == null &&
                    child.GetComponent<StationView>() == null)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Permite ao usuário fixar manualmente a associação pai/filho ou categoria de um objeto (ex: item em Revisar).
        /// </summary>
        public static bool ApplyManualParentAndLock(
            Transform child,
            Transform newParent,
            PizzeriaHierarchyCategory category,
            out string message)
        {
            if (child == null || newParent == null)
            {
                message = "Filho ou novo pai inválido.";
                return false;
            }

            if (!SafeReparentPreservingWorldTransform(child, newParent, out string warn))
            {
                message = warn;
                return false;
            }

            var id = child.GetComponent<PizzeriaNodeIdentity>();
            if (id == null) id = Undo.AddComponent<PizzeriaNodeIdentity>(child.gameObject);
            id.EnsureGuid();

            var parentId = newParent.GetComponent<PizzeriaNodeIdentity>();
            if (parentId == null) parentId = Undo.AddComponent<PizzeriaNodeIdentity>(newParent.gameObject);
            parentId.EnsureGuid();

            Undo.RecordObject(id, "Fixar Associação Manual");
            id.category = category;
            id.manualAssociationLocked = true;
            id.lockedParentGuid = parentId.nodeGuid;
            id.pendingReview = false;
            id.reviewReason = string.Empty;
            EditorUtility.SetDirty(id);

            message = string.IsNullOrEmpty(warn)
                ? $"'{child.name}' associado manualmente a '{newParent.name}' e fixado."
                : warn;
            return true;
        }

        /// <summary>
        /// Altera a categoria de múltiplos objetos selecionados em lote e move para o contêiner correspondente.
        /// </summary>
        public static int BatchAssignCategory(
            Transform prefabRoot,
            IEnumerable<GameObject> targets,
            PizzeriaHierarchyCategory newCategory,
            bool lockManualAssociation = true)
        {
            if (prefabRoot == null || targets == null) return 0;
            int count = 0;
            var settings = PizzeriaAuthoringSettings.FindOrCreateDefault();

            foreach (var go in targets)
            {
                if (go == null || go.transform == prefabRoot) continue;
                if (!go.transform.IsChildOf(prefabRoot)) continue;

                Vector3 localPos = prefabRoot.InverseTransformPoint(ComputeSelfWorldBounds(go.transform).center);
                string room = (newCategory == PizzeriaHierarchyCategory.Review || newCategory == PizzeriaHierarchyCategory.Anchors)
                    ? null
                    : ResolveRoomNameForLocalPosition(localPos, settings);
                Transform container = GetOrCreateCategoryContainer(prefabRoot, newCategory, room);

                if (SafeReparentPreservingWorldTransform(go.transform, container, out _))
                {
                    var id = go.GetComponent<PizzeriaNodeIdentity>();
                    if (id == null) id = Undo.AddComponent<PizzeriaNodeIdentity>(go);
                    id.EnsureGuid();
                    Undo.RecordObject(id, "Alterar Categoria em Lote");
                    id.category = newCategory;
                    id.manualAssociationLocked = lockManualAssociation;
                    id.pendingReview = (newCategory == PizzeriaHierarchyCategory.Review);
                    if (!id.pendingReview) id.reviewReason = string.Empty;
                    EditorUtility.SetDirty(id);
                    count++;
                }
            }

            CleanupEmptyOrganizationalContainers(prefabRoot, settings);
            return count;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Planejador geométrico que aloca lotes retangulares modulares ao longo do perímetro da quadra.
    /// Respeita o passo de 2 metros (nunca estica nem deforma peças) e garante contenção estrita no polígono.
    /// </summary>
    public static class ModularLotPlanner
    {
        /// <summary>
        /// Planeja e gera os BuildingLayouts para uma quadra completa.
        /// </summary>
        public static List<BuildingLayout> PlanBlock(BlockInfo block, CityConfig config, int blockIndex,
            int citySeed, List<BlockFiller.OBB2D> reservedOBBs, ModularBuildingProfile profile = null)
        {
            List<BuildingLayout> plannedLayouts = new List<BuildingLayout>();
            if (block.polygon == null || block.polygon.Length < 3) return plannedLayouts;

            Vector3[] poly = block.polygon;
            int n = poly.Length;

            // Perfil padrão caso não fornecido
            if (profile == null)
            {
                profile = ModularBuildingProfile.CreateDefault(block.zoneType);
                if (config != null)
                {
                    if (config.minBuildingModulesWidth > 0) profile.minModulesWidth = config.minBuildingModulesWidth;
                    if (config.maxBuildingModulesWidth >= profile.minModulesWidth) profile.maxModulesWidth = config.maxBuildingModulesWidth;
                    if (config.minBuildingModulesDepth > 0) profile.minModulesDepth = config.minBuildingModulesDepth;
                    if (config.maxBuildingModulesDepth >= profile.minModulesDepth) profile.maxModulesDepth = config.maxBuildingModulesDepth;
                    if (config.minBuildingFloors > 0) profile.minFloors = config.minBuildingFloors;
                    if (config.maxBuildingFloors >= profile.minFloors) profile.maxFloors = config.maxBuildingFloors;
                }
            }

            // OBBs ocupadas (inicia com reservas da pizzaria se houver)
            List<BlockFiller.OBB2D> occupiedOBBs = new List<BlockFiller.OBB2D>();
            if (reservedOBBs != null && reservedOBBs.Count > 0)
            {
                occupiedOBBs.AddRange(reservedOBBs);
            }

            // Sentido de enrolamento do polígono para garantir normal interna correta
            float signedArea = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                signedArea += poly[i].x * poly[j].z - poly[j].x * poly[i].z;
            }
            float windingSign = Mathf.Sign(signedArea);

            int buildingCounter = 0;
            float groundY = 0.02f + (config != null && config.sidewalkHeight > 0f ? config.sidewalkHeight : 0.15f);

            // Percorre cada aresta da quadra
            for (int edgeIdx = 0; edgeIdx < n; edgeIdx++)
            {
                Vector3 p0 = poly[(edgeIdx - 1 + n) % n];
                Vector3 p1 = poly[edgeIdx];
                Vector3 p2 = poly[(edgeIdx + 1) % n];
                Vector3 p3 = poly[(edgeIdx + 2) % n];

                Vector3 edgeVec = p2 - p1;
                float edgeLength = edgeVec.magnitude;
                if (edgeLength < 4f) continue; // Arestas menores que 4m não comportam um prédio modular mínimo

                Vector3 edgeDir = edgeVec.normalized;
                Vector3 prevEdgeDir = (p1 - p0).normalized;
                Vector3 nextEdgeDir = (p3 - p2).normalized;

                // Normal para dentro da quadra
                Vector3 inwardNormal = new Vector3(-edgeDir.z * windingSign, 0f, edgeDir.x * windingSign);

                // Margens de esquina (arredondadas para passos múltiplos de 2m)
                float angle1 = Vector3.Angle(-prevEdgeDir, edgeDir);
                float angle2 = Vector3.Angle(-edgeDir, nextEdgeDir);

                float margin1 = angle1 > 160f ? 0f : (config != null ? config.blockCornerMargin : 2f);
                float margin2 = angle2 > 160f ? 0f : (config != null ? config.blockCornerMargin : 2f);

                margin1 = Mathf.Min(margin1, edgeLength * 0.35f);
                margin2 = Mathf.Min(margin2, edgeLength * 0.35f);

                // Alinha as distâncias para passos inteiros de 2m
                float currentDistance = Mathf.Ceil(margin1 / 2f) * 2f;
                float maxDistance = edgeLength - margin2;

                while (currentDistance + (profile.minModulesWidth * 2f) <= maxDistance)
                {
                    // Seed determinística única para este slot e prédio
                    int buildingSeed = CalculateDeterministicSeed(citySeed, blockIndex, edgeIdx, buildingCounter);
                    System.Random slotRng = new System.Random(buildingSeed);

                    // Escolhe número de módulos de largura e profundidade
                    int targetWidthMods = slotRng.Next(profile.minModulesWidth, profile.maxModulesWidth + 1);
                    int targetDepthMods = slotRng.Next(profile.minModulesDepth, profile.maxModulesDepth + 1);
                    int targetFloors = slotRng.Next(profile.minFloors, profile.maxFloors + 1);

                    // Ajusta se ultrapassar a aresta
                    float availableWidth = maxDistance - currentDistance;
                    int maxPossibleWidthMods = Mathf.FloorToInt(availableWidth / 2.0f);
                    if (maxPossibleWidthMods < profile.minModulesWidth)
                    {
                        break;
                    }
                    targetWidthMods = Mathf.Min(targetWidthMods, maxPossibleWidthMods);

                    // Tenta encaixar o retângulo, reduzindo módulos caso necessário (nunca esticando nem quebrando peças)
                    bool placed = false;
                    for (int w = targetWidthMods; w >= profile.minModulesWidth && !placed; w--)
                    {
                        float widthMeters = w * 2.0f;
                        float distAlongEdge = currentDistance + (widthMeters * 0.5f);
                        Vector3 edgeCenterPoint = p1 + edgeDir * distAlongEdge;
                        edgeCenterPoint.y = groundY;

                        for (int d = targetDepthMods; d >= profile.minModulesDepth && !placed; d--)
                        {
                            float depthMeters = d * 2.0f;

                            // Testa contenção estrita do retângulo dentro do polígono da quadra
                            if (!IsRectangleContainedInPolygon(edgeCenterPoint, edgeDir, inwardNormal, widthMeters, depthMeters, poly))
                            {
                                continue;
                            }

                            // Rotação: a fachada olha para fora da quadra (direção oposta à inwardNormal)
                            Vector3 forwardDir = -inwardNormal;
                            if (forwardDir.sqrMagnitude < 0.01f) forwardDir = Vector3.forward;
                            Quaternion rot = Quaternion.LookRotation(forwardDir, Vector3.up);

                            // Centro 2D da OBB
                            Vector3 center3D = edgeCenterPoint + inwardNormal * (depthMeters * 0.5f);
                            Vector2 center2D = new Vector2(center3D.x, center3D.z);
                            Vector2 axisW = new Vector2(edgeDir.x, edgeDir.z).normalized;
                            Vector2 axisD = new Vector2(inwardNormal.x, inwardNormal.z).normalized;

                            // Margem de segurança de 0.1m para evitar overlaps numéricos
                            BlockFiller.OBB2D candidateOBB = new BlockFiller.OBB2D(
                                center2D,
                                new Vector2(widthMeters * 0.5f - 0.05f, depthMeters * 0.5f - 0.05f),
                                axisW,
                                axisD
                            );

                            // Valida contra prédios existentes e reservas (Pizzaria)
                            bool collides = false;
                            for (int o = 0; o < occupiedOBBs.Count; o++)
                            {
                                if (BlockFiller.OBBOverlap(candidateOBB, occupiedOBBs[o], out _, out _))
                                {
                                    collides = true;
                                    break;
                                }
                            }

                            if (collides) continue;

                            // Lote aprovado! Cria o layout puro
                            string bId = $"Building_B{blockIndex}_E{edgeIdx}_{buildingCounter}";
                            BuildingLayout layout = ModularBuildingAssembler.CreateProceduralLayout(
                                w, d, targetFloors, profile, edgeCenterPoint, rot, buildingSeed, bId, blockIndex);

                            plannedLayouts.Add(layout);
                            occupiedOBBs.Add(candidateOBB);
                            buildingCounter++;

                            currentDistance += widthMeters;
                            placed = true;
                        }
                    }

                    if (!placed)
                    {
                        // Avança 2 metros e tenta novamente
                        currentDistance += 2.0f;
                    }
                }
            }

            return plannedLayouts;
        }

        /// <summary>
        /// Verifica se todos os 4 cantos e as 4 arestas do retângulo estão estritamente contidos no polígono.
        /// </summary>
        public static bool IsRectangleContainedInPolygon(Vector3 frontCenter, Vector3 edgeDir, Vector3 inwardNormal,
            float width, float depth, Vector3[] poly)
        {
            float halfW = width * 0.5f;

            // 4 cantos do retângulo no chão (plano XZ)
            Vector3 cFL = frontCenter - edgeDir * halfW;
            Vector3 cFR = frontCenter + edgeDir * halfW;
            Vector3 cBR = cFR + inwardNormal * depth;
            Vector3 cBL = cFL + inwardNormal * depth;

            Vector2 pFL = new Vector2(cFL.x, cFL.z);
            Vector2 pFR = new Vector2(cFR.x, cFR.z);
            Vector2 pBR = new Vector2(cBR.x, cBR.z);
            Vector2 pBL = new Vector2(cBL.x, cBL.z);

            // 1. Os cantos de trás (e da frente) devem estar dentro ou na borda do polígono
            // Inset de 0.05m para os fundos
            Vector2 insetBR = pBR - new Vector2(inwardNormal.x, inwardNormal.z) * 0.05f;
            Vector2 insetBL = pBL - new Vector2(inwardNormal.x, inwardNormal.z) * 0.05f;

            if (!PolygonUtils.IsPointInPolygon(insetBR, poly) || !PolygonUtils.IsPointInPolygon(insetBL, poly))
            {
                return false;
            }

            // 2. Nenhuma das arestas laterais ou de fundo pode cruzar qualquer aresta da borda do polígono
            Vector2[] rectCorners = new[] { pFL, pFR, pBR, pBL };
            for (int r = 0; r < 4; r++)
            {
                Vector2 rA = rectCorners[r];
                Vector2 rB = rectCorners[(r + 1) % 4];

                for (int p = 0; p < poly.Length; p++)
                {
                    Vector2 polyA = new Vector2(poly[p].x, poly[p].z);
                    Vector2 polyB = new Vector2(poly[(p + 1) % poly.Length].x, poly[(p + 1) % poly.Length].z);

                    // Ignora aresta frontal se coincidir com a aresta da calçada
                    if (r == 0) continue;

                    if (SegmentsIntersect(rA, rB, polyA, polyB))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float d1 = Cross2D(a2 - a1, b1 - a1);
            float d2 = Cross2D(a2 - a1, b2 - a1);
            float d3 = Cross2D(b2 - b1, a1 - b1);
            float d4 = Cross2D(b2 - b1, a2 - b1);

            if (((d1 > 0.001f && d2 < -0.001f) || (d1 < -0.001f && d2 > 0.001f)) &&
                ((d3 > 0.001f && d4 < -0.001f) || (d3 < -0.001f && d4 > 0.001f)))
            {
                return true;
            }

            return false;
        }

        private static float Cross2D(Vector2 u, Vector2 v)
        {
            return u.x * v.y - u.y * v.x;
        }

        /// <summary>
        /// Função hash determinística sem alocação e independente de GetHashCode() para estabilidade em rede.
        /// </summary>
        public static int CalculateDeterministicSeed(int citySeed, int blockId, int edgeId, int buildingIndex)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + citySeed;
                hash = hash * 31 + blockId;
                hash = hash * 31 + edgeId;
                hash = hash * 31 + buildingIndex;
                hash = hash * 31 + 42069; // generator version tag
                return Math.Abs(hash == 0 ? 1 : hash);
            }
        }
    }
}

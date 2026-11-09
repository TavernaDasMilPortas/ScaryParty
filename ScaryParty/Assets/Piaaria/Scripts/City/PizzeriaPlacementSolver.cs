using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.City
{
    /// <summary>
    /// Resultado detalhado da resolução de implantação da pizzaria.
    /// </summary>
    public class PlacementResult
    {
        public bool success;
        public int blockIndex = -1;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 entranceDirectionWorld = Vector3.forward;
        public Vector3 bancadaPosition;
        public List<BlockFiller.OBB2D> reservedOBBs = new List<BlockFiller.OBB2D>();
        public List<Vector3[]> worldStructuralPolygons = new List<Vector3[]>();
        public string failureReason = string.Empty;
        public float score = float.MaxValue;
    }

    /// <summary>
    /// Resolvedor geométrico de implantação da pizzaria modular em quadras poligonais.
    /// Garante que a planta em L fique completamente contida no polígono edificável da quadra,
    /// alinha a fachada com a rua frontal respeitando recuos, e calcula a altura exata do piso.
    /// </summary>
    public static class PizzeriaPlacementSolver
    {
        /// <summary>
        /// Resolve a pose ideal e a reserva da pizzaria dentro da lista de quadras da cidade.
        /// </summary>
        public static PlacementResult Solve(
            List<BlockInfo> blocks,
            GameObject pizzeriaPrefab,
            CityConfig config,
            System.Random rng = null,
            float frontSetback = 0.5f,
            float minStreetSeparation = 1.0f)
        {
            var result = new PlacementResult();

            if (blocks == null || blocks.Count == 0)
            {
                result.failureReason = "Nenhuma quadra disponível para posicionar a pizzaria.";
                return result;
            }

            if (pizzeriaPrefab == null)
            {
                result.failureReason = "pizzeriaPrefab nulo.";
                return result;
            }

            PizzeriaFootprint footprint = pizzeriaPrefab.GetComponent<PizzeriaFootprint>();
            Rect[] localRegions = footprint != null ? footprint.GetStructuralRegions() : new Rect[] { Rect.MinMaxRect(0f, -13.2f, 32.8f, 9.2f), Rect.MinMaxRect(16.4f, -27.6f, 32.8f, -13.2f) };
            Vector2 entranceDirLocal = footprint != null ? footprint.entranceDirectionLocal : new Vector2(-1f, 0f);
            float floorLocalY = footprint != null ? footprint.floorSurfaceLocalY : -1.6f;

            // Calcula área total requerida
            float requiredArea = 0f;
            float minDimensionX = 0f;
            float minDimensionZ = 0f;
            for (int r = 0; r < localRegions.Length; r++)
            {
                requiredArea += localRegions[r].width * localRegions[r].height;
                minDimensionX = Mathf.Max(minDimensionX, localRegions[r].width);
                minDimensionZ = Mathf.Max(minDimensionZ, localRegions[r].height);
            }
            float minBlockDimension = Mathf.Max(minDimensionX, minDimensionZ) * 0.85f;

            PlacementResult bestCandidate = null;
            float bestScore = float.MaxValue;

            // Avalia cada quadra
            for (int bIdx = 0; bIdx < blocks.Count; bIdx++)
            {
                BlockInfo block = blocks[bIdx];
                if (block.polygon == null || block.polygon.Length < 3) continue;

                // Rejeição estrita de quadras pequenas (sem penalidade arbitrária)
                if (block.area < requiredArea * 1.05f) continue;
                float maxDim = Mathf.Max(block.size.x, block.size.z);
                if (maxDim < minBlockDimension) continue;

                Vector3[] poly = block.polygon;
                int n = poly.Length;

                float signedArea = 0f;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    signedArea += poly[i].x * poly[j].z - poly[j].x * poly[i].z;
                }
                float windingSign = Mathf.Sign(signedArea);

                // Testa cada aresta da quadra
                for (int e = 0; e < n; e++)
                {
                    Vector3 p1 = poly[e];
                    Vector3 p2 = poly[(e + 1) % n];
                    Vector3 edgeVec = p2 - p1;
                    float edgeLen = edgeVec.magnitude;

                    // Aresta deve ter comprimento suficiente para a fachada
                    if (edgeLen < 15f) continue;

                    Vector3 edgeDir = edgeVec.normalized;
                    Vector3 inwardNormal = new Vector3(-edgeDir.z * windingSign, 0, edgeDir.x * windingSign);
                    Vector3 outwardNormal = -inwardNormal; // Aponta para a rua

                    // Rotação: a fachada local aponta para a normal externa (para a rua)
                    Vector3 frontLocal3 = new Vector3(entranceDirLocal.x, 0f, entranceDirLocal.y);
                    if (frontLocal3.sqrMagnitude < 0.0001f) frontLocal3 = Vector3.back;
                    frontLocal3.Normalize();

                    Quaternion candidateRot = Quaternion.LookRotation(outwardNormal, Vector3.up)
                                              * Quaternion.Inverse(Quaternion.LookRotation(frontLocal3, Vector3.up));

                    // Projeção máxima do prédio na direção frontal
                    float maxFrontProj = float.MinValue;
                    for (int r = 0; r < localRegions.Length; r++)
                    {
                        Rect rect = localRegions[r];
                        Vector3[] corners = GetRectCornersLocal(rect);
                        for (int c = 0; c < corners.Length; c++)
                        {
                            float p = Vector3.Dot(corners[c], frontLocal3);
                            if (p > maxFrontProj) maxFrontProj = p;
                        }
                    }

                    // Amostra posições ao longo da aresta
                    float margin = Mathf.Min(8f, edgeLen * 0.25f);
                    int sampleSteps = Mathf.Clamp(Mathf.FloorToInt((edgeLen - 2f * margin) / 4f), 1, 6);
                    float stepSize = (edgeLen - 2f * margin) / Mathf.Max(1, sampleSteps);

                    for (int s = 0; s <= sampleSteps; s++)
                    {
                        float currentDist = margin + s * stepSize;
                        Vector3 edgePoint = p1 + edgeDir * currentDist;

                        // Posição raiz: recuo frontal para que a fachada fique tangenciando a calçada
                        Vector3 candidatePos = edgePoint - outwardNormal * (maxFrontProj + frontSetback);

                        // Altura exata: a calçada apoia o piso
                        candidatePos.y = config.sidewalkHeight - floorLocalY;

                        // Validação de contenção estrita no polígono da quadra
                        if (!ValidateContainment(candidatePos, candidateRot, localRegions, poly, minStreetSeparation))
                            continue;

                        // Score: menor distância ao centro da cidade + bônus de zona comercial
                        float distToCenter = Vector3.Distance(block.worldCenter, Vector3.zero);
                        float zoneBonus = 0f;
                        switch (block.zoneType)
                        {
                            case ZoneType.Commercial: zoneBonus = 0f; break;
                            case ZoneType.Residential: zoneBonus = 15f; break;
                            case ZoneType.Industrial: zoneBonus = 40f; break;
                            case ZoneType.MonsterZone: zoneBonus = 100f; break;
                        }

                        float score = distToCenter + zoneBonus;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestCandidate = new PlacementResult
                            {
                                success = true,
                                blockIndex = bIdx,
                                position = candidatePos,
                                rotation = candidateRot,
                                entranceDirectionWorld = outwardNormal,
                                score = score
                            };

                            // Constrói OBBs reservadas para o BlockFiller
                            bestCandidate.reservedOBBs = BuildReservedOBBs(candidatePos, candidateRot, localRegions, 0.5f);
                            bestCandidate.worldStructuralPolygons = BuildWorldPolygons(candidatePos, candidateRot, localRegions);

                            // Posição da bancada (na calçada em frente à fachada)
                            Vector3 bancadaPos = edgePoint + outwardNormal * 1.5f;
                            bancadaPos.y = config.sidewalkHeight + 0.5f;
                            bestCandidate.bancadaPosition = bancadaPos;
                        }
                    }
                }
            }

            if (bestCandidate != null && bestCandidate.success)
            {
                return bestCandidate;
            }

            result.failureReason = $"Nenhuma quadra foi capaz de conter a planta em L sem invasão de rua ou colisão perimetral (Avaliadas {blocks.Count} quadras).";
            return result;
        }

        private static bool ValidateContainment(
            Vector3 pos,
            Quaternion rot,
            Rect[] localRegions,
            Vector3[] blockPoly,
            float minSetback)
        {
            for (int r = 0; r < localRegions.Length; r++)
            {
                Rect rect = localRegions[r];
                Vector3[] corners = GetRectCornersLocal(rect);
                Vector3[] worldCorners = new Vector3[4];

                for (int c = 0; c < 4; c++)
                {
                    worldCorners[c] = pos + rot * corners[c];

                    // 1. Cada vértice deve estar dentro do polígono da quadra
                    if (!IsPointInsidePolygonXZ(worldCorners[c], blockPoly))
                        return false;
                }

                // 2. Nenhuma aresta do retângulo pode cruzar as arestas do polígono da quadra
                for (int c = 0; c < 4; c++)
                {
                    Vector3 segA = worldCorners[c];
                    Vector3 segB = worldCorners[(c + 1) % 4];

                    for (int p = 0; p < blockPoly.Length; p++)
                    {
                        Vector3 polyA = blockPoly[p];
                        Vector3 polyB = blockPoly[(p + 1) % blockPoly.Length];

                        if (SegmentsIntersectXZ(segA, segB, polyA, polyB))
                            return false;
                    }
                }
            }

            return true;
        }

        private static List<BlockFiller.OBB2D> BuildReservedOBBs(
            Vector3 pos,
            Quaternion rot,
            Rect[] localRegions,
            float safetyMargin)
        {
            var list = new List<BlockFiller.OBB2D>(localRegions.Length);
            Vector2 axisX = new Vector2((rot * Vector3.right).x, (rot * Vector3.right).z).normalized;
            Vector2 axisZ = new Vector2((rot * Vector3.forward).x, (rot * Vector3.forward).z).normalized;

            for (int i = 0; i < localRegions.Length; i++)
            {
                Rect r = localRegions[i];
                Vector3 centerLocal = new Vector3(r.center.x, 0f, r.center.y);
                Vector3 centerWorld = pos + rot * centerLocal;

                Vector2 halfExtents = new Vector2((r.width * 0.5f) + safetyMargin, (r.height * 0.5f) + safetyMargin);
                Vector2 center2D = new Vector2(centerWorld.x, centerWorld.z);

                list.Add(new BlockFiller.OBB2D(center2D, halfExtents, axisX, axisZ));
            }

            return list;
        }

        private static List<Vector3[]> BuildWorldPolygons(
            Vector3 pos,
            Quaternion rot,
            Rect[] localRegions)
        {
            var list = new List<Vector3[]>(localRegions.Length);
            for (int i = 0; i < localRegions.Length; i++)
            {
                Rect r = localRegions[i];
                Vector3[] cLocal = GetRectCornersLocal(r);
                Vector3[] cWorld = new Vector3[4];
                for (int c = 0; c < 4; c++)
                {
                    cWorld[c] = pos + rot * cLocal[c];
                }
                list.Add(cWorld);
            }
            return list;
        }

        public static Vector3[] GetRectCornersLocal(Rect r)
        {
            return new Vector3[]
            {
                new Vector3(r.xMin, 0f, r.yMin),
                new Vector3(r.xMax, 0f, r.yMin),
                new Vector3(r.xMax, 0f, r.yMax),
                new Vector3(r.xMin, 0f, r.yMax)
            };
        }

        public static bool IsPointInsidePolygonXZ(Vector3 point, Vector3[] poly)
        {
            int n = poly.Length;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((poly[i].z > point.z) != (poly[j].z > point.z)) &&
                    (point.x < (poly[j].x - poly[i].x) * (point.z - poly[i].z) / (poly[j].z - poly[i].z + 0.00001f) + poly[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static bool SegmentsIntersectXZ(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2)
        {
            float d1 = Cross2D(a2 - a1, b1 - a1);
            float d2 = Cross2D(a2 - a1, b2 - a1);
            float d3 = Cross2D(b2 - b1, a1 - b1);
            float d4 = Cross2D(b2 - b1, a2 - b1);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            return false;
        }

        private static float Cross2D(Vector3 v, Vector3 w)
        {
            return v.x * w.z - v.z * w.x;
        }
    }
}

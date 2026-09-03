using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fills city blocks with procedurally generated buildings along the perimeter.
/// Buildings face outward toward the street, leaving the center empty.
/// </summary>
public class BlockFiller : MonoBehaviour
{
    /// <summary>
    /// Fills a block with procedurally generated buildings.
    /// </summary>
    public void FillBlock(BlockInfo block, CityConfig config, System.Random rng, Transform parent, Material[] buildingMaterials, int blockIndex)
    {
        if (block.polygon != null && block.polygon.Length >= 3)
        {
            FillBlockPerimeter(block, config, rng, parent, buildingMaterials, blockIndex);
        }
        else
        {
            // Backward Compatibility: Fall back to rectangular grid
            FillBlockGrid(block, config, rng, parent, buildingMaterials, blockIndex);
        }
    }

    /// <summary>
    /// Places buildings tightly along the edges (perimeter) of the block polygon, facing outward.
    /// </summary>
    private void FillBlockPerimeter(BlockInfo block, CityConfig config, System.Random rng, Transform parent, Material[] buildingMaterials, int blockIndex)
    {
        GameObject blockParent = new GameObject($"Block_{blockIndex}");
        blockParent.transform.SetParent(parent);
        blockParent.transform.position = block.worldCenter; // Center for hierarchy

        Vector3[] poly = block.polygon;
        int n = poly.Length;
        
        // Calculate centroid to help determine inward normal
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            centroid += poly[i];
        }
        centroid /= n;

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        int buildingCount = 0;
        List<OBB2D> placedBuildings = new List<OBB2D>();

        // Calculate winding sign once for the polygon
        float signedArea = 0f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            signedArea += poly[i].x * poly[j].z - poly[j].x * poly[i].z;
        }
        float windingSign = Mathf.Sign(signedArea);

        // Varredura: Pass 1 coloca prédios normais. Pass 2 varre novamente colocando casas pequenas (filler).
        for (int pass = 1; pass <= 2; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = poly[(i - 1 + n) % n];
                Vector3 p1 = poly[i];
                Vector3 p2 = poly[(i + 1) % n];
                Vector3 p3 = poly[(i + 2) % n];

                Vector3 edgeVec = p2 - p1;
                float edgeLength = edgeVec.magnitude;
                
                if (edgeLength < 5f) continue; 

                Vector3 edgeDir = edgeVec.normalized;
                Vector3 prevEdgeDir = (p1 - p0).normalized;
                Vector3 nextEdgeDir = (p3 - p2).normalized;

                // Robust inward normal using the polygon's winding sign
                Vector3 inwardNormal = new Vector3(-edgeDir.z * windingSign, 0, edgeDir.x * windingSign);

                float angle1 = Vector3.Angle(-prevEdgeDir, edgeDir);
                float angle2 = Vector3.Angle(-edgeDir, nextEdgeDir);
                
                float margin1 = angle1 > 160f ? 0f : config.blockCornerMargin;
                if (angle1 < 85f) margin1 = Mathf.Max(margin1, config.maxBuildingDepth / Mathf.Tan(angle1 * 0.5f * Mathf.Deg2Rad));
                
                float margin2 = angle2 > 160f ? 0f : config.blockCornerMargin;
                if (angle2 < 85f) margin2 = Mathf.Max(margin2, config.maxBuildingDepth / Mathf.Tan(angle2 * 0.5f * Mathf.Deg2Rad));

                // Cap the margins so acute corners don't consume the entire edge
                float maxMargin = config.maxBuildingDepth * 1.5f;
                margin1 = Mathf.Min(margin1, maxMargin);
                margin2 = Mathf.Min(margin2, maxMargin);

                margin1 = Mathf.Min(margin1, edgeLength * 0.4f);
                margin2 = Mathf.Min(margin2, edgeLength * 0.4f);

                CityGenLogger.StartEdge(i, edgeLength, margin1, margin2);

                float currentDistance = margin1;
                float maxDistance = edgeLength - margin2;

                while (currentDistance < maxDistance)
                {
                    float bWidth;
                    if (pass == 1) bWidth = config.minBuildingWidth + (float)rng.NextDouble() * (config.maxBuildingWidth - config.minBuildingWidth);
                    else bWidth = 2f + (float)rng.NextDouble() * 3f;

                    float requestedWidth = bWidth;

                    if (currentDistance + bWidth > maxDistance)
                    {
                        bWidth = maxDistance - currentDistance;
                        if (bWidth < 2f) break; 
                    }

                    float bDepth;
                    if (pass == 1) bDepth = config.minBuildingDepth + (float)rng.NextDouble() * (config.maxBuildingDepth - config.minBuildingDepth);
                    else bDepth = 2f + (float)rng.NextDouble() * 5f;

                    float requestedDepth = bDepth;

                    float bHeight = GetRandomHeight(block.zoneType, rng);
                    float distAlongEdge = currentDistance + (bWidth * 0.5f);
                
                // Camada 2: Raycast depth calculation
                Vector3 edgePoint = p1 + edgeDir * distAlongEdge;
                float originalBDepth = bDepth;
                bool isInside = false;

                // Dynamic width and depth shrinking loop
                float minAllowedDepth = 2f;
                float minAllowedWidth = 2f;

                while (bWidth >= minAllowedWidth)
                {
                    float availableDepth = GetBlockDepthAtPoint(p1, edgeDir, inwardNormal, currentDistance, bWidth, poly);
                    
                    // To fill the large empty spaces inside the blocks, we stretch the building depth inwards
                    float targetDepth = availableDepth * 0.5f; // Meet buildings from the opposite side in the middle
                    
                    // Cap it so it doesn't get ridiculously long on massive blocks, but allow it to be much larger than maxBuildingDepth
                    float absoluteMaxDepth = Mathf.Max(config.maxBuildingDepth * 2f, 35f); 
                    
                    bDepth = targetDepth - (float)rng.NextDouble() * 2f;
                    bDepth = Mathf.Clamp(bDepth, config.minBuildingDepth, absoluteMaxDepth);

                    if (bDepth >= minAllowedDepth)
                    {
                        // Check if back corners are strictly inside the polygon
                        float extX = (bWidth * 0.5f) - 0.1f;
                        float extZ = (bDepth * 0.5f) - 0.1f;
                        Vector3 cCenter = edgePoint + inwardNormal * (bDepth * 0.5f);
                        Vector3 backRight = cCenter + edgeDir * extX + inwardNormal * extZ;
                        Vector3 backLeft = cCenter - edgeDir * extX + inwardNormal * extZ;

                        if (IsPointInPolygon(new Vector2(backRight.x, backRight.z), poly) && 
                            IsPointInPolygon(new Vector2(backLeft.x, backLeft.z), poly))
                        {
                            isInside = true;
                            break;
                        }
                    }

                    // Shrink depth, then width if needed
                    bDepth -= 0.2f; 
                    if (bDepth < minAllowedDepth)
                    {
                        bWidth -= 1f;
                        bDepth = originalBDepth; 
                    }
                }

                if (!isInside || bWidth < minAllowedWidth || bDepth < minAllowedDepth) 
                {
                    CityGenLogger.LogAttempt(pass, currentDistance, requestedWidth, requestedDepth, false, bWidth, bDepth, "FailedPolygonOrDepth");
                    currentDistance += 1f;
                    continue; 
                }

                Vector3 forwardDir = -inwardNormal;
                if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;
                Quaternion rotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                // Camada 3: OBB 2D Collision (SAT)
                // Garante eixos normalizados no plano XZ para que as projeções SAT sejam corretas.
                Vector2 axisW = new Vector2(edgeDir.x, edgeDir.z).normalized;
                Vector2 axisD = new Vector2(inwardNormal.x, inwardNormal.z).normalized;

                Vector3 centerPos = edgePoint + inwardNormal * (bDepth * 0.5f);
                
                // Aplicar a margem de segurança no tamanho do OBB para permitir costas-com-costas em blocos finos
                float obbDepthExtents = Mathf.Max(0.1f, (bDepth * 0.5f) - (config.buildingInnerSafetyMargin * 0.5f));
                
                OBB2D candidateOBB = new OBB2D(
                    new Vector2(centerPos.x, centerPos.z),
                    new Vector2(bWidth * 0.5f - 0.1f, obbDepthExtents),
                    axisW,
                    axisD
                );
                
                bool discarded = false;
                foreach (var otherOBB in placedBuildings)
                {
                    if (OBBOverlap(candidateOBB, otherOBB, out float penetration, out Vector2 shrinkAxis))
                    {
                        // Projeta a penetração no eixo de profundidade (axisD) do candidato.
                        // Só faz sentido encolher bDepth se o overlap estiver alinhado com esse eixo.
                        float depthAlignment = Mathf.Abs(Vector2.Dot(shrinkAxis.normalized, axisD));
                        float depthPenetration = penetration / Mathf.Max(depthAlignment, 0.01f);

                        if (depthPenetration >= bDepth * 0.4f || depthAlignment < 0.3f)
                        {
                            // Penetração severa, ou no eixo errado: descarta o prédio
                            discarded = true;
                            Debug.LogWarning($"Building discarded due to overlap in Block_{blockIndex}");
                            break;
                        }
                        else
                        {
                            bDepth -= depthPenetration;
                            if (bDepth < minAllowedDepth)
                            {
                                discarded = true;
                                break;
                            }
                            centerPos = edgePoint + inwardNormal * (bDepth * 0.5f);
                            float newObbDepthExtents = Mathf.Max(0.1f, (bDepth * 0.5f) - (config.buildingInnerSafetyMargin * 0.5f));
                            candidateOBB = new OBB2D(
                                new Vector2(centerPos.x, centerPos.z),
                                new Vector2(bWidth * 0.5f - 0.1f, newObbDepthExtents),
                                axisW,
                                axisD
                            );
                        }
                    }
                }

                if (discarded)
                {
                    CityGenLogger.LogAttempt(pass, currentDistance, requestedWidth, requestedDepth, false, bWidth, bDepth, "FailedSAT");
                    currentDistance += 1f;
                    continue;
                }

                CityGenLogger.LogAttempt(pass, currentDistance, requestedWidth, requestedDepth, true, bWidth, bDepth, "");
                placedBuildings.Add(candidateOBB);

                GameObject buildingObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                buildingObj.name = $"Building_{buildingCount++}";
                buildingObj.transform.SetParent(blockParent.transform);
                buildingObj.transform.position = centerPos + new Vector3(0, bHeight * 0.5f, 0);
                buildingObj.transform.rotation = rotation;
                buildingObj.transform.localScale = new Vector3(bWidth, bHeight, bDepth);

                Renderer renderer = buildingObj.GetComponent<Renderer>();
                
                Material baseMat = null;
                if (buildingMaterials != null && buildingMaterials.Length > 0)
                {
                    baseMat = buildingMaterials[rng.Next(buildingMaterials.Length)];
                }
                
                if (baseMat == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    baseMat = new Material(shader);
                }

                renderer.sharedMaterial = baseMat;

                Color buildingColor = GenerateOrganicColor(rng);
                propBlock.SetColor("_Color", buildingColor); 
                propBlock.SetColor("_BaseColor", buildingColor); 
                propBlock.SetColor("_MainColor", buildingColor); 
                renderer.SetPropertyBlock(propBlock);

                CityBuilding building = buildingObj.AddComponent<CityBuilding>();
                building.zone = block.zoneType;
                building.canHaveDeliveryPoint = (block.zoneType != ZoneType.Industrial);
                building.buildingColor = buildingColor;
                building.CalculateEntrance(block.worldCenter);

                currentDistance += bWidth;
                // Gap ZERO para garantir envelopamento contínuo (parede de prédios)
            }
        }
        } // Closing the pass loop

        // Gera o miolo maciço para impedir visão interna (apenas se configurado)
        if (config.generateSolidCore)
        {
            GenerateSolidCore(poly, centroid, blockParent.transform, GetRandomHeight(block.zoneType, rng) * 0.8f, Color.gray);
        }
    }

    private void GenerateSolidCore(Vector3[] poly, Vector3 centroid, Transform parent, float height, Color color)
    {
        GameObject core = new GameObject("SolidCore_Maciço");
        core.transform.SetParent(parent);
        core.transform.position = Vector3.zero;

        MeshFilter mf = core.AddComponent<MeshFilter>();
        MeshRenderer mr = core.AddComponent<MeshRenderer>();
        
        Mesh mesh = new Mesh();
        mesh.name = "SolidCoreMesh";
        
        int n = poly.Length;
        Vector3[] vertices = new Vector3[n * 2 + 2];
        int[] triangles = new int[n * 12];
        
        vertices[0] = new Vector3(centroid.x, 0, centroid.z);
        vertices[1] = new Vector3(centroid.x, height, centroid.z);
        
        for (int i = 0; i < n; i++)
        {
            // Inset by 15% so the core doesn't clip through the thin building facades
            Vector3 insetPos = Vector3.Lerp(poly[i], centroid, 0.15f);
            vertices[i * 2 + 2] = new Vector3(insetPos.x, 0, insetPos.z);
            vertices[i * 2 + 3] = new Vector3(insetPos.x, height, insetPos.z);
        }

        int t = 0;
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int v0 = i * 2 + 2;
            int v1 = i * 2 + 3;
            int v2 = next * 2 + 2;
            int v3 = next * 2 + 3;
            
            triangles[t++] = v0; triangles[t++] = v1; triangles[t++] = v2;
            triangles[t++] = v2; triangles[t++] = v1; triangles[t++] = v3;
            
            triangles[t++] = v1; triangles[t++] = 1; triangles[t++] = v3;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        mf.mesh = mesh;
        
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader) { color = color };
        mr.sharedMaterial = mat;
    }

    /// <summary>
    /// Generates a varied, organic color based on the zone type using HSV shifts.
    /// </summary>
    private Color GenerateOrganicColor(ZoneType zone, System.Random rng)
    {
        // 15% chance for a neutral/grey/white building in any zone for realism
        if (rng.NextDouble() < 0.15)
        {
            float v = 0.3f + (float)rng.NextDouble() * 0.6f; // Dark grey to almost white
            float s = (float)rng.NextDouble() * 0.1f; // Very low saturation
            float h = (float)rng.NextDouble();
            return Color.HSVToRGB(h, s, v);
        }

        float baseHue = 0f;
        float hueVar = 0.05f; // Slight hue shift
        float satMin = 0.4f, satMax = 0.8f;
        float valMin = 0.4f, valMax = 0.9f;

        switch (zone)
        {
            case ZoneType.Residential: 
                baseHue = 0.6f; // Blue
                break;
            case ZoneType.Commercial: 
                baseHue = 0.15f; // Yellow/Orange
                satMax = 0.9f;
                valMin = 0.6f;
                break;
            case ZoneType.Industrial: 
                baseHue = 0.05f; // Rust/Brown
                satMax = 0.5f;
                valMin = 0.3f;
                valMax = 0.6f;
                break;
            case ZoneType.MonsterZone: 
                baseHue = 0.8f; // Purple
                satMin = 0.6f;
                valMin = 0.2f;
                break;
        }

        // Apply random shifts
        float finalHue = baseHue + (float)((rng.NextDouble() - 0.5) * hueVar * 2f);
        if (finalHue < 0f) finalHue += 1f;
        if (finalHue > 1f) finalHue -= 1f;

        float finalSat = satMin + (float)rng.NextDouble() * (satMax - satMin);
        float finalVal = valMin + (float)rng.NextDouble() * (valMax - valMin);

        return Color.HSVToRGB(finalHue, finalSat, finalVal);
    }

    /// <summary>
    /// Old method for rectangular grid subdivision (used as fallback).
    /// </summary>
    private void FillBlockGrid(BlockInfo block, CityConfig config, System.Random rng, Transform parent, Material[] buildingMaterials, int blockIndex)
    {
        GameObject blockParent = new GameObject($"Block_{blockIndex}");
        blockParent.transform.SetParent(parent);
        blockParent.transform.position = block.worldCenter;

        int cols = rng.Next(1, 4);
        int rows = rng.Next(1, 4);

        float margin = 2f;
        float availableWidth = block.size.x - (margin * 2);
        float availableDepth = block.size.z - (margin * 2);

        float cellWidth = availableWidth / cols;
        float cellDepth = availableDepth / rows;
        float spacing = 1f;

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

        for (int x = 0; x < cols; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                float bWidth = cellWidth - spacing;
                float bDepth = cellDepth - spacing;

                if (bWidth <= 1f || bDepth <= 1f) continue;

                float bHeight = GetRandomHeight(block.zoneType, rng);

                Vector3 localPos = new Vector3(
                    -availableWidth / 2f + (cellWidth * x) + (cellWidth / 2f),
                    bHeight / 2f,
                    -availableDepth / 2f + (cellDepth * z) + (cellDepth / 2f)
                );

                bWidth *= (float)(0.8 + rng.NextDouble() * 0.4);
                bDepth *= (float)(0.8 + rng.NextDouble() * 0.4);

                localPos.x += (float)((rng.NextDouble() - 0.5) * spacing);
                localPos.z += (float)((rng.NextDouble() - 0.5) * spacing);

                GameObject buildingObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                buildingObj.name = $"Building_{x}_{z}";
                buildingObj.transform.SetParent(blockParent.transform);
                buildingObj.transform.localPosition = localPos;
                buildingObj.transform.localScale = new Vector3(bWidth, bHeight, bDepth);

                Renderer renderer = buildingObj.GetComponent<Renderer>();
                
                Material baseMat = null;
                if (buildingMaterials != null && buildingMaterials.Length > 0)
                {
                    baseMat = buildingMaterials[rng.Next(buildingMaterials.Length)];
                }
                
                if (baseMat == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    baseMat = new Material(shader);
                }

                renderer.sharedMaterial = baseMat;

                Color buildingColor = GenerateOrganicColor(rng);
                buildingColor.a = 1f;
                propBlock.SetColor("_Color", buildingColor); 
                propBlock.SetColor("_BaseColor", buildingColor); 
                propBlock.SetColor("_MainColor", buildingColor); 
                renderer.SetPropertyBlock(propBlock);

                CityBuilding building = buildingObj.AddComponent<CityBuilding>();
                building.zone = block.zoneType;
                building.canHaveDeliveryPoint = (block.zoneType != ZoneType.Industrial);
                building.buildingColor = buildingColor; 
                building.CalculateEntrance(block.worldCenter);
            }
        }
    }

    private float GetRandomHeight(ZoneType zone, System.Random rng)
    {
        switch (zone)
        {
            case ZoneType.Residential: return 5f + (float)rng.NextDouble() * 10f;
            case ZoneType.Commercial: return 8f + (float)rng.NextDouble() * 17f;
            case ZoneType.Industrial: return 4f + (float)rng.NextDouble() * 8f;
            case ZoneType.MonsterZone: return 3f + (float)rng.NextDouble() * 17f;
            default: return 10f;
        }
    }

    private Color GenerateOrganicColor(System.Random rng)
    {
        float h = (float)rng.NextDouble();
        float s = 0.5f + (float)rng.NextDouble() * 0.5f;
        float v = 0.4f + (float)rng.NextDouble() * 0.6f;
        return Color.HSVToRGB(h, s, v);
    }

    private float GetBlockDepthAtPoint(Vector3 p1, Vector3 edgeDir, Vector3 inNormal, float currentDistance, float bWidth, Vector3[] poly)
    {
        Vector3 rayStart = p1 + edgeDir * (currentDistance + bWidth * 0.5f);
        float minDepth = 9999f;

        for (int i = 0; i < poly.Length; i++)
        {
            Vector3 pA = poly[i];
            Vector3 pB = poly[(i + 1) % poly.Length];
            if (LineLineIntersection(out Vector3 intersect, rayStart, inNormal, pA, (pB - pA).normalized))
            {
                float t = Vector3.Dot(intersect - pA, (pB - pA).normalized);
                if (t >= 0 && t <= Vector3.Distance(pA, pB))
                {
                    // BUG FIX: Ensure the intersection is actually IN FRONT of the ray, not behind it!
                    if (Vector3.Dot((intersect - rayStart).normalized, inNormal) > 0.9f)
                    {
                        float dist = Vector3.Distance(rayStart, intersect);
                        if (dist > 0.1f && dist < minDepth)
                        {
                            minDepth = dist;
                        }
                    }
                }
            }
        }
        return minDepth;
    }

    private bool LineLineIntersection(out Vector3 intersection, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
    {
        float det = lineVec1.x * lineVec2.z - lineVec1.z * lineVec2.x;
        if (Mathf.Abs(det) < 0.0001f)
        {
            intersection = Vector3.zero;
            return false;
        }

        float dx = linePoint2.x - linePoint1.x;
        float dz = linePoint2.z - linePoint1.z;

        float u = (dx * lineVec2.z - dz * lineVec2.x) / det;
        intersection = linePoint1 + lineVec1 * u;
        return true;
    }

    private bool IsPointInPolygon(Vector2 p, Vector3[] poly)
    {
        bool inside = false;
        int j = poly.Length - 1;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 pi = new Vector2(poly[i].x, poly[i].z);
            Vector2 pj = new Vector2(poly[j].x, poly[j].z);
            if (((pi.y > p.y) != (pj.y > p.y)) &&
                (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x))
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }

    private struct OBB2D
    {
        public Vector2 center;
        public Vector2 halfExtents;
        public Vector2 axisX;
        public Vector2 axisZ;

        public OBB2D(Vector2 center, Vector2 halfExtents, Vector2 axisX, Vector2 axisZ)
        {
            this.center = center;
            this.halfExtents = halfExtents;
            this.axisX = axisX;
            this.axisZ = axisZ;
        }
    }

    private static bool OBBOverlap(OBB2D a, OBB2D b, out float penetration, out Vector2 shrinkAxis)
    {
        penetration = float.MaxValue;
        shrinkAxis = Vector2.zero;

        // Todos os eixos devem ser normalizados para que as projeções SAT sejam métricas corretas.
        Vector2[] axes = new Vector2[]
        {
            a.axisX.normalized,
            a.axisZ.normalized,
            b.axisX.normalized,
            b.axisZ.normalized
        };

        foreach (Vector2 axis in axes)
        {
            if (axis.sqrMagnitude < 1e-6f) continue; // Ignora eixos degenerados

            float rA = a.halfExtents.x * Mathf.Abs(Vector2.Dot(a.axisX.normalized, axis))
                     + a.halfExtents.y * Mathf.Abs(Vector2.Dot(a.axisZ.normalized, axis));
            float rB = b.halfExtents.x * Mathf.Abs(Vector2.Dot(b.axisX.normalized, axis))
                     + b.halfExtents.y * Mathf.Abs(Vector2.Dot(b.axisZ.normalized, axis));

            float distance = Mathf.Abs(Vector2.Dot(b.center - a.center, axis));

            if (distance >= rA + rB)
            {
                return false; // Eixo separador encontrado: sem overlap
            }

            float overlap = (rA + rB) - distance;
            if (overlap < penetration)
            {
                penetration = overlap;
                shrinkAxis = axis; // Eixo com menor penetração (MPV — Minimum Penetration Vector)
            }
        }

        return true; // Overlap em todos os eixos testados
    }
}

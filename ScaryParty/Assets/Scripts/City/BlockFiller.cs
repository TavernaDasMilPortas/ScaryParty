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

        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = poly[(i - 1 + n) % n];
            Vector3 p1 = poly[i];
            Vector3 p2 = poly[(i + 1) % n];
            Vector3 p3 = poly[(i + 2) % n];

            Vector3 edgeVec = p2 - p1;
            float edgeLength = edgeVec.magnitude;
            
            if (edgeLength < 5f) continue; // Skip edges that are too small

            Vector3 edgeDir = edgeVec.normalized;
            Vector3 prevEdgeDir = (p1 - p0).normalized;
            Vector3 nextEdgeDir = (p3 - p2).normalized;

            // Calculate normals perpendicular to the edge
            Vector3 normal1 = new Vector3(-edgeDir.z, 0, edgeDir.x);
            Vector3 normal2 = new Vector3(edgeDir.z, 0, -edgeDir.x);

            // Test which normal points toward the centroid
            Vector3 testP1 = p1 + edgeDir * (edgeLength * 0.5f) + normal1 * 1f;
            Vector3 testP2 = p1 + edgeDir * (edgeLength * 0.5f) + normal2 * 1f;

            Vector3 inwardNormal = (Vector3.Distance(testP1, centroid) < Vector3.Distance(testP2, centroid)) ? normal1 : normal2;

            // Envelopamento contínuo, MAS com respeito trigonométrico às esquinas para NUNCA cruzar casas.
            float angle1 = Vector3.Angle(-prevEdgeDir, edgeDir);
            float angle2 = Vector3.Angle(-edgeDir, nextEdgeDir);
            
            float maxD = config.maxBuildingDepth;
            float margin1 = config.blockCornerMargin;
            if (angle1 > 5f && angle1 < 175f) margin1 = Mathf.Max(margin1, maxD / Mathf.Tan(angle1 * 0.5f * Mathf.Deg2Rad));
            
            float margin2 = config.blockCornerMargin;
            if (angle2 > 5f && angle2 < 175f) margin2 = Mathf.Max(margin2, maxD / Mathf.Tan(angle2 * 0.5f * Mathf.Deg2Rad));

            // Limitar para que a margem não consuma a rua inteira em triângulos muito agudos
            margin1 = Mathf.Min(margin1, edgeLength * 0.4f);
            margin2 = Mathf.Min(margin2, edgeLength * 0.4f);

            float currentDistance = margin1;
            float maxDistance = edgeLength - margin2;

            while (currentDistance < maxDistance)
            {
                float bWidth = config.minBuildingWidth + (float)rng.NextDouble() * (config.maxBuildingWidth - config.minBuildingWidth);
                if (currentDistance + bWidth > maxDistance)
                {
                    bWidth = maxDistance - currentDistance; // Force it to fill the exact remaining space perfectly!
                    if (bWidth < 2f) break; 
                }

                float bDepth = config.minBuildingDepth + (float)rng.NextDouble() * (config.maxBuildingDepth - config.minBuildingDepth);
                float bHeight = GetRandomHeight(block.zoneType, rng);

                float distAlongEdge = currentDistance + (bWidth * 0.5f);
                
                // Camada 2: Clamp pela distância ao centroide projetada na normal
                Vector3 edgePoint = p1 + edgeDir * distAlongEdge;
                float maxSafeDepth = Vector3.Dot(centroid - edgePoint, inwardNormal);
                maxSafeDepth = Mathf.Max(maxSafeDepth, 0f);
                maxSafeDepth = Mathf.Max(maxSafeDepth - config.buildingInnerSafetyMargin, config.minBuildingDepth * 0.5f);
                bDepth = Mathf.Min(bDepth, maxSafeDepth);

                if (bDepth < 2f) 
                {
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
                OBB2D candidateOBB = new OBB2D(
                    new Vector2(centerPos.x, centerPos.z),
                    new Vector2(bWidth * 0.5f, bDepth * 0.5f),
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
                            if (bDepth < 2f)
                            {
                                discarded = true;
                                break;
                            }
                            centerPos = edgePoint + inwardNormal * (bDepth * 0.5f);
                            candidateOBB = new OBB2D(
                                new Vector2(centerPos.x, centerPos.z),
                                new Vector2(bWidth * 0.5f, bDepth * 0.5f),
                                axisW,
                                axisD
                            );
                        }
                    }
                }

                if (discarded)
                {
                    currentDistance += 1f;
                    continue;
                }

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

                Color buildingColor = GenerateOrganicColor(block.zoneType, rng);
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

        // Gera o miolo maciço para impedir visão interna (apenas se configurado)
        if (config.generateSolidCore)
        {
            GenerateSolidCore(poly, centroid, blockParent.transform, GetRandomHeight(block.zoneType, rng) * 0.8f, GetZoneColor(block.zoneType) * 0.5f);
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

        Color zoneColor = GetZoneColor(block.zoneType);
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

                Color buildingColor = zoneColor * (float)(0.7 + rng.NextDouble() * 0.6);
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

    private Color GetZoneColor(ZoneType zone)
    {
        switch (zone)
        {
            case ZoneType.Residential: return new Color(0.3f, 0.5f, 0.9f);
            case ZoneType.Commercial: return new Color(0.9f, 0.8f, 0.3f);
            case ZoneType.Industrial: return new Color(0.8f, 0.4f, 0.2f);
            case ZoneType.MonsterZone: return new Color(0.6f, 0.2f, 0.7f);
            default: return Color.gray;
        }
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

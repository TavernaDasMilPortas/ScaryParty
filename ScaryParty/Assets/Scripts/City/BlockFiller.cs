using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fills city blocks with procedurally generated buildings.
/// Buildings are simple cubes with zone-based coloring via MaterialPropertyBlock.
/// </summary>
public class BlockFiller : MonoBehaviour
{
    /// <summary>
    /// Fills a block with procedurally generated buildings.
    /// </summary>
    public void FillBlock(BlockInfo block, CityConfig config, System.Random rng, Transform parent, Material[] buildingMaterials)
    {
        GameObject blockParent = new GameObject($"Block_{block.gridX}_{block.gridY}");
        blockParent.transform.SetParent(parent);
        blockParent.transform.position = block.worldCenter;

        // Grid subdivision for building placement
        int cols = rng.Next(1, 4);
        int rows = rng.Next(1, 4);

        // Leave space at block edges for sidewalks
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

                // Add size randomness
                bWidth *= (float)(0.8 + rng.NextDouble() * 0.4);
                bDepth *= (float)(0.8 + rng.NextDouble() * 0.4);

                // Add positional randomness
                localPos.x += (float)((rng.NextDouble() - 0.5) * spacing);
                localPos.z += (float)((rng.NextDouble() - 0.5) * spacing);

                // Create building cube
                GameObject buildingObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                buildingObj.name = $"Building_{x}_{z}";
                buildingObj.transform.SetParent(blockParent.transform);
                buildingObj.transform.localPosition = localPos;
                buildingObj.transform.localScale = new Vector3(bWidth, bHeight, bDepth);

                // Material & color tinting
                Renderer renderer = buildingObj.GetComponent<Renderer>();
                
                Material baseMat = null;
                if (buildingMaterials != null && buildingMaterials.Length > 0)
                {
                    baseMat = buildingMaterials[rng.Next(buildingMaterials.Length)];
                }
                
                // If no material provided, create a fallback URP/Standard material
                if (baseMat == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    baseMat = new Material(shader);
                }

                renderer.sharedMaterial = baseMat;

                Color buildingColor = zoneColor * (float)(0.7 + rng.NextDouble() * 0.6);
                buildingColor.a = 1f;
                propBlock.SetColor("_Color", buildingColor); // Standard
                propBlock.SetColor("_BaseColor", buildingColor); // URP
                propBlock.SetColor("_MainColor", buildingColor); // Distant Lands Toon
                renderer.SetPropertyBlock(propBlock);

                // Attach CityBuilding component
                CityBuilding building = buildingObj.AddComponent<CityBuilding>();
                building.zone = block.zoneType;
                building.canHaveDeliveryPoint = (block.zoneType != ZoneType.Industrial);
                building.buildingColor = buildingColor; // Save for play mode
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
}

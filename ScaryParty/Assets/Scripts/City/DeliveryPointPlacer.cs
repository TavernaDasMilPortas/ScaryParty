using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Places delivery point markers on buildings throughout the city.
/// Ensures balanced distribution across all blocks.
/// </summary>
public class DeliveryPointPlacer : MonoBehaviour
{
    /// <summary>
    /// Places delivery points on eligible buildings across all blocks.
    /// </summary>
    public List<DeliveryPoint> PlaceDeliveryPoints(BlockInfo[] blocks, CityBuilding[] buildings,
        CityConfig config, System.Random rng, Transform parent)
    {
        List<DeliveryPoint> deliveryPoints = new List<DeliveryPoint>();

        if (buildings == null || buildings.Length == 0) return deliveryPoints;

        // Group buildings by block
        Dictionary<string, List<CityBuilding>> buildingsByBlock = new Dictionary<string, List<CityBuilding>>();
        foreach (var building in buildings)
        {
            if (!building.canHaveDeliveryPoint) continue;

            // Use parent name to identify block (Block_X_Y)
            string blockKey = building.transform.parent != null ? building.transform.parent.name : "unknown";
            if (!buildingsByBlock.ContainsKey(blockKey))
                buildingsByBlock[blockKey] = new List<CityBuilding>();
            buildingsByBlock[blockKey].Add(building);
        }

        int pointIndex = 0;
        foreach (var kvp in buildingsByBlock)
        {
            List<CityBuilding> blockBuildings = kvp.Value;
            int numPoints = rng.Next(config.minDeliveryPointsPerBlock,
                Mathf.Min(config.maxDeliveryPointsPerBlock + 1, blockBuildings.Count + 1));

            // Shuffle buildings for random selection
            for (int i = blockBuildings.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var temp = blockBuildings[i];
                blockBuildings[i] = blockBuildings[j];
                blockBuildings[j] = temp;
            }

            for (int i = 0; i < numPoints && i < blockBuildings.Count; i++)
            {
                CityBuilding building = blockBuildings[i];

                // Create delivery point marker
                GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                markerObj.name = $"DeliveryPoint_{pointIndex}";
                markerObj.transform.SetParent(parent);
                
                // Position it on top of the sidewalk
                float yPos = config.sidewalkHeight > 0f ? config.sidewalkHeight : 0.15f;
                markerObj.transform.position = building.entrancePosition + Vector3.up * (yPos + 0.05f);
                
                // Visual scale: flat disc on the ground
                markerObj.transform.localScale = new Vector3(4f, 0.05f, 4f);

                // The PrimitiveType.Cylinder comes with a squashed CapsuleCollider. Destroy it.
                var oldCol = markerObj.GetComponent<Collider>();
                if (oldCol != null) Object.DestroyImmediate(oldCol);

                // Add a large, tall BoxCollider for easy collision detection
                BoxCollider boxCol = markerObj.AddComponent<BoxCollider>();
                boxCol.isTrigger = true;
                // Since the local scale Y is 0.05f, a size of Y=100 makes the collider 5 units tall in world space
                boxCol.size = new Vector3(1f, 100f, 1f); 
                boxCol.center = new Vector3(0, 50f, 0);

                // Glowing emissive material
                Renderer renderer = markerObj.GetComponent<Renderer>();
                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
                propBlock.SetColor("_BaseColor", new Color(1f, 0.4f, 0f, 0.8f));
                propBlock.SetColor("_EmissionColor", new Color(1f, 0.6f, 0f) * 3f);
                renderer.SetPropertyBlock(propBlock);

                // Setup component
                DeliveryPoint dp = markerObj.AddComponent<DeliveryPoint>();
                dp.associatedBuilding = building;
                dp.pointIndex = pointIndex;
                dp.isActive = false;

                building.deliveryPointIndex = pointIndex;
                deliveryPoints.Add(dp);
                pointIndex++;
            }
        }

        return deliveryPoints;
    }
}

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
                markerObj.transform.position = building.entrancePosition + Vector3.up * 0.1f;
                markerObj.transform.localScale = new Vector3(4f, 0.1f, 4f);

                // Set collider as trigger so it doesn't block player movement, but can still be Raycasted!
                var col = markerObj.GetComponent<Collider>();
                if (col != null)
                {
                    col.isTrigger = true;
                }

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

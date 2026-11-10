using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Places delivery point markers on eligible buildings throughout the city.
/// Scales target delivery points proportionally with the total number of eligible buildings
/// (using config.deliveryBuildingDensity) and ensures balanced geographic distribution across blocks.
/// Uses explicit blockId and unified elevation reference.
/// </summary>
public class DeliveryPointPlacer : MonoBehaviour
{
    public List<DeliveryPoint> PlaceDeliveryPoints(BlockInfo[] blocks, CityBuilding[] buildings,
        CityConfig config, System.Random rng, Transform parent)
    {
        List<DeliveryPoint> deliveryPoints = new List<DeliveryPoint>();
        if (buildings == null || buildings.Length == 0) return deliveryPoints;

        // Collect and sort all eligible buildings deterministically
        List<CityBuilding> eligible = new List<CityBuilding>();
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] != null && buildings[i].canHaveDeliveryPoint)
            {
                eligible.Add(buildings[i]);
            }
        }

        if (eligible.Count == 0) return deliveryPoints;

        // Stable sort by buildingId (or instance ID if buildingId missing) to prevent non-deterministic order
        eligible.Sort((a, b) => string.CompareOrdinal(a.buildingId ?? a.name, b.buildingId ?? b.name));

        // Group eligible buildings by block using explicit blockId if available
        Dictionary<string, List<CityBuilding>> buildingsByBlock = new Dictionary<string, List<CityBuilding>>();
        for (int i = 0; i < eligible.Count; i++)
        {
            var building = eligible[i];
            string blockKey = building.blockId >= 0 
                ? $"Block_{building.blockId}" 
                : (building.transform.parent != null ? building.transform.parent.name : "Block_Default");

            if (!buildingsByBlock.ContainsKey(blockKey))
            {
                buildingsByBlock[blockKey] = new List<CityBuilding>();
            }
            buildingsByBlock[blockKey].Add(building);
        }

        // Calculate target count based on density
        float density = config != null ? config.deliveryBuildingDensity : 0.18f;
        int minPoints = config != null ? config.minimumDeliveryPoints : 4;
        int maxPoints = config != null ? config.maximumDeliveryPoints : 50;

        int targetTotalPoints = Mathf.Clamp(Mathf.RoundToInt(eligible.Count * density), minPoints, Mathf.Min(maxPoints, eligible.Count));

        // Select buildings across blocks evenly
        List<CityBuilding> selectedBuildings = new List<CityBuilding>();
        var blockKeys = new List<string>(buildingsByBlock.Keys);
        blockKeys.Sort(string.CompareOrdinal);

        // Shuffle within each block using deterministic rng
        foreach (var key in blockKeys)
        {
            var bList = buildingsByBlock[key];
            for (int i = bList.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var temp = bList[i];
                bList[i] = bList[j];
                bList[j] = temp;
            }
        }

        // Round-robin selection across blocks until target count is reached
        int round = 0;
        bool addedAny = true;
        while (selectedBuildings.Count < targetTotalPoints && addedAny)
        {
            addedAny = false;
            for (int k = 0; k < blockKeys.Count; k++)
            {
                var bList = buildingsByBlock[blockKeys[k]];
                if (round < bList.Count)
                {
                    selectedBuildings.Add(bList[round]);
                    addedAny = true;
                    if (selectedBuildings.Count >= targetTotalPoints) break;
                }
            }
            round++;
        }

        // Instantiate markers for selected buildings
        int pointIndex = 0;
        for (int i = 0; i < selectedBuildings.Count; i++)
        {
            CityBuilding building = selectedBuildings[i];

            GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObj.name = $"DeliveryPoint_{pointIndex}";
            markerObj.transform.SetParent(parent);

            // Usa deliveryAnchor se configurado, senão entrancePosition
            Vector3 targetMarkerPos = building.deliveryAnchor != null ? building.deliveryAnchor.position : building.entrancePosition;
            // Desnível de segurança de 0.05m para evitar z-fighting sem duplicar altura de calçada
            markerObj.transform.position = new Vector3(targetMarkerPos.x, targetMarkerPos.y + 0.05f, targetMarkerPos.z);

            // Visual scale: flat disc on the ground
            markerObj.transform.localScale = new Vector3(4f, 0.05f, 4f);

            var oldCol = markerObj.GetComponent<Collider>();
            if (oldCol != null) DestroyImmediate(oldCol);

            BoxCollider boxCol = markerObj.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(1f, 100f, 1f);
            boxCol.center = new Vector3(0, 50f, 0);

            Renderer renderer = markerObj.GetComponent<Renderer>();
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_BaseColor", new Color(1f, 0.4f, 0f, 0.8f));
            propBlock.SetColor("_EmissionColor", new Color(1f, 0.6f, 0f) * 3f);
            renderer.SetPropertyBlock(propBlock);

            DeliveryPoint dp = markerObj.AddComponent<DeliveryPoint>();
            dp.associatedBuilding = building;
            dp.pointIndex = pointIndex;
            dp.isActive = false;

            building.deliveryPointIndex = pointIndex;
            deliveryPoints.Add(dp);
            pointIndex++;
        }

        return deliveryPoints;
    }
}

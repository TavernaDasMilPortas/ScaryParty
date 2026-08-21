using UnityEngine;

/// <summary>
/// Configuration data for procedural city generation.
/// Create from Assets menu: ScaryParty > City > CityConfig.
/// </summary>
[CreateAssetMenu(fileName = "NewCityConfig", menuName = "ScaryParty/City/CityConfig")]
public class CityConfig : ScriptableObject
{
    [Header("Grid Settings")]
    [Tooltip("Number of blocks in the X axis")]
    [Range(1, 50)] public int gridWidth = 6;

    [Tooltip("Number of blocks in the Z axis")]
    [Range(1, 50)] public int gridHeight = 6;

    [Tooltip("Width of a single city block in meters (X axis)")]
    public float blockWidth = 40f;

    [Tooltip("Depth of a single city block in meters (Z axis)")]
    public float blockDepth = 40f;

    [Tooltip("Width of the streets between blocks in meters")]
    public float streetWidth = 12f;

    [Tooltip("Width of the sidewalks on each side of the street")]
    public float sidewalkWidth = 2f;

    [Header("Generation Settings")]
    [Tooltip("Seed for random generation. 0 = random seed each time.")]
    public int seed = 0;

    [Header("Block & Buildings")]
    [Tooltip("How dense the buildings are within a block (0 to 1)")]
    [Range(0f, 1f)] public float buildingDensity = 0.8f;

    public float minBuildingHeight = 5f;
    public float maxBuildingHeight = 30f;

    public int minBuildingsPerBlock = 1;
    public int maxBuildingsPerBlock = 10;

    [Header("Zones")]
    [Tooltip("Probability of a block being Residential")]
    [Range(0f, 1f)] public float residentialProb = 0.4f;

    [Tooltip("Probability of a block being Commercial")]
    [Range(0f, 1f)] public float commercialProb = 0.3f;

    [Tooltip("Probability of a block being Industrial")]
    [Range(0f, 1f)] public float industrialProb = 0.2f;

    [Tooltip("Probability of a block being a Monster Zone")]
    [Range(0f, 1f)] public float monsterZoneProb = 0.1f;

    [Header("Streets")]
    [Tooltip("Chance to remove a street segment to create dead ends and variety")]
    [Range(0f, 0.3f)] public float streetRemovalChance = 0.1f;

    [Header("Intersections")]
    [Tooltip("Probability that a major intersection gets a traffic light")]
    [Range(0f, 1f)] public float trafficLightProbability = 0.5f;

    [Header("Delivery Points")]
    public int minDeliveryPointsPerBlock = 0;
    public int maxDeliveryPointsPerBlock = 3;

    /// <summary>
    /// Returns the total spacing between block centers on the X axis.
    /// </summary>
    public float SpacingX => blockWidth + streetWidth;

    /// <summary>
    /// Returns the total spacing between block centers on the Z axis.
    /// </summary>
    public float SpacingZ => blockDepth + streetWidth;

    private void OnValidate()
    {
        gridWidth = Mathf.Max(1, gridWidth);
        gridHeight = Mathf.Max(1, gridHeight);

        blockWidth = Mathf.Max(1f, blockWidth);
        blockDepth = Mathf.Max(1f, blockDepth);
        streetWidth = Mathf.Max(0f, streetWidth);
        sidewalkWidth = Mathf.Max(0f, sidewalkWidth);

        minBuildingHeight = Mathf.Max(1f, minBuildingHeight);
        maxBuildingHeight = Mathf.Max(minBuildingHeight, maxBuildingHeight);

        minBuildingsPerBlock = Mathf.Max(0, minBuildingsPerBlock);
        maxBuildingsPerBlock = Mathf.Max(minBuildingsPerBlock, maxBuildingsPerBlock);

        minDeliveryPointsPerBlock = Mathf.Max(0, minDeliveryPointsPerBlock);
        maxDeliveryPointsPerBlock = Mathf.Max(minDeliveryPointsPerBlock, maxDeliveryPointsPerBlock);

        // Normalize zone probabilities to sum to 1
        float totalProb = residentialProb + commercialProb + industrialProb + monsterZoneProb;
        if (totalProb > 0f)
        {
            residentialProb /= totalProb;
            commercialProb /= totalProb;
            industrialProb /= totalProb;
            monsterZoneProb /= totalProb;
        }
    }
}

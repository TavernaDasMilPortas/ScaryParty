using UnityEngine;

/// <summary>
/// Places enemy spawn points throughout the city based on zone types.
/// MonsterZone = high concentration, Industrial = light patrol, others = none.
/// Spawn points are positioned at dark corners of blocks (far from center).
/// </summary>
public class EnemySpawnPointGenerator : MonoBehaviour
{
    /// <summary>
    /// Generates enemy spawn points for all blocks and stores them in CityData.
    /// </summary>
    public void GenerateSpawnPoints(CityData cityData, CityConfig config, System.Random rng)
    {
        if (cityData.enemySpawnPoints == null)
        {
            cityData.enemySpawnPoints = new System.Collections.Generic.List<EnemySpawnPoint>();
        }
        else
        {
            cityData.enemySpawnPoints.Clear();
        }
        
        if (cityData.blocks == null) return;
        
        for (int i = 0; i < cityData.blocks.Count; i++)
        {
            BlockInfo block = cityData.blocks[i];
            int maxPoints = 0;
            
            switch (block.zoneType)
            {
                case ZoneType.MonsterZone:
                    maxPoints = config.maxEnemySpawnPointsPerZone;
                    break;
                case ZoneType.Industrial:
                    maxPoints = Mathf.Max(1, config.maxEnemySpawnPointsPerZone / 3);
                    break;
                default:
                    maxPoints = 0; // Safe zones
                    break;
            }
            
            int numPoints = rng.Next(0, maxPoints + 1);
            
            for (int p = 0; p < numPoints; p++)
            {
                // Position at corners/edges of the block (dark alleys)
                float halfW = block.size.x * 0.4f;
                float halfD = block.size.z * 0.4f;
                Vector3 offset = new Vector3(
                    (float)(rng.NextDouble() * 2 - 1) * halfW,
                    0f,
                    (float)(rng.NextDouble() * 2 - 1) * halfD
                );
                
                cityData.enemySpawnPoints.Add(new EnemySpawnPoint
                {
                    position = block.worldCenter + offset,
                    blockIndex = i,
                    zone = block.zoneType
                });
            }
        }
    }
}

using UnityEngine;

/// <summary>
/// Component attached to each procedurally generated building.
/// Stores metadata about the building for delivery and gameplay systems.
/// </summary>
public class CityBuilding : MonoBehaviour
{
    [Header("Building Info")]
    public ZoneType zone;
    public bool canHaveDeliveryPoint;
    public int deliveryPointIndex = -1;
    public Vector3 entrancePosition;
    public Color buildingColor = Color.white; // Saves color for Play Mode

    private void Start()
    {
        // Re-apply color when entering Play Mode, because MaterialPropertyBlocks
        // applied in Edit Mode are often lost.
        if (buildingColor != Color.white)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", buildingColor);
                propBlock.SetColor("_BaseColor", buildingColor);
                propBlock.SetColor("_MainColor", buildingColor);
                renderer.SetPropertyBlock(propBlock);
            }
        }
    }

    /// <summary>
    /// Calculates the entrance position based on the nearest street side.
    /// The entrance is placed at the front of the building facing the block center.
    /// </summary>
    public void CalculateEntrance(Vector3 blockCenter)
    {
        // Direction from block center to building (outwards towards the street)
        Vector3 dirOutward = (transform.position - blockCenter);
        dirOutward.y = 0;
        
        // If the building is exactly at the center (rare), just pick North
        if (dirOutward.sqrMagnitude < 0.01f) dirOutward = Vector3.forward;

        // Find which axis it's furthest from the center to determine which street it faces
        if (Mathf.Abs(dirOutward.x) > Mathf.Abs(dirOutward.z))
        {
            // Faces East or West
            float sign = Mathf.Sign(dirOutward.x);
            entrancePosition = transform.position + new Vector3(sign * transform.localScale.x / 2f, 0, 0);
            
            // Move it slightly out onto the sidewalk so it's accessible
            entrancePosition += new Vector3(sign * 1.5f, 0, 0);
        }
        else
        {
            // Faces North or South
            float sign = Mathf.Sign(dirOutward.z);
            entrancePosition = transform.position + new Vector3(0, 0, sign * transform.localScale.z / 2f);
            
            // Move it slightly out onto the sidewalk so it's accessible
            entrancePosition += new Vector3(0, 0, sign * 1.5f);
        }
        
        entrancePosition.y = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(entrancePosition, 0.5f);
        Gizmos.DrawLine(transform.position, entrancePosition);
    }
}

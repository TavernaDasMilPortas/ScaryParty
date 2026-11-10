using System;
using UnityEngine;

/// <summary>
/// Component attached to each procedurally generated building.
/// Stores metadata about the building for delivery, pedestrian routines, and gameplay systems.
/// Decoupled into entrance anchors, sidewalk approaches, and BuildingPortal.
/// Supports both legacy primitive buildings and modular kit composed buildings.
/// </summary>
public class CityBuilding : MonoBehaviour
{
    [Header("Building Identity")]
    public string buildingId;
    public int blockId = -1;
    public ZoneType zone;

    [Header("Modular Geometry")]
    [Tooltip("Dimensões da base do edifício no plano XZ em metros (Largura, Profundidade).")]
    public Vector2 footprint = Vector2.zero;
    [Tooltip("Número de pavimentos habitáveis.")]
    public int floorCount = 1;

    [Header("Pedestrian & Gameplay Features")]
    public bool canAdmitPedestrians = true;
    public bool canHaveDeliveryPoint = true;
    public int deliveryPointIndex = -1;

    [Header("Navigation Anchors")]
    public Vector3 entrancePosition;
    public Vector3 entranceDirection = Vector3.forward;
    public Transform entranceAnchor;
    public Transform sidewalkApproachAnchor;
    public Transform doorAnchor;
    public Transform approachAnchor;
    public Transform interiorAnchor;
    public Transform deliveryAnchor;
    public BuildingPortal portal;

    [Header("Visuals")]
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

        EnsurePortal();
        CityBuildingRegistry.EnsureInstance()?.Register(this);
    }

    private void OnDestroy()
    {
        if (CityBuildingRegistry.Instance != null)
        {
            CityBuildingRegistry.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// Configures entrance position and anchors derived directly from building facing direction
    /// and the actual elevation of the sidewalk/ground surface.
    /// </summary>
    public void SetupEntrance(Vector3 forwardDir, float surfaceY)
    {
        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude < 0.001f) forwardDir = transform.forward;
        forwardDir.Normalize();

        entranceDirection = forwardDir;

        // Front facade position: usa footprint se disponível, senão fallback para localScale
        float halfDepth = footprint.y > 0.1f ? footprint.y * 0.5f : transform.localScale.z * 0.5f;
        Vector3 facadePoint = transform.position + forwardDir * halfDepth;
        facadePoint.y = surfaceY;
        entrancePosition = facadePoint;

        EnsurePortal();
    }

    public BuildingPortal EnsurePortal()
    {
        if (portal == null) portal = GetComponent<BuildingPortal>();
        if (portal == null) portal = gameObject.AddComponent<BuildingPortal>();
        portal.building = this;

        if (entranceAnchor == null)
        {
            Transform existing = transform.Find("EntranceAnchor");
            if (existing != null) entranceAnchor = existing;
            else
            {
                GameObject ea = new GameObject("EntranceAnchor");
                ea.transform.SetParent(transform, false);
                ea.transform.position = entrancePosition;
                entranceAnchor = ea.transform;
            }
        }
        else
        {
            entranceAnchor.position = entrancePosition;
        }

        if (doorAnchor == null)
        {
            Transform existing = transform.Find("DoorAnchor");
            doorAnchor = existing != null ? existing : entranceAnchor;
        }

        if (sidewalkApproachAnchor == null)
        {
            Transform existing = transform.Find("SidewalkApproachAnchor");
            if (existing != null) sidewalkApproachAnchor = existing;
            else
            {
                GameObject aa = new GameObject("SidewalkApproachAnchor");
                aa.transform.SetParent(transform, false);
                aa.transform.position = entrancePosition + entranceDirection * 1.0f;
                sidewalkApproachAnchor = aa.transform;
            }
        }
        else
        {
            sidewalkApproachAnchor.position = entrancePosition + entranceDirection * 1.0f;
        }

        if (approachAnchor == null)
        {
            approachAnchor = sidewalkApproachAnchor;
        }

        if (interiorAnchor == null)
        {
            Transform existing = transform.Find("InteriorAnchor");
            if (existing != null) interiorAnchor = existing;
            else
            {
                GameObject ia = new GameObject("InteriorAnchor");
                ia.transform.SetParent(transform, false);
                ia.transform.position = entrancePosition - entranceDirection * 1.0f;
                interiorAnchor = ia.transform;
            }
        }

        if (deliveryAnchor == null)
        {
            Transform existing = transform.Find("DeliveryAnchor");
            if (existing != null) deliveryAnchor = existing;
            else
            {
                GameObject dla = new GameObject("DeliveryAnchor");
                dla.transform.SetParent(transform, false);
                dla.transform.position = entrancePosition + entranceDirection * 0.4f;
                deliveryAnchor = dla.transform;
            }
        }

        portal.entranceAnchor = entranceAnchor;
        portal.approachAnchor = sidewalkApproachAnchor;

        return portal;
    }

    /// <summary>
    /// Calculates the entrance position based on the nearest street side (backward compatibility).
    /// Preserves existing callers while respecting real street-facing direction.
    /// </summary>
    public void CalculateEntrance(Vector3 blockCenter)
    {
        Vector3 dirOutward = (transform.position - blockCenter);
        dirOutward.y = 0;
        if (dirOutward.sqrMagnitude < 0.01f) dirOutward = transform.forward;
        dirOutward.Normalize();

        float effectiveWidth = footprint.x > 0.1f ? footprint.x : transform.localScale.x;
        float effectiveDepth = footprint.y > 0.1f ? footprint.y : transform.localScale.z;
        float effectiveHeight = floorCount > 1 ? floorCount * 3.0f : transform.localScale.y;

        // If transform rotation is already aligned with street, prioritize transform.forward
        if (Vector3.Dot(transform.forward, dirOutward) > 0.3f)
        {
            SetupEntrance(transform.forward, transform.position.y - effectiveHeight * 0.5f);
            return;
        }

        // Find which axis it's furthest from the center to determine which street it faces
        Vector3 entranceOffset;
        if (Mathf.Abs(dirOutward.x) > Mathf.Abs(dirOutward.z))
        {
            float sign = Mathf.Sign(dirOutward.x);
            entranceOffset = new Vector3(sign * effectiveWidth / 2f, 0, 0);
            entranceDirection = new Vector3(sign, 0, 0);
        }
        else
        {
            float sign = Mathf.Sign(dirOutward.z);
            entranceOffset = new Vector3(0, 0, sign * effectiveDepth / 2f);
            entranceDirection = new Vector3(0, 0, sign);
        }

        float y = transform.position.y - effectiveHeight * 0.5f;
        entrancePosition = transform.position + entranceOffset;
        entrancePosition.y = Mathf.Max(0f, y);

        EnsurePortal();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(entrancePosition, 0.4f);
        Gizmos.DrawLine(transform.position, entrancePosition);

        if (sidewalkApproachAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(sidewalkApproachAnchor.position, 0.3f);
            Gizmos.DrawLine(entrancePosition, sidewalkApproachAnchor.position);
        }

        if (interiorAnchor != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(interiorAnchor.position, 0.3f);
            Gizmos.DrawLine(entrancePosition, interiorAnchor.position);
        }

        if (deliveryAnchor != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawSphere(deliveryAnchor.position, 0.25f);
        }
    }
}

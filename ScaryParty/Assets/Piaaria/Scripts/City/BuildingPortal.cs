using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logical portal attached to city buildings.
/// Represents the transition point between the exterior city sidewalk and the building interior.
/// In the current phase, pedestrians enter virtually (visual pooled/hidden, tracked as occupant)
/// and emerge after a dwell duration to resume city routines.
/// </summary>
[RequireComponent(typeof(CityBuilding))]
public class BuildingPortal : MonoBehaviour
{
    [Header("Building Reference")]
    public CityBuilding building;

    [Header("Anchors")]
    public Transform entranceAnchor;
    public Transform approachAnchor;

    public Vector3 EntrancePosition => entranceAnchor != null ? entranceAnchor.position : (building != null ? building.entrancePosition : transform.position);
    public Vector3 ApproachPosition => approachAnchor != null ? approachAnchor.position : EntrancePosition;
    public Vector3 FacingDirection => building != null && building.entranceDirection.sqrMagnitude > 0.01f ? building.entranceDirection : transform.forward;

    private readonly HashSet<string> _virtualOccupants = new HashSet<string>();
    public int VirtualOccupantCount => _virtualOccupants.Count;

    public event Action<BuildingPortal, string> OnNpcEntered;
    public event Action<BuildingPortal, string> OnNpcExited;

    private void Awake()
    {
        if (building == null) building = GetComponent<CityBuilding>();
        EnsureAnchors();
    }

    public void EnsureAnchors()
    {
        if (building == null) building = GetComponent<CityBuilding>();

        if (entranceAnchor == null)
        {
            Transform existing = transform.Find("EntranceAnchor");
            if (existing != null) entranceAnchor = existing;
            else
            {
                GameObject ea = new GameObject("EntranceAnchor");
                ea.transform.SetParent(transform, false);
                ea.transform.position = building != null ? building.entrancePosition : transform.position;
                entranceAnchor = ea.transform;
            }
        }

        if (approachAnchor == null)
        {
            Transform existing = transform.Find("ApproachAnchor");
            if (existing != null) approachAnchor = existing;
            else
            {
                GameObject aa = new GameObject("ApproachAnchor");
                aa.transform.SetParent(transform, false);
                Vector3 forward = FacingDirection;
                aa.transform.position = EntrancePosition + forward * 1.0f;
                approachAnchor = aa.transform;
            }
        }
    }

    public void RegisterOccupant(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        _virtualOccupants.Add(npcId);
        OnNpcEntered?.Invoke(this, npcId);
    }

    public void UnregisterOccupant(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        _virtualOccupants.Remove(npcId);
        OnNpcExited?.Invoke(this, npcId);
    }

    public bool HasOccupant(string npcId) => _virtualOccupants.Contains(npcId);

    public void ClearOccupants()
    {
        _virtualOccupants.Clear();
    }
}

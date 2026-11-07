using System.Collections;
using UnityEngine;
using Unity.Netcode;

/// <summary>Registers the player with the map across either spawn order and removes stale references on despawn.</summary>
public class MinimapController : NetworkBehaviour
{
    private Coroutine _registration;

    public override void OnNetworkSpawn()
    {
        if (_registration != null) StopCoroutine(_registration);
        _registration = StartCoroutine(RegisterWhenReady());
    }

    private IEnumerator RegisterWhenReady()
    {
        while (IsSpawned && MinimapRouteManager.Instance == null) yield return null;
        if (IsSpawned) MinimapRouteManager.Instance.RegisterPlayer(GetComponent<PlayerState>());
        _registration = null;
    }

    public override void OnNetworkDespawn()
    {
        if (_registration != null) { StopCoroutine(_registration); _registration = null; }
        MinimapRouteManager.Instance?.UnregisterPlayer(NetworkObjectId);
    }

    private void OnDisable()
    {
        if (_registration != null) { StopCoroutine(_registration); _registration = null; }
        if (IsSpawned) MinimapRouteManager.Instance?.UnregisterPlayer(NetworkObjectId);
    }
}

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public static class PlayerContextResolver
    {
        public static bool TryGetPlayer(ulong clientId, out NetworkObject playerNetObj, out Transform headTransform)
        {
            playerNetObj = null;
            headTransform = null;

            if (NetworkManager.Singleton == null) return false;

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                playerNetObj = client.PlayerObject;
                if (playerNetObj != null)
                {
                    // Procurar ponto de visão / câmera / cabeça
                    var cam = playerNetObj.GetComponentInChildren<Camera>();
                    if (cam != null)
                    {
                        headTransform = cam.transform;
                    }
                    else
                    {
                        headTransform = playerNetObj.transform;
                    }
                    return true;
                }
            }

            return false;
        }

        public static Vector3 GetPlayerPosition(ulong clientId)
        {
            if (TryGetPlayer(clientId, out var netObj, out _))
            {
                return netObj.transform.position;
            }
            return Vector3.zero;
        }
    }

    public static class StationRegistry
    {
        private static readonly Dictionary<int, Transform> _stations = new Dictionary<int, Transform>();

        public static void Register(int stationId, Transform transform)
        {
            if (transform != null)
                _stations[stationId] = transform;
        }

        public static void Unregister(int stationId)
        {
            _stations.Remove(stationId);
        }

        public static void Clear()
        {
            _stations.Clear();
        }

        public static bool TryGetStation(int stationId, out Transform transform)
        {
            if (_stations.TryGetValue(stationId, out transform) && transform != null)
                return true;

            var all = Object.FindObjectsByType<Stations.StationView>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].stationId == stationId)
                {
                    transform = all[i].transform;
                    _stations[stationId] = transform;
                    return true;
                }
            }

            transform = null;
            return false;
        }
    }

    public static class InteractionValidator
    {
        public static bool ValidateDistanceAndSight(
            ulong clientId, 
            Vector3 targetPosition, 
            float maxDistance = 3.0f, 
            float tolerance = 1.8f, 
            bool requireLineOfSight = true)
        {
            if (!PlayerContextResolver.TryGetPlayer(clientId, out var playerObj, out var head))
            {
                Debug.LogWarning($"[InteractionValidator] Validacao falhou: Jogador {clientId} nao encontrado por PlayerContextResolver!");
                return false;
            }

            float allowedDist = maxDistance + tolerance;
            float actualDist = Vector3.Distance(head.position, targetPosition);
            if (actualDist > allowedDist)
            {
                Debug.LogWarning($"[InteractionValidator] Validacao falhou para cliente {clientId}: Distancia ({actualDist:F2}m) > Permitido ({allowedDist:F2}m). Cabeca: {head.position}, Alvo: {targetPosition}");
                return false;
            }

            if (requireLineOfSight)
            {
                int layerMask = ~LayerMask.GetMask("Ignore Raycast");
                if (Physics.Linecast(head.position, targetPosition, out RaycastHit hit, layerMask, QueryTriggerInteraction.Ignore))
                {
                    if (Vector3.Distance(hit.point, targetPosition) > 1.2f &&
                        hit.transform != playerObj.transform &&
                        !hit.transform.IsChildOf(playerObj.transform))
                    {
                        if (Vector3.Distance(hit.transform.position, targetPosition) > 1.5f)
                        {
                            Debug.LogWarning($"[InteractionValidator] Validacao falhou para cliente {clientId}: Linha de visao bloqueada por '{hit.collider?.name}' (Ponto impacto a alvo: {Vector3.Distance(hit.point, targetPosition):F2}m, Transform a alvo: {Vector3.Distance(hit.transform.position, targetPosition):F2}m).");
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }

    public class PizzeriaCommit
    {
        private readonly ReplicationProjector _projector;
        private readonly PizzeriaNetworkState _netState;

        public PizzeriaCommit(PizzeriaNetworkState netState)
        {
            _netState = netState;
            _projector = new ReplicationProjector(netState);
        }

        public void Commit(ScaryParty.Pizzeria.Domain.Models.PizzeriaState domainState)
        {
            if (domainState == null || _netState == null) return;
            domainState.IncrementRevision();
            _projector.ProjectFullState(domainState);
        }
    }

    public static class PizzeriaSession
    {
        public static Composition.PizzeriaRoot Root { get; internal set; }
        public static PizzeriaNetworkState NetState { get; internal set; }
        public static KitchenCommands Kitchen { get; internal set; }
        public static OrderCommands Orders { get; internal set; }
        public static LogisticsCommands Logistics { get; internal set; }
        public static PizzeriaNotifier Notifier { get; internal set; }
        public static ScaryParty.Pizzeria.Domain.Definitions.DefinitionCatalog Catalog => Root != null ? Root.Catalog : null;

        public static bool IsReady => Root != null && NetState != null;

        public static void Clear()
        {
            Root = null;
            NetState = null;
            Kitchen = null;
            Orders = null;
            Logistics = null;
            Notifier = null;
        }
    }
}

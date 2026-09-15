using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Integration
{
    public class CargoMapAdapter : MonoBehaviour
    {
        private readonly HashSet<int> _displayedDestinations = new HashSet<int>();
        private float _checkTimer = 0f;

        private void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer < 0.5f) return;
            _checkTimer = 0f;

            UpdateCargoRoutes();
        }

        private void UpdateCargoRoutes()
        {
            var routeMgr = MinimapRouteManager.Instance;
            var netState = PizzeriaNetworkState.Instance;
            if (routeMgr == null || netState == null || NetworkManager.Singleton == null) return;

            ulong myId = NetworkManager.Singleton.LocalClientId;
            var currentDestinations = new HashSet<int>();

            // Find all destinations labeled on boxes currently held by local player (hands or backpack)
            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                bool isCarriedByMe = item.HolderId == myId &&
                    (item.LocationType == (byte)LocationType.Hand || item.LocationType == (byte)LocationType.Backpack);

                if (isCarriedByMe && item.LabelDestinationId > 0)
                {
                    currentDestinations.Add(item.LabelDestinationId);
                }
            }

            // Remove destinations no longer carried
            var toRemove = new List<int>();
            foreach (int destId in _displayedDestinations)
            {
                if (!currentDestinations.Contains(destId))
                {
                    routeMgr.RemoveRoute($"Cargo_Dest_{destId}");
                    toRemove.Add(destId);
                }
            }
            for (int i = 0; i < toRemove.Count; i++) _displayedDestinations.Remove(toRemove[i]);

            // Add newly carried destinations
            foreach (int destId in currentDestinations)
            {
                if (!_displayedDestinations.Contains(destId))
                {
                    Vector3 targetPos = FindDeliveryPointPosition(destId);
                    if (targetPos != Vector3.zero)
                    {
                        routeMgr.CreateRoute($"Cargo_Dest_{destId}", transform.position, targetPos);
                        _displayedDestinations.Add(destId);
                    }
                }
            }
        }

        private Vector3 FindDeliveryPointPosition(int destId)
        {
            // Find in scene delivery points
            var points = Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].pointIndex == destId)
                    return points[i].transform.position;
            }
            return Vector3.zero;
        }

        private void OnDestroy()
        {
            var routeMgr = MinimapRouteManager.Instance;
            if (routeMgr != null)
            {
                foreach (int destId in _displayedDestinations)
                {
                    routeMgr.RemoveRoute($"Cargo_Dest_{destId}");
                }
            }
            _displayedDestinations.Clear();
        }
    }
}

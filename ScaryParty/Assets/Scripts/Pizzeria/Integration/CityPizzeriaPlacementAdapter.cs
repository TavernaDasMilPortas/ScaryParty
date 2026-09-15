using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Integration
{
    public static class CityPizzeriaPlacementAdapter
    {
        public static GameObject SpawnPizzeria(
            GameObject pizzeriaPrefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            CityData cityData,
            float sidewalkHeight)
        {
            // Ground the position on the sidewalk level
            position.y = sidewalkHeight;

            GameObject instance = null;
            if (pizzeriaPrefab != null)
            {
                instance = Object.Instantiate(pizzeriaPrefab, position, rotation, parent);
                instance.name = "The_Pizzeria_Building";

                // If running as server in a networked session, spawn the network object
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening)
                {
                    var netObj = instance.GetComponent<NetworkObject>();
                    if (netObj != null && !netObj.IsSpawned)
                    {
                        netObj.Spawn();
                    }
                }
            }

            if (cityData != null)
            {
                cityData.pizzariaPosition = position;

                // Bancada sits just outside the front door, facing the street.
                // The rotation was built with LookRotation(-streetDir), so
                // rotation * forward points INWARD.  The street is in -forward.
                Vector3 streetDir = -(rotation * Vector3.forward);
                streetDir.y = 0;
                if (streetDir.sqrMagnitude < 0.01f) streetDir = Vector3.forward;
                streetDir.Normalize();

                // 6 m (half depth) + 1.5 m gap on the sidewalk
                Vector3 bancadaPos = position + streetDir * 7.5f;
                bancadaPos.y = sidewalkHeight + 0.5f;
                cityData.bancadaPosition = bancadaPos;
            }

            return instance;
        }
    }
}

using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Integration
{
    /// <summary>
    /// Adaptador de posicionamento da pizzaria na cidade.
    /// Calcula as coordenadas de mundo da pizzaria e da bancada externa de entrega,
    /// registrando em CityData e delegando o spawn ao PizzeriaSpawner no servidor.
    /// </summary>
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
            // Alinha a altura ao nível da calçada
            position.y = sidewalkHeight;

            if (cityData != null)
            {
                cityData.pizzariaPosition = position;

                // Bancada externa de frente para a rua
                Vector3 streetDir = -(rotation * Vector3.forward);
                streetDir.y = 0;
                if (streetDir.sqrMagnitude < 0.01f) streetDir = Vector3.forward;
                streetDir.Normalize();

                Vector3 bancadaPos = position + streetDir * 7.5f;
                bancadaPos.y = sidewalkHeight + 0.5f;
                cityData.bancadaPosition = bancadaPos;
            }

            // Se PizzeriaSpawner estiver ativo na cena e for servidor de rede
            if (PizzeriaSpawner.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                return PizzeriaSpawner.Instance.SpawnPizzeria(position, rotation);
            }

            // Em tempo de execução se o servidor estiver ouvindo
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening)
            {
                if (pizzeriaPrefab != null)
                {
                    var instance = Object.Instantiate(pizzeriaPrefab, position, rotation, null);
                    instance.name = "The_Pizzeria_Building";
                    var netObj = instance.GetComponent<NetworkObject>();
                    if (netObj != null && !netObj.IsSpawned)
                    {
                        netObj.Spawn();
                    }
                    return instance;
                }
            }
            else if (!Application.isPlaying)
            {
                // Em EditMode no Editor para preview estático
                if (pizzeriaPrefab != null)
                {
                    var instance = Object.Instantiate(pizzeriaPrefab, position, rotation, parent);
                    instance.name = "The_Pizzeria_Building";
                    return instance;
                }
            }

            return null;
        }
    }
}

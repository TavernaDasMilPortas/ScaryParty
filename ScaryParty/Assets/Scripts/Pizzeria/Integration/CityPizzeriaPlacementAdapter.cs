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
            float sidewalkHeight,
            float frontDistance = 7.5f,
            Vector3 streetDirOverride = default)
        {
            // Alinha a altura ao nível da calçada
            position.y = sidewalkHeight;

            if (cityData != null)
            {
                cityData.pizzariaPosition = position;

                // Bancada externa de frente para a rua
                Vector3 streetDir = streetDirOverride.sqrMagnitude > 0.01f ? streetDirOverride : -(rotation * Vector3.forward);
                streetDir.y = 0;
                if (streetDir.sqrMagnitude < 0.01f) streetDir = Vector3.forward;
                streetDir.Normalize();

                Vector3 bancadaPos = position + streetDir * frontDistance;
                bancadaPos.y = sidewalkHeight + 0.5f;
                cityData.bancadaPosition = bancadaPos;
            }

            if (pizzeriaPrefab == null && (PizzeriaSpawner.Instance == null || PizzeriaSpawner.Instance.PizzeriaPrefab == null))
            {
                Debug.LogError("[CityPizzeriaPlacementAdapter] ❌ pizzeriaPrefab não atribuído no CityGenerator!");
                return null;
            }

            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            // Se PizzeriaSpawner estiver ativo na cena e for servidor de rede
            if (PizzeriaSpawner.Instance != null && isServer)
            {
                var spawned = PizzeriaSpawner.Instance.SpawnPizzeria(position, rotation);
                if (spawned != null) return spawned;
                // O spawner pode ainda não ter dado spawn (IsServer=false): cai no fallback abaixo.
            }

            // Em tempo de execução se o servidor estiver ouvindo
            if (isServer && NetworkManager.Singleton.IsListening)
            {
                if (pizzeriaPrefab != null)
                {
                    var instance = Object.Instantiate(pizzeriaPrefab, position, rotation, null);
                    instance.name = "The_Pizzeria_Building";
                    if (parent != null && parent.gameObject.scene.IsValid() && parent.gameObject.scene.isLoaded && instance.scene != parent.gameObject.scene)
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, parent.gameObject.scene);
                    var netObj = instance.GetComponent<NetworkObject>();
                    if (netObj != null && !netObj.IsSpawned)
                    {
                        netObj.Spawn();
                    }
                    return instance;
                }
            }
            else if (!Application.isPlaying || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                // EditMode (preview estático) ou play sem rede: instancia local simples.
                if (pizzeriaPrefab != null)
                {
                    var instance = Object.Instantiate(pizzeriaPrefab, position, rotation, parent);
                    instance.name = "The_Pizzeria_Building";
                    return instance;
                }
            }

            // Cliente remoto: a pizzaria chega via NGO.
            return null;
        }
    }
}

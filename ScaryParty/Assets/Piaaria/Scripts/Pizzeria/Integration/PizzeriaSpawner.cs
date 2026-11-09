using UnityEngine;
using Unity.Netcode;

namespace ScaryParty.Pizzeria.Integration
{
    /// <summary>
    /// Gerenciador autoritativo in-scene responsável por instanciar e dar Spawn()
    /// na raiz da pizzaria no servidor, sem pai na hierarquia (__City__).
    /// Se a cidade for regenerada, realiza despawn seguro da instância prévia.
    /// Clientes remotos nunca instanciam diretamente (sincronização via NGO).
    /// </summary>
    public class PizzeriaSpawner : NetworkBehaviour
    {
        public static PizzeriaSpawner Instance { get; private set; }

        [Header("Prefab da Pizzaria")]
        [SerializeField] private GameObject _pizzeriaPrefab;
        public GameObject PizzeriaPrefab { get => _pizzeriaPrefab; set => _pizzeriaPrefab = value; }

        private GameObject _spawnedInstance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public GameObject SpawnPizzeria(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return null;

            DespawnExisting();

            var prefab = _pizzeriaPrefab;
            if (prefab == null)
            {
                var cityGen = Object.FindAnyObjectByType<CityGenerator>();
                if (cityGen != null && cityGen.pizzeriaPrefab != null)
                {
                    prefab = cityGen.pizzeriaPrefab;
                }
            }

            if (prefab == null)
            {
                Debug.LogError("[PizzeriaSpawner] ❌ Nenhum prefab de pizzaria configurado para Spawn!");
                return null;
            }

            // Instancia no servidor SEM pai na hierarquia (não filho de __City__)
            _spawnedInstance = Object.Instantiate(prefab, position, rotation, null);
            _spawnedInstance.name = "The_Pizzeria_Building";

            // Garante que a instância fica na cena do jogo (e não na cena ativa de um momento
            // de transição, ex.: ReadyScene, que seria descarregada depois).
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded && _spawnedInstance.scene != gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_spawnedInstance, gameObject.scene);

            var netObj = _spawnedInstance.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
            {
                netObj.Spawn();
                Debug.Log($"[PizzeriaSpawner] ✅ Pizzaria spawnada com sucesso no servidor em {position}!");
            }

            return _spawnedInstance;
        }

        public void DespawnExisting()
        {
            if (!IsServer) return;

            if (_spawnedInstance != null)
            {
                var netObj = _spawnedInstance.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(_spawnedInstance);
                }
                _spawnedInstance = null;
                Debug.Log("[PizzeriaSpawner] Instância prévia da pizzaria despawnada.");
            }
        }
    }
}

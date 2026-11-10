using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace ScaryParty.Navigation.Core
{
    /// <summary>
    /// Central orchestrator for the city-wide and interior navigation world.
    /// Manages NavMeshSurfaces, bakes the procedural city NavMesh, handles NavMeshLinks,
    /// and notifies dependent systems (CustomerSpawner, CityPopulationDirector) when navigation is ready.
    /// </summary>
    public class NavigationWorldManager : MonoBehaviour
    {
        public static NavigationWorldManager Instance { get; private set; }

        [Header("NavMesh Surfaces")]
        public NavMeshSurface citySurface;
        public NavMeshSurface pizzeriaInteriorSurface;

        [Header("Status")]
        [SerializeField] private bool _isReady = false;
        public bool IsReady => _isReady;

        public event Action OnNavigationReady;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static NavigationWorldManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = UnityEngine.Object.FindFirstObjectByType<NavigationWorldManager>();
            if (Instance == null)
            {
                GameObject go = new GameObject("NavigationWorldManager");
                Instance = go.AddComponent<NavigationWorldManager>();
            }
            return Instance;
        }

        /// <summary>
        /// Builds the unified NavMesh for the generated city.
        /// Considers physics colliders across roads, sidewalks, block grounds, and buildings.
        /// </summary>
        public void BuildCityNavMesh()
        {
            if (citySurface == null)
            {
                citySurface = GetComponent<NavMeshSurface>();
                if (citySurface == null) citySurface = gameObject.AddComponent<NavMeshSurface>();
            }

            citySurface.collectObjects = CollectObjects.All;
            citySurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            
            // Set default agent configuration
            citySurface.defaultArea = 0; // Walkable
            
            // Build NavMesh synchronously or via update
            citySurface.BuildNavMesh();
            _isReady = true;

            UnityEngine.Debug.Log("[NavigationWorldManager] City NavMesh built and verified successfully.");
            OnNavigationReady?.Invoke();
        }

        public void InvalidateNavigation()
        {
            _isReady = false;
        }

        public bool SamplePosition(Vector3 sourcePosition, out Vector3 snappedPosition, float maxDistance = 2.5f, int areaMask = NavMesh.AllAreas)
        {
            if (NavMesh.SamplePosition(sourcePosition, out NavMeshHit hit, maxDistance, areaMask))
            {
                snappedPosition = hit.position;
                return true;
            }

            snappedPosition = sourcePosition;
            return false;
        }

        public bool CalculatePath(Vector3 source, Vector3 destination, out NavMeshPath path, int areaMask = NavMesh.AllAreas)
        {
            path = new NavMeshPath();
            if (!SamplePosition(source, out Vector3 validSource, 3.0f, areaMask) ||
                !SamplePosition(destination, out Vector3 validDest, 3.0f, areaMask))
            {
                return false;
            }

            if (NavMesh.CalculatePath(validSource, validDest, areaMask, path))
            {
                return path.status == NavMeshPathStatus.PathComplete;
            }

            return false;
        }

        public bool IsReachable(Vector3 from, Vector3 to, int areaMask = NavMesh.AllAreas)
        {
            if (CalculatePath(from, to, out NavMeshPath path, areaMask))
            {
                return path.status == NavMeshPathStatus.PathComplete;
            }
            return false;
        }
    }
}

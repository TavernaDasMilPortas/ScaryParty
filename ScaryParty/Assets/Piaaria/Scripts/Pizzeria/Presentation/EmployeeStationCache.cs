using UnityEngine;
using UnityEngine.SceneManagement;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Presentation
{
    public enum EmployeeHighLevelState
    {
        SpawnArrive = 0,
        IdleObserve = 1,
        ClaimTask = 2,
        Navigate = 3,
        Align = 4,
        PickUp = 5,
        Carry = 6,
        Work = 7,
        Deliver = 8,
        Complete = 9,
        RecoverReplan = 10,
        Blocked = 11
    }

    /// <summary>
    /// Cache de estações e depósitos da pizzaria para evitar FindFirstObjectByType/FindObjectsByType
    /// recorrentes no loop de IA dos funcionários. Invalidado automaticamente na troca de cena.
    /// </summary>
    public static class EmployeeStationCache
    {
        private static bool _sceneHooked;
        private static int _cachedSceneHandle = -1;

        private static CustomerCounterStation[] _counters;
        private static StorageStation[] _storages;
        private static PhoneStation _phone;
        private static TicketBoardStation _ticketBoard;
        private static PrepStation _prep;
        private static AssemblyStation _assembly;
        private static OvenStation _oven;
        private static PackagingStation _packaging;
        private static StagingStation _staging;

        private static void EnsureSceneValidity()
        {
            if (!_sceneHooked)
            {
                SceneManager.sceneLoaded += OnSceneChanged;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                _sceneHooked = true;
            }

            int handle = SceneManager.GetActiveScene().handle;
            if (handle != _cachedSceneHandle)
            {
                Invalidate();
                _cachedSceneHandle = handle;
            }
        }

        private static void OnSceneChanged(Scene scene, LoadSceneMode mode) => Invalidate();
        private static void OnSceneUnloaded(Scene scene) => Invalidate();

        public static void Invalidate()
        {
            _counters = null;
            _storages = null;
            _phone = null;
            _ticketBoard = null;
            _prep = null;
            _assembly = null;
            _oven = null;
            _packaging = null;
            _staging = null;
            _cachedSceneHandle = -1;
        }

        private static bool HasDestroyed<T>(T[] arr) where T : Component
        {
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == null) return true;
            return false;
        }

        public static CustomerCounterStation[] GetCounters()
        {
            EnsureSceneValidity();
            if (_counters == null || _counters.Length == 0 || HasDestroyed(_counters))
                _counters = Object.FindObjectsByType<CustomerCounterStation>(FindObjectsSortMode.None);
            return _counters;
        }

        public static StorageStation[] GetStorages()
        {
            EnsureSceneValidity();
            if (_storages == null || _storages.Length == 0 || HasDestroyed(_storages))
                _storages = Object.FindObjectsByType<StorageStation>(FindObjectsSortMode.None);
            return _storages;
        }

        public static PhoneStation GetPhone()
        {
            EnsureSceneValidity();
            if (_phone == null) _phone = Object.FindFirstObjectByType<PhoneStation>();
            return _phone;
        }

        public static TicketBoardStation GetTicketBoard()
        {
            EnsureSceneValidity();
            if (_ticketBoard == null) _ticketBoard = Object.FindFirstObjectByType<TicketBoardStation>();
            return _ticketBoard;
        }

        public static PrepStation GetPrep()
        {
            EnsureSceneValidity();
            if (_prep == null) _prep = Object.FindFirstObjectByType<PrepStation>();
            return _prep;
        }

        public static AssemblyStation GetAssembly()
        {
            EnsureSceneValidity();
            if (_assembly == null) _assembly = Object.FindFirstObjectByType<AssemblyStation>();
            return _assembly;
        }

        public static OvenStation GetOven()
        {
            EnsureSceneValidity();
            if (_oven == null) _oven = Object.FindFirstObjectByType<OvenStation>();
            return _oven;
        }

        public static PackagingStation GetPackaging()
        {
            EnsureSceneValidity();
            if (_packaging == null) _packaging = Object.FindFirstObjectByType<PackagingStation>();
            return _packaging;
        }

        public static StagingStation GetStaging()
        {
            EnsureSceneValidity();
            if (_staging == null) _staging = Object.FindFirstObjectByType<StagingStation>();
            return _staging;
        }

        public static T GetCachedOrFind<T>() where T : Component
        {
            EnsureSceneValidity();
            if (typeof(T) == typeof(PhoneStation)) return GetPhone() as T;
            if (typeof(T) == typeof(TicketBoardStation)) return GetTicketBoard() as T;
            if (typeof(T) == typeof(PrepStation)) return GetPrep() as T;
            if (typeof(T) == typeof(AssemblyStation)) return GetAssembly() as T;
            if (typeof(T) == typeof(OvenStation)) return GetOven() as T;
            if (typeof(T) == typeof(PackagingStation)) return GetPackaging() as T;
            if (typeof(T) == typeof(StagingStation)) return GetStaging() as T;
            return Object.FindFirstObjectByType<T>();
        }
    }
}

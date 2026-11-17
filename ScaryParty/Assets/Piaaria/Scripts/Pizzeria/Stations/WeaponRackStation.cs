using ScaryParty.Combat;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Player;
using ScaryParty.Pizzeria.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Pizzeria.Stations
{
    /// <summary>
    /// Rack de armas externo da pizzaria: mantém exatamente 1 Pistola (1 mão) e 1 Escopeta (2 mãos)
    /// sincronizadas em rede via PizzeriaNetworkState.Weapons.
    /// </summary>
    public class WeaponRackStation : MonoBehaviour, IInteractable
    {
        public static WeaponRackStation Instance { get; private set; }

        [Header("Visuais no Rack")]
        [SerializeField] private GameObject pistolRackVisual;
        [SerializeField] private GameObject shotgunRackVisual;

        public string InteractPrompt
        {
            get
            {
                var state = PizzeriaNetworkState.Instance;
                ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
                if (state != null && state.TryGetEquippedWeapon(localId, out var equipped))
                {
                    string name = equipped.WeaponType == (byte)WeaponType.Shotgun ? "Escopeta" : "Pistola";
                    return $"[E] Devolver {name} ao Rack | Clique no slot desejado para trocar";
                }
                return "[E] Equipar/Devolver Arma do Rack";
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                PizzeriaCommandHandler.Instance?.EnsureDefaultWeaponsInitializedServer();
            }
        }

        private void Update()
        {
            SyncRackVisuals();
        }

        public void ConfigureVisuals(GameObject pistolVisual, GameObject shotgunVisual)
        {
            pistolRackVisual = pistolVisual;
            shotgunRackVisual = shotgunVisual;
        }

        private void SyncRackVisuals()
        {
            var state = PizzeriaNetworkState.Instance;
            if (state == null || state.Weapons == null) return;

            bool pistolAvail = false;
            bool shotgunAvail = false;
            for (int i = 0; i < state.Weapons.Count; i++)
            {
                var w = state.Weapons[i];
                if (w.WeaponType == (byte)WeaponType.Pistol && w.IsAvailableOnRack) pistolAvail = true;
                else if (w.WeaponType == (byte)WeaponType.Shotgun && w.IsAvailableOnRack) shotgunAvail = true;
            }

            if (pistolRackVisual != null && pistolRackVisual.activeSelf != pistolAvail)
                pistolRackVisual.SetActive(pistolAvail);
            if (shotgunRackVisual != null && shotgunRackVisual.activeSelf != shotgunAvail)
                shotgunRackVisual.SetActive(shotgunAvail);
        }

        public void OnInteract(GameObject interactor)
        {
            var state = PizzeriaNetworkState.Instance;
            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            if (state != null && state.TryGetEquippedWeapon(localId, out _))
            {
                RequestInteractSlot((byte)WeaponType.Unarmed);
                return;
            }
            RequestInteractSlot((byte)WeaponType.Pistol);
        }

        public void RequestInteractSlot(byte requestedWeaponType, uint requestedWeaponId = 0)
        {
            if (requestedWeaponId > 0)
                PizzeriaCommandHandler.Instance?.InteractWeaponRackByIdServerRpc(requestedWeaponType, requestedWeaponId);
            else
                PizzeriaCommandHandler.Instance?.InteractWeaponRackServerRpc(requestedWeaponType);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }
    }

    public class WeaponRackSlotInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private WeaponRackStation parentRack;
        [SerializeField] private WeaponType weaponType = WeaponType.Pistol;

        public void Configure(WeaponRackStation rack, WeaponType type)
        {
            parentRack = rack;
            weaponType = type;
        }

        public string InteractPrompt
        {
            get
            {
                var state = PizzeriaNetworkState.Instance;
                ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
                string label = weaponType == WeaponType.Shotgun ? "Escopeta (2 mãos)" : "Pistola (1 mão)";

                if (state != null)
                {
                    if (state.TryGetEquippedWeapon(localId, out var equipped) && equipped.WeaponType == (byte)weaponType)
                    {
                        return $"[E] Devolver {label} ao Rack";
                    }

                    int availCount = 0;
                    int totalCount = 0;
                    NetWeaponStateDto firstAvail = default;
                    for (int i = 0; i < state.Weapons.Count; i++)
                    {
                        if (state.Weapons[i].WeaponType == (byte)weaponType)
                        {
                            totalCount++;
                            if (state.Weapons[i].IsAvailableOnRack)
                            {
                                if (availCount == 0) firstAvail = state.Weapons[i];
                                availCount++;
                            }
                        }
                    }

                    if (totalCount > 0)
                    {
                        if (availCount > 0)
                        {
                            string qtyInfo = totalCount > 1 ? $" ({availCount}/{totalCount} disp.)" : string.Empty;
                            return $"[E] Pegar {label}{qtyInfo} ({firstAvail.CurrentMagAmmo}/{firstAvail.RackReserveAmmo})";
                        }
                        return $"{label} em uso por outro(s) jogador(es)";
                    }
                }

                return $"[E] Pegar {label}";
            }
        }

        public void OnInteract(GameObject interactor)
        {
            if (parentRack == null) return;
            var state = PizzeriaNetworkState.Instance;
            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            if (state != null && state.TryGetEquippedWeapon(localId, out var equipped) && equipped.WeaponType == (byte)weaponType)
            {
                parentRack.RequestInteractSlot((byte)WeaponType.Unarmed);
                return;
            }

            parentRack.RequestInteractSlot((byte)weaponType);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }
    }
}

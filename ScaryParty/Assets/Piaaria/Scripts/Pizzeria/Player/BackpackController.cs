using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Player
{
    public class BackpackController : MonoBehaviour
    {
        private const float RepairHoldDurationSeconds = 3.0f;

        private NetworkObject _netObj;
        private PlayerInteraction _interaction;
        private ScaryParty.Combat.PlayerHealth _health;
        private float _repairHoldTimer;
        private bool _wasRepairingHudShown;
        private ulong _activeRepairBagId;
        private void Awake()
        {
            _netObj = GetComponent<NetworkObject>();
            _interaction = GetComponent<PlayerInteraction>();
            _health = GetComponent<ScaryParty.Combat.PlayerHealth>();
            if (GetComponent<BackpackVisualizer>() == null) gameObject.AddComponent<BackpackVisualizer>();
        }

        public ulong GetOwnerClientId()
        {
            if (_netObj != null) return _netObj.OwnerClientId;
            return NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : BackpackState.NoOwner;
        }

        private void Update()
        {
            if (_netObj != null && !_netObj.IsOwner) return;
            if (_health == null) _health = GetComponent<ScaryParty.Combat.PlayerHealth>();
            if (_health != null && (_health.IsIncapacitated || _health.IsDead))
            {
                CancelRepairHold();
                return;
            }
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame && !GameplayInputGate.IsLocked)
                BackpackPanelPresenter.Instance?.Toggle();

            HandleBagRepairHold();
        }

        private void HandleBagRepairHold()
        {
            if (GameplayInputGate.IsLocked || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            {
                CancelRepairHold();
                return;
            }

            if (_interaction != null && _interaction.IsBusyWithStationOrAiming)
            {
                CancelRepairHold();
                return;
            }

            bool holdingF = false;
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.isPressed)
                holdingF = true;
            else if (Input.GetKey(KeyCode.F))
                holdingF = true;

            if (!holdingF || !TryGetEquippedBag(out var bag) || bag.Integrity >= bag.MaxIntegrity)
            {
                CancelRepairHold();
                return;
            }

            if (_activeRepairBagId != bag.BackpackId)
            {
                _activeRepairBagId = bag.BackpackId;
                _repairHoldTimer = 0f;
                LogisticsCommands.Instance?.BeginRepairBackpackHoldServerRpc(bag.BackpackId);
            }

            _repairHoldTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_repairHoldTimer / RepairHoldDurationSeconds);
            _wasRepairingHudShown = true;
            ScaryParty.UI.Piaria.Presenters.HudPresenter.Instance?.ShowProgress(
                progress,
                bag.IsTorn ? "Costurando mochila rasgada [F]..." : "Reparando mochila [F]...");

            if (_repairHoldTimer >= RepairHoldDurationSeconds)
            {
                ulong repairedId = bag.BackpackId;
                _repairHoldTimer = 0f;
                _activeRepairBagId = 0;
                if (_wasRepairingHudShown)
                {
                    _wasRepairingHudShown = false;
                    ScaryParty.UI.Piaria.Presenters.HudPresenter.Instance?.HideProgress();
                }
                LogisticsCommands.Instance?.RepairBackpackServerRpc(repairedId);
            }
        }

        private void CancelRepairHold()
        {
            _repairHoldTimer = 0f;
            if (_activeRepairBagId != 0)
            {
                _activeRepairBagId = 0;
                LogisticsCommands.Instance?.CancelRepairBackpackHoldServerRpc();
            }
            if (_wasRepairingHudShown)
            {
                _wasRepairingHudShown = false;
                ScaryParty.UI.Piaria.Presenters.HudPresenter.Instance?.HideProgress();
            }
        }

        public bool TryGetEquippedBag(out NetBackpackDto bag)
            => CarriedCargoResolver.TryGetEquippedBackpack(PizzeriaNetworkState.Instance, GetOwnerClientId(), out bag);

        public IReadOnlyList<NetItemDto> GetBoxesInBackpack()
        {
            var items = new List<NetItemDto>();
            var state = PizzeriaNetworkState.Instance;
            if (state == null || !TryGetEquippedBag(out var bag)) return items;
            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.Backpack && item.HolderId == bag.BackpackId)
                    items.Add(item);
            }
            return items;
        }

        public bool HasBoxInHand(HandSlotIndex hand)
        {
            var state = PizzeriaNetworkState.Instance;
            if (state == null) return false;
            ulong owner = GetOwnerClientId();
            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.Hand && item.HolderId == owner && item.SlotId == (int)hand)
                    return item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box;
            }
            return false;
        }

        public bool TryStoreFromHand(HandSlotIndex hand, ulong targetBagId = 0)
        {
            var state = PizzeriaNetworkState.Instance;
            if (state == null || LogisticsCommands.Instance == null) return false;
            if (targetBagId == 0)
            {
                if (!TryGetEquippedBag(out var equipped))
                {
                    NotificationManager.Show("Sem mochila equipada.", NotificationType.Warning);
                    return false;
                }
                targetBagId = equipped.BackpackId;
            }
            ulong owner = GetOwnerClientId();
            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType != (byte)LocationType.Hand || item.HolderId != owner || item.SlotId != (int)hand) continue;
                if (item.PackagingState != (byte)PackagingState.Boxed && item.Category != (byte)ItemCategory.Box) break;
                LogisticsCommands.Instance.StoreBackpackItemServerRpc(targetBagId, item.ItemId, hand);
                return true;
            }
            NotificationManager.Show("Segure uma caixa na mão selecionada.", NotificationType.Warning);
            return false;
        }

        public bool TryRetrieveToHand(ulong itemId, HandSlotIndex hand, ulong targetBagId = 0)
        {
            if (LogisticsCommands.Instance == null) return false;
            if (targetBagId == 0)
            {
                if (!TryGetEquippedBag(out var bag)) return false;
                targetBagId = bag.BackpackId;
            }
            LogisticsCommands.Instance.RetrieveBackpackItemServerRpc(targetBagId, itemId, hand);
            return true;
        }
    }
}

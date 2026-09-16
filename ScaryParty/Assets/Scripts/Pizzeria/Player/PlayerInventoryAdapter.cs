using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Player
{
    public class PlayerInventoryAdapter : NetworkBehaviour
    {
        public HandSlotIndex ActiveHand { get; private set; } = HandSlotIndex.Left;

        public void ToggleActiveHand()
        {
            if (!IsOwner) return;
            ActiveHand = ActiveHand == HandSlotIndex.Left ? HandSlotIndex.Right : HandSlotIndex.Left;
            UpdateUi();
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Q key toggles active hand
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame)
            {
                ToggleActiveHand();
            }

            UpdateUi();
        }

        private void UpdateUi()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || UIManager.Instance == null) return;

            ulong myId = NetworkManager.Singleton.LocalClientId;
            string leftDesc = "Vazio";
            string rightDesc = "Vazio";

            // Check items in hands
            for (int i = 0; i < netState.Items.Count; i++)
            {
                var it = netState.Items[i];
                if (it.LocationType == (byte)LocationType.Hand && it.HolderId == myId)
                {
                    string name = GetItemDisplayName(it);
                    if (it.SlotId == (int)HandSlotIndex.Left) leftDesc = name;
                    else if (it.SlotId == (int)HandSlotIndex.Right) rightDesc = name;
                }
            }

            // Check tools in hands
            for (int i = 0; i < netState.Tools.Count; i++)
            {
                var tool = netState.Tools[i];
                if (tool.LocationType == (byte)LocationType.Hand && tool.HolderId == myId)
                {
                    string toolName = tool.DefinitionId == 1 ? "Ralador" : "Faca";
                    if (tool.SlotId == (int)HandSlotIndex.Left) leftDesc = toolName;
                    else if (tool.SlotId == (int)HandSlotIndex.Right) rightDesc = toolName;
                }
            }

            if (ActiveHand == HandSlotIndex.Left) leftDesc = $"[★] {leftDesc}";
            else rightDesc = $"[★] {rightDesc}";

            UIManager.Instance.UpdateHand(false, leftDesc);
            UIManager.Instance.UpdateHand(true, rightDesc);
        }

        private string GetItemDisplayName(NetItemDto it)
        {
            if (it.PackagingState == (byte)PackagingState.Boxed)
            {
                return it.LabelDestinationId > 0 ? $"Caixa (Dest #{it.LabelDestinationId})" : "Caixa (Sem Rótulo)";
            }
            if (it.Category == (byte)ItemCategory.Pizza)
            {
                string stage = it.CookingStage switch
                {
                    (byte)CookingStage.Baked => "Assada",
                    (byte)CookingStage.Burned => "QUEIMADA",
                    (byte)CookingStage.Cooking => "Cozinhando",
                    _ => "Crua"
                };
                return $"Pizza ({stage})";
            }
            
            string baseName = $"Item #{it.DefinitionId}";
            var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Catalog;
            if (catalog != null && catalog.Ingredients.TryGetValue(it.DefinitionId, out var def))
            {
                baseName = def.Name;
            }
            else
            {
                // Fallbacks
                switch ((ItemCategory)it.Category)
                {
                    case ItemCategory.PizzaBase: baseName = "Massa Aberta"; break;
                    case ItemCategory.Dough: baseName = "Massa"; break;
                    case ItemCategory.Sauce: baseName = "Molho"; break;
                    case ItemCategory.Cheese: baseName = "Queijo"; break;
                    case ItemCategory.Topping: baseName = "Cobertura"; break;
                }
            }
            
            return baseName;
        }
    }
}

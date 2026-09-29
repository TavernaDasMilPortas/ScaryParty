using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Player
{
    public class PlayerInventoryAdapter : MonoBehaviour
    {
        public HandSlotIndex ActiveHand { get; private set; } = HandSlotIndex.Left;

        private NetworkObject _netObj;

        private void Awake()
        {
            _netObj = GetComponent<NetworkObject>();
        }

        private bool IsOwnerPlayer()
        {
            if (_netObj != null)
                return _netObj.IsOwner;
            return true;
        }

        public void SetActiveHand(HandSlotIndex hand)
        {
            if (!IsOwnerPlayer()) return;
            if (ActiveHand != hand)
            {
                ActiveHand = hand;
                UpdateUi();
            }
        }

        private void Update()
        {
            if (!IsOwnerPlayer()) return;

            // Teclado (New Input System)
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                {
                    SetActiveHand(HandSlotIndex.Left);
                }
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                {
                    SetActiveHand(HandSlotIndex.Right);
                }
                else if (kb.qKey.wasPressedThisFrame)
                {
                    SetActiveHand(ActiveHand == HandSlotIndex.Left ? HandSlotIndex.Right : HandSlotIndex.Left);
                }
            }
            else
            {
                // Fallback para Input legado
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                    SetActiveHand(HandSlotIndex.Left);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                    SetActiveHand(HandSlotIndex.Right);
                else if (Input.GetKeyDown(KeyCode.Q))
                    SetActiveHand(ActiveHand == HandSlotIndex.Left ? HandSlotIndex.Right : HandSlotIndex.Left);
            }

            // Scroll do mouse para alternar mãos
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                float scroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
                if (scroll > 0.1f)
                    SetActiveHand(HandSlotIndex.Left);
                else if (scroll < -0.1f)
                    SetActiveHand(HandSlotIndex.Right);
            }
            else if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.1f)
            {
                if (Input.mouseScrollDelta.y > 0.1f)
                    SetActiveHand(HandSlotIndex.Left);
                else if (Input.mouseScrollDelta.y < -0.1f)
                    SetActiveHand(HandSlotIndex.Right);
            }

            UpdateUi();
        }

        private bool _hadItemInActiveHand = false;

        private void UpdateUi()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || UIManager.Instance == null) return;

            ulong myId = _netObj != null ? _netObj.OwnerClientId : (NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
            string leftDesc = "Vazio";
            string rightDesc = "Vazio";
            bool leftHasContent = false;
            bool rightHasContent = false;

            // Verificar itens nas mãos
            for (int i = 0; i < netState.Items.Count; i++)
            {
                var it = netState.Items[i];
                if (it.LocationType == (byte)LocationType.Hand && it.HolderId == myId)
                {
                    string name = GetItemDisplayName(it);
                    if (it.SlotId == (int)HandSlotIndex.Left) { leftDesc = name; leftHasContent = true; }
                    else if (it.SlotId == (int)HandSlotIndex.Right) { rightDesc = name; rightHasContent = true; }
                }
            }

            // Verificar ferramentas nas mãos
            for (int i = 0; i < netState.Tools.Count; i++)
            {
                var tool = netState.Tools[i];
                if (tool.LocationType == (byte)LocationType.Hand && tool.HolderId == myId)
                {
                    string toolName = tool.DefinitionId == 1 ? "Ralador" : "Faca";
                    if (tool.SlotId == (int)HandSlotIndex.Left) { leftDesc = toolName; leftHasContent = true; }
                    else if (tool.SlotId == (int)HandSlotIndex.Right) { rightDesc = toolName; rightHasContent = true; }
                }
            }

            bool activeHandHasContent = ActiveHand == HandSlotIndex.Left ? leftHasContent : rightHasContent;
            bool otherHandHasContent = ActiveHand == HandSlotIndex.Left ? rightHasContent : leftHasContent;

            // Se a mão ativa tinha algo e acabou de esvaziar, mas a outra mão tem item/utensílio:
            if (_hadItemInActiveHand && !activeHandHasContent && otherHandHasContent)
            {
                ActiveHand = ActiveHand == HandSlotIndex.Left ? HandSlotIndex.Right : HandSlotIndex.Left;
                activeHandHasContent = otherHandHasContent;
            }

            _hadItemInActiveHand = activeHandHasContent;

            UIManager.Instance.SetHandState(false, ActiveHand == HandSlotIndex.Left, leftDesc);
            UIManager.Instance.SetHandState(true, ActiveHand == HandSlotIndex.Right, rightDesc);
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
            
            string baseName = null;
            var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Catalog;
            if (catalog != null && catalog.Ingredients.TryGetValue(it.DefinitionId, out var def))
            {
                baseName = def.Name;
            }
            
            if (string.IsNullOrEmpty(baseName))
            {
                // Fallbacks
                baseName = (ItemCategory)it.Category switch
                {
                    ItemCategory.PizzaBase => "Massa Aberta",
                    ItemCategory.Dough => "Massa",
                    ItemCategory.Sauce => "Molho de Tomate",
                    ItemCategory.Cheese => "Queijo Mussarela",
                    ItemCategory.Topping => "Pepperoni",
                    _ => $"Item #{it.DefinitionId}"
                };
            }
            
            return baseName;
        }
    }
}

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
        private float _lastScrollTime = 0f;
        private const float ScrollCooldown = 0.15f;

        private void Awake()
        {
            _netObj = GetComponent<NetworkObject>();
        }

        private void Start()
        {
            UpdateUi();
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

        public void ToggleActiveHand()
        {
            SetActiveHand(ActiveHand == HandSlotIndex.Left ? HandSlotIndex.Right : HandSlotIndex.Left);
        }

        private void Update()
        {
            if (!IsOwnerPlayer()) return;
            if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked) return;

            // 1. Teclado: 1 (Mão Esquerda), 2 (Mão Direita), Q (Alternar)
            bool key1 = false;
            bool key2 = false;
            bool toggleKey = false;

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                key1 = kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
                key2 = kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
                toggleKey = kb.qKey.wasPressedThisFrame;
            }

            // Fallback para Input legado
            if (!key1 && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))) key1 = true;
            if (!key2 && (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))) key2 = true;
            if (!toggleKey && Input.GetKeyDown(KeyCode.Q)) toggleKey = true;

            if (key1)
            {
                SetActiveHand(HandSlotIndex.Left);
            }
            else if (key2)
            {
                SetActiveHand(HandSlotIndex.Right);
            }
            else if (toggleKey)
            {
                ToggleActiveHand();
            }

            // 2. Scroll do mouse para alternar/ciclar mãos
            float scrollDelta = 0f;
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                scrollDelta = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
            }
            if (Mathf.Abs(scrollDelta) < 0.01f && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                scrollDelta = Input.mouseScrollDelta.y;
            }

            if (Mathf.Abs(scrollDelta) > 0.1f)
            {
                if (Time.time - _lastScrollTime >= ScrollCooldown)
                {
                    _lastScrollTime = Time.time;
                    ToggleActiveHand();
                }
            }

            // Atualiza o estado da interface HUD a cada frame
            UpdateUi();
        }

        private void UpdateUi()
        {
            if (UIManager.Instance == null) return;

            var netState = PizzeriaNetworkState.Instance;
            ulong myId = _netObj != null ? _netObj.OwnerClientId : (NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
            string leftDesc = "Vazio";
            string rightDesc = "Vazio";

            if (netState != null)
            {
                // Verificar itens nas mãos
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

                // Verificar ferramentas nas mãos
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
            }

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

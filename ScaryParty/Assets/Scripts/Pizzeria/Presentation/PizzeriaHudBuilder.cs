using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Presentation
{
    public class PizzeriaHudBuilder : MonoBehaviour
    {
        public static PizzeriaHudBuilder Instance { get; private set; }

        private bool _showStagingAddressPicker = false;
        private ulong _stagingBoxId = 0;
        private int _stagingSlotIndex = 0;
        private int _selectedDestinationIndex = 4;

        private bool _showDevPanel = false;

        private bool _showStoragePicker = false;
        private int _storageStationId = 0;
        private Vector2 _storageScrollPos = Vector2.zero;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // F9 toggles dev panel
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
            {
                _showDevPanel = !_showDevPanel;
                UpdateUIMode();
            }
        }

        public void OpenStagingAddressPicker(ulong boxId, int slotIndex)
        {
            _stagingBoxId = boxId;
            _stagingSlotIndex = slotIndex;
            _showStagingAddressPicker = true;
            SetUIMode(true);
        }

        public void OpenStoragePicker(int stationId)
        {
            _storageStationId = stationId;
            _showStoragePicker = true;
            SetUIMode(true);
        }

        public void CloseStoragePicker()
        {
            _showStoragePicker = false;
            UpdateUIMode();
        }

        private void UpdateUIMode()
        {
            bool anyUIOpen = _showStagingAddressPicker || _showStoragePicker || _showDevPanel;
            SetUIMode(anyUIOpen);
        }

        private void SetUIMode(bool uiActive)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var inputs = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<StarterAssets.StarterAssetsInputs>();
                if (inputs != null)
                {
                    inputs.cursorLocked = !uiActive;
                    inputs.cursorInputForLook = !uiActive;
                }
            }
            Cursor.lockState = uiActive ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = uiActive;
        }

        private void OnGUI()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;

            // Display Restaurant Budget at Top Left below player money
            GUI.Box(new Rect(10, 80, 220, 30), $"Caixa da Pizzaria: R$ {netState.RestaurantBudget.Value}");

            // Display Active Orders on the Top Right
            GUI.Box(new Rect(Screen.width - 260, 10, 250, 400), "PEDIDOS ATIVOS");
            int yOffset = 40;
            
            var catalog = PizzeriaRoot.Instance?.Catalog;
            
            foreach (var order in netState.Orders)
            {
                if (order.Lifecycle != (byte)ScaryParty.Pizzeria.Domain.Types.OrderLifecycle.Accepted) continue;
                
                string content = $"Destino: Casa #{order.DestinationId}\n";
                if (order.LineCount > 0)
                {
                    string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_0, out var r1) ? r1.Name : $"Receita {order.RecipeId_0}";
                    content += $"- {order.Qty_0}x {rName}\n";
                }
                if (order.LineCount > 1)
                {
                    string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_1, out var r2) ? r2.Name : $"Receita {order.RecipeId_1}";
                    content += $"- {order.Qty_1}x {rName}\n";
                }
                if (order.LineCount > 2)
                {
                    string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_2, out var r3) ? r3.Name : $"Receita {order.RecipeId_2}";
                    content += $"- {order.Qty_2}x {rName}\n";
                }
                
                double timeRemaining = order.DeadlineTime - PizzeriaRoot.Instance.Clock.Now;
                content += $"Tempo: {Mathf.Max(0, (float)timeRemaining):F0}s";

                GUIStyle style = new GUIStyle(GUI.skin.label);
                if (timeRemaining < 30) style.normal.textColor = Color.red;
                else if (timeRemaining < 60) style.normal.textColor = Color.yellow;
                else style.normal.textColor = Color.green;

                GUI.Label(new Rect(Screen.width - 250, yOffset, 230, 80), content, style);
                yOffset += 90;
            }

            // Display Inventory Stock Summary
            GUI.Box(new Rect(10, 120, 220, 120), "ESTOQUE (Resumo)");
            int stockFridge = 0;
            int stockCupboard = 0;
            for (int i = 0; i < netState.StorageSlots.Count; i++)
            {
                if (netState.StorageSlots[i].StorageId == 1) stockFridge += netState.StorageSlots[i].Quantity;
                if (netState.StorageSlots[i].StorageId == 2) stockCupboard += netState.StorageSlots[i].Quantity;
            }
            for (int i = 0; i < netState.Tools.Count; i++)
            {
                var tool = netState.Tools[i];
                if (tool.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot)
                {
                    if (tool.HolderId == 1) stockFridge++;
                    if (tool.HolderId == 2) stockCupboard++;
                }
            }
            GUI.Label(new Rect(20, 150, 200, 25), $"Geladeira: {stockFridge} itens/utensílios");
            GUI.Label(new Rect(20, 180, 200, 25), $"Armário: {stockCupboard} itens/utensílios");

            // Backpack contents
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.LocalClient != null)
            {
                ulong myId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
                int backpackCount = 0;
                string backpackContent = "";
                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    if (item.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Backpack && item.HolderId == myId)
                    {
                        backpackCount++;
                        string label = item.LabelDestinationId > 0 ? $"Caixa → #{item.LabelDestinationId}" : "Caixa";
                        backpackContent += $"  Slot {item.SlotId}: {label}\n";
                    }
                }
                if (backpackCount > 0)
                {
                    GUI.Box(new Rect(10, 250, 220, 30 + backpackCount * 25), $"MOCHILA ({backpackCount}/2)");
                    GUI.Label(new Rect(15, 275, 210, backpackCount * 25), backpackContent);
                }
            }

            // Staging Address Picker Window
            if (_showStagingAddressPicker)
            {
                GUI.Box(new Rect(Screen.width / 2 - 175, Screen.height / 2 - 120, 350, 240), "ETIQUETAR CAIXA (Bancada de Retirada)");
                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 80, 300, 40), "Escolha o endereço de entrega para esta caixa.\n(A etiqueta NÃO revela se a receita está correta!)");

                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 30, 150, 25), $"Endereço: #{_selectedDestinationIndex}");
                if (GUI.Button(new Rect(Screen.width / 2 + 10, Screen.height / 2 - 30, 40, 25), "-"))
                {
                    if (_selectedDestinationIndex > 1) _selectedDestinationIndex--;
                }
                if (GUI.Button(new Rect(Screen.width / 2 + 60, Screen.height / 2 - 30, 40, 25), "+"))
                {
                    _selectedDestinationIndex++;
                }

                if (GUI.Button(new Rect(Screen.width / 2 - 140, Screen.height / 2 + 30, 130, 35), "Confirmar Etiqueta"))
                {
                    var cmd = PizzeriaCommandHandler.Instance;
                    if (cmd != null)
                    {
                        cmd.ConfirmStageBoxServerRpc(_stagingBoxId, _stagingSlotIndex, _selectedDestinationIndex);
                    }
                    _showStagingAddressPicker = false;
                    UpdateUIMode();
                }

                if (GUI.Button(new Rect(Screen.width / 2 + 10, Screen.height / 2 + 30, 130, 35), "Cancelar"))
                {
                    _showStagingAddressPicker = false;
                    UpdateUIMode();
                }
            }

            // Storage Picker Window
            if (_showStoragePicker)
            {
                GUI.Box(new Rect(Screen.width / 2 - 200, Screen.height / 2 - 150, 400, 300), $"ESTOQUE (Armazém #{_storageStationId})");
                
                if (GUI.Button(new Rect(Screen.width / 2 + 160, Screen.height / 2 - 150, 40, 20), "X"))
                {
                    CloseStoragePicker();
                }

                _storageScrollPos = GUI.BeginScrollView(new Rect(Screen.width / 2 - 190, Screen.height / 2 - 120, 380, 260), _storageScrollPos, new Rect(0, 0, 360, 1000));
                
                int storageY = 0;
                
                // Show items in this storage (e.g. from StorageSlots, which actually holds the logical quantity of ingredients)
                // Wait, netState.StorageSlots holds the logical count of ingredients available to dispense.
                for (int i = 0; i < netState.StorageSlots.Count; i++)
                {
                    var slot = netState.StorageSlots[i];
                    if (slot.StorageId == _storageStationId && slot.Quantity > 0)
                    {
                        string ingName = catalog != null && catalog.Ingredients.TryGetValue(slot.IngredientDefId, out var def) ? def.Name : $"Ingrediente {slot.IngredientDefId}";
                        GUI.Label(new Rect(10, storageY, 200, 30), $"{ingName} (Qtd: {slot.Quantity})");
                        
                        if (GUI.Button(new Rect(220, storageY, 100, 30), "Pegar"))
                        {
                            var cmd = PizzeriaCommandHandler.Instance;
                            var adapter = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
                            if (cmd != null && adapter != null)
                            {
                                cmd.DispenseIngredientServerRpc(_storageStationId, slot.IngredientDefId, (byte)adapter.ActiveHand);
                                
                                // After clicking pegou, assume they might have full hands and maybe close
                                var pInteract = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerInteraction>();
                                if (pInteract != null)
                                {
                                    if (pInteract.CountItemsInHands() >= 2)
                                    {
                                        // Fechar interface se as mãos ficarem cheias após pegar ou já estiverem
                                        // Let's close anyway after picking up for convenience, or they can click again if they toggled hand.
                                        CloseStoragePicker();
                                    }
                                }
                            }
                        }
                        storageY += 40;
                    }
                }

                // Show tools in this storage
                for (int i = 0; i < netState.Tools.Count; i++)
                {
                    var tool = netState.Tools[i];
                    if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)_storageStationId)
                    {
                        string toolName = tool.DefinitionId == 1 ? "Ralador" : "Faca";
                        GUI.Label(new Rect(10, storageY, 200, 30), $"{toolName}");
                        
                        if (GUI.Button(new Rect(220, storageY, 100, 30), "Pegar"))
                        {
                            var cmd = PizzeriaCommandHandler.Instance;
                            var adapter = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
                            if (cmd != null && adapter != null)
                            {
                                cmd.TransferToolServerRpc(tool.ToolId, (byte)LocationType.Hand, NetworkManager.Singleton.LocalClientId, (int)adapter.ActiveHand);
                                CloseStoragePicker();
                            }
                        }
                        storageY += 40;
                    }
                }

                GUI.EndScrollView();
            }

            // Dev Panel (F9)
            if (_showDevPanel)
            {
                GUI.Box(new Rect(Screen.width - 320, 10, 310, 420), "PAINEL DEV - PIZZERIA (F9)");
                int y = 40;

                if (GUI.Button(new Rect(Screen.width - 300, y, 270, 30), "Atender Pedido Teste (#4: Calabresa+Muss)"))
                {
                    var cmd = PizzeriaCommandHandler.Instance;
                    if (cmd != null) cmd.AnswerPhoneServerRpc(12);
                }
                y += 40;

                if (GUI.Button(new Rect(Screen.width - 300, y, 270, 30), "Comprar Suprimento Queijo (5x)"))
                {
                    var cmd = PizzeriaCommandHandler.Instance;
                    if (cmd != null) cmd.PurchaseSupplyServerRpc(3, 1);
                }
                y += 40;

                if (GUI.Button(new Rect(Screen.width - 300, y, 270, 30), "Comprar Suprimento Calabresa (5x)"))
                {
                    var cmd = PizzeriaCommandHandler.Instance;
                    if (cmd != null) cmd.PurchaseSupplyServerRpc(4, 1);
                }
                y += 40;

                GUI.Label(new Rect(Screen.width - 300, y, 270, 20), "<b>Upgrades Demonstráveis:</b>");
                y += 25;

                if (GUI.Button(new Rect(Screen.width - 300, y, 270, 28), "Forno Rápido (-20% tempo)"))
                {
                    var cmd = PizzeriaCommandHandler.Instance;
                    if (cmd != null) cmd.SetUpgradeLevelServerRpc(1, 1);
                }
                y += 32;

                if (GUI.Button(new Rect(Screen.width - 300, y, 270, 28), "Slot Forno Extra (+1 slot)"))
                {
                    var cmd = PizzeriaCommandHandler.Instance;
                    if (cmd != null) cmd.SetUpgradeLevelServerRpc(2, 1);
                }
                y += 32;

                if (GUI.Button(new Rect(Screen.width - 300, y, 270, 28), "Mochila Maior (+1 slot)"))
                {
                    var cmd = PizzeriaCommandHandler.Instance;
                    if (cmd != null) cmd.SetUpgradeLevelServerRpc(3, 1);
                }
                y += 32;

                if (GUI.Button(new Rect(Screen.width - 300, y, 270, 28), "Desbloquear Cogumelo"))
                {
                    var cmd = PizzeriaCommandHandler.Instance;
                    if (cmd != null) cmd.SetUpgradeLevelServerRpc(5, 1);
                }
                y += 35;

                if (GUI.Button(new Rect(Screen.width - 300, y, 270, 25), "Fechar Painel (F9)"))
                {
                    _showDevPanel = false;
                    UpdateUIMode();
                }
            }
        }
    }
}

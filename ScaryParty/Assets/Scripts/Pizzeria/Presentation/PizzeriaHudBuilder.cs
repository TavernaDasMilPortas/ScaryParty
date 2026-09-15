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
            }
        }

        public void OpenStagingAddressPicker(ulong boxId, int slotIndex)
        {
            _stagingBoxId = boxId;
            _stagingSlotIndex = slotIndex;
            _showStagingAddressPicker = true;
        }

        private void OnGUI()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

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

            // Display Inventory Stock
            GUI.Box(new Rect(10, 120, 220, 120), "ESTOQUE");
            int itemsInFridge = 0;
            int itemsInCupboard = 0;
            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                if (item.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot)
                {
                    if (item.SlotId == 1) itemsInFridge++; // Fridge
                    if (item.SlotId == 2) itemsInCupboard++; // Cupboard
                }
            }
            GUI.Label(new Rect(20, 150, 200, 25), $"Geladeira: {itemsInFridge} itens");
            GUI.Label(new Rect(20, 180, 200, 25), $"Armário: {itemsInCupboard} itens");

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
                }

                if (GUI.Button(new Rect(Screen.width / 2 + 10, Screen.height / 2 + 30, 130, 35), "Cancelar"))
                {
                    _showStagingAddressPicker = false;
                }
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
                }
            }
        }
    }
}

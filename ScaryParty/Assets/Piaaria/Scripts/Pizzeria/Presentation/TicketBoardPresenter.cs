using System.Text;
using UnityEngine;
using TMPro;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Presentation
{
    public class TicketBoardPresenter : MonoBehaviour
    {
        [Header("UI Reference")]
        public TextMeshPro textDisplay;

        private float _updateTimer = 0f;

        private void Update()
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer < 0.5f) return;
            _updateTimer = 0f;

            UpdateBoardText();
        }

        private void UpdateBoardText()
        {
            if (textDisplay == null) return;

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null)
            {
                textDisplay.text = "<b>=== QUADRO DE COMANDAS ===</b>\n\nNenhum pedido ativo.";
                return;
            }

            var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Catalog;

            var sb = new StringBuilder();
            sb.AppendLine("<b>=== QUADRO DE COMANDAS ===</b>\n");

            double now = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening
                ? Unity.Netcode.NetworkManager.Singleton.ServerTime.Time
                : Time.time;

            int activeCount = 0;
            for (int i = 0; i < netState.Orders.Count; i++)
            {
                var order = netState.Orders[i];
                if (order.Lifecycle == (byte)OrderLifecycle.Accepted && !order.TicketArchived)
                {
                    activeCount++;
                    string destHeader = TicketSheetView.FormatOrderDestinationHeader(order);
                    double rem = order.DeadlineTime - now;
                    string timerTag = rem > 0
                        ? $" [{(int)(rem / 60):00}:{(int)(rem % 60):00}]"
                        : " [EXPIRADO]";
                    sb.AppendLine($"<color=yellow>PEDIDO #{order.OrderId}</color> -> <color=cyan>{destHeader}</color>{timerTag}");
                    
                    if (order.LineCount > 0 && order.Qty_0 > 0)
                    {
                        sb.AppendLine($" • {TicketSheetView.FormatOrderLineCompact(order.Spec_0, order.RecipeId_0, order.Qty_0, catalog)}");
                    }
                    if (order.LineCount > 1 && order.Qty_1 > 0)
                    {
                        sb.AppendLine($" • {TicketSheetView.FormatOrderLineCompact(order.Spec_1, order.RecipeId_1, order.Qty_1, catalog)}");
                    }
                    if (order.LineCount > 2 && order.Qty_2 > 0)
                    {
                        sb.AppendLine($" • {TicketSheetView.FormatOrderLineCompact(order.Spec_2, order.RecipeId_2, order.Qty_2, catalog)}");
                    }
                    sb.AppendLine("----------------------------");
                }
            }

            if (activeCount == 0)
            {
                sb.AppendLine("Nenhum pedido ativo no momento.\nAtenda clientes no balcão ou chamadas no telefone!");
            }

            textDisplay.text = sb.ToString();
        }
    }
}

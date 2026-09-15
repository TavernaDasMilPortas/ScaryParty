using System.Text;
using UnityEngine;
using TMPro;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;

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
                textDisplay.text = "<b>=== QUADRO DE PEDIDOS ===</b>\n\nNenhum pedido ativo.";
                return;
            }

            var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Catalog;

            var sb = new StringBuilder();
            sb.AppendLine("<b>=== COMANDAS DE DELIVERY ===</b>\n");

            int activeCount = 0;
            for (int i = 0; i < netState.Orders.Count; i++)
            {
                var order = netState.Orders[i];
                if (order.Lifecycle == (byte)OrderLifecycle.Accepted)
                {
                    activeCount++;
                    sb.AppendLine($"<color=yellow>PEDIDO #{order.OrderId}</color> -> <color=cyan>ENDEREÇO #{order.DestinationId}</color>");
                    
                    if (order.LineCount > 0 && order.Qty_0 > 0)
                    {
                        string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_0, out var r1) ? r1.Name : $"Pizza #{order.RecipeId_0}";
                        sb.AppendLine($" • {order.Qty_0}x {rName}");
                    }
                    if (order.LineCount > 1 && order.Qty_1 > 0)
                    {
                        string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_1, out var r2) ? r2.Name : $"Pizza #{order.RecipeId_1}";
                        sb.AppendLine($" • {order.Qty_1}x {rName}");
                    }
                    if (order.LineCount > 2 && order.Qty_2 > 0)
                    {
                        string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_2, out var r3) ? r3.Name : $"Pizza #{order.RecipeId_2}";
                        sb.AppendLine($" • {order.Qty_2}x {rName}");
                    }
                    sb.AppendLine("----------------------------");
                }
            }

            if (activeCount == 0)
            {
                sb.AppendLine("Nenhum pedido ativo no momento.\nAguarde chamadas no telefone!");
            }

            textDisplay.text = sb.ToString();
        }
    }
}

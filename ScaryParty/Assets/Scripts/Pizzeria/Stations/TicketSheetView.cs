using System;
using System.Text;
using UnityEngine;
using TMPro;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Stations
{
    public class TicketSheetView : MonoBehaviour, IInteractable
    {
        [Header("Components")]
        public TextMeshPro textMesh;
        public Renderer paperRenderer;

        public NetOrderDto Order { get; private set; }
        public bool HasOrder { get; private set; }

        private Material _paperMaterial;
        private Color _basePaperColor = new Color(0.96f, 0.94f, 0.88f);
        private Color _focusedColor = new Color(1.0f, 0.98f, 0.75f);
        private bool _isFocused = false;

        public string InteractPrompt => HasOrder ? $"[E] Abrir Comanda #{Order.OrderId}" : string.Empty;

        private void Awake()
        {
            if (paperRenderer == null)
            {
                paperRenderer = GetComponent<Renderer>();
            }

            if (paperRenderer != null)
            {
                _paperMaterial = paperRenderer.material;
                _paperMaterial.color = _basePaperColor;
            }

            var col = GetComponent<Collider>();
            if (col == null || col is MeshCollider)
            {
                if (col != null) Destroy(col);
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 1f, 0.08f);
                box.center = new Vector3(0f, 0f, -0.02f);
            }
        }

        public void BindOrder(NetOrderDto order, double serverTime, DefinitionCatalog catalog)
        {
            Order = order;
            HasOrder = true;
            gameObject.SetActive(true);

            if (textMesh == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"<size=120%><b>#{order.OrderId}</b></size>");
            sb.AppendLine($"<color=#990000><b>{AddressFormat.Format(new AddressId(order.DestinationId))}</b></color>");
            sb.AppendLine("<size=70%>------------------</size>");

            // Receitas
            if (order.LineCount > 0 && order.Qty_0 > 0)
            {
                string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_0, out var r0) ? r0.Name : $"Pizza #{order.RecipeId_0}";
                sb.AppendLine($"• {order.Qty_0}x {rName}");
            }
            if (order.LineCount > 1 && order.Qty_1 > 0)
            {
                string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_1, out var r1) ? r1.Name : $"Pizza #{order.RecipeId_1}";
                sb.AppendLine($"• {order.Qty_1}x {rName}");
            }
            if (order.LineCount > 2 && order.Qty_2 > 0)
            {
                string rName = catalog != null && catalog.Recipes.TryGetValue(order.RecipeId_2, out var r2) ? r2.Name : $"Pizza #{order.RecipeId_2}";
                sb.AppendLine($"• {order.Qty_2}x {rName}");
            }

            sb.AppendLine("<size=70%>------------------</size>");

            // Temporizador e status
            if (order.Lifecycle == (byte)OrderLifecycle.Delivered)
            {
                sb.AppendLine("<color=green><b>ENTREGUE</b></color>");
            }
            else if (order.Lifecycle == (byte)OrderLifecycle.Cancelled)
            {
                sb.AppendLine("<color=red><b>CANCELADO</b></color>");
            }
            else
            {
                double remaining = order.DeadlineTime - serverTime;
                if (remaining > 0)
                {
                    int mins = (int)(remaining / 60);
                    int secs = (int)(remaining % 60);
                    string colorTag = remaining > 120 ? "<color=#008800>" : (remaining > 45 ? "<color=#CC8800>" : "<color=#CC0000>");
                    sb.AppendLine($"{colorTag}<b>⏱ {mins:00}:{secs:00}</b></color>");
                }
                else
                {
                    sb.AppendLine("<color=red><b>EXPIRADO</b></color>");
                }
            }

            textMesh.text = sb.ToString();
        }

        public void Clear()
        {
            HasOrder = false;
            gameObject.SetActive(false);
            if (textMesh != null) textMesh.text = string.Empty;
        }

        public void OnInteract(GameObject interactor)
        {
            if (!HasOrder) return;

            if (TicketModalPresenter.Instance != null)
            {
                TicketModalPresenter.Instance.Open(Order);
            }
        }

        public void OnFocus()
        {
            _isFocused = true;
            if (_paperMaterial != null)
            {
                _paperMaterial.color = _focusedColor;
            }
        }

        public void OnLoseFocus()
        {
            _isFocused = false;
            if (_paperMaterial != null)
            {
                _paperMaterial.color = _basePaperColor;
            }
        }

        private void OnDestroy()
        {
            if (_paperMaterial != null)
            {
                Destroy(_paperMaterial);
            }
        }
    }
}

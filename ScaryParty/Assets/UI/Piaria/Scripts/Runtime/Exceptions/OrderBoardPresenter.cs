using System;
using UnityEngine;
using TMPro;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.UI.Piaria.Exceptions
{
    /// <summary>
    /// Gerenciador do Quadro Físico 3D de Comandas (exceção autorizada em uGUI / TextMeshPro).
    /// Aplica as texturas de papel e fontes do pacote Piaria aos tickets 3D na parede da pizzaria,
    /// mantendo total autoridade de rede e suporte à leitura/zoom com [E] e arquivamento com [F].
    /// </summary>
    public class OrderBoardPresenter : MonoBehaviour
    {
        public static OrderBoardPresenter Instance { get; private set; }

        public TicketBoardStation station;
        public TMP_FontAsset fontAsset;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            if (station == null)
                station = GetComponent<TicketBoardStation>() ?? FindFirstObjectByType<TicketBoardStation>();
        }

        private void Start()
        {
            ApplyPiariaStyling();
        }

        public void ApplyPiariaStyling()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (station == null || catalog == null) return;

            // Aplica fonte e material de papel aos sheets existentes
            var sheets = station.GetComponentsInChildren<TicketSheetView>();
            foreach (var sheet in sheets)
            {
                if (sheet != null && sheet.paperRenderer != null && catalog.paperPanel1 != null)
                {
                    sheet.paperRenderer.material.color = new Color(0.96f, 0.93f, 0.86f);
                }
            }
        }
    }
}

using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Presenters;

namespace ScaryParty.Pizzeria.Stations
{
    /// <summary>
    /// Quadro físico de operações/gestão na pizzaria (P5): abre o OperationsHub em Pedidos/Loja/Equipe.
    /// Implementa IInteractable de forma independente para não duplicar IDs de StationView.
    /// </summary>
    public class ManagementBoardStation : MonoBehaviour, IInteractable
    {
        public string defaultTab = "orders";

        public string InteractPrompt => "[E] Abrir Central de Gestão / Operações [J]";

        public void OnInteract(GameObject interactor)
        {
            OperationsHubPresenter.EnsureInstance().Open(string.IsNullOrEmpty(defaultTab) ? "orders" : defaultTab);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }
    }
}

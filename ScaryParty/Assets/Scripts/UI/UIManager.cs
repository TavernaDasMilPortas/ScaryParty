using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Presenters;

/// <summary>Compatibility API for gameplay systems; Piaria presenters own every visual.</summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    public UIDocument uiDocument;
    private int _completedDeliveries;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void UpdateMoneyDisplay(int total) => HudPresenter.Instance?.SetMoney(total);

    public void AddCompletedDelivery() => HudPresenter.Instance?.SetDeliveries(++_completedDeliveries);

    public void UpdateHand(bool isRightHand, string itemName) => SetHandState(isRightHand, false, itemName);

    public void SetHandState(bool isRightHand, bool isSelected, string itemName) =>
        HudPresenter.Instance?.SetHandState(isRightHand, isSelected, itemName);

    public void ShowInteractionPrompt(string message) => HudPresenter.Instance?.ShowPrompt(message);

    public void HideInteractionPrompt() => HudPresenter.Instance?.HidePrompt();

    public void ToggleMinimapFullscreen(bool isFullscreen) =>
        HudPresenter.Instance?.ToggleMinimapFullscreen(isFullscreen);

    public void UpdateOrdersList(List<string> activeOrders)
    {
        if (HudPresenter.Instance != null)
            HudPresenter.Instance.UpdateActiveOrdersDisplay(activeOrders == null || activeOrders.Count == 0
                ? "NENHUMA COMANDA ATIVA"
                : $"COMANDAS ATIVAS ({activeOrders.Count}): {string.Join(", ", activeOrders)}");
    }
}

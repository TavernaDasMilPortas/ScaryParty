using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Controlador da página de Resumo da Noite dentro do shell de Intermissão 2.1.
    /// </summary>
    public sealed class NightSummaryPagePresenter
    {
        private readonly VisualElement _root;
        private readonly Label _headline;
        private readonly Label _subline;
        private readonly Label _kpiRevenue;
        private readonly Label _kpiExpenses;
        private readonly Label _kpiProfit;
        private readonly Label _kpiOrders;
        private readonly Label _kpiReputation;
        private readonly Label _kpiBudget;

        public NightSummaryPagePresenter(VisualElement pageRoot)
        {
            _root = pageRoot;
            _headline = pageRoot.Q<Label>("summary-headline");
            _subline = pageRoot.Q<Label>("summary-subline");
            _kpiRevenue = pageRoot.Q<Label>("kpi-revenue");
            _kpiExpenses = pageRoot.Q<Label>("kpi-expenses");
            _kpiProfit = pageRoot.Q<Label>("kpi-profit");
            _kpiOrders = pageRoot.Q<Label>("kpi-orders");
            _kpiReputation = pageRoot.Q<Label>("kpi-reputation");
            _kpiBudget = pageRoot.Q<Label>("kpi-budget");
        }

        public void Bind(NetNightStateDto night, NetRunStateDto run, int budget, bool animate = true)
        {
            if (_headline != null)
            {
                _headline.text = night.NightNumber <= 0
                    ? "PREPARAÇÃO PARA A NOITE 1"
                    : $"NOITE {night.NightNumber} CONCLUÍDA — META BATIDA!";
            }

            int completedGoal = night.LastCompletedNightRevenueGoal > 0 ? night.LastCompletedNightRevenueGoal : night.RevenueGoal;
            if (_subline != null)
            {
                _subline.text = night.NightNumber <= 0
                    ? $"Meta da Noite {run.TargetNightNumber}: R$ {night.RevenueGoal} • Turno {run.TargetNightNumber} de {run.MaxRunNights}."
                    : $"Meta batida: R$ {completedGoal} • Preparando turno para a Noite {run.TargetNightNumber} de {run.MaxRunNights}.";
            }

            int revenue = night.EarnedRevenue;
            int expenses = night.NightSupplySpent + night.NightShopSpent + night.NightWagesSpent;
            int profit = revenue - expenses;

            if (_kpiRevenue != null)
            {
                if (animate) UIToolkitTweenService.TweenCounter(_kpiRevenue, 0, revenue, 0.55f, "R$ ");
                else _kpiRevenue.text = UIToolkitTweenService.FormatCounterValue(revenue, "R$ ");
            }

            if (_kpiExpenses != null)
            {
                if (animate) UIToolkitTweenService.TweenCounter(_kpiExpenses, 0, expenses, 0.55f, "R$ ");
                else _kpiExpenses.text = UIToolkitTweenService.FormatCounterValue(expenses, "R$ ");
            }

            if (_kpiProfit != null)
            {
                if (animate) UIToolkitTweenService.TweenCounter(_kpiProfit, 0, profit, 0.65f, "R$ ");
                else _kpiProfit.text = UIToolkitTweenService.FormatCounterValue(profit, "R$ ");
            }

            if (_kpiOrders != null)
            {
                if (animate) UIToolkitTweenService.TweenCounter(_kpiOrders, 0, night.OrdersCompleted, 0.5f);
                else _kpiOrders.text = night.OrdersCompleted.ToString();
            }

            if (_kpiReputation != null)
            {
                _kpiReputation.text = $"{night.Reputation:F0}%";
            }

            if (_kpiBudget != null)
            {
                _kpiBudget.text = $"R$ {budget}";
            }
        }
    }
}

using System.Text;
using Unity.Netcode;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Controlador da Central de Situação (TAB) com 4 blocos: Noite/Meta, Reputação/Operação, Efeitos/Upgrades e Equipe Online.
    /// </summary>
    public sealed class OperationsStatusPresenter
    {
        private readonly Label _nightGoal;
        private readonly Label _repOrders;
        private readonly Label _effectsUpgrades;
        private readonly Label _onlineTeam;

        public OperationsStatusPresenter(VisualElement root)
        {
            _nightGoal = root.Q<Label>("ops-night-goal");
            _repOrders = root.Q<Label>("ops-reputation-orders");
            _effectsUpgrades = root.Q<Label>("ops-effects-upgrades");
            _onlineTeam = root.Q<Label>("ops-online-team");
        }

        public void Bind(PizzeriaNetworkState net)
        {
            if (net == null) return;

            var night = net.NightState.Value;
            var run = net.RunState.Value;
            int budget = net.RestaurantBudget.Value;

            if (_nightGoal != null)
            {
                _nightGoal.text =
                    $"Noite {night.NightNumber} de {run.MaxRunNights}\n" +
                    $"Receita: R$ {night.EarnedRevenue} / Meta: R$ {night.RevenueGoal}\n" +
                    $"Caixa da Equipe: R$ {budget}";
            }

            if (_repOrders != null)
            {
                _repOrders.text =
                    $"Reputação: {night.Reputation}%\n" +
                    $"Pedidos Concluídos: {night.OrdersCompleted}  |  Falhos: {night.OrdersFailed}\n" +
                    $"Clientes Perdidos: {night.LostCustomers}";
            }

            if (_effectsUpgrades != null)
            {
                string oven = run.HasOvenTimer ? "Sim" : "Não";
                _effectsUpgrades.text =
                    $"Timer de Forno: {oven}  |  Mochila: +{run.ExtraBackpackCapacity}\n" +
                    $"Demanda: {run.DemandMultiplier:F2}x  |  Vampiros: {run.VampireSpawnMultiplier:F2}x\n" +
                    $"Erro Equipe: {run.EmployeeErrorMultiplier:F2}x";
            }

            if (_onlineTeam != null)
            {
                var sb = new StringBuilder();
                if (net.RunPlayerStates.Count > 0)
                {
                    for (int i = 0; i < net.RunPlayerStates.Count; i++)
                    {
                        var rp = net.RunPlayerStates[i];
                        string pName = rp.PlayerName.Value.ToString();
                        if (string.IsNullOrWhiteSpace(pName)) pName = $"Jogador #{rp.ClientId}";
                        string readyTag = rp.IsReady ? " [Pronto]" : string.Empty;
                        sb.AppendLine($"• {pName}{readyTag}");
                    }
                }
                else
                {
                    var allPlayers = UnityEngine.Object.FindObjectsByType<PlayerState>(UnityEngine.FindObjectsSortMode.None);
                    if (allPlayers != null && allPlayers.Length > 0)
                    {
                        for (int i = 0; i < allPlayers.Length; i++)
                        {
                            var ps = allPlayers[i];
                            if (ps == null) continue;
                            string raw = ps.PlayerName.Value.ToString();
                            string pName = string.IsNullOrWhiteSpace(raw) ? $"Jogador #{ps.OwnerClientId}" : raw;
                            sb.AppendLine($"• {pName}");
                        }
                    }
                    else
                    {
                        sb.AppendLine("• Offline / Local");
                    }
                }
                _onlineTeam.text = sb.ToString().TrimEnd();
            }
        }
    }
}

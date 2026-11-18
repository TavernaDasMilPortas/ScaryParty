using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Controlador das páginas Loja (3x2) e Preparar (resumo de votos + equipe + Pronto).
    /// </summary>
    public sealed class RunShopPagePresenter
    {
        private readonly VisualElement _shopSection;
        private readonly VisualElement _prepareSection;
        private readonly Label _activeUpgradesLabel;
        private readonly VisualElement _shopGrid;
        private readonly Label _votesSummaryLabel;
        private readonly VisualElement _shortcutsRow;
        private readonly VisualElement _teamCardsContainer;
        private readonly Button _readyBtn;
        private readonly Action<NightIntermissionPresenter.TabPage> _navigateTab;

        private string _pendingOfferId = string.Empty;

        public RunShopPagePresenter(VisualElement pageRoot, Action<NightIntermissionPresenter.TabPage> navigateTab)
        {
            _navigateTab = navigateTab;
            _shopSection = pageRoot.Q<VisualElement>("shop-section");
            _prepareSection = pageRoot.Q<VisualElement>("prepare-section");
            _activeUpgradesLabel = pageRoot.Q<Label>("shop-active-upgrades");
            _shopGrid = pageRoot.Q<VisualElement>("shop-items-grid");
            _votesSummaryLabel = pageRoot.Q<Label>("prepare-votes-summary");
            _shortcutsRow = pageRoot.Q<VisualElement>("prepare-shortcuts-row");
            _teamCardsContainer = pageRoot.Q<VisualElement>("prepare-team-cards");
            _readyBtn = pageRoot.Q<Button>("prepare-ready-btn");
        }

        public void Bind(bool isPrepareTab, PizzeriaNetworkState net)
        {
            if (net == null) return;

            if (_shopSection != null)
                _shopSection.style.display = isPrepareTab ? DisplayStyle.None : DisplayStyle.Flex;
            if (_prepareSection != null)
                _prepareSection.style.display = isPrepareTab ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isPrepareTab) BindShop(net);
            else BindPrepare(net);
        }

        private void BindShop(PizzeriaNetworkState net)
        {
            var run = net.RunState.Value;
            int budget = net.RestaurantBudget.Value;
            var icons = NightIntermissionIconCatalog.ResolveActive();

            if (_activeUpgradesLabel != null)
            {
                string oven = run.HasOvenTimer ? "Timer de Forno ✓" : "Timer de Forno (não)";
                string bag = $"Mochila +{run.ExtraBackpackCapacity}";
                _activeUpgradesLabel.text = $"Caixa: R$ {budget}  |  {oven}  |  {bag}";
            }

            if (_shopGrid == null) return;
            _shopGrid.Clear();

            for (int i = 0; i < net.RunShopOffers.Count; i++)
            {
                var offer = net.RunShopOffers[i];
                string offerId = offer.OfferId.ToString();
                bool isPending = string.Equals(_pendingOfferId, offerId, StringComparison.Ordinal);

                var card = new VisualElement();
                card.AddToClassList("modifier-card");
                card.style.width = Length.Percent(32f);
                card.style.marginBottom = 10f;

                var top = new VisualElement();
                top.AddToClassList("modifier-card-top");
                top.Add(new Label(icons != null ? icons.ResolveSymbol(offerId, RunModifierKind.PermanentUpgrade) : "🛒") { pickingMode = PickingMode.Ignore });
                top.Add(new Label($"Nv. {offer.CurrentLevel}/{offer.MaxLevel}") { pickingMode = PickingMode.Ignore });

                var title = new Label(offer.DisplayName.ToString());
                title.AddToClassList("modifier-card-title");
                var desc = new Label(offer.Description.ToString());
                desc.AddToClassList("modifier-card-desc");

                var footer = new VisualElement();
                footer.AddToClassList("modifier-card-footer");
                footer.Add(new Label($"R$ {offer.EffectiveCost}"));

                bool canBuy = offer.CanPurchase && !isPending && budget >= offer.EffectiveCost;
                var buyBtn = new Button(() =>
                {
                    if (!canBuy)
                    {
                        UIToolkitTweenService.ShakeReject(card);
                        NightIntermissionFeelBridge.Play(NightUIFeedbackId.ShopItemPurchaseReject, FeedbackPriority.High);
                        return;
                    }
                    _pendingOfferId = offerId;
                    NightIntermissionFeelBridge.Play(NightUIFeedbackId.ShopItemPurchasePending, FeedbackPriority.Normal);
                    PizzeriaCommandHandler.Instance?.PurchaseRunShopOfferServerRpc(new FixedString64Bytes(offerId));
                })
                {
                    text = isPending ? "..." : (offer.CanPurchase ? "COMPRAR" : "MÁX.")
                };
                buyBtn.AddToClassList("modifier-card-btn");
                buyBtn.SetEnabled(canBuy);

                footer.Add(buyBtn);
                card.Add(top);
                card.Add(title);
                card.Add(desc);
                card.Add(footer);
                _shopGrid.Add(card);
            }
        }

        private void BindPrepare(PizzeriaNetworkState net)
        {
            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            bool hasH = false, hasO = false, isReady = false;
            string hId = "-", oId = "-";

            for (int i = 0; i < net.RunPlayerStates.Count; i++)
            {
                var p = net.RunPlayerStates[i];
                if (p.ClientId != localId) continue;
                hasH = p.HasVotedHindrance;
                hasO = p.HasVotedOpportunity;
                isReady = p.IsReady;
                if (hasH) hId = p.VotedHindranceId.ToString();
                if (hasO) oId = p.VotedOpportunityId.ToString();
                break;
            }

            if (_votesSummaryLabel != null)
            {
                _votesSummaryLabel.text = $"Seus votos: Imprevisto ({hId})  |  Oportunidade ({oId})";
            }

            if (_shortcutsRow != null)
            {
                _shortcutsRow.Clear();
                if (!hasH)
                {
                    var btnH = new Button(() => _navigateTab?.Invoke(NightIntermissionPresenter.TabPage.Hindrance))
                    {
                        text = "Votar em Imprevistos →"
                    };
                    btnH.AddToClassList("modifier-card-btn");
                    btnH.style.marginRight = 8f;
                    _shortcutsRow.Add(btnH);
                }
                if (!hasO)
                {
                    var btnO = new Button(() => _navigateTab?.Invoke(NightIntermissionPresenter.TabPage.Opportunity))
                    {
                        text = "Votar em Oportunidades →"
                    };
                    btnO.AddToClassList("modifier-card-btn");
                    _shortcutsRow.Add(btnO);
                }
            }

            if (_teamCardsContainer != null)
            {
                _teamCardsContainer.Clear();
                for (int i = 0; i < net.RunPlayerStates.Count; i++)
                {
                    var p = net.RunPlayerStates[i];
                    var card = new VisualElement();
                    card.AddToClassList("summary-kpi-card");
                    card.style.width = Length.Percent(23.5f);
                    card.Add(new Label(p.PlayerName.ToString()) { style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold } });
                    card.Add(new Label($"Imprevisto: {(p.HasVotedHindrance ? "✓" : "...")}"));
                    card.Add(new Label($"Oportunidade: {(p.HasVotedOpportunity ? "✓" : "...")}"));
                    card.Add(new Label(p.IsReady ? "PRONTO ✓" : "Aguardando..."));
                    _teamCardsContainer.Add(card);
                }
            }

            if (_readyBtn != null)
            {
                bool canReady = hasH && hasO;
                _readyBtn.text = isReady ? "CANCELAR PRONTO" : "MARCAR COMO PRONTO";
                _readyBtn.SetEnabled(isReady || canReady);
                _readyBtn.clicked -= OnReadyClicked;
                _readyBtn.clicked += OnReadyClicked;
            }
        }

        private void OnReadyClicked()
        {
            var net = PizzeriaNetworkState.Instance;
            if (net == null) return;

            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            bool currentlyReady = false;
            for (int i = 0; i < net.RunPlayerStates.Count; i++)
            {
                if (net.RunPlayerStates[i].ClientId == localId)
                {
                    currentlyReady = net.RunPlayerStates[i].IsReady;
                    break;
                }
            }

            NightIntermissionFeelBridge.Play(
                currentlyReady ? NightUIFeedbackId.ReadyToggleOff : NightUIFeedbackId.ReadyToggleOn,
                FeedbackPriority.Normal);
            PizzeriaCommandHandler.Instance?.SetRunReadyServerRpc(!currentlyReady);
        }

        public void ClearPendingPurchase() => _pendingOfferId = string.Empty;
    }
}

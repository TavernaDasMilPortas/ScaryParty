using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Página de votação por categoria (Imprevistos ou Oportunidades) com 3 cards e animação de queda.
    /// </summary>
    public sealed class ModifierVotePagePresenter
    {
        private readonly VisualElement _root;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly VisualElement _cardsRow;
        private readonly List<VisualElement> _cardElements = new List<VisualElement>(3);

        public ModifierVotePagePresenter(VisualElement pageRoot)
        {
            _root = pageRoot;
            _title = pageRoot.Q<Label>("vote-category-title");
            _subtitle = pageRoot.Q<Label>("vote-category-subtitle");
            _cardsRow = pageRoot.Q<VisualElement>("vote-cards-row");
        }

        public void Bind(RunModifierKind category, PizzeriaNetworkState net, bool animateDrop)
        {
            if (_cardsRow == null || net == null) return;

            var theme = NightIntermissionTheme.ResolveActive();
            var icons = NightIntermissionIconCatalog.ResolveActive();

            if (_title != null)
            {
                _title.text = theme != null ? theme.GetCategoryLabel(category).ToUpperInvariant() : category.ToString();
            }

            if (_subtitle != null)
            {
                _subtitle.text = category == RunModifierKind.Hindrance
                    ? "Escolha qual imprevisto terá mais bilhetes na urna da próxima noite."
                    : "Escolha qual oportunidade terá mais bilhetes na urna da próxima noite.";
            }

            string localVoteId = FindLocalPlayerVote(net, category);
            bool isLocked = net.RunState.Value.IntermissionStage >= (byte)IntermissionStage.Locked;

            _cardsRow.Clear();
            _cardElements.Clear();

            for (int i = 0; i < net.RunVoteOptions.Count; i++)
            {
                var opt = net.RunVoteOptions[i];
                if (opt.Category != (byte)category) continue;

                string optionId = opt.OptionId.ToString();
                bool isSelected = !string.IsNullOrEmpty(localVoteId) && string.Equals(localVoteId, optionId, System.StringComparison.Ordinal);

                var card = new VisualElement();
                card.AddToClassList("modifier-card");
                if (isSelected) card.AddToClassList("modifier-card-selected");

                var top = new VisualElement();
                top.AddToClassList("modifier-card-top");
                var iconLbl = new Label(icons != null ? icons.ResolveSymbol(optionId, category) : "★");
                iconLbl.AddToClassList("modifier-card-icon");
                var tagLbl = new Label(opt.DurationTag.ToString());
                tagLbl.AddToClassList("modifier-card-tag");
                top.Add(iconLbl);
                top.Add(tagLbl);

                var body = new VisualElement();
                var titleLbl = new Label(opt.Title.ToString());
                titleLbl.AddToClassList("modifier-card-title");
                var descLbl = new Label(opt.Description.ToString());
                descLbl.AddToClassList("modifier-card-desc");
                body.Add(titleLbl);
                body.Add(descLbl);

                var footer = new VisualElement();
                footer.AddToClassList("modifier-card-footer");
                var ticketsLbl = new Label($"{opt.VoteCount} bilhete(s)");
                ticketsLbl.AddToClassList("modifier-card-tickets");

                var voteBtn = new Button(() =>
                {
                    if (isLocked)
                    {
                        UIToolkitTweenService.ShakeReject(card, theme != null ? theme.errorShakePixels : 6f);
                        NightIntermissionFeelBridge.Play(NightUIFeedbackId.ErrorShake, FeedbackPriority.High);
                        return;
                    }

                    UIToolkitTweenService.SelectCardPulse(card, theme != null ? theme.impactScale : 1.04f);
                    NightIntermissionFeelBridge.Play(
                        category == RunModifierKind.Hindrance ? NightUIFeedbackId.HindranceCardSelect : NightUIFeedbackId.OpportunityCardSelect,
                        FeedbackPriority.Normal);

                    PizzeriaCommandHandler.Instance?.CastRunCategoryVoteServerRpc((byte)category, new FixedString64Bytes(optionId));
                })
                {
                    text = isSelected ? "VOTADO ✓" : "VOTAR"
                };
                voteBtn.AddToClassList("modifier-card-btn");
                if (isSelected) voteBtn.AddToClassList("modifier-card-btn-selected");
                voteBtn.SetEnabled(!isLocked);

                footer.Add(ticketsLbl);
                footer.Add(voteBtn);

                card.Add(top);
                card.Add(body);
                card.Add(footer);

                card.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    UIToolkitTweenService.HoverCardLift(card, theme != null ? theme.hoverScale : 1.015f);
                    NightIntermissionFeelBridge.Play(NightUIFeedbackId.CardHover, FeedbackPriority.Low);
                });
                card.RegisterCallback<PointerLeaveEvent>(_ => UIToolkitTweenService.HoverCardReset(card));

                _cardsRow.Add(card);
                _cardElements.Add(card);
            }

            if (animateDrop && _cardElements.Count > 0)
            {
                float stagger = theme != null ? theme.cardDropStagger : 0.20f;
                float dist = theme != null ? theme.cardDropDistance : 90f;
                float dur = theme != null ? theme.cardDropDuration : 0.45f;
                UIToolkitTweenService.StaggerDropCards(_cardElements, stagger, dist, dur);
                NightIntermissionFeelBridge.Play(NightUIFeedbackId.CardDropIn, FeedbackPriority.Normal);
            }
        }

        private static string FindLocalPlayerVote(PizzeriaNetworkState net, RunModifierKind category)
        {
            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            for (int i = 0; i < net.RunPlayerStates.Count; i++)
            {
                var p = net.RunPlayerStates[i];
                if (p.ClientId != localId) continue;
                return category == RunModifierKind.Hindrance
                    ? p.VotedHindranceId.ToString()
                    : p.VotedOpportunityId.ToString();
            }
            return string.Empty;
        }
    }
}

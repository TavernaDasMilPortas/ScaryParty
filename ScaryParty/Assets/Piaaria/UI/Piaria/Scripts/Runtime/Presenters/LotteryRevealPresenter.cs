using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Controlador da revelação sequencial (Imprevisto -> Oportunidade) e contagem regressiva 3-2-1 sincronizada pelo servidor.
    /// </summary>
    public sealed class LotteryRevealPresenter
    {
        private readonly VisualElement _root;
        private readonly Label _statusLabel;
        private readonly VisualElement _hindranceCard;
        private readonly Label _hindranceTitle;
        private readonly Label _hindranceDesc;
        private readonly VisualElement _opportunityCard;
        private readonly Label _opportunityTitle;
        private readonly Label _opportunityDesc;
        private readonly Label _countdownLabel;

        private bool _hindranceRevealed;
        private bool _opportunityRevealed;
        private int _lastCountdownDigit = -1;

        public LotteryRevealPresenter(VisualElement pageRoot)
        {
            _root = pageRoot;
            _statusLabel = pageRoot.Q<Label>("reveal-status-label");
            _hindranceCard = pageRoot.Q<VisualElement>("reveal-hindrance-card");
            _hindranceTitle = pageRoot.Q<Label>("reveal-hindrance-title");
            _hindranceDesc = pageRoot.Q<Label>("reveal-hindrance-desc");
            _opportunityCard = pageRoot.Q<VisualElement>("reveal-opportunity-card");
            _opportunityTitle = pageRoot.Q<Label>("reveal-opportunity-title");
            _opportunityDesc = pageRoot.Q<Label>("reveal-opportunity-desc");
            _countdownLabel = pageRoot.Q<Label>("reveal-countdown-label");
        }

        public void Tick(PizzeriaNetworkState net, double serverNow)
        {
            if (net == null) return;

            var run = net.RunState.Value;
            double elapsed = serverNow - run.RevealStartedAtServerTime;

            if (!_hindranceRevealed && elapsed >= 0.8)
            {
                _hindranceRevealed = true;
                ApplyWinnerCard(net, RunModifierKind.Hindrance, run.WinningHindranceId.ToString(), _hindranceCard, _hindranceTitle, _hindranceDesc);
                NightIntermissionFeelBridge.Play(NightUIFeedbackId.LotteryHindranceReveal, FeedbackPriority.High);
            }

            if (!_opportunityRevealed && elapsed >= 2.1)
            {
                _opportunityRevealed = true;
                ApplyWinnerCard(net, RunModifierKind.Opportunity, run.WinningOpportunityId.ToString(), _opportunityCard, _opportunityTitle, _opportunityDesc);
                NightIntermissionFeelBridge.Play(NightUIFeedbackId.LotteryOpportunityReveal, FeedbackPriority.High);
            }

            if (serverNow >= run.CountdownStartsAtServerTime)
            {
                if (_statusLabel != null)
                    _statusLabel.text = "TURNO PRESTES A COMEÇAR!";

                double remaining = System.Math.Max(0.0, run.StartNightAtServerTime - serverNow);
                int digit = remaining <= 0.02 ? 0 : Mathf.CeilToInt((float)remaining);
                if (digit != _lastCountdownDigit && digit >= 0 && digit <= 3)
                {
                    _lastCountdownDigit = digit;
                    if (_countdownLabel != null)
                    {
                        _countdownLabel.text = digit == 0 ? "VALENDO!" : digit.ToString();
                        UIToolkitTweenService.PunchCountdownNumber(_countdownLabel);
                    }
                    if (digit == 0 && _statusLabel != null)
                    {
                        _statusLabel.text = "INICIANDO TURNO!";
                    }
                    var tickId = digit switch
                    {
                        3 => NightUIFeedbackId.CountdownTick3,
                        2 => NightUIFeedbackId.CountdownTick2,
                        1 => NightUIFeedbackId.CountdownTick1,
                        _ => NightUIFeedbackId.CountdownGo
                    };
                    NightIntermissionFeelBridge.Play(tickId, FeedbackPriority.High);
                }
            }
        }

        public void Reset()
        {
            _hindranceRevealed = false;
            _opportunityRevealed = false;
            _lastCountdownDigit = -1;
            if (_root != null) UIToolkitTweenService.KillTweens(_root);
        }

        private static void ApplyWinnerCard(
            PizzeriaNetworkState net,
            RunModifierKind kind,
            string winningId,
            VisualElement card,
            Label titleLbl,
            Label descLbl)
        {
            string title = winningId;
            string desc = string.Empty;

            for (int i = 0; i < net.RunVoteOptions.Count; i++)
            {
                var opt = net.RunVoteOptions[i];
                if (opt.Category == (byte)kind && opt.OptionId.ToString() == winningId)
                {
                    title = opt.Title.ToString();
                    desc = opt.Description.ToString();
                    break;
                }
            }

            if (titleLbl != null) titleLbl.text = string.IsNullOrEmpty(title) ? "Nenhum" : title;
            if (descLbl != null) descLbl.text = desc;
            if (card != null)
            {
                card.AddToClassList("modifier-card-winner");
                UIToolkitTweenService.RevealWinnerStamp(card);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Painel inter-noites (M6): votação de modificador (Opção A vs B), loja da pizzaria com caixa compartilhado
    /// e controle coletivo de prontidão (Pronto X/Y) entre Noite 1 -> 2 -> 3.
    /// </summary>
    public class RunPreparationPresenter : MonoBehaviour
    {
        public static RunPreparationPresenter Instance { get; private set; }

        public static RunPreparationPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<RunPreparationPresenter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("RunPreparationPresenter");
            return go.AddComponent<RunPreparationPresenter>();
        }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private ScrollView _bodyScroll;
        private Label _headerLabel;
        private Label _budgetLabel;
        private VisualElement _stagesContainer;
        private VisualElement _shopContainer;
        private Label _modifiersSummaryLabel;
        private Button _readyBtn;
        private Label _readyStatusLabel;

        private bool _isOpen;
        private bool _hasLock;
        private bool _localReady;
        private string _lastVoteSignature = string.Empty;
        private string _lastShopSignature = string.Empty;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_hasLock)
            {
                GameplayInputGate.PopLock();
                _hasLock = false;
            }
            if (Instance == this) Instance = null;
        }

        private void EnsureUIInitialized()
        {
            if (_overlay != null) return;

            var hud = FindFirstObjectByType<ScaryParty.UI.Piaria.Presenters.HudPresenter>();
            _doc = hud != null ? hud.uiDocument : GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            if (_doc.panelSettings == null)
            {
                var ps = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (ps != null && ps.Length > 0) _doc.panelSettings = ps[0];
            }

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            _overlay = new VisualElement { name = "RunPreparationOverlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.88f);
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.display = DisplayStyle.None;

            var card = new VisualElement();
            card.style.width = Length.Percent(92);
            card.style.maxWidth = 960;
            card.style.maxHeight = Length.Percent(92);
            card.style.backgroundColor = new Color(0.10f, 0.11f, 0.15f, 0.98f);
            card.style.borderTopWidth = 3;
            card.style.borderBottomWidth = 3;
            card.style.borderLeftWidth = 3;
            card.style.borderRightWidth = 3;
            card.style.borderTopColor = new Color(0.95f, 0.72f, 0.25f);
            card.style.borderBottomColor = new Color(0.95f, 0.72f, 0.25f);
            card.style.borderLeftColor = new Color(0.95f, 0.72f, 0.25f);
            card.style.borderRightColor = new Color(0.95f, 0.72f, 0.25f);
            card.style.borderTopLeftRadius = 12;
            card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12;
            card.style.borderBottomRightRadius = 12;
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;

            _headerLabel = new Label("PREPARAÇÃO DA PRÓXIMA NOITE");
            _headerLabel.style.fontSize = 22;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.style.color = new Color(0.96f, 0.82f, 0.35f);
            _headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(_headerLabel);

            _budgetLabel = new Label("Caixa Compartilhado: R$ 0");
            _budgetLabel.style.fontSize = 15;
            _budgetLabel.style.color = new Color(0.55f, 0.92f, 0.6f);
            _budgetLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _budgetLabel.style.marginBottom = 8;
            card.Add(_budgetLabel);

            _bodyScroll = new ScrollView(ScrollViewMode.Vertical);
            _bodyScroll.style.flexGrow = 1;
            _bodyScroll.style.flexShrink = 1;
            _bodyScroll.style.marginBottom = 8;

            var voteTitle = new Label("1. Votação por Categoria:");
            voteTitle.style.fontSize = 15;
            voteTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            voteTitle.style.color = Color.white;
            voteTitle.style.marginBottom = 6;
            _bodyScroll.Add(voteTitle);

            _stagesContainer = new VisualElement();
            _stagesContainer.style.marginBottom = 10;
            _bodyScroll.Add(_stagesContainer);

            var shopTitle = new Label("2. Loja da Pizzaria (Caixa Compartilhado):");
            shopTitle.style.fontSize = 15;
            shopTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            shopTitle.style.color = Color.white;
            shopTitle.style.marginBottom = 6;
            _bodyScroll.Add(shopTitle);

            _shopContainer = new VisualElement();
            _shopContainer.style.flexDirection = FlexDirection.Row;
            _shopContainer.style.flexWrap = Wrap.Wrap;
            _shopContainer.style.marginBottom = 10;
            _bodyScroll.Add(_shopContainer);

            _modifiersSummaryLabel = new Label("");
            _modifiersSummaryLabel.style.fontSize = 13;
            _modifiersSummaryLabel.style.color = new Color(0.75f, 0.82f, 0.9f);
            _modifiersSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            _modifiersSummaryLabel.style.marginBottom = 8;
            _bodyScroll.Add(_modifiersSummaryLabel);

            card.Add(_bodyScroll);

            _readyStatusLabel = new Label("Prontos: 0 / 1");
            _readyStatusLabel.style.fontSize = 14;
            _readyStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _readyStatusLabel.style.color = new Color(0.95f, 0.85f, 0.45f);
            _readyStatusLabel.style.marginBottom = 6;
            card.Add(_readyStatusLabel);

            _readyBtn = new Button(ToggleReady) { text = "MARCAR PRONTO" };
            _readyBtn.style.height = 44;
            _readyBtn.style.fontSize = 16;
            _readyBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _readyBtn.style.backgroundColor = new Color(0.18f, 0.56f, 0.34f);
            _readyBtn.style.color = Color.white;
            card.Add(_readyBtn);

            _overlay.Add(card);
            _root.Add(_overlay);
        }

        private void ToggleReady()
        {
            _localReady = !_localReady;
            PizzeriaCommandHandler.Instance?.SetRunReadyServerRpc(_localReady);
        }

        private void Update()
        {
            var ns = PizzeriaNetworkState.Instance;
            if (ns == null) return;

            var night = ns.NightState.Value;
            bool shouldBeOpen = night.Phase == (byte)NightPhase.PreparingNextNight &&
                                ScaryParty.UI.Piaria.Presenters.NightIntermissionPresenter.Instance == null;

            if (shouldBeOpen)
            {
                if (!_isOpen)
                {
                    EnsureUIInitialized();
                    if (_overlay == null) return;
                    _isOpen = true;
                    _localReady = false;
                    _lastVoteSignature = string.Empty;
                    _lastShopSignature = string.Empty;
                    _overlay.style.display = DisplayStyle.Flex;
                    if (!_hasLock)
                    {
                        GameplayInputGate.PushLock();
                        _hasLock = true;
                    }
                }
                GameplayInputGate.MaintainMenuCursorUnlocked();
                RefreshUI(ns);
            }
            else if (_isOpen)
            {
                Close();
            }
        }

        private void RefreshUI(PizzeriaNetworkState ns)
        {
            var run = ns.RunState.Value;
            var night = ns.NightState.Value;
            int maxNights = run.MaxRunNights > 0 ? run.MaxRunNights : 3;
            int targetNight = run.TargetNightNumber > 0
                ? run.TargetNightNumber
                : (night.TargetNightNumber > 0 ? night.TargetNightNumber : Mathf.Min(maxNights, run.CurrentRunNight + 1));

            var cfg = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Config;
            var resolved = cfg != null
                ? cfg.ResolveEffectiveNightParameters(targetNight, Mathf.Max(1, run.TotalHumans), cfg.runDifficulty, ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetEffectiveRunPreset())
                : default;

            if (_headerLabel != null)
            {
                bool isUnlimited = resolved.UnlimitedShiftTime || night.UnlimitedShiftTime ||
                    (ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.GetEffectiveRunPreset()?.unlimitedShiftTime == true);
                string durLabel = isUnlimited ? "SEM LIMITE" : $"{Mathf.CeilToInt(resolved.DurationSeconds > 0f ? resolved.DurationSeconds : 360f)}s";
                _headerLabel.text = $"PREPARAÇÃO — NOITE {targetNight} / {maxNights} (Duração: {durLabel})";
            }

            if (_budgetLabel != null)
                _budgetLabel.text = $"Caixa Compartilhado: R$ {ns.RestaurantBudget.Value}";

            RebuildVoteStagesIfChanged(ns, run);
            RebuildShopOffersIfChanged(ns);

            if (_modifiersSummaryLabel != null)
            {
                int bagStacks = Mathf.Clamp(run.ExtraBackpackCapacity, 0, 5);
                int trainStacks = Mathf.Clamp(Mathf.RoundToInt((1f - run.EmployeeErrorMultiplier) / 0.25f), 0, 5);
                _modifiersSummaryLabel.text =
                    $"Status Atual — Timer Forno: {(run.HasOvenTimer ? "ATIVO" : "Inativo")} | " +
                    $"Mochila: +{bagStacks} slots | " +
                    $"Treinamento: {trainStacks}x (Erro x{run.EmployeeErrorMultiplier:F2}) | " +
                    $"Demanda: x{run.DemandMultiplier:F2} | Vampiros: x{run.VampireSpawnMultiplier:F2}";
            }

            if (_readyStatusLabel != null)
            {
                int connectedCount = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                    ? Mathf.Max(1, NetworkManager.Singleton.ConnectedClientsIds.Count)
                    : 1;
                int totalHumans = Mathf.Max(run.TotalHumans, connectedCount);
                _readyStatusLabel.text = $"Jogadores Prontos: {run.ReadyCount} / {totalHumans} (vote nas categorias obrigatórias)";
            }

            if (_readyBtn != null)
            {
                _readyBtn.text = _localReady ? "PRONTO ✓ (clique para cancelar)" : "MARCAR PRONTO PARA NOITE";
                _readyBtn.style.backgroundColor = _localReady ? new Color(0.68f, 0.45f, 0.14f) : new Color(0.18f, 0.56f, 0.34f);
            }
        }

        private void RebuildVoteStagesIfChanged(PizzeriaNetworkState ns, NetRunStateDto run)
        {
            if (_stagesContainer == null) return;

            var sigBuilder = new System.Text.StringBuilder();
            sigBuilder.Append(run.IsVoteResolved ? '1' : '0');
            if (ns.RunVoteOptions != null)
            {
                for (int i = 0; i < ns.RunVoteOptions.Count; i++)
                {
                    var opt = ns.RunVoteOptions[i];
                    sigBuilder.Append('|').Append(opt.StageIndex).Append(':').Append(opt.OptionId).Append(':').Append(opt.VoteCount).Append(':').Append(opt.IsResolvedWinner);
                }
            }
            string sig = sigBuilder.ToString();
            if (sig == _lastVoteSignature) return;
            _lastVoteSignature = sig;

            _stagesContainer.Clear();
            if (ns.RunVoteOptions == null || ns.RunVoteOptions.Count == 0)
            {
                var emptyLbl = new Label("Nenhuma categoria de votacao ativa no momento.");
                emptyLbl.style.color = new Color(0.75f, 0.78f, 0.85f);
                _stagesContainer.Add(emptyLbl);
                return;
            }

            int currentStage = -1;
            VisualElement currentRow = null;
            for (int i = 0; i < ns.RunVoteOptions.Count; i++)
            {
                var opt = ns.RunVoteOptions[i];
                if (opt.StageIndex != currentStage)
                {
                    currentStage = opt.StageIndex;
                    string catName = RunDescriptionFormatter.FormatKindLabel((RunModifierKind)opt.Category);
                    string mandTag = opt.IsMandatory ? "Obrigatorio" : "Opcional";
                    var stageHeader = new Label($"Etapa {currentStage + 1}: {catName} ({mandTag})");
                    stageHeader.style.fontSize = 13;
                    stageHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                    stageHeader.style.color = opt.IsMandatory ? new Color(0.98f, 0.65f, 0.36f) : new Color(0.62f, 0.88f, 0.98f);
                    stageHeader.style.marginTop = 4;
                    stageHeader.style.marginBottom = 4;
                    _stagesContainer.Add(stageHeader);

                    currentRow = new VisualElement();
                    currentRow.style.flexDirection = FlexDirection.Row;
                    currentRow.style.flexWrap = Wrap.Wrap;
                    currentRow.style.marginBottom = 6;
                    _stagesContainer.Add(currentRow);
                }

                byte stageIdx = opt.StageIndex;
                var optIdFs = opt.OptionId;
                var btn = new Button(() =>
                {
                    PizzeriaCommandHandler.Instance?.CastRunStageVoteServerRpc(stageIdx, optIdFs);
                })
                {
                    text = $"{opt.Title} [{opt.DurationTag}] — {opt.VoteCount} voto(s)\n{opt.Description}"
                };
                btn.SetEnabled(!run.IsVoteResolved);
                btn.style.flexGrow = 1;
                btn.style.flexBasis = 260;
                btn.style.minHeight = 56;
                btn.style.marginRight = 6;
                btn.style.marginBottom = 6;
                btn.style.whiteSpace = WhiteSpace.Normal;
                btn.style.fontSize = 12;
                btn.style.unityTextAlign = TextAnchor.UpperLeft;
                if (opt.IsResolvedWinner)
                {
                    btn.style.backgroundColor = new Color(0.22f, 0.50f, 0.28f);
                }
                currentRow.Add(btn);
            }
        }

        private void RebuildShopOffersIfChanged(PizzeriaNetworkState ns)
        {
            if (_shopContainer == null) return;

            int budget = ns.RestaurantBudget.Value;
            var sigBuilder = new System.Text.StringBuilder();
            sigBuilder.Append(budget);
            if (ns.RunShopOffers != null)
            {
                for (int i = 0; i < ns.RunShopOffers.Count; i++)
                {
                    var o = ns.RunShopOffers[i];
                    sigBuilder.Append('|').Append(o.OfferId).Append(':').Append(o.EffectiveCost).Append(':').Append(o.CurrentLevel).Append(':').Append(o.CanPurchase).Append(':').Append(o.LockReason);
                }
            }
            string sig = sigBuilder.ToString();
            if (sig == _lastShopSignature) return;
            _lastShopSignature = sig;

            _shopContainer.Clear();
            if (ns.RunShopOffers == null || ns.RunShopOffers.Count == 0)
            {
                var emptyLbl = new Label("Aguardando ofertas do catalogo...");
                emptyLbl.style.color = new Color(0.75f, 0.78f, 0.85f);
                _shopContainer.Add(emptyLbl);
                return;
            }

            for (int i = 0; i < ns.RunShopOffers.Count; i++)
            {
                var offer = ns.RunShopOffers[i];
                var offerIdFs = offer.OfferId;
                string lvl = offer.MaxLevel > 0 ? $" [Nv. {offer.CurrentLevel}/{offer.MaxLevel}]" : string.Empty;
                string lockInfo = !offer.CanPurchase && offer.LockReason.Length > 0 ? $"\n({offer.LockReason})" : string.Empty;

                var btn = new Button(() =>
                {
                    PizzeriaCommandHandler.Instance?.PurchaseRunShopOfferServerRpc(offerIdFs);
                })
                {
                    text = $"{offer.DisplayName}{lvl} — R$ {offer.EffectiveCost}\n{offer.Description}{lockInfo}"
                };
                btn.SetEnabled(offer.CanPurchase && budget >= offer.EffectiveCost);
                btn.style.flexGrow = 1;
                btn.style.flexBasis = 260;
                btn.style.minHeight = 52;
                btn.style.marginRight = 6;
                btn.style.marginBottom = 6;
                btn.style.fontSize = 12;
                btn.style.whiteSpace = WhiteSpace.Normal;
                btn.style.unityTextAlign = TextAnchor.UpperLeft;
                _shopContainer.Add(btn);
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _localReady = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            if (_hasLock)
            {
                GameplayInputGate.PopLock();
                _hasLock = false;
            }
        }
    }
}

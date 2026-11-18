using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
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
        private Label _headerLabel;
        private Label _budgetLabel;
        private Label _modifiersSummaryLabel;
        private Button _voteABtn;
        private Button _voteBBtn;
        private Label _voteStatusLabel;
        private Button _readyBtn;
        private Label _readyStatusLabel;

        private bool _isOpen;
        private bool _hasLock;
        private bool _localReady;

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
            card.style.width = 720;
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
            card.style.paddingLeft = 24;
            card.style.paddingRight = 24;
            card.style.paddingTop = 20;
            card.style.paddingBottom = 20;

            _headerLabel = new Label("PREPARAÇÃO DA PRÓXIMA NOITE");
            _headerLabel.style.fontSize = 24;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.style.color = new Color(0.96f, 0.82f, 0.35f);
            _headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(_headerLabel);

            _budgetLabel = new Label("Caixa Compartilhado: R$ 0");
            _budgetLabel.style.fontSize = 16;
            _budgetLabel.style.color = new Color(0.55f, 0.92f, 0.6f);
            _budgetLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _budgetLabel.style.marginBottom = 10;
            card.Add(_budgetLabel);

            var voteTitle = new Label("1. Votação de Evento / Modificador da Noite:");
            voteTitle.style.fontSize = 15;
            voteTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            voteTitle.style.color = Color.white;
            voteTitle.style.marginBottom = 6;
            card.Add(voteTitle);

            var voteRow = new VisualElement();
            voteRow.style.flexDirection = FlexDirection.Row;
            voteRow.style.marginBottom = 6;

            _voteABtn = new Button(() => CastVote(0)) { text = "Opção A" };
            _voteABtn.style.flexGrow = 1;
            _voteABtn.style.marginRight = 6;
            _voteABtn.style.height = 58;
            _voteABtn.style.whiteSpace = WhiteSpace.Normal;
            voteRow.Add(_voteABtn);

            _voteBBtn = new Button(() => CastVote(1)) { text = "Opção B" };
            _voteBBtn.style.flexGrow = 1;
            _voteBBtn.style.marginLeft = 6;
            _voteBBtn.style.height = 58;
            _voteBBtn.style.whiteSpace = WhiteSpace.Normal;
            voteRow.Add(_voteBBtn);
            card.Add(voteRow);

            _voteStatusLabel = new Label("");
            _voteStatusLabel.style.fontSize = 13;
            _voteStatusLabel.style.color = new Color(0.82f, 0.86f, 0.92f);
            _voteStatusLabel.style.marginBottom = 10;
            card.Add(_voteStatusLabel);

            var shopTitle = new Label("2. Loja da Pizzaria (Caixa Compartilhado):");
            shopTitle.style.fontSize = 15;
            shopTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            shopTitle.style.color = Color.white;
            shopTitle.style.marginBottom = 6;
            card.Add(shopTitle);

            var shopGrid = new VisualElement();
            shopGrid.style.flexDirection = FlexDirection.Row;
            shopGrid.style.flexWrap = Wrap.Wrap;
            shopGrid.style.marginBottom = 10;

            AddShopButton(shopGrid, "Timer do Forno (R$ 90)", 1);
            AddShopButton(shopGrid, "Bolsa Reforçada +1 (R$ 75)", 2);
            AddShopButton(shopGrid, "Treinamento Equipe (R$ 85)", 3);
            AddShopButton(shopGrid, "Suprimentos Base +5 (R$ 45)", 4);
            AddShopButton(shopGrid, "Contratar Caixa (R$ 180)", 5);
            AddShopButton(shopGrid, "Contratar Pizzaiolo (R$ 120)", 6);
            card.Add(shopGrid);

            _modifiersSummaryLabel = new Label("");
            _modifiersSummaryLabel.style.fontSize = 13;
            _modifiersSummaryLabel.style.color = new Color(0.75f, 0.82f, 0.9f);
            _modifiersSummaryLabel.style.marginBottom = 12;
            card.Add(_modifiersSummaryLabel);

            _readyStatusLabel = new Label("Prontos: 0 / 1");
            _readyStatusLabel.style.fontSize = 15;
            _readyStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _readyStatusLabel.style.color = new Color(0.95f, 0.85f, 0.45f);
            _readyStatusLabel.style.marginBottom = 6;
            card.Add(_readyStatusLabel);

            _readyBtn = new Button(ToggleReady) { text = "MARCAR PRONTO" };
            _readyBtn.style.height = 46;
            _readyBtn.style.fontSize = 17;
            _readyBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _readyBtn.style.backgroundColor = new Color(0.18f, 0.56f, 0.34f);
            _readyBtn.style.color = Color.white;
            card.Add(_readyBtn);

            _overlay.Add(card);
            _root.Add(_overlay);
        }

        private static void AddShopButton(VisualElement parent, string label, byte shopItemId)
        {
            var btn = new Button(() => PizzeriaCommandHandler.Instance?.PurchaseRunShopItemServerRpc(shopItemId))
            {
                text = label
            };
            btn.style.width = 216;
            btn.style.height = 38;
            btn.style.marginRight = 6;
            btn.style.marginBottom = 6;
            btn.style.fontSize = 13;
            parent.Add(btn);
        }

        private void CastVote(int idx)
        {
            var ns = PizzeriaNetworkState.Instance;
            if (ns == null) return;
            var run = ns.RunState.Value;
            FixedString32Bytes optId = idx == 0 ? run.OptionAId : run.OptionBId;
            if (optId.Length == 0) return;
            PizzeriaCommandHandler.Instance?.CastRunVoteServerRpc(optId);
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
            bool shouldBeOpen = night.Phase == (byte)NightPhase.PreparingNextNight;

            if (shouldBeOpen)
            {
                if (!_isOpen)
                {
                    EnsureUIInitialized();
                    if (_overlay == null) return;
                    _isOpen = true;
                    _localReady = false;
                    _overlay.style.display = DisplayStyle.Flex;
                    if (!_hasLock)
                    {
                        GameplayInputGate.PushLock();
                        _hasLock = true;
                    }
                }
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
            int nextNight = Mathf.Min(run.MaxRunNights > 0 ? run.MaxRunNights : 3, run.CurrentRunNight + 1);
            var cfg = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Config;
            float nextDurationSec = cfg != null
                ? cfg.ResolveEffectiveNightParameters(nextNight, Mathf.Max(1, run.TotalHumans), cfg.runDifficulty, ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetEffectiveRunPreset()).DurationSeconds
                : 360f;
            if (_headerLabel != null)
                _headerLabel.text = $"PREPARAÇÃO — NOITE {nextNight} / {(run.MaxRunNights > 0 ? run.MaxRunNights : 3)} (Duração: {Mathf.CeilToInt(nextDurationSec)}s)";

            if (_budgetLabel != null)
                _budgetLabel.text = $"Caixa Compartilhado: R$ {ns.RestaurantBudget.Value}";

            if (_voteABtn != null)
            {
                _voteABtn.text = $"{run.OptionATitle}\n{run.OptionADesc} [{run.OptionAVotes} voto(s)]";
                _voteABtn.SetEnabled(!run.IsVoteResolved && run.OptionAId.Length > 0);
            }
            if (_voteBBtn != null)
            {
                _voteBBtn.text = $"{run.OptionBTitle}\n{run.OptionBDesc} [{run.OptionBVotes} voto(s)]";
                _voteBBtn.SetEnabled(!run.IsVoteResolved && run.OptionBId.Length > 0);
            }
            if (_voteStatusLabel != null)
            {
                _voteStatusLabel.text = run.IsVoteResolved
                    ? $"Vencedor definido: {run.WinningOptionTitle}"
                    : "Vote na opção desejada; empate entre opções é resolvido deterministicamente pelo servidor ao iniciar.";
            }

            if (_modifiersSummaryLabel != null)
            {
                int bagStacks = Mathf.Clamp(run.ExtraBackpackCapacity, 0, 2);
                int trainStacks = Mathf.Clamp(Mathf.RoundToInt((1f - run.EmployeeErrorMultiplier) / 0.25f), 0, 2);
                _modifiersSummaryLabel.text =
                    $"Upgrades ativos — Timer Forno: {(run.HasOvenTimer ? "1/1 MAX" : "0/1")} | " +
                    $"Bolsa Reforçada: {bagStacks}/2 | " +
                    $"Treinamento: {trainStacks}/2 (Erro x{run.EmployeeErrorMultiplier:F2}) | " +
                    $"Demanda: x{run.DemandMultiplier:F2}";
            }

            if (_readyStatusLabel != null)
            {
                int connectedCount = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                    ? Mathf.Max(1, NetworkManager.Singleton.ConnectedClientsIds.Count)
                    : 1;
                int totalHumans = Mathf.Max(run.TotalHumans, connectedCount);
                _readyStatusLabel.text = $"Jogadores Prontos: {run.ReadyCount} / {totalHumans} (resiliente a desconexão)";
            }

            if (_readyBtn != null)
            {
                _readyBtn.text = _localReady ? "PRONTO ✓ (clique para cancelar)" : "MARCAR PRONTO PARA NOITE";
                _readyBtn.style.backgroundColor = _localReady ? new Color(0.68f, 0.45f, 0.14f) : new Color(0.18f, 0.56f, 0.34f);
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

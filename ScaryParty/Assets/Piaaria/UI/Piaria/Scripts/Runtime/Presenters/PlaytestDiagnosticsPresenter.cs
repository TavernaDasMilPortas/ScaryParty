using System;
using System.Collections.Generic;
using System.Text;
using ScaryParty.Combat.Monsters;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Painel recolhível de diagnóstico e comandos de playtest (P6):
    /// - Toggle em [F8] ou botão discreto quando AllowDebugPanel == true;
    /// - Completamente oculto no preset Entrega_3Noites (AllowDebugPanel == false);
    /// - Todas as mutações são exclusivas do Host via ServerRpc nos serviços reais.
    /// </summary>
    public class PlaytestDiagnosticsPresenter : MonoBehaviour, IModalView
    {
        public static PlaytestDiagnosticsPresenter Instance { get; private set; }

        public bool CanCloseWithEscape => _isExpanded;
        public VisualElement RootElement => _panelCard;
        public bool IsExpanded => _isExpanded;

        public static PlaytestDiagnosticsPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<PlaytestDiagnosticsPresenter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var hud = FindFirstObjectByType<HudPresenter>();
            if (hud != null)
                return hud.gameObject.AddComponent<PlaytestDiagnosticsPresenter>();

            var go = new GameObject("PlaytestDiagnosticsPresenter");
            return go.AddComponent<PlaytestDiagnosticsPresenter>();
        }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _container;
        private Button _togglePillBtn;
        private VisualElement _panelCard;
        private Label _taintedBannerLabel;
        private Label _buildSeedLabel;
        private Label _nightGoalLabel;
        private Label _countsLabel;
        private Label _speciesOverlayLabel;
        private Button _overlayToggleBtn;
        private readonly List<(Button button, bool hostOnly)> _actionButtons = new List<(Button, bool)>();

        private bool _isExpanded;
        private bool _pushedToStack;
        private bool _pushedGateFallback;
        private float _refreshTimer;
        private string _cachedBuildSummary;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_isExpanded)
            {
                _isExpanded = false;
                ReleaseModalLock();
            }
            MonsterPopulationDirector.ShowTechnicalSpeciesOverlay = false;
            if (Instance == this) Instance = null;
        }

        public void Open() => SetExpanded(true);
        public void Close() => SetExpanded(false);

        public void Toggle() => SetExpanded(!_isExpanded);

        private void SetExpanded(bool expanded)
        {
            if (expanded && !IsDebugPanelAllowedNow())
                expanded = false;

            EnsureUIInitialized();
            if (_panelCard == null) return;

            if (_isExpanded == expanded)
            {
                _panelCard.style.display = _isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }

            _isExpanded = expanded;
            _panelCard.style.display = _isExpanded ? DisplayStyle.Flex : DisplayStyle.None;

            if (_isExpanded)
            {
                AcquireModalLock();
                RefreshTelemetry();
            }
            else
            {
                ReleaseModalLock();
            }
        }

        private static bool IsDebugPanelAllowedNow()
        {
            var net = PizzeriaNetworkState.Instance;
            if (net != null && net.IsSpawned)
                return net.RunState.Value.AllowDebugPanel;

            var root = PizzeriaRoot.Instance;
            if (root != null && root.DomainState != null)
                return root.DomainState.AllowDebugPanel;

            var preset = root != null ? root.GetEffectiveRunPreset() : null;
            return preset == null || preset.allowDebugPanel;
        }

        private void Update()
        {
            EnsureUIInitialized();
            if (_container == null) return;

            bool allowed = IsDebugPanelAllowedNow();
            if (!allowed)
            {
                if (_isExpanded) SetExpanded(false);
                if (MonsterPopulationDirector.ShowTechnicalSpeciesOverlay)
                    MonsterPopulationDirector.ShowTechnicalSpeciesOverlay = false;
                if (_container.style.display != DisplayStyle.None)
                    _container.style.display = DisplayStyle.None;
                return;
            }

            if (_container.style.display != DisplayStyle.Flex)
                _container.style.display = DisplayStyle.Flex;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                if (_isExpanded)
                {
                    Close();
                    return;
                }
                if (PauseEscapeMenuPresenter.Instance == null || !PauseEscapeMenuPresenter.Instance.IsOpen)
                {
                    Toggle();
                    return;
                }
            }
#endif

            _refreshTimer += Time.unscaledDeltaTime;
            if (_isExpanded && _refreshTimer >= 0.35f)
            {
                _refreshTimer = 0f;
                RefreshTelemetry();
            }
        }

        private void EnsureUIInitialized()
        {
            if (_container != null) return;

            var hud = FindFirstObjectByType<HudPresenter>();
            _doc = hud != null ? hud.uiDocument : GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            if (_doc.panelSettings == null)
            {
                var ps = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (ps != null && ps.Length > 0) _doc.panelSettings = ps[0];
            }

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            _container = new VisualElement { name = "PlaytestDiagnosticsRoot" };
            _container.style.position = Position.Absolute;
            _container.style.top = 12;
            _container.style.right = 12;
            _container.style.alignItems = Align.FlexEnd;
            _container.pickingMode = PickingMode.Ignore;
            _container.style.display = DisplayStyle.None;

            _togglePillBtn = new Button(Toggle) { text = "[F8] Diagnóstico" };
            _togglePillBtn.style.height = 28;
            _togglePillBtn.style.paddingLeft = 10;
            _togglePillBtn.style.paddingRight = 10;
            _togglePillBtn.style.backgroundColor = new Color(0.14f, 0.16f, 0.22f, 0.90f);
            _togglePillBtn.style.color = new Color(0.95f, 0.84f, 0.45f);
            _togglePillBtn.style.fontSize = 12;
            _togglePillBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetBorderRadius(_togglePillBtn, 6);
            SetBorder(_togglePillBtn, 1, new Color(0.95f, 0.72f, 0.25f, 0.7f));
            _container.Add(_togglePillBtn);

            _panelCard = new VisualElement { name = "PlaytestDiagnosticsCard" };
            _panelCard.style.width = 430;
            _panelCard.style.maxHeight = 640;
            _panelCard.style.marginTop = 6;
            _panelCard.style.paddingLeft = 12;
            _panelCard.style.paddingRight = 12;
            _panelCard.style.paddingTop = 10;
            _panelCard.style.paddingBottom = 10;
            _panelCard.style.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            SetBorderRadius(_panelCard, 10);
            SetBorder(_panelCard, 2, new Color(0.95f, 0.72f, 0.25f));
            _panelCard.style.display = DisplayStyle.None;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 605;
            _panelCard.Add(scroll);

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 6;

            var title = new Label("PAINEL TÉCNICO DE PLAYTEST [F8]");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.98f, 0.82f, 0.36f);
            titleRow.Add(title);

            var closeBtn = new Button(Close) { text = "Fechar" };
            closeBtn.style.height = 24;
            closeBtn.style.fontSize = 11;
            titleRow.Add(closeBtn);
            scroll.Add(titleRow);

            _taintedBannerLabel = new Label("RUN DE TESTE (DEBUG USADO — Vitória acelerada não conta como playtest completo)");
            _taintedBannerLabel.style.whiteSpace = WhiteSpace.Normal;
            _taintedBannerLabel.style.backgroundColor = new Color(0.55f, 0.18f, 0.12f, 0.92f);
            _taintedBannerLabel.style.color = Color.white;
            _taintedBannerLabel.style.paddingLeft = 6;
            _taintedBannerLabel.style.paddingRight = 6;
            _taintedBannerLabel.style.paddingTop = 4;
            _taintedBannerLabel.style.paddingBottom = 4;
            _taintedBannerLabel.style.marginBottom = 6;
            _taintedBannerLabel.style.fontSize = 11;
            _taintedBannerLabel.style.display = DisplayStyle.None;
            scroll.Add(_taintedBannerLabel);

            _buildSeedLabel = CreateInfoLabel();
            _nightGoalLabel = CreateInfoLabel();
            _countsLabel = CreateInfoLabel();
            scroll.Add(_buildSeedLabel);
            scroll.Add(_nightGoalLabel);
            scroll.Add(_countsLabel);

            var overlayRow = new VisualElement();
            overlayRow.style.flexDirection = FlexDirection.Row;
            overlayRow.style.marginTop = 4;
            overlayRow.style.marginBottom = 4;
            _overlayToggleBtn = CreateActionButton("Overlay Técnico de Espécies: OFF", false, () =>
            {
                MonsterPopulationDirector.ShowTechnicalSpeciesOverlay = !MonsterPopulationDirector.ShowTechnicalSpeciesOverlay;
                RefreshTelemetry();
            });
            overlayRow.Add(_overlayToggleBtn);
            scroll.Add(overlayRow);

            _speciesOverlayLabel = CreateInfoLabel();
            _speciesOverlayLabel.style.color = new Color(0.75f, 0.92f, 1f);
            _speciesOverlayLabel.style.display = DisplayStyle.None;
            scroll.Add(_speciesOverlayLabel);

            scroll.Add(CreateSectionHeader("Comandos Exclusivos do Host (Serviços Reais)"));

            var grid1 = CreateButtonRow();
            grid1.Add(CreateActionButton("Pedido 2 Receitas", true, () => PizzeriaCommandHandler.Instance?.DebugCreateMultiRecipeOrderServerRpc()));
            grid1.Add(CreateActionButton("Pedido Qtd 3", true, () => PizzeriaCommandHandler.Instance?.DebugCreateQuantityThreeOrderServerRpc()));
            scroll.Add(grid1);

            var grid2 = CreateButtonRow();
            grid2.Add(CreateActionButton("Spawn Cliente", true, () => PizzeriaCommandHandler.Instance?.DebugSpawnEntityServerRpc((byte)NpcSpecies.Human, true)));
            grid2.Add(CreateActionButton("Spawn Vampiro", true, () => PizzeriaCommandHandler.Instance?.DebugSpawnEntityServerRpc((byte)NpcSpecies.Vampire, false)));
            grid2.Add(CreateActionButton("Spawn Lobisomem", true, () => PizzeriaCommandHandler.Instance?.DebugSpawnEntityServerRpc((byte)NpcSpecies.Werewolf, false)));
            scroll.Add(grid2);

            var grid3 = CreateButtonRow();
            grid3.Add(CreateActionButton("Spawn Fantasma", true, () => PizzeriaCommandHandler.Instance?.DebugSpawnEntityServerRpc((byte)NpcSpecies.Ghost, false)));
            grid3.Add(CreateActionButton("Spawn Zumbi", true, () => PizzeriaCommandHandler.Instance?.DebugSpawnEntityServerRpc((byte)NpcSpecies.Zombie, false)));
            scroll.Add(grid3);

            var grid4 = CreateButtonRow();
            grid4.Add(CreateActionButton("Contratar Caixa", true, () => PizzeriaCommandHandler.Instance?.DebugHireEmployeeServerRpc((byte)EmployeeRole.Cashier)));
            grid4.Add(CreateActionButton("Contratar Pizzaiolo", true, () => PizzeriaCommandHandler.Instance?.DebugHireEmployeeServerRpc((byte)EmployeeRole.Pizzaiolo)));
            scroll.Add(grid4);

            var grid5 = CreateButtonRow();
            grid5.Add(CreateActionButton("+R$200 & Estoque+5", true, () => PizzeriaCommandHandler.Instance?.DebugAddResourcesServerRpc(200, 5)));
            grid5.Add(CreateActionButton("Dano Player (-25 HP)", true, () => PizzeriaCommandHandler.Instance?.DebugApplyControlledDamageServerRpc(25)));
            scroll.Add(grid5);

            var grid6 = CreateButtonRow();
            grid6.Add(CreateActionButton("Acelerar Relógio (-30s)", true, () => PizzeriaCommandHandler.Instance?.DebugAdvanceClockServerRpc(30f, false)));
            grid6.Add(CreateActionButton("Encerrar Fase Agora", true, () => PizzeriaCommandHandler.Instance?.DebugAdvanceClockServerRpc(0f, true)));
            scroll.Add(grid6);

            var grid7 = CreateButtonRow();
            grid7.Add(CreateActionButton("Nova RUN (Preset Atual)", true, () => PizzeriaCommandHandler.Instance?.DebugStartNewRunServerRpc(new FixedString32Bytes(string.Empty))));
            grid7.Add(CreateActionButton("Nova RUN (Noite 0)", true, () => PizzeriaCommandHandler.Instance?.DebugStartNewRunServerRpc(new FixedString32Bytes("Tutorial_Noite0"))));
            scroll.Add(grid7);

            _container.Add(_panelCard);
            _root.Add(_container);
        }

        private void RefreshTelemetry()
        {
            if (_buildSeedLabel == null) return;

            var net = PizzeriaNetworkState.Instance;
            var root = PizzeriaRoot.Instance;
            var dom = root != null ? root.DomainState : null;
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            var cityGen = FindFirstObjectByType<CityGenerator>();
            int seed = cityGen != null ? cityGen.CurrentSeed : 0;

            string presetId = net != null && net.IsSpawned
                ? net.RunState.Value.ActivePresetId.ToString()
                : (dom != null ? dom.ActivePresetId : "Default");
            bool tainted = (net != null && net.IsSpawned && net.RunState.Value.IsDebugTaintedRun) ||
                           (dom != null && dom.IsDebugTaintedRun);

            _taintedBannerLabel.style.display = tainted ? DisplayStyle.Flex : DisplayStyle.None;

            if (string.IsNullOrEmpty(_cachedBuildSummary))
                _cachedBuildSummary = ReadBuildVersionSummary();

            _buildSeedLabel.text = $"{_cachedBuildSummary}\nPreset: {presetId} | Seed Cidade: {seed} | Papel: {(isHost ? "HOST (Mutações liberadas)" : "CLIENTE (Leitura apenas)")}";

            if (net != null && net.IsSpawned)
            {
                var n = net.NightState.Value;
                var r = net.RunState.Value;
                _nightGoalLabel.text =
                    $"Noite {n.NightNumber}/{r.MaxRunNights} | Fase: {(NightPhase)n.Phase} | Timer: {Mathf.CeilToInt(n.RemainingSeconds)}s (Prep {Mathf.CeilToInt(n.PrepRemainingSeconds)}s)\n" +
                    $"Participantes Congelados: {n.FrozenHumanCount} | Base: R$ {n.BaseGoal} x Escala({n.HumanScaleFactor:0.##}) x Mod({n.ModifierMultiplier:0.##}) = Meta Efetiva: R$ {n.RevenueGoal} [{(GoalResolutionSource)n.GoalSource}]\n" +
                    $"Faturado: R$ {n.EarnedRevenue} | Caixa: R$ {net.RestaurantBudget.Value} | Reputação: {n.Reputation}% | Gastos: Sup {n.NightSupplySpent}/Loja {n.NightShopSpent}/Eqp {n.NightWagesSpent}";
            }
            else
            {
                _nightGoalLabel.text = "Rede inativa / aguardando spawn de PizzeriaNetworkState.";
            }

            var monDir = MonsterPopulationDirector.Instance;
            int srvMonsters = monDir != null ? monDir.ActiveMonsters.Count : 0;
            int netMonsters = net != null && net.IsSpawned && net.Monsters != null ? net.Monsters.Count : 0;
            int proxyMonsters = monDir != null ? monDir.ClientProxyCount : 0;

            int srvEmps = EmployeePopulationDirector.ServerSpawnedCount;
            int netEmps = net != null && net.IsSpawned && net.Employees != null ? net.Employees.Count : 0;
            int proxyEmps = EmployeePopulationDirector.ClientProxyCount;

            int customers = net != null && net.IsSpawned && net.Customers != null ? net.Customers.Count : 0;
            int orders = net != null && net.IsSpawned && net.Orders != null ? net.Orders.Count : 0;
            int items = net != null && net.IsSpawned && net.Items != null ? net.Items.Count : 0;

            string domainDetails = dom != null
                ? $"Operações: {dom.ActiveOperations.Count} | ReservasSlot: {dom.StationSlotReservations.Count} | Unidades: {dom.OrderUnits.Count} | VínculosPizza: {dom.ItemToOrderUnit.Count}"
                : "Operações/Reservas/Vínculos: autoridade somente no Host";

            _countsLabel.text =
                $"Entidades & Proxies:\n" +
                $"• Monstros: Srv={srvMonsters} | Net={netMonsters} | Proxies={proxyMonsters}\n" +
                $"• Funcionários: Srv={srvEmps} | Net={netEmps} | Proxies={proxyEmps}\n" +
                $"• Clientes: {customers} | Pedidos: {orders} | Itens: {items}\n" +
                $"• {domainDetails}";

            bool overlayOn = MonsterPopulationDirector.ShowTechnicalSpeciesOverlay;
            _overlayToggleBtn.text = overlayOn ? "Overlay Técnico de Espécies: ON" : "Overlay Técnico de Espécies: OFF";

            if (overlayOn)
            {
                var specSb = new StringBuilder();
                specSb.AppendLine("Overlay Técnico (somente Debug):");
                if (net != null && net.IsSpawned && net.Monsters != null)
                {
                    for (int i = 0; i < net.Monsters.Count; i++)
                    {
                        var m = net.Monsters[i];
                        specSb.AppendLine($"• Monstro #{m.MonsterId}: {(NpcSpecies)m.Species} | {(NpcBehaviorState)m.BehaviorState} | HP {m.CurrentHp}/{m.MaxHp}");
                    }
                }
                if (net != null && net.IsSpawned && net.Customers != null)
                {
                    for (int i = 0; i < net.Customers.Count; i++)
                    {
                        var c = net.Customers[i];
                        specSb.AppendLine($"• Cliente #{c.CustomerId}: {(CustomerPhase)c.Phase} | Monstro={c.IsMonsterCustomer}");
                    }
                }
                if (specSb.Length < 40) specSb.Append("(nenhum monstro ou cliente ativo no momento)");
                _speciesOverlayLabel.text = specSb.ToString().TrimEnd();
                _speciesOverlayLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _speciesOverlayLabel.style.display = DisplayStyle.None;
            }

            foreach (var entry in _actionButtons)
            {
                if (entry.hostOnly)
                {
                    entry.button.SetEnabled(isHost);
                    entry.button.style.opacity = isHost ? 1f : 0.45f;
                }
            }
        }

        private static string ReadBuildVersionSummary()
        {
            var manifest = PlaytestReferenceManifest.LoadDefault();
            string branch = manifest != null ? manifest.branchName : "?";
            string sha = manifest != null ? manifest.baseCommitSha : "?";
            var txt = Resources.Load<TextAsset>("Playtest/BuildVersionInfo");
            if (txt != null && !string.IsNullOrEmpty(txt.text))
            {
                foreach (var rawLine in txt.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (rawLine.StartsWith("branch=", StringComparison.OrdinalIgnoreCase))
                        branch = rawLine.Substring("branch=".Length).Trim();
                    else if (rawLine.StartsWith("sha=", StringComparison.OrdinalIgnoreCase))
                        sha = rawLine.Substring("sha=".Length).Trim();
                }
            }
            string shortSha = !string.IsNullOrEmpty(sha) && sha.Length > 8 ? sha.Substring(0, 8) : sha;
            return $"Build: {branch} @ {shortSha} | Unity {Application.unityVersion}";
        }

        private void AcquireModalLock()
        {
            if (UIModalStack.Instance != null)
            {
                if (!_pushedToStack)
                {
                    UIModalStack.Instance.PushModal(this);
                    _pushedToStack = true;
                }
            }
            else if (!_pushedGateFallback)
            {
                GameplayInputGate.PushModal();
                _pushedGateFallback = true;
            }
        }

        private void ReleaseModalLock()
        {
            if (_pushedToStack)
            {
                _pushedToStack = false;
                UIModalStack.Instance?.PopModal(this);
            }
            if (_pushedGateFallback)
            {
                _pushedGateFallback = false;
                GameplayInputGate.PopModal();
            }
        }

        private static Label CreateInfoLabel()
        {
            var lbl = new Label();
            lbl.style.fontSize = 11;
            lbl.style.whiteSpace = WhiteSpace.Normal;
            lbl.style.color = new Color(0.90f, 0.92f, 0.96f);
            lbl.style.marginBottom = 6;
            return lbl;
        }

        private static Label CreateSectionHeader(string text)
        {
            var lbl = new Label(text);
            lbl.style.fontSize = 12;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new Color(0.98f, 0.80f, 0.40f);
            lbl.style.marginTop = 6;
            lbl.style.marginBottom = 4;
            return lbl;
        }

        private static VisualElement CreateButtonRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 4;
            return row;
        }

        private Button CreateActionButton(string label, bool hostOnly, Action onClick)
        {
            var btn = new Button(() => onClick?.Invoke()) { text = label };
            btn.style.height = 24;
            btn.style.fontSize = 11;
            btn.style.marginRight = 4;
            btn.style.marginBottom = 3;
            btn.style.paddingLeft = 7;
            btn.style.paddingRight = 7;
            btn.style.backgroundColor = new Color(0.18f, 0.22f, 0.29f, 0.98f);
            btn.style.color = Color.white;
            SetBorderRadius(btn, 4);
            SetBorder(btn, 1, new Color(0.35f, 0.42f, 0.54f));
            _actionButtons.Add((btn, hostOnly));
            return btn;
        }

        private static void SetBorderRadius(VisualElement el, float r)
        {
            el.style.borderTopLeftRadius = r;
            el.style.borderTopRightRadius = r;
            el.style.borderBottomLeftRadius = r;
            el.style.borderBottomRightRadius = r;
        }

        private static void SetBorder(VisualElement el, float width, Color color)
        {
            el.style.borderTopWidth = width;
            el.style.borderBottomWidth = width;
            el.style.borderLeftWidth = width;
            el.style.borderRightWidth = width;
            el.style.borderTopColor = color;
            el.style.borderBottomColor = color;
            el.style.borderLeftColor = color;
            el.style.borderRightColor = color;
        }
    }
}

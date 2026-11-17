using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Checklist tutorial contextual não-intrusivo da primeira pizza e dicas de primeiro contato (O3 / N4).
    /// </summary>
    public class ContextualTutorialPresenter : MonoBehaviour
    {
        public static ContextualTutorialPresenter Instance { get; private set; }

        public static ContextualTutorialPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<ContextualTutorialPresenter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("ContextualTutorialPresenter");
            return go.AddComponent<ContextualTutorialPresenter>();
        }

        private static readonly HashSet<string> ShownTips = new HashSet<string>();

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _panel;
        private VisualElement _body;
        private Label[] _stepLabels = new Label[6];
        private Button _toggleMinimizeBtn;
        private Button _skipBtn;
        private Button _skipNight0Btn;

        public static bool IsTutorialNight0Enabled { get; set; } = false;

        private bool _dismissed = false;
        private bool _minimized = false;
        private float _checkTimer = 0f;
        private readonly bool[] _completedSteps = new bool[6];

        private static readonly string[] StepDescriptions =
        {
            "1. Atender cliente no balcão ou telefone [E]",
            "2. Pegar massa no armazém e abrir na bancada [E]",
            "3. Aplicar molho de tomate na massa aberta",
            "4. Ralar mussarela e aplicar na pizza",
            "5. Assar no forno até o ponto ideal",
            "6. Servir na mesa ou embalar e entregar"
        };

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
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            EnsureUIInitialized();
        }

        private void EnsureUIInitialized()
        {
            if (_panel != null) return;

            var hud = FindFirstObjectByType<ScaryParty.UI.Piaria.Presenters.HudPresenter>();
            _doc = hud != null ? hud.uiDocument : GetComponent<UIDocument>();
            if (_doc == null)
                _doc = gameObject.AddComponent<UIDocument>();

            if (_doc.panelSettings == null)
            {
                var panelSettingsList = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (panelSettingsList != null && panelSettingsList.Length > 0)
                    _doc.panelSettings = panelSettingsList[0];
            }

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            _panel = new VisualElement();
            _panel.name = "ContextualTutorialPanel";
            _panel.pickingMode = PickingMode.Position;
            _panel.style.position = Position.Absolute;
            _panel.style.top = 74;
            _panel.style.right = 18;
            _panel.style.width = 330;
            _panel.style.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.88f);
            _panel.style.borderTopWidth = 2;
            _panel.style.borderBottomWidth = 2;
            _panel.style.borderLeftWidth = 2;
            _panel.style.borderRightWidth = 2;
            _panel.style.borderTopColor = new Color(0.95f, 0.72f, 0.28f);
            _panel.style.borderBottomColor = new Color(0.95f, 0.72f, 0.28f);
            _panel.style.borderLeftColor = new Color(0.95f, 0.72f, 0.28f);
            _panel.style.borderRightColor = new Color(0.95f, 0.72f, 0.28f);
            _panel.style.borderTopLeftRadius = 10;
            _panel.style.borderTopRightRadius = 10;
            _panel.style.borderBottomLeftRadius = 10;
            _panel.style.borderBottomRightRadius = 10;
            _panel.style.paddingLeft = 12;
            _panel.style.paddingRight = 12;
            _panel.style.paddingTop = 10;
            _panel.style.paddingBottom = 10;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;

            var title = new Label("GUIA OPERACIONAL [H]");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.98f, 0.82f, 0.42f);
            header.Add(title);

            var headerBtns = new VisualElement();
            headerBtns.style.flexDirection = FlexDirection.Row;

            _skipNight0Btn = new Button(SkipTutorialToNight1);
            _skipNight0Btn.text = "Ir p/ Noite 1";
            _skipNight0Btn.style.height = 24;
            _skipNight0Btn.style.marginRight = 4;
            _skipNight0Btn.style.fontSize = 11;
            _skipNight0Btn.style.backgroundColor = new Color(0.18f, 0.42f, 0.28f);
            _skipNight0Btn.style.color = Color.white;
            _skipNight0Btn.style.display = DisplayStyle.None;
            headerBtns.Add(_skipNight0Btn);

            _toggleMinimizeBtn = new Button(ToggleMinimize);
            _toggleMinimizeBtn.text = "—";
            _toggleMinimizeBtn.style.width = 26;
            _toggleMinimizeBtn.style.height = 24;
            _toggleMinimizeBtn.style.marginRight = 4;
            headerBtns.Add(_toggleMinimizeBtn);

            _skipBtn = new Button(DismissTutorial);
            _skipBtn.text = "✕";
            _skipBtn.style.width = 26;
            _skipBtn.style.height = 24;
            _skipBtn.style.backgroundColor = new Color(0.55f, 0.22f, 0.22f);
            _skipBtn.style.color = Color.white;
            headerBtns.Add(_skipBtn);

            header.Add(headerBtns);
            _panel.Add(header);

            _body = new VisualElement();
            for (int i = 0; i < StepDescriptions.Length; i++)
            {
                var lbl = new Label("☐ " + StepDescriptions[i]);
                lbl.style.fontSize = 13;
                lbl.style.color = new Color(0.9f, 0.9f, 0.94f);
                lbl.style.whiteSpace = WhiteSpace.Normal;
                lbl.style.marginBottom = 4;
                _stepLabels[i] = lbl;
                _body.Add(lbl);
            }

            _panel.Add(_body);
            _root.Add(_panel);
        }

        private void ToggleMinimize()
        {
            _minimized = !_minimized;
            if (_body != null)
                _body.style.display = _minimized ? DisplayStyle.None : DisplayStyle.Flex;
            if (_toggleMinimizeBtn != null)
                _toggleMinimizeBtn.text = _minimized ? "+" : "—";
        }

        public void DismissTutorial()
        {
            _dismissed = true;
            if (_panel != null)
                _panel.style.display = DisplayStyle.None;
        }

        public void ShowTutorial()
        {
            EnsureUIInitialized();
            _dismissed = false;
            _minimized = false;
            if (_body != null) _body.style.display = DisplayStyle.Flex;
            if (_toggleMinimizeBtn != null) _toggleMinimizeBtn.text = "—";
            if (_panel != null) _panel.style.display = DisplayStyle.Flex;
            RefreshChecklistVisuals();
        }

        public void SkipTutorialToNight1()
        {
            IsTutorialNight0Enabled = false;
            DismissTutorial();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                PizzeriaCommandHandler.Instance?.SkipTutorialToNight1ServerRpc();
            }
        }

        public static void NotifyContextualTip(string tipKey, string title, string message)
        {
            if (string.IsNullOrEmpty(tipKey) || !ShownTips.Add(tipKey)) return;
            NotificationManager.Show($"{title}: {message}", NotificationType.Info);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && !GameplayInputGate.IsLocked)
            {
                if (_dismissed) ShowTutorial();
                else ToggleMinimize();
            }
#endif
            if (_dismissed || _panel == null) return;

            var netCheck = PizzeriaNetworkState.Instance;
            if (_skipNight0Btn != null && netCheck != null)
            {
                bool showSkipN0 = netCheck.NightState.Value.NightNumber == 0 || IsTutorialNight0Enabled;
                _skipNight0Btn.style.display = showSkipN0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            _checkTimer += Time.deltaTime;
            if (_checkTimer < 0.5f) return;
            _checkTimer = 0f;

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

            if (netState.Orders.Count > 0)
                _completedSteps[0] = true;

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var it = netState.Items[i];
                if (it.PrepStage == (byte)ProcessingStage.Prepared &&
                    (it.Category == (byte)ItemCategory.Dough || it.Category == (byte)ItemCategory.PizzaBase || it.Category == (byte)ItemCategory.Pizza || it.Category == (byte)ItemCategory.Box))
                {
                    _completedSteps[0] = true;
                    _completedSteps[1] = true;
                }

                if ((it.IngredientMask & 1) != 0)
                {
                    _completedSteps[1] = true;
                    _completedSteps[2] = true;
                }

                if ((it.IngredientMask & 2) != 0)
                {
                    _completedSteps[2] = true;
                    _completedSteps[3] = true;
                }

                if (it.CookingStage == (byte)CookingStage.Baked)
                {
                    _completedSteps[3] = true;
                    _completedSteps[4] = true;
                }

                if (it.PackagingState == (byte)PackagingState.Boxed)
                {
                    _completedSteps[4] = true;
                }
            }

            if (netState.NightState.Value.OrdersCompleted > 0)
            {
                for (int s = 0; s < _completedSteps.Length; s++)
                    _completedSteps[s] = true;
            }

            RefreshChecklistVisuals();
        }

        private void RefreshChecklistVisuals()
        {
            for (int i = 0; i < _stepLabels.Length; i++)
            {
                var lbl = _stepLabels[i];
                if (lbl == null) continue;
                bool done = _completedSteps[i];
                lbl.text = (done ? "☑ " : "☐ ") + StepDescriptions[i];
                lbl.style.color = done
                    ? new Color(0.45f, 0.88f, 0.52f)
                    : new Color(0.92f, 0.92f, 0.95f);
            }
        }
    }
}

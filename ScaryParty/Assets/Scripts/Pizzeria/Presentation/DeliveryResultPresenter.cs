using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Modal UI Toolkit exibindo o resumo detalhado da entrega concluída:
    /// pizzas corretas, queimadas, incorretas, valor ganho pelo jogador e restaurante.
    /// Pagamento 0 não é apresentado como sucesso.
    /// </summary>
    public class DeliveryResultPresenter : MonoBehaviour
    {
        public static DeliveryResultPresenter Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _card;

        private Label _titleLabel;
        private Label _addressLabel;
        private Label _summaryDelivered;
        private Label _summaryCorrect;
        private Label _summaryBurned;
        private Label _summaryWrong;
        private Label _personalMoneyLabel;
        private Label _restaurantMoneyLabel;
        private Label _orderStatusLabel;
        private Button _closeBtn;

        private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            EnsureUIInitialized();
        }

        private void EnsureUIInitialized()
        {
            if (_overlay != null) return;

            if (UIManager.Instance != null && UIManager.Instance.uiDocument != null && UIManager.Instance.uiDocument.rootVisualElement != null)
            {
                _root = UIManager.Instance.uiDocument.rootVisualElement;
            }
            else
            {
                _doc = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
                if (_doc.panelSettings == null)
                {
                    var panelSettingsList = Resources.FindObjectsOfTypeAll<PanelSettings>();
                    if (panelSettingsList != null && panelSettingsList.Length > 0)
                        _doc.panelSettings = panelSettingsList[0];
                }
                _root = _doc.rootVisualElement;
            }

            if (_root == null) return;

            BuildUIHierarchy();
        }

        private void BuildUIHierarchy()
        {
            _overlay = new VisualElement();
            _overlay.name = "DeliveryResultOverlay";
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.display = DisplayStyle.None;

            _card = new VisualElement();
            _card.name = "DeliveryResultCard";
            _card.style.width = 460;
            _card.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.98f);
            _card.style.borderTopLeftRadius = 12;
            _card.style.borderTopRightRadius = 12;
            _card.style.borderBottomLeftRadius = 12;
            _card.style.borderBottomRightRadius = 12;
            _card.style.borderTopWidth = 2;
            _card.style.borderBottomWidth = 2;
            _card.style.borderLeftWidth = 2;
            _card.style.borderRightWidth = 2;
            _card.style.borderTopColor = new Color(0.2f, 0.8f, 0.4f);
            _card.style.borderBottomColor = new Color(0.2f, 0.8f, 0.4f);
            _card.style.borderLeftColor = new Color(0.2f, 0.8f, 0.4f);
            _card.style.borderRightColor = new Color(0.2f, 0.8f, 0.4f);
            _card.style.paddingTop = 20;
            _card.style.paddingBottom = 20;
            _card.style.paddingLeft = 24;
            _card.style.paddingRight = 24;

            _titleLabel = new Label("ENTREGA CONCLUÍDA!");
            _titleLabel.style.fontSize = 20;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleLabel.style.marginBottom = 4;
            _card.Add(_titleLabel);

            _addressLabel = new Label("Endereço: ...");
            _addressLabel.style.fontSize = 14;
            _addressLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            _addressLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _addressLabel.style.marginBottom = 16;
            _card.Add(_addressLabel);

            var statsBox = new VisualElement();
            statsBox.style.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            statsBox.style.paddingTop = 10;
            statsBox.style.paddingBottom = 10;
            statsBox.style.paddingLeft = 14;
            statsBox.style.paddingRight = 14;
            statsBox.style.borderTopLeftRadius = 8;
            statsBox.style.borderTopRightRadius = 8;
            statsBox.style.borderBottomLeftRadius = 8;
            statsBox.style.borderBottomRightRadius = 8;
            statsBox.style.marginBottom = 14;

            _summaryDelivered = new Label("• Total de caixas entregues: 0");
            _summaryDelivered.style.fontSize = 13;
            _summaryDelivered.style.color = Color.white;
            _summaryDelivered.style.marginBottom = 4;
            statsBox.Add(_summaryDelivered);

            _summaryCorrect = new Label("• Pizzas corretas e no ponto: 0");
            _summaryCorrect.style.fontSize = 13;
            _summaryCorrect.style.color = new Color(0.3f, 0.9f, 0.4f);
            _summaryCorrect.style.marginBottom = 4;
            statsBox.Add(_summaryCorrect);

            _summaryBurned = new Label("• Pizzas queimadas/cruas: 0");
            _summaryBurned.style.fontSize = 13;
            _summaryBurned.style.color = new Color(0.9f, 0.5f, 0.2f);
            _summaryBurned.style.marginBottom = 4;
            statsBox.Add(_summaryBurned);

            _summaryWrong = new Label("• Pizzas incorretas: 0");
            _summaryWrong.style.fontSize = 13;
            _summaryWrong.style.color = new Color(0.9f, 0.3f, 0.3f);
            statsBox.Add(_summaryWrong);

            _card.Add(statsBox);

            var moneyBox = new VisualElement();
            moneyBox.style.flexDirection = FlexDirection.Row;
            moneyBox.style.justifyContent = Justify.SpaceBetween;
            moneyBox.style.marginBottom = 12;

            _personalMoneyLabel = new Label("💰 Ganho Pessoal: +R$ 0");
            _personalMoneyLabel.style.fontSize = 15;
            _personalMoneyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _personalMoneyLabel.style.color = new Color(0.3f, 0.95f, 0.3f);
            moneyBox.Add(_personalMoneyLabel);

            _restaurantMoneyLabel = new Label("🏪 Pizzaria: +R$ 0");
            _restaurantMoneyLabel.style.fontSize = 14;
            _restaurantMoneyLabel.style.color = new Color(0.9f, 0.8f, 0.3f);
            moneyBox.Add(_restaurantMoneyLabel);

            _card.Add(moneyBox);

            _orderStatusLabel = new Label("Comanda Finalizada ✅");
            _orderStatusLabel.style.fontSize = 13;
            _orderStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _orderStatusLabel.style.marginBottom = 16;
            _card.Add(_orderStatusLabel);

            _closeBtn = new Button(Close);
            _closeBtn.text = "Continuar [Espaço / Esc]";
            _closeBtn.style.height = 36;
            _closeBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
            _closeBtn.style.color = Color.white;
            _closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _card.Add(_closeBtn);

            _overlay.Add(_card);
            _root.Add(_overlay);
        }

        public void Show(int addressIdVal, int deliveredCount, int correctCount, int burnedCount, int wrongCount, int personalEarned, int restaurantEarned, bool orderCompleted)
        {
            EnsureUIInitialized();
            if (_overlay == null) return;

            bool isSuccess = personalEarned > 0;

            if (isSuccess)
            {
                _titleLabel.text = "ENTREGA REALIZADA COM SUCESSO!";
                _titleLabel.style.color = new Color(0.25f, 0.9f, 0.4f);
                _card.style.borderTopColor = new Color(0.2f, 0.8f, 0.4f);
                _card.style.borderBottomColor = new Color(0.2f, 0.8f, 0.4f);
                _card.style.borderLeftColor = new Color(0.2f, 0.8f, 0.4f);
                _card.style.borderRightColor = new Color(0.2f, 0.8f, 0.4f);
            }
            else
            {
                _titleLabel.text = "ENTREGA RECEBIDA (PAGAMENTO R$ 0)";
                _titleLabel.style.color = new Color(0.95f, 0.55f, 0.1f);
                _card.style.borderTopColor = new Color(0.9f, 0.5f, 0.1f);
                _card.style.borderBottomColor = new Color(0.9f, 0.5f, 0.1f);
                _card.style.borderLeftColor = new Color(0.9f, 0.5f, 0.1f);
                _card.style.borderRightColor = new Color(0.9f, 0.5f, 0.1f);
            }

            _addressLabel.text = $"Endereço: {AddressFormat.Display(addressIdVal)}";
            _summaryDelivered.text = $"• Total de caixas entregues: {deliveredCount}";
            _summaryCorrect.text = $"• Pizzas corretas e no ponto: {correctCount}";
            _summaryBurned.text = $"• Pizzas queimadas/cruas: {burnedCount}";
            _summaryWrong.text = $"• Pizzas incorretas/desnecessárias: {wrongCount}";

            _personalMoneyLabel.text = $"💰 Ganho Pessoal: +R$ {personalEarned}";
            _restaurantMoneyLabel.text = $"🏪 Pizzaria: +R$ {restaurantEarned}";

            if (orderCompleted)
            {
                _orderStatusLabel.text = "Comanda Finalizada com Sucesso! ✅";
                _orderStatusLabel.style.color = new Color(0.3f, 0.9f, 0.4f);
            }
            else
            {
                _orderStatusLabel.text = "Sem comanda ativa para este endereço ⚠️";
                _orderStatusLabel.style.color = new Color(0.9f, 0.7f, 0.2f);
            }

            _isOpen = true;
            _overlay.style.display = DisplayStyle.Flex;
            GameplayInputGate.PushModal();
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            if (_overlay != null)
            {
                _overlay.style.display = DisplayStyle.None;
            }
            GameplayInputGate.PopModal();
        }

        private void Update()
        {
            if (!_isOpen) return;

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                {
                    Close();
                }
            }
        }

        private void OnDestroy()
        {
            if (_isOpen)
            {
                GameplayInputGate.Reset();
            }
            if (Instance == this) Instance = null;
        }
    }
}

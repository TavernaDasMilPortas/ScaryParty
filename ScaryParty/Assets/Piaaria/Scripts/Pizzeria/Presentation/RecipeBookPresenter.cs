using UnityEngine;
using UnityEngine.UIElements;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Guia de Montagem de Pizzas por Ingredientes e Atendimento em UI Toolkit.
    /// Aberto via tecla [H], exibe a estrutura de composição por ingredientes (Massa + Molho + 1..3 Toppings),
    /// preparo de ingredientes, diferença entre atendimento presencial (sem caixa na mesa) e delivery (com caixa).
    /// </summary>
    public class RecipeBookPresenter : MonoBehaviour
    {
        public static RecipeBookPresenter Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _card;
        private ScrollView _recipesScroll;
        private Label _titleLabel;
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
            _overlay.name = "RecipeBookOverlay";
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.display = DisplayStyle.None;

            _card = new VisualElement();
            _card.name = "RecipeBookCard";
            _card.style.width = 780;
            _card.style.maxHeight = 720;
            _card.style.backgroundColor = new Color(0.96f, 0.94f, 0.88f, 1f);
            if (ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance?.paperPanel3 != null)
            {
                _card.style.backgroundImage = new StyleBackground(ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance.paperPanel3);
            }
            _card.style.borderTopLeftRadius = 14;
            _card.style.borderTopRightRadius = 14;
            _card.style.borderBottomLeftRadius = 14;
            _card.style.borderBottomRightRadius = 14;
            _card.style.borderTopWidth = 3;
            _card.style.borderBottomWidth = 3;
            _card.style.borderLeftWidth = 3;
            _card.style.borderRightWidth = 3;
            _card.style.borderTopColor = new Color(0.85f, 0.55f, 0.2f);
            _card.style.borderBottomColor = new Color(0.85f, 0.55f, 0.2f);
            _card.style.borderLeftColor = new Color(0.85f, 0.55f, 0.2f);
            _card.style.borderRightColor = new Color(0.85f, 0.55f, 0.2f);
            _card.style.paddingTop = 22;
            _card.style.paddingBottom = 22;
            _card.style.paddingLeft = 26;
            _card.style.paddingRight = 26;

            _titleLabel = new Label("📖 GUIA DE MONTAGEM DE PIZZAS POR INGREDIENTES");
            _titleLabel.style.fontSize = 24;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.95f, 0.7f, 0.25f);
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _card.Add(_titleLabel);

            var guideHint = new Label("Aperte [H] ou [Esc] para fechar | Cada comanda pede uma combinação específica de ingredientes!");
            guideHint.style.fontSize = 16;
            guideHint.style.color = new Color(0.8f, 0.8f, 0.8f);
            guideHint.style.unityTextAlign = TextAnchor.MiddleCenter;
            guideHint.style.marginBottom = 16;
            _card.Add(guideHint);

            _recipesScroll = new ScrollView();
            _recipesScroll.style.height = 480;
            _recipesScroll.style.marginBottom = 14;
            _recipesScroll.style.backgroundColor = new Color(0.08f, 0.07f, 0.06f, 0.85f);
            _recipesScroll.style.borderTopLeftRadius = 10;
            _recipesScroll.style.borderTopRightRadius = 10;
            _recipesScroll.style.borderBottomLeftRadius = 10;
            _recipesScroll.style.borderBottomRightRadius = 10;
            _recipesScroll.style.paddingTop = 12;
            _recipesScroll.style.paddingBottom = 12;
            _recipesScroll.style.paddingLeft = 16;
            _recipesScroll.style.paddingRight = 16;
            _card.Add(_recipesScroll);

            PopulateGuide();

            _closeBtn = new Button(Toggle);
            _closeBtn.text = "Fechar Guia [H / Esc]";
            _closeBtn.style.fontSize = 18;
            _closeBtn.style.height = 46;
            _closeBtn.style.backgroundColor = new Color(0.4f, 0.25f, 0.15f);
            _closeBtn.style.color = Color.white;
            _closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _card.Add(_closeBtn);

            _overlay.Add(_card);
            _root.Add(_overlay);
        }

        private void PopulateGuide()
        {
            _recipesScroll.Clear();

            AddGuideCard("1. Base Obrigatória de Toda Pizza",
                "• Massa Crua (Dispensa) -> Abra na Bancada de Preparo [Segurar F] -> Massa Aberta\n" +
                "• Molho de Tomate (Geladeira) -> Adicione direto sobre a Massa Aberta na Bancada de Montagem",
                "Sem Massa Aberta e Molho de Tomate a pizza ficará incompleta!",
                new Color(0.95f, 0.78f, 0.45f));

            AddGuideCard("2. Coberturas / Toppings (Queijo conta como Topping!)",
                "• Queijo Ralado: retire Queijo Inteiro da Geladeira + use o Ralador na Bancada de Preparo [Segurar F]\n" +
                "• Calabresa Fatiada: retire Calabresa Inteira da Geladeira + use a Faca na Bancada de Preparo [Segurar F]\n" +
                "• Cogumelo Fatiado (quando disponível): Cogumelo Inteiro + Faca na Bancada de Preparo [Segurar F]",
                "Confira na comanda exatamente quais toppings o cliente pediu (ex: só Queijo, só Calabresa, ou Queijo + Calabresa).",
                new Color(0.95f, 0.6f, 0.4f));

            AddGuideCard("3. Clientes Presenciais (Salão) vs Delivery (Telefone)",
                "• Cliente no Salão: atenda no balcão [Segurar F por 0,6s] -> cliente senta na mesa -> sirva a pizza ASSADA DIRETO NA MESA (SEM CAIXA)!\n" +
                "• Pedido por Telefone: atenda com [E] -> asse a pizza -> EMBALE NA CAIXA -> etiquete o endereço -> entregue na casa!",
                "Dica: observe os traços e a barra de paciência dos clientes no salão!",
                new Color(0.55f, 0.85f, 0.95f));

            var stepsBox = new VisualElement();
            stepsBox.style.marginTop = 14;
            stepsBox.style.paddingTop = 14;
            stepsBox.style.paddingBottom = 14;
            stepsBox.style.paddingLeft = 14;
            stepsBox.style.paddingRight = 14;
            stepsBox.style.backgroundColor = new Color(0.12f, 0.15f, 0.18f);
            stepsBox.style.borderTopLeftRadius = 8;
            stepsBox.style.borderTopRightRadius = 8;
            stepsBox.style.borderBottomLeftRadius = 8;
            stepsBox.style.borderBottomRightRadius = 8;

            var stepsTitle = new Label("🔥 RESUMO RÁPIDO DE COMANDOS:");
            stepsTitle.style.fontSize = 18;
            stepsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            stepsTitle.style.color = new Color(0.4f, 0.8f, 1f);
            stepsTitle.style.marginBottom = 8;
            stepsBox.Add(stepsTitle);

            var stepsContent = new Label(
                "• [Segurar F] no Cliente da Fila no Balcão: anota o pedido e reserva a mesa.\n" +
                "• [Segurar F] na Bancada de Preparo: abre massa, rala queijo ou fatia calabresa/cogumelo.\n" +
                "• [E] na Bancada de Montagem: coloca a massa aberta e adiciona molho + toppings pedidos.\n" +
                "• [E] no Forno: insere e retira a pizza no ponto certo (~15s).\n" +
                "• [E] na Mesa do Cliente: serve a pizza assada sem caixa para receber pagamento na hora.\n" +
                "• [F] na Embalagem -> [E] na Expedição -> [B] Mochila: fluxo completo de Delivery.");
            stepsContent.style.fontSize = 16;
            stepsContent.style.color = new Color(0.9f, 0.9f, 0.95f);
            stepsBox.Add(stepsContent);

            _recipesScroll.Add(stepsBox);
        }

        private void AddGuideCard(string name, string ingredients, string details, Color headerColor)
        {
            var box = new VisualElement();
            box.style.marginBottom = 12;
            box.style.paddingTop = 10;
            box.style.paddingBottom = 10;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;
            box.style.backgroundColor = new Color(0.16f, 0.14f, 0.13f);
            box.style.borderTopLeftRadius = 8;
            box.style.borderTopRightRadius = 8;
            box.style.borderBottomLeftRadius = 8;
            box.style.borderBottomRightRadius = 8;

            var title = new Label(name);
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = headerColor;
            title.style.marginBottom = 6;
            box.Add(title);

            var ing = new Label(ingredients);
            ing.style.fontSize = 16;
            ing.style.color = Color.white;
            ing.style.marginBottom = 6;
            box.Add(ing);

            var det = new Label(details);
            det.style.fontSize = 15;
            det.style.color = new Color(0.7f, 0.9f, 0.7f);
            box.Add(det);

            _recipesScroll.Add(box);
        }

        public void Toggle()
        {
            EnsureUIInitialized();
            if (_overlay == null) return;

            _isOpen = !_isOpen;
            if (_isOpen)
            {
                _overlay.style.display = DisplayStyle.Flex;
                GameplayInputGate.PushModal();
            }
            else
            {
                _overlay.style.display = DisplayStyle.None;
                GameplayInputGate.PopModal();
            }
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.hKey.wasPressedThisFrame)
                {
                    Toggle();
                }
                else if (_isOpen && kb.escapeKey.wasPressedThisFrame)
                {
                    Toggle();
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

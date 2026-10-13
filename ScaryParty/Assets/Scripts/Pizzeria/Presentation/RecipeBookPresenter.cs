using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Guia e Livro de Receitas interativo em UI Toolkit.
    /// Aberto via tecla [H], exibe receitas, composição de ingredientes,
    /// utensílios necessários, tempos de forno e fluxo de preparo.
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
            _card.style.width = 640;
            _card.style.maxHeight = 620;
            _card.style.backgroundColor = new Color(0.14f, 0.12f, 0.11f, 0.98f);
            _card.style.borderTopLeftRadius = 12;
            _card.style.borderTopRightRadius = 12;
            _card.style.borderBottomLeftRadius = 12;
            _card.style.borderBottomRightRadius = 12;
            _card.style.borderTopWidth = 3;
            _card.style.borderBottomWidth = 3;
            _card.style.borderLeftWidth = 3;
            _card.style.borderRightWidth = 3;
            _card.style.borderTopColor = new Color(0.85f, 0.55f, 0.2f);
            _card.style.borderBottomColor = new Color(0.85f, 0.55f, 0.2f);
            _card.style.borderLeftColor = new Color(0.85f, 0.55f, 0.2f);
            _card.style.borderRightColor = new Color(0.85f, 0.55f, 0.2f);
            _card.style.paddingTop = 18;
            _card.style.paddingBottom = 18;
            _card.style.paddingLeft = 24;
            _card.style.paddingRight = 24;

            _titleLabel = new Label("📖 LIVRO DE RECEITAS DA PIZZARIA");
            _titleLabel.style.fontSize = 20;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.95f, 0.7f, 0.25f);
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _card.Add(_titleLabel);

            var guideHint = new Label("Aperte [H] ou [Esc] para fechar | Dica: não deixe a pizza passar do ponto no forno!");
            guideHint.style.fontSize = 12;
            guideHint.style.color = new Color(0.75f, 0.75f, 0.75f);
            guideHint.style.unityTextAlign = TextAnchor.MiddleCenter;
            guideHint.style.marginBottom = 14;
            _card.Add(guideHint);

            _recipesScroll = new ScrollView();
            _recipesScroll.style.height = 420;
            _recipesScroll.style.marginBottom = 12;
            _recipesScroll.style.backgroundColor = new Color(0.08f, 0.07f, 0.06f, 0.85f);
            _recipesScroll.style.borderTopLeftRadius = 8;
            _recipesScroll.style.borderTopRightRadius = 8;
            _recipesScroll.style.borderBottomLeftRadius = 8;
            _recipesScroll.style.borderBottomRightRadius = 8;
            _recipesScroll.style.paddingTop = 10;
            _recipesScroll.style.paddingBottom = 10;
            _recipesScroll.style.paddingLeft = 14;
            _recipesScroll.style.paddingRight = 14;
            _card.Add(_recipesScroll);

            PopulateRecipes();

            _closeBtn = new Button(Toggle);
            _closeBtn.text = "Fechar Guia [H / Esc]";
            _closeBtn.style.height = 36;
            _closeBtn.style.backgroundColor = new Color(0.4f, 0.25f, 0.15f);
            _closeBtn.style.color = Color.white;
            _closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _card.Add(_closeBtn);

            _overlay.Add(_card);
            _root.Add(_overlay);
        }

        private void PopulateRecipes()
        {
            _recipesScroll.Clear();

            AddRecipeCard("1. Pizza Margherita (Clássica)",
                "• Massa (aberta na bancada)\n• Molho de Tomate (Geladeira)\n• Queijo Ralado (Queijo Inteiro + Ralador)",
                "Recompensa Base: R$ 35 | Forno: ~15 s",
                new Color(0.9f, 0.8f, 0.5f));

            AddRecipeCard("2. Pizza de Calabresa",
                "• Massa (aberta na bancada)\n• Molho de Tomate\n• Queijo Ralado\n• Calabresa Fatiada (Calabresa Inteira + Faca)",
                "Recompensa Base: R$ 45 | Forno: ~15 s",
                new Color(0.95f, 0.6f, 0.4f));

            AddRecipeCard("3. Pizza de Cogumelos",
                "• Massa (aberta na bancada)\n• Molho de Tomate\n• Queijo Ralado\n• Cogumelo Fatiado (Cogumelo Inteiro + Faca)",
                "Recompensa Base: R$ 50 | Forno: ~15 s",
                new Color(0.7f, 0.85f, 0.6f));

            AddRecipeCard("4. Pizza Quatro Queijos",
                "• Massa (aberta na bancada)\n• Molho de Tomate\n• Queijo Ralado (Porção Dupla)",
                "Recompensa Base: R$ 55 | Forno: ~15 s",
                new Color(1.0f, 0.9f, 0.4f));

            // Fluxo passo a passo
            var stepsBox = new VisualElement();
            stepsBox.style.marginTop = 12;
            stepsBox.style.paddingTop = 10;
            stepsBox.style.paddingBottom = 10;
            stepsBox.style.paddingLeft = 12;
            stepsBox.style.paddingRight = 12;
            stepsBox.style.backgroundColor = new Color(0.12f, 0.15f, 0.18f);
            stepsBox.style.borderTopLeftRadius = 6;
            stepsBox.style.borderTopRightRadius = 6;
            stepsBox.style.borderBottomLeftRadius = 6;
            stepsBox.style.borderBottomRightRadius = 6;

            var stepsTitle = new Label("🔥 FLUXO DA COZINHA AO CLIENTE:");
            stepsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            stepsTitle.style.color = new Color(0.4f, 0.8f, 1f);
            stepsTitle.style.marginBottom = 6;
            stepsBox.Add(stepsTitle);

            var stepsContent = new Label(
                "1. [Telefone] Atenda chamadas com [E] para receber comandas.\n" +
                "2. [Preparo] Abra a massa, rale queijo e pique ingredientes com [F segurado].\n" +
                "3. [Montagem] Monte a pizza crua e coloque no forno com [E].\n" +
                "4. [Forno] Retire a pizza assada assim que o sino tocar (antes de queimar!).\n" +
                "5. [Embalagem] Embale na bancada com [F segurado] e coloque etiqueta de destino.\n" +
                "6. [Entrega] Guarde na mochila [B], vá até a casa e entregue no ponto [E].");
            stepsContent.style.fontSize = 12;
            stepsContent.style.color = new Color(0.9f, 0.9f, 0.95f);
            stepsBox.Add(stepsContent);

            _recipesScroll.Add(stepsBox);
        }

        private void AddRecipeCard(string name, string ingredients, string details, Color headerColor)
        {
            var box = new VisualElement();
            box.style.marginBottom = 10;
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 10;
            box.style.paddingRight = 10;
            box.style.backgroundColor = new Color(0.16f, 0.14f, 0.13f);
            box.style.borderTopLeftRadius = 6;
            box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6;
            box.style.borderBottomRightRadius = 6;

            var title = new Label(name);
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = headerColor;
            title.style.marginBottom = 4;
            box.Add(title);

            var ing = new Label(ingredients);
            ing.style.fontSize = 12;
            ing.style.color = Color.white;
            ing.style.marginBottom = 4;
            box.Add(ing);

            var det = new Label(details);
            det.style.fontSize = 11;
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

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Singleton manager for the Main Game UI using UI Toolkit.
/// Updates the HUD dynamically during gameplay.
/// Includes a Tab-activated scoreboard showing all players, their pizza slots, and money.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Document")]
    [Tooltip("The UIDocument component in the scene containing MainGameUI")]
    public UIDocument uiDocument;

    // UI Elements
    private Label _moneyLabel;
    private Label _scoreLabel;
    private ScrollView _ordersList;
    private Label _interactionPrompt;
    private Label _leftHandContent;
    private Label _rightHandContent;

    // Scoreboard Elements
    private VisualElement _scoreboardOverlay;
    private ScrollView _scoreboardList;

    // State
    private int _completedDeliveries = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }

    private VisualElement _minimapImage;

    private void OnEnable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var root = uiDocument.rootVisualElement;

        // Bind Elements
        _moneyLabel = root.Q<Label>("MoneyLabel");
        _scoreLabel = root.Q<Label>("ScoreLabel");
        _ordersList = root.Q<ScrollView>("OrdersList");
        _interactionPrompt = root.Q<Label>("InteractionPrompt");
        _leftHandContent = root.Q<Label>("LeftHandContent");
        _rightHandContent = root.Q<Label>("RightHandContent");
        _minimapImage = root.Q<VisualElement>("MinimapImage");

        // Scoreboard
        _scoreboardOverlay = root.Q<VisualElement>("ScoreboardOverlay");
        _scoreboardList = root.Q<ScrollView>("ScoreboardList");

        UpdateMoneyDisplay(0);
        UpdateScore(0);
        HideInteractionPrompt();
        UpdateHand(true, "Empty");
        UpdateHand(false, "Empty");
        HideScoreboard();

        // Minimap drag and drop setup
        if (_minimapImage != null)
        {
            _minimapImage.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop; // Prevents distortion/off-center
            _minimapImage.RegisterCallback<PointerDownEvent>(OnMinimapPointerDown);
            _minimapImage.RegisterCallback<PointerMoveEvent>(OnMinimapPointerMove);
            _minimapImage.RegisterCallback<PointerUpEvent>(OnMinimapPointerUp);
            _minimapImage.RegisterCallback<PointerLeaveEvent>(OnMinimapPointerLeave);
        }
    }

    private bool _isDraggingMap = false;
    private Vector2 _lastMousePos;

    private void OnMinimapPointerDown(PointerDownEvent evt)
    {
        if (MinimapRouteManager.Instance != null && MinimapRouteManager.Instance.IsFullscreen)
        {
            _isDraggingMap = true;
            _lastMousePos = (Vector2)evt.position;
            _minimapImage.CapturePointer(evt.pointerId);
        }
    }

    private void OnMinimapPointerMove(PointerMoveEvent evt)
    {
        if (_isDraggingMap && MinimapRouteManager.Instance != null)
        {
            Vector2 delta = (Vector2)evt.position - _lastMousePos;
            _lastMousePos = (Vector2)evt.position;
            MinimapRouteManager.Instance.PanMap(delta);
        }
    }

    private void OnMinimapPointerUp(PointerUpEvent evt)
    {
        if (_isDraggingMap)
        {
            _isDraggingMap = false;
            _minimapImage.ReleasePointer(evt.pointerId);
        }
    }

    private void OnMinimapPointerLeave(PointerLeaveEvent evt)
    {
        if (_isDraggingMap)
        {
            _isDraggingMap = false;
            // PointerLeave doesn't require ReleasePointer for pointerId usually, but just in case
        }
    }

    private void Update()
    {
        // Continuously try to bind the RenderTexture if it exists and hasn't been bound yet
        // UGUIWorldImage generates the RT dynamically, so it might not be ready in OnEnable.
        if (_minimapImage != null && _minimapImage.resolvedStyle.backgroundImage == null)
        {
            var worldImage = FindObjectOfType<Kamgam.UGUIWorldImage.WorldImage>();
            if (worldImage != null && worldImage.RenderTexture != null)
            {
                _minimapImage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(worldImage.RenderTexture));
            }
        }

        // Scoreboard: mostra enquanto Tab está pressionado
        HandleScoreboardInput();
    }

    // ========================================
    // SCOREBOARD (Tab)
    // ========================================

    private void HandleScoreboardInput()
    {
        bool tabHeld = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            tabHeld = Keyboard.current.tabKey.isPressed;
#else
        tabHeld = Input.GetKey(KeyCode.Tab);
#endif

        if (tabHeld)
        {
            ShowScoreboard();
        }
        else
        {
            HideScoreboard();
        }
    }

    private void ShowScoreboard()
    {
        if (_scoreboardOverlay == null) return;

        _scoreboardOverlay.RemoveFromClassList("hidden");
        _scoreboardOverlay.style.display = DisplayStyle.Flex;

        RefreshScoreboardData();
    }

    private void HideScoreboard()
    {
        if (_scoreboardOverlay == null) return;

        _scoreboardOverlay.AddToClassList("hidden");
        _scoreboardOverlay.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Lê todos os PlayerState na cena e monta as linhas do scoreboard.
    /// Cada linha: Nome ············ ■/□ slots ············ R$xxx
    /// ■ = slot com pizza, □ = slot vazio
    /// </summary>
    private void RefreshScoreboardData()
    {
        if (_scoreboardList == null) return;

        _scoreboardList.Clear();

        // Busca todos os jogadores ativos
        var allPlayers = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            if (player == null) continue;

            string playerName = player.PlayerName.Value.ToString();
            if (string.IsNullOrEmpty(playerName)) playerName = "???";

            int heldPizzas = player.HeldPizzas.Value;
            int money = player.Money.Value;
            Color playerColor = player.PlayerColor.Value;

            // Monta a string de slots: ■ para pizza, □ para vazio (2 slots = 2 mãos)
            const int maxSlots = 2;
            string slotsDisplay = "";
            for (int i = 0; i < maxSlots; i++)
            {
                if (i < heldPizzas)
                    slotsDisplay += "●  "; // Círculo cheio = slot com pizza
                else
                    slotsDisplay += "■  "; // Quadrado = slot vazio
            }
            slotsDisplay = slotsDisplay.TrimEnd();

            // Cria a row do scoreboard
            var row = new VisualElement();
            row.AddToClassList("scoreboard-row");

            // Nome do jogador (com cor)
            var nameLabel = new Label(playerName);
            nameLabel.AddToClassList("scoreboard-cell");
            nameLabel.AddToClassList("scoreboard-name");
            nameLabel.style.color = new StyleColor(playerColor);

            // Indicador de slots (pizzas)
            var slotsLabel = new Label(slotsDisplay);
            slotsLabel.AddToClassList("scoreboard-cell");
            slotsLabel.AddToClassList("scoreboard-slots");

            // Dinheiro
            var moneyLabel = new Label($"R${money}");
            moneyLabel.AddToClassList("scoreboard-cell");
            moneyLabel.AddToClassList("scoreboard-money");

            row.Add(nameLabel);
            row.Add(slotsLabel);
            row.Add(moneyLabel);

            _scoreboardList.Add(row);
        }
    }

    // ========================================
    // MONEY (agora via NetworkVariable)
    // ========================================

    /// <summary>
    /// Atualiza o display de dinheiro no HUD.
    /// Chamado pelo callback OnMoneyChanged do PlayerState (NetworkVariable).
    /// </summary>
    public void UpdateMoneyDisplay(int total)
    {
        if (_moneyLabel != null)
        {
            _moneyLabel.text = $"R${total}";
        }
    }

    /// <summary>
    /// Adds a completed delivery and updates the score UI.
    /// </summary>
    public void AddCompletedDelivery()
    {
        _completedDeliveries++;
        UpdateScore(_completedDeliveries);
    }

    private void UpdateScore(int total)
    {
        if (_scoreLabel != null)
        {
            _scoreLabel.text = $"Entregas: {total}";
        }
    }

    // ========================================
    // HANDS / INTERACTION
    // ========================================

    /// <summary>
    /// Updates the text shown in a hand slot.
    /// </summary>
    public void UpdateHand(bool isRightHand, string itemName)
    {
        if (isRightHand && _rightHandContent != null)
            _rightHandContent.text = string.IsNullOrEmpty(itemName) ? "Empty" : itemName;
        else if (!isRightHand && _leftHandContent != null)
            _leftHandContent.text = string.IsNullOrEmpty(itemName) ? "Empty" : itemName;
    }

    /// <summary>
    /// Shows the interaction prompt with a specific message.
    /// Explicitly styles it to show [E] clearly.
    /// </summary>
    public void ShowInteractionPrompt(string message)
    {
        if (_interactionPrompt != null)
        {
            // Ensure the UI shows [E] prominently
            if (!message.Contains("[E]"))
            {
                message = $"[E] {message}";
            }
            
            _interactionPrompt.text = message;
            _interactionPrompt.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.8f));
            _interactionPrompt.style.color = new StyleColor(Color.white);
            _interactionPrompt.style.paddingLeft = 10;
            _interactionPrompt.style.paddingRight = 10;
            _interactionPrompt.style.paddingTop = 5;
            _interactionPrompt.style.paddingBottom = 5;
            _interactionPrompt.style.borderTopLeftRadius = 5;
            _interactionPrompt.style.borderTopRightRadius = 5;
            _interactionPrompt.style.borderBottomLeftRadius = 5;
            _interactionPrompt.style.borderBottomRightRadius = 5;
            
            _interactionPrompt.RemoveFromClassList("hidden");
            // Also ensure display is block in case class list isn't working
            _interactionPrompt.style.display = DisplayStyle.Flex;
        }
    }

    /// <summary>
    /// Hides the interaction prompt.
    /// </summary>
    public void HideInteractionPrompt()
    {
        if (_interactionPrompt != null)
        {
            _interactionPrompt.AddToClassList("hidden");
            _interactionPrompt.style.display = DisplayStyle.None;
        }
    }

    // ========================================
    // MINIMAP
    // ========================================

    /// <summary>
    /// Toggles the minimap VisualElement to cover most of the screen.
    /// </summary>
    public void ToggleMinimapFullscreen(bool isFullscreen)
    {
        if (_minimapImage == null || _minimapImage.parent == null) return;

        var container = _minimapImage.parent;

        if (isFullscreen)
        {
            // Center and expand the CONTAINER
            container.style.width = Length.Percent(80);
            container.style.height = Length.Percent(80);
            container.style.bottom = Length.Percent(10);
            container.style.right = Length.Percent(10);
            container.style.top = Length.Percent(10);
            container.style.left = Length.Percent(10);
        }
        else
        {
            // Restore to original HUD position
            container.style.width = StyleKeyword.Null;
            container.style.height = StyleKeyword.Null;
            container.style.bottom = StyleKeyword.Null;
            container.style.right = StyleKeyword.Null;
            container.style.top = StyleKeyword.Null;
            container.style.left = StyleKeyword.Null;
        }
    }

    // ========================================
    // ORDERS LIST
    // ========================================

    /// <summary>
    /// Updates the orders list on the right side of the screen.
    /// </summary>
    public void UpdateOrdersList(List<string> activeOrders)
    {
        if (_ordersList == null) return;

        _ordersList.Clear();

        foreach (var order in activeOrders)
        {
            Label orderLabel = new Label(order);
            orderLabel.AddToClassList("order-item");
            orderLabel.AddToClassList("order-item-text");

            // Apply route color to the left border and the text itself so it's very visible
            if (MinimapRouteManager.Instance != null)
            {
                Color routeColor = MinimapRouteManager.Instance.GetOrderColor(order);
                orderLabel.style.borderLeftColor = new StyleColor(routeColor);
                orderLabel.style.color = new StyleColor(routeColor);
            }

            _ordersList.Add(orderLabel);
        }
    }
}

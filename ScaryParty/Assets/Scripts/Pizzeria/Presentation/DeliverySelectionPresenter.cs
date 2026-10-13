using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Modal UI Toolkit para seleção das pizzas a entregar no endereço de destino.
    /// Exibe caixas na mão e na mochila, suas etiquetas e permite selecionar individualmente ou todas.
    /// </summary>
    public class DeliverySelectionPresenter : MonoBehaviour
    {
        public static DeliverySelectionPresenter Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _card;

        private Label _titleLabel;
        private Label _subtitleLabel;
        private ScrollView _boxListContainer;
        private Button _selectAllBtn;
        private Button _confirmBtn;
        private Button _cancelBtn;

        private int _currentAddressId = -1;
        private readonly List<NetItemDto> _availableBoxes = new List<NetItemDto>();
        private readonly HashSet<ulong> _selectedItemIds = new HashSet<ulong>();
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
            _overlay.name = "DeliverySelectionOverlay";
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.display = DisplayStyle.None;

            _card = new VisualElement();
            _card.name = "DeliverySelectionCard";
            _card.style.width = 640;
            _card.style.maxHeight = 680;
            _card.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.98f);
            _card.style.borderTopLeftRadius = 12;
            _card.style.borderTopRightRadius = 12;
            _card.style.borderBottomLeftRadius = 12;
            _card.style.borderBottomRightRadius = 12;
            _card.style.borderTopWidth = 2;
            _card.style.borderBottomWidth = 2;
            _card.style.borderLeftWidth = 2;
            _card.style.borderRightWidth = 2;
            _card.style.borderTopColor = new Color(0.9f, 0.6f, 0.2f);
            _card.style.borderBottomColor = new Color(0.9f, 0.6f, 0.2f);
            _card.style.borderLeftColor = new Color(0.9f, 0.6f, 0.2f);
            _card.style.borderRightColor = new Color(0.9f, 0.6f, 0.2f);
            _card.style.paddingTop = 22;
            _card.style.paddingBottom = 22;
            _card.style.paddingLeft = 24;
            _card.style.paddingRight = 24;

            _titleLabel = new Label("ENTREGA DE PIZZAS");
            _titleLabel.style.fontSize = 26;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.95f, 0.7f, 0.2f);
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _card.Add(_titleLabel);

            _subtitleLabel = new Label("Selecione as caixas que deseja entregar ao cliente:");
            _subtitleLabel.style.fontSize = 17;
            _subtitleLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            _subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _subtitleLabel.style.marginBottom = 14;
            _card.Add(_subtitleLabel);

            _boxListContainer = new ScrollView();
            _boxListContainer.style.height = 300;
            _boxListContainer.style.marginBottom = 14;
            _boxListContainer.style.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.8f);
            _boxListContainer.style.borderTopLeftRadius = 8;
            _boxListContainer.style.borderTopRightRadius = 8;
            _boxListContainer.style.borderBottomLeftRadius = 8;
            _boxListContainer.style.borderBottomRightRadius = 8;
            _boxListContainer.style.paddingTop = 8;
            _boxListContainer.style.paddingBottom = 8;
            _boxListContainer.style.paddingLeft = 10;
            _boxListContainer.style.paddingRight = 10;
            _card.Add(_boxListContainer);

            // Botão Selecionar Todas
            _selectAllBtn = new Button(ToggleSelectAll);
            _selectAllBtn.text = "Selecionar Todas as Caixas";
            _selectAllBtn.style.fontSize = 17;
            _selectAllBtn.style.height = 42;
            _selectAllBtn.style.marginBottom = 12;
            _selectAllBtn.style.backgroundColor = new Color(0.2f, 0.35f, 0.5f);
            _selectAllBtn.style.color = Color.white;
            _selectAllBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _card.Add(_selectAllBtn);

            // Container de Ações
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.SpaceBetween;

            _cancelBtn = new Button(Close);
            _cancelBtn.text = "Cancelar [Esc]";
            _cancelBtn.style.fontSize = 18;
            _cancelBtn.style.flexGrow = 1;
            _cancelBtn.style.marginRight = 8;
            _cancelBtn.style.height = 44;
            _cancelBtn.style.backgroundColor = new Color(0.4f, 0.2f, 0.2f);
            _cancelBtn.style.color = Color.white;
            btnRow.Add(_cancelBtn);

            _confirmBtn = new Button(ConfirmDelivery);
            _confirmBtn.text = "Confirmar Entrega (0)";
            _confirmBtn.style.fontSize = 18;
            _confirmBtn.style.flexGrow = 1;
            _confirmBtn.style.marginLeft = 8;
            _confirmBtn.style.height = 44;
            _confirmBtn.style.backgroundColor = new Color(0.15f, 0.55f, 0.25f);
            _confirmBtn.style.color = Color.white;
            _confirmBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btnRow.Add(_confirmBtn);

            _card.Add(btnRow);
            _overlay.Add(_card);
            _root.Add(_overlay);
        }

        public void Open(int destinationAddressId)
        {
            EnsureUIInitialized();
            if (_overlay == null) return;

            _currentAddressId = destinationAddressId;
            _availableBoxes.Clear();
            _selectedItemIds.Clear();

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || NetworkManager.Singleton == null) return;

            ulong myId = NetworkManager.Singleton.LocalClientId;

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                bool isCarriedByMe = item.HolderId == myId &&
                    (item.LocationType == (byte)LocationType.Hand || item.LocationType == (byte)LocationType.Backpack);

                if (isCarriedByMe && (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box))
                {
                    _availableBoxes.Add(item);
                }
            }

            if (_availableBoxes.Count == 0)
            {
                NotificationManager.Show("Você não possui nenhuma pizza para entregar aqui!", NotificationType.Warning);
                return;
            }

            _titleLabel.text = $"ENTREGA DE PIZZAS — {AddressFormat.Display(_currentAddressId)}";
            RefreshBoxList();

            _isOpen = true;
            _overlay.style.display = DisplayStyle.Flex;
            GameplayInputGate.PushModal();
        }

        private void RefreshBoxList()
        {
            _boxListContainer.Clear();

            for (int i = 0; i < _availableBoxes.Count; i++)
            {
                var item = _availableBoxes[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.paddingTop = 8;
                row.style.paddingBottom = 8;
                row.style.paddingLeft = 12;
                row.style.paddingRight = 12;
                row.style.marginBottom = 6;
                row.style.backgroundColor = new Color(0.16f, 0.16f, 0.2f);
                row.style.borderTopLeftRadius = 6;
                row.style.borderTopRightRadius = 6;
                row.style.borderBottomLeftRadius = 6;
                row.style.borderBottomRightRadius = 6;

                string locStr = item.LocationType == (byte)LocationType.Hand
                    ? $"Mão {(item.SlotId == 0 ? "Esquerda" : "Direita")}"
                    : $"Mochila (Slot {item.SlotId})";

                string labelStr = item.LabelDestinationId > 0
                    ? $"Etiqueta: {AddressFormat.Display(item.LabelDestinationId)}"
                    : "Sem etiqueta";

                bool isMatch = item.LabelDestinationId == _currentAddressId;
                if (isMatch)
                {
                    row.style.borderLeftWidth = 4;
                    row.style.borderLeftColor = new Color(0.2f, 0.8f, 0.3f);
                }

                var labelBox = new Label($"📦 {locStr} | {labelStr}");
                labelBox.style.color = isMatch ? new Color(0.4f, 0.9f, 0.4f) : Color.white;
                labelBox.style.fontSize = 17;
                row.Add(labelBox);

                var toggle = new Toggle();
                toggle.style.scale = new Scale(new Vector2(1.2f, 1.2f));
                toggle.value = _selectedItemIds.Contains(item.ItemId);
                ulong itemId = item.ItemId;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) _selectedItemIds.Add(itemId);
                    else _selectedItemIds.Remove(itemId);
                    UpdateConfirmButton();
                });
                row.Add(toggle);

                _boxListContainer.Add(row);
            }

            UpdateConfirmButton();
        }

        private void ToggleSelectAll()
        {
            if (_selectedItemIds.Count == _availableBoxes.Count)
            {
                _selectedItemIds.Clear();
            }
            else
            {
                _selectedItemIds.Clear();
                for (int i = 0; i < _availableBoxes.Count; i++)
                {
                    _selectedItemIds.Add(_availableBoxes[i].ItemId);
                }
            }
            RefreshBoxList();
        }

        private void UpdateConfirmButton()
        {
            int count = _selectedItemIds.Count;
            _confirmBtn.text = $"Confirmar Entrega ({count})";
            _confirmBtn.SetEnabled(count > 0);
        }

        private void ConfirmDelivery()
        {
            if (_selectedItemIds.Count == 0) return;

            var cmd = LogisticsCommands.Instance;
            if (cmd != null)
            {
                var idList = new List<ulong>(_selectedItemIds);
                ulong attemptId = ((ulong)System.DateTime.UtcNow.Ticks) ^ ((ulong)UnityEngine.Random.Range(1, 1000000));
                cmd.SubmitDeliveryServerRpc(_currentAddressId, idList.ToArray(), attemptId);
            }

            Close();
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
                if (kb.escapeKey.wasPressedThisFrame)
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

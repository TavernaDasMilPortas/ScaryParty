using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Player;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Modal UI Toolkit para a Mochila Térmica do entregador (tecla B).
    /// Exibe os slots, caixas guardadas com suas etiquetas de destino,
    /// e ações para guardar da mão, retirar para a mão e largar mochila no chão.
    /// </summary>
    public class BackpackPanelPresenter : MonoBehaviour
    {
        public static BackpackPanelPresenter Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _card;

        private Label _titleLabel;
        private Label _capacityLabel;
        private ScrollView _slotsContainer;
        private Button _storeHandBtn;
        private Button _dropBackpackBtn;
        private Button _closeBtn;

        private bool _isOpen = false;
        private int _openedFrame = -1;
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
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
            _overlay.name = "BackpackPanelOverlay";
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
            _card.name = "BackpackPanelCard";
            _card.style.width = 600;
            _card.style.maxHeight = 620;
            _card.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.98f);
            _card.style.borderTopLeftRadius = 12;
            _card.style.borderTopRightRadius = 12;
            _card.style.borderBottomLeftRadius = 12;
            _card.style.borderBottomRightRadius = 12;
            _card.style.borderTopWidth = 2;
            _card.style.borderBottomWidth = 2;
            _card.style.borderLeftWidth = 2;
            _card.style.borderRightWidth = 2;
            _card.style.borderTopColor = new Color(0.9f, 0.35f, 0.2f);
            _card.style.borderBottomColor = new Color(0.9f, 0.35f, 0.2f);
            _card.style.borderLeftColor = new Color(0.9f, 0.35f, 0.2f);
            _card.style.borderRightColor = new Color(0.9f, 0.35f, 0.2f);
            _card.style.paddingTop = 20;
            _card.style.paddingBottom = 20;
            _card.style.paddingLeft = 24;
            _card.style.paddingRight = 24;

            _titleLabel = new Label("🎒 MOCHILA TÉRMICA DO ENTREGADOR");
            _titleLabel.style.fontSize = 24;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.95f, 0.5f, 0.3f);
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _card.Add(_titleLabel);

            _capacityLabel = new Label("Capacidade: 0 / 3 caixas");
            _capacityLabel.style.fontSize = 17;
            _capacityLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            _capacityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _capacityLabel.style.marginBottom = 14;
            _card.Add(_capacityLabel);

            _slotsContainer = new ScrollView();
            _slotsContainer.style.height = 260;
            _slotsContainer.style.marginBottom = 14;
            _slotsContainer.style.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.8f);
            _slotsContainer.style.borderTopLeftRadius = 8;
            _slotsContainer.style.borderTopRightRadius = 8;
            _slotsContainer.style.borderBottomLeftRadius = 8;
            _slotsContainer.style.borderBottomRightRadius = 8;
            _slotsContainer.style.paddingTop = 8;
            _slotsContainer.style.paddingBottom = 8;
            _slotsContainer.style.paddingLeft = 10;
            _slotsContainer.style.paddingRight = 10;
            _card.Add(_slotsContainer);

            // Botão Guardar da Mão Ativa
            _storeHandBtn = new Button(StoreFromActiveHand);
            _storeHandBtn.text = "Guardar Caixa da Mão Ativa";
            _storeHandBtn.style.fontSize = 18;
            _storeHandBtn.style.height = 46;
            _storeHandBtn.style.marginBottom = 10;
            _storeHandBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
            _storeHandBtn.style.color = Color.white;
            _storeHandBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _card.Add(_storeHandBtn);

            // Container de Ações Inferiores
            var bottomRow = new VisualElement();
            bottomRow.style.flexDirection = FlexDirection.Row;
            bottomRow.style.justifyContent = Justify.SpaceBetween;

            _dropBackpackBtn = new Button(DropBackpack);
            _dropBackpackBtn.text = "Largar Mochila";
            _dropBackpackBtn.style.fontSize = 17;
            _dropBackpackBtn.style.flexGrow = 1;
            _dropBackpackBtn.style.marginRight = 8;
            _dropBackpackBtn.style.height = 44;
            _dropBackpackBtn.style.backgroundColor = new Color(0.5f, 0.25f, 0.15f);
            _dropBackpackBtn.style.color = Color.white;
            bottomRow.Add(_dropBackpackBtn);

            _closeBtn = new Button(Toggle);
            _closeBtn.text = "Fechar [B / Esc]";
            _closeBtn.style.fontSize = 17;
            _closeBtn.style.flexGrow = 1;
            _closeBtn.style.marginLeft = 8;
            _closeBtn.style.height = 44;
            _closeBtn.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
            _closeBtn.style.color = Color.white;
            bottomRow.Add(_closeBtn);

            _card.Add(bottomRow);
            _overlay.Add(_card);
            _root.Add(_overlay);
        }

        public void Toggle()
        {
            EnsureUIInitialized();
            if (_overlay == null) return;

            _isOpen = !_isOpen;
            if (_isOpen)
            {
                _openedFrame = Time.frameCount;
                RefreshSlots();
                _overlay.style.display = DisplayStyle.Flex;
                GameplayInputGate.PushModal();
            }
            else
            {
                _overlay.style.display = DisplayStyle.None;
                GameplayInputGate.PopModal();
            }
        }

        private BackpackController GetLocalBackpackController()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return null;
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            return playerObj != null ? playerObj.GetComponent<BackpackController>() : null;
        }

        private void RefreshSlots()
        {
            _slotsContainer.Clear();
            var controller = GetLocalBackpackController();
            if (controller == null)
            {
                var lbl = new Label("Você não possui uma mochila equipada no momento.");
                lbl.style.color = new Color(0.9f, 0.4f, 0.4f);
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                lbl.style.marginTop = 20;
                _slotsContainer.Add(lbl);
                _capacityLabel.text = "Mochila não equipada";
                return;
            }

            var boxes = controller.GetBoxesInBackpack();
            int cap = controller.capacity;
            _capacityLabel.text = $"Capacidade: {boxes.Count} / {cap} caixas";

            for (int i = 0; i < cap; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
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

                if (i < boxes.Count)
                {
                    var item = boxes[i];
                    string labelDest = item.LabelDestinationId > 0
                        ? $"Etiqueta: {AddressFormat.Display(item.LabelDestinationId)}"
                        : "Sem etiqueta";

                    var desc = new Label($"Slot {i + 1}: 📦 Caixa #{item.ItemId} ({labelDest})");
                    desc.style.fontSize = 17;
                    desc.style.color = new Color(0.95f, 0.8f, 0.4f);
                    row.Add(desc);

                    ulong itemId = item.ItemId;
                    var takeBtn = new Button(() => RetrieveToHand(itemId));
                    takeBtn.text = "Retirar para Mão";
                    takeBtn.style.fontSize = 16;
                    takeBtn.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f);
                    takeBtn.style.color = Color.white;
                    takeBtn.style.height = 36;
                    takeBtn.style.paddingLeft = 14;
                    takeBtn.style.paddingRight = 14;
                    row.Add(takeBtn);
                }
                else
                {
                    var emptyDesc = new Label($"Slot {i + 1}: [Vazio]");
                    emptyDesc.style.fontSize = 17;
                    emptyDesc.style.color = new Color(0.6f, 0.6f, 0.6f);
                    row.Add(emptyDesc);
                }

                _slotsContainer.Add(row);
            }
        }

        private void StoreFromActiveHand()
        {
            var controller = GetLocalBackpackController();
            if (controller == null) return;

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var adapter = localPlayer != null ? localPlayer.GetComponent<PlayerInventoryAdapter>() : null;
            var activeHand = adapter != null ? adapter.ActiveHand : HandSlotIndex.Left;

            bool success = controller.TryStoreFromHand(activeHand);
            if (success)
            {
                RefreshSlots();
            }
        }

        private void RetrieveToHand(ulong boxItemId)
        {
            var controller = GetLocalBackpackController();
            if (controller == null) return;

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var adapter = localPlayer != null ? localPlayer.GetComponent<PlayerInventoryAdapter>() : null;
            var activeHand = adapter != null ? adapter.ActiveHand : HandSlotIndex.Left;

            bool success = controller.TryRetrieveToHand(boxItemId, activeHand);
            if (success)
            {
                RefreshSlots();
            }
        }

        private void DropBackpack()
        {
            var logistics = LogisticsCommands.Instance;
            if (logistics != null)
            {
                logistics.DropBackpackServerRpc();
                NotificationManager.Show("Mochila largada no chão.", NotificationType.Info);
            }
            Toggle();
        }

        private void Update()
        {
            if (!_isOpen) return;
            if (Time.frameCount == _openedFrame) return;

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.bKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Player;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Modal UI Toolkit para a Mochila Térmica do entregador (tecla B).
    /// Exibe os slots, caixas guardadas com suas etiquetas de destino e composição de ingredientes,
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
        private float _nextRefresh;
        private ulong _inspectedBagId;
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

            var hud = FindFirstObjectByType<ScaryParty.UI.Piaria.Presenters.HudPresenter>();
            _doc = hud != null ? hud.uiDocument : null;
            if (_doc == null) _doc = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            if (_doc.panelSettings == null)
            {
                var panelSettingsList = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (panelSettingsList != null && panelSettingsList.Length > 0)
                    _doc.panelSettings = panelSettingsList[0];
            }
            _root = _doc.rootVisualElement;

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
            _card.style.backgroundColor = new Color(0.96f, 0.94f, 0.88f, 1f);
            if (ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance?.paperPanel2 != null)
            {
                _card.style.backgroundImage = new StyleBackground(ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance.paperPanel2);
            }
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
            _titleLabel = new Label("MOCHILA TERMICA DO ENTREGADOR");
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
            _storeHandBtn.text = "Guardar caixa da mao ativa";
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

        public void Inspect(ulong bagId)
        {
            if (bagId == 0) return;
            _inspectedBagId = bagId;
            if (!IsInspectedBagAccessible()) { _inspectedBagId = 0; return; }
            if (!_isOpen) Toggle();
            _inspectedBagId = bagId;
            RefreshSlots();
        }

        public void Toggle()
        {
            if (!_isOpen) _inspectedBagId = 0;
            EnsureUIInitialized();
            if (_overlay == null) return;

            _isOpen = !_isOpen;
            if (_isOpen)
            {
                _openedFrame = Time.frameCount;
                _overlay.style.display = DisplayStyle.Flex;
                GameplayInputGate.PushModal();
                RefreshSlots();
            }
            else
            {
                _overlay.style.display = DisplayStyle.None;
                _inspectedBagId = 0;
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
            _dropBackpackBtn.style.display = _inspectedBagId == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var state = PizzeriaNetworkState.Instance;
            NetBackpackDto bag = default;
            bool found = false;
            if (_inspectedBagId != 0)
            {
                if (state == null) { if (_isOpen) Toggle(); return; }
                for (int i = 0; i < state.Backpacks.Count; i++)
                    if (state.Backpacks[i].BackpackId == _inspectedBagId &&
                        state.Backpacks[i].EquippedBy == ScaryParty.Pizzeria.Domain.Models.BackpackState.NoOwner)
                    {
                        bag = state.Backpacks[i];
                        found = true;
                        break;
                    }
                if (!found) { if (_isOpen) Toggle(); return; }
            }
            else if (controller != null) found = controller.TryGetEquippedBag(out bag);
            if (!found)
            {
                _dropBackpackBtn.SetEnabled(false);
                _storeHandBtn.SetEnabled(false);
                var lbl = new Label("Sem mochila equipada.");
                lbl.style.color = new Color(0.9f, 0.4f, 0.4f);
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                lbl.style.marginTop = 20;
                _slotsContainer.Add(lbl);
                _capacityLabel.text = "Sem mochila equipada";
                return;
            }

            _dropBackpackBtn.SetEnabled(_inspectedBagId == 0);
            _storeHandBtn.SetEnabled(bag.ItemCount < bag.Capacity);
            var boxes = new List<NetItemDto>();
            if (state != null)
                for (int i = 0; i < state.Backpacks.Count && i < 1; i++)
                {
                    for (int j = 0; j < state.Items.Count; j++)
                    {
                        var item = state.Items[j];
                        if (item.LocationType == (byte)LocationType.Backpack && item.HolderId == bag.BackpackId)
                            boxes.Add(item);
                    }
                }
            int cap = bag.Capacity;
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
                    string labelDest = item.LabelDestinationId >= 0
                        ? $"Etiqueta: #{item.LabelDestinationId + 1}"
                        : "Sem etiqueta";
                    string compStr = PizzaOrderFormatter.FormatItemCompositionSummary(item.IngredientMask, item.IngredientCount);
                    var desc = new Label($"Slot {i + 1}: Caixa #{item.ItemId} ({labelDest} — {compStr})");
                    desc.style.fontSize = 16;
                    desc.style.color = new Color(0.95f, 0.8f, 0.4f);
                    row.Add(desc);

                    ulong itemId = item.ItemId;
                    var takeBtn = new Button(() => RetrieveToHand(itemId));
                    takeBtn.text = "Retirar para mao";
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
            if (adapter == null) return;

            bool success = controller.TryStoreFromHand(adapter.ActiveHand, _inspectedBagId);
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
            if (adapter == null) return;

            bool success = controller.TryRetrieveToHand(boxItemId, adapter.ActiveHand, _inspectedBagId);
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
            }
        }

        private bool IsInspectedBagAccessible()
        {
            var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var state = PizzeriaNetworkState.Instance;
            if (player == null || state == null) return false;
            for (int i = 0; i < state.Backpacks.Count; i++)
            {
                var bag = state.Backpacks[i];
                if (bag.BackpackId != _inspectedBagId) continue;
                return bag.EquippedBy == ScaryParty.Pizzeria.Domain.Models.BackpackState.NoOwner &&
                    Vector3.Distance(player.transform.position, new Vector3(bag.PosX, bag.PosY, bag.PosZ)) <= 4.8f;
            }
            return false;
        }

        private void OnDisable()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _inspectedBagId = 0;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            GameplayInputGate.PopModal();
        }

        private void Update()
        {
            if (!_isOpen) return;
            if (_inspectedBagId != 0 && !IsInspectedBagAccessible())
            {
                Toggle();
                return;
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.3f;
                RefreshSlots();
            }
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
            if (_isOpen) GameplayInputGate.PopModal();
            if (_overlay != null) _overlay.RemoveFromHierarchy();
            if (Instance == this) Instance = null;
        }
    }
}

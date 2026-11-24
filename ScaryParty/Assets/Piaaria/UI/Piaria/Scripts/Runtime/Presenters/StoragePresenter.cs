using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.UI.Piaria.Presenters
{
    public class StoragePresenter : MonoBehaviour, IModalView
    {
        public static StoragePresenter Instance { get; private set; }

        public bool CanCloseWithEscape => true;
        public VisualElement RootElement { get; private set; }

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;

        private VisualElement _storageCard;
        private Label _storageTitle;
        private Button _btnClose;
        private Button _btnSelectLeft;
        private Button _btnSelectRight;
        private ScrollView _storageList;
        private Label _statusLabel;

        private int _currentStationId = -1;
        private HandSlotIndex _selectedHand = HandSlotIndex.Left;
        private bool _isPending = false;
        private int _pendingStorageId = -1;
        private int _pendingIngredientId = -1;
        private int _pendingPreviousQuantity = -1;
        private ulong _pendingToolId = 0;
        private bool _isOpen = false;
        private Coroutine _timeoutCoroutine;

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

            ResolveUIDocument();
            SubscribeNetworkEvents();
        }

        private bool _netSubscribed;

        private void OnDestroy()
        {
            if (_isOpen)
            {
                _isOpen = false;
                UIModalStack.Instance?.PopModal(this);
            }
            UnsubscribeNetworkEvents();
            if (Instance == this) Instance = null;
        }

        private void SubscribeNetworkEvents()
        {
            if (_netSubscribed || PizzeriaNetworkState.Instance == null) return;
            _netSubscribed = true;
            PizzeriaNetworkState.Instance.StorageSlots.OnListChanged += OnStorageListChanged;
            PizzeriaNetworkState.Instance.Tools.OnListChanged += OnToolsListChanged;
            PizzeriaNetworkState.Instance.SupplyOrders.OnListChanged += OnSupplyOrdersChanged;
        }

        private void UnsubscribeNetworkEvents()
        {
            if (!_netSubscribed) return;
            _netSubscribed = false;
            if (PizzeriaNetworkState.Instance != null)
            {
                PizzeriaNetworkState.Instance.StorageSlots.OnListChanged -= OnStorageListChanged;
                PizzeriaNetworkState.Instance.Tools.OnListChanged -= OnToolsListChanged;
                PizzeriaNetworkState.Instance.SupplyOrders.OnListChanged -= OnSupplyOrdersChanged;
            }
        }

        private void OnSupplyOrdersChanged(Unity.Netcode.NetworkListEvent<NetSupplyDto> changeEvent)
        {
            if (_isOpen)
            {
                RefreshItems();
            }
        }

        private void OnStorageListChanged(Unity.Netcode.NetworkListEvent<NetStorageDto> changeEvent)
        {
            if (_isPending && _isOpen)
            {
                bool matchesMyIngredient = _pendingIngredientId >= 0 &&
                    changeEvent.Value.StorageId == _pendingStorageId &&
                    changeEvent.Value.IngredientDefId == _pendingIngredientId &&
                    changeEvent.Value.Quantity < _pendingPreviousQuantity;
                if (matchesMyIngredient)
                    OnAuthoritativeSuccess();
                else
                    RefreshItems();
            }
            else if (_isOpen)
            {
                RefreshItems();
            }
        }

        private void OnToolsListChanged(Unity.Netcode.NetworkListEvent<NetToolDto> changeEvent)
        {
            if (_isPending && _isOpen)
            {
                ulong myId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
                bool matchesMyTool = _pendingToolId != 0 &&
                    changeEvent.Value.ToolId == _pendingToolId &&
                    changeEvent.Value.LocationType == (byte)LocationType.Hand &&
                    changeEvent.Value.HolderId == myId;
                if (matchesMyTool)
                    OnAuthoritativeSuccess();
                else
                    RefreshItems();
            }
            else if (_isOpen)
            {
                RefreshItems();
            }
        }

        private void OnAuthoritativeSuccess()
        {
            _isPending = false;
            _pendingStorageId = -1;
            _pendingIngredientId = -1;
            _pendingPreviousQuantity = -1;
            _pendingToolId = 0;
            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
            }
            UIFeedbackService.Instance?.PlaySuccess();
            Close();
        }

        private System.Collections.IEnumerator PendingTimeoutCoroutine()
        {
            yield return new WaitForSeconds(3.0f);
            if (_isPending && _isOpen)
            {
                _isPending = false;
                _pendingStorageId = -1;
                _pendingIngredientId = -1;
                _pendingPreviousQuantity = -1;
                _pendingToolId = 0;
                if (_statusLabel != null) _statusLabel.text = "Tempo esgotado. Tente novamente.";
                _storageList?.SetEnabled(true);
                UIFeedbackService.Instance?.PlayError();
            }
            _timeoutCoroutine = null;
        }

        public void ResolveUIDocument()
        {
            PiariaPresenterUtils.ResolveUIDocument(this, ref uiDocument, "Assets/Piaaria/UI/Piaria/UXML/MainGameHUD.uxml");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveUIDocument();
        }

        private void Reset()
        {
            ResolveUIDocument();
        }
#endif

        private void Start()
        {
            var activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeSceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                activeSceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enabled = false;
                return;
            }

            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (RootElement != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            RootElement = uiDocument.rootVisualElement.Q<VisualElement>("StorageModalRoot");
            if (RootElement == null)
            {
                var template = PiariaPresenterUtils.ResolveTemplate(
                    c => c.templateStorageModal,
                    "Assets/Piaaria/UI/Piaria/UXML/StorageModal.uxml");
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    RootElement = uiDocument.rootVisualElement.Q<VisualElement>("StorageModalRoot");
                }
            }
            if (RootElement == null) return;

            _storageCard = RootElement.Q<VisualElement>("StorageCard");
            _storageTitle = RootElement.Q<Label>("StorageTitle");
            _btnClose = RootElement.Q<Button>("BtnCloseStorage");
            _btnSelectLeft = RootElement.Q<Button>("BtnSelectLeftHand");
            _btnSelectRight = RootElement.Q<Button>("BtnSelectRightHand");
            _storageList = RootElement.Q<ScrollView>("StorageList");
            _statusLabel = RootElement.Q<Label>("StorageStatusLabel");

            ApplyCatalogAssets();
            BindEvents();

            RootElement.style.display = DisplayStyle.None;
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null || _storageCard == null) return;

            if (catalog.paperPanel1 != null)
                _storageCard.style.backgroundImage = new StyleBackground(catalog.paperPanel1);
        }

        private void BindEvents()
        {
            if (_btnClose != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnClose);
                _btnClose.clicked += Close;
            }

            if (_btnSelectLeft != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnSelectLeft);
                _btnSelectLeft.clicked += () => SelectHand(HandSlotIndex.Left);
            }

            if (_btnSelectRight != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnSelectRight);
                _btnSelectRight.clicked += () => SelectHand(HandSlotIndex.Right);
            }
        }

        private void SelectHand(HandSlotIndex hand)
        {
            _selectedHand = hand;
            if (_btnSelectLeft != null)
            {
                if (hand == HandSlotIndex.Left) _btnSelectLeft.AddToClassList("hand-btn-active");
                else _btnSelectLeft.RemoveFromClassList("hand-btn-active");
            }
            if (_btnSelectRight != null)
            {
                if (hand == HandSlotIndex.Right) _btnSelectRight.AddToClassList("hand-btn-active");
                else _btnSelectRight.RemoveFromClassList("hand-btn-active");
            }
        }

        public void Open(int stationId)
        {
            EnsureInitialized();
            if (RootElement == null) return;

            SubscribeNetworkEvents();
            _currentStationId = stationId;
            _isOpen = true;
            _isPending = false;
            if (_statusLabel != null) _statusLabel.text = "";
            _storageList?.SetEnabled(true);

            if (_storageTitle != null)
                _storageTitle.text = $"ESTOQUE — ARMAZÉM #{stationId}";

            // Pega a mão ativa do jogador local por padrão
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var adapter = localPlayer != null ? localPlayer.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>() : null;
            if (adapter != null)
            {
                SelectHand(adapter.ActiveHand);
            }

            RefreshItems();

            UIModalStack.Instance?.PushModal(this);
            var curr = RootElement;
            while (curr != null && curr != uiDocument?.rootVisualElement)
            {
                curr.style.display = DisplayStyle.Flex;
                curr = curr.parent;
            }
            UIToolkitTweenService.PopIn(_storageCard != null ? _storageCard : RootElement, 0.2f);
        }

        public void Close()
        {
            if (!_isOpen || RootElement == null) return;
            _isOpen = false;

            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
            }
            _isPending = false;

            if (_currentStationId >= 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                PizzeriaCommandHandler.Instance?.CloseStorageSessionServerRpc(_currentStationId);
            }
            _currentStationId = -1;

            UIModalStack.Instance?.PopModal(this);
            UIToolkitTweenService.PopOut(_storageCard != null ? _storageCard : RootElement, 0.15f, 0.95f, () =>
            {
                RootElement.style.display = DisplayStyle.None;
                if (RootElement.parent != null && RootElement.parent != uiDocument?.rootVisualElement)
                {
                    RootElement.parent.style.display = DisplayStyle.None;
                }
            });
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Fecha automaticamente se o jogador se afastar mais de 4 metros
            if (_currentStationId >= 0)
            {
                var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
                if (localPlayer != null && StationRegistry.TryGetStation(_currentStationId, out var stTrans) && stTrans != null)
                {
                    float distSq = (localPlayer.transform.position - stTrans.position).sqrMagnitude;
                    if (distSq > 16f)
                    {
                        Close();
                    }
                }
            }
        }

        private void RefreshItems()
        {
            if (_storageList == null) return;
            _storageList.Clear();

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

            var catalog = PizzeriaRoot.Instance?.Catalog;
            int count = 0;

            // 1. Ingredientes no armazém (com nível visual e alerta de estoque O8/N6)
            for (int i = 0; i < netState.StorageSlots.Count; i++)
            {
                var slot = netState.StorageSlots[i];
                if (slot.StorageId == _currentStationId)
                {
                    count++;
                    string ingName = catalog != null && catalog.Ingredients.TryGetValue(slot.IngredientDefId, out var def) ? def.Name : $"Ingrediente #{slot.IngredientDefId}";
                    int ingId = slot.IngredientDefId;
                    int maxCap = Mathf.Max(1, slot.MaxCapacity);
                    float ratio = Mathf.Clamp01((float)slot.Quantity / maxCap);
                    string stockStatus = slot.Quantity <= 0
                        ? "ESGOTADO"
                        : (slot.Quantity <= 3 ? "CRÍTICO" : (ratio <= 0.35f ? "BAIXO" : "OK"));
                    Color statusColor = slot.Quantity <= 3
                        ? new Color(0.88f, 0.22f, 0.20f)
                        : (ratio <= 0.35f ? new Color(0.92f, 0.65f, 0.16f) : new Color(0.22f, 0.72f, 0.34f));

                    int currentQty = slot.Quantity;
                    var row = CreateItemRow(
                        ingName,
                        $"Qtd: {slot.Quantity}/{maxCap} [{stockStatus}]",
                        () =>
                        {
                            if (_isPending) return;
                            _isPending = true;
                            _pendingStorageId = _currentStationId;
                            _pendingIngredientId = ingId;
                            _pendingPreviousQuantity = currentQty;
                            _pendingToolId = 0;
                            _storageList?.SetEnabled(false);
                            if (_statusLabel != null) _statusLabel.text = "Retirando ingrediente...";

                            PizzeriaCommandHandler.Instance?.DispenseIngredientServerRpc(_currentStationId, ingId, (byte)_selectedHand);
                            _timeoutCoroutine = StartCoroutine(PendingTimeoutCoroutine());
                        },
                        ratio,
                        statusColor,
                        canTake: slot.Quantity > 0);

                    _storageList.Add(row);
                }
            }

            // 2. Ferramentas no armazém
            for (int i = 0; i < netState.Tools.Count; i++)
            {
                var tool = netState.Tools[i];
                if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)_currentStationId)
                {
                    count++;
                    string toolName = tool.DefinitionId == 1 ? "Ralador de Queijo" : "Faca de Corte";
                    ulong tId = tool.ToolId;

                    var row = CreateItemRow(toolName, "Ferramenta", () =>
                    {
                        if (_isPending) return;
                        _isPending = true;
                        _pendingStorageId = -1;
                        _pendingIngredientId = -1;
                        _pendingPreviousQuantity = -1;
                        _pendingToolId = tId;
                        _storageList?.SetEnabled(false);
                        if (_statusLabel != null) _statusLabel.text = "Pegando ferramenta...";

                        if (NetworkManager.Singleton != null)
                        {
                            PizzeriaCommandHandler.Instance?.TransferToolServerRpc(tId, (byte)LocationType.Hand, NetworkManager.Singleton.LocalClientId, (int)_selectedHand);
                            _timeoutCoroutine = StartCoroutine(PendingTimeoutCoroutine());
                        }
                    });

                    _storageList.Add(row);
                }
            }

            // 3. Pedidos de reposição ativos (O8 / N6)
            if (netState.SupplyOrders != null && netState.SupplyOrders.Count > 0)
            {
                var supHeader = new Label("REPOSIÇÃO DE SUPRIMENTOS:");
                supHeader.style.fontSize = 15;
                supHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                supHeader.style.color = new Color(0.25f, 0.2f, 0.15f);
                supHeader.style.marginTop = 10;
                supHeader.style.marginBottom = 4;
                _storageList.Add(supHeader);

                double now = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                    ? NetworkManager.Singleton.ServerTime.Time
                    : Time.timeAsDouble;

                for (int i = 0; i < netState.SupplyOrders.Count; i++)
                {
                    var sup = netState.SupplyOrders[i];
                    var st = (SupplyOrderLifecycle)sup.State;
                    if (st == SupplyOrderLifecycle.Unloaded) continue;

                    string statusDesc;
                    if (st == SupplyOrderLifecycle.InTransit)
                    {
                        int secs = Mathf.Max(0, Mathf.CeilToInt((float)(sup.ExpectedArrivalTime - now)));
                        statusDesc = $"Em trânsito ({secs}s restantes)";
                    }
                    else if (st == SupplyOrderLifecycle.Arrived)
                    {
                        statusDesc = $"Na doca (Baía #{sup.ReceivingSlotIndex + 1}) — pronto para descarregar";
                    }
                    else
                    {
                        statusDesc = "Aguardando baía de recebimento";
                    }

                    var supLbl = new Label($"• Pedido #{sup.SupplyOrderId} (R$ {sup.TotalCost}): {statusDesc}");
                    supLbl.style.fontSize = 14;
                    supLbl.style.color = new Color(0.22f, 0.35f, 0.55f);
                    supLbl.style.marginBottom = 2;
                    _storageList.Add(supLbl);
                }
            }

            if (count == 0)
            {
                var emptyLbl = new Label("Estoque vazio neste compartimento.");
                emptyLbl.style.fontSize = 17;
                emptyLbl.style.color = new Color(0.5f, 0.4f, 0.3f);
                emptyLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLbl.style.marginTop = 30;
                _storageList.Add(emptyLbl);
            }
        }

        private VisualElement CreateItemRow(string title, string badge, Action onTake, float fillRatio = -1f, Color? fillColor = null, bool canTake = true)
        {
            var row = new VisualElement();
            row.AddToClassList("storage-item-row");

            var info = new VisualElement();
            info.AddToClassList("storage-item-info");

            var titleLbl = new Label(title);
            titleLbl.AddToClassList("storage-item-name");

            var qtyLbl = new Label(badge);
            qtyLbl.AddToClassList("storage-item-qty");
            if (fillColor.HasValue)
            {
                qtyLbl.style.color = fillColor.Value;
                qtyLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            info.Add(titleLbl);
            info.Add(qtyLbl);

            if (fillRatio >= 0f)
            {
                var barBg = new VisualElement();
                barBg.style.height = 6;
                barBg.style.width = 180;
                barBg.style.marginTop = 3;
                barBg.style.backgroundColor = new Color(0.18f, 0.16f, 0.14f, 0.7f);
                barBg.style.borderTopLeftRadius = 3;
                barBg.style.borderTopRightRadius = 3;
                barBg.style.borderBottomLeftRadius = 3;
                barBg.style.borderBottomRightRadius = 3;

                var barFill = new VisualElement();
                barFill.style.height = 6;
                barFill.style.width = new Length(Mathf.Clamp01(fillRatio) * 100f, LengthUnit.Percent);
                barFill.style.backgroundColor = fillColor ?? new Color(0.22f, 0.72f, 0.34f);
                barFill.style.borderTopLeftRadius = 3;
                barFill.style.borderTopRightRadius = 3;
                barFill.style.borderBottomLeftRadius = 3;
                barFill.style.borderBottomRightRadius = 3;
                barBg.Add(barFill);
                info.Add(barBg);
            }

            var takeBtn = new Button(onTake) { text = canTake ? "PEGAR" : "VAZIO" };
            takeBtn.SetEnabled(canTake);
            takeBtn.AddToClassList("piaria-button");
            takeBtn.AddToClassList("piaria-button-primary");
            takeBtn.AddToClassList("btn-take-item");
            UIFeedbackService.Instance?.BindButton(takeBtn);

            row.Add(info);
            row.Add(takeBtn);
            return row;
        }
    }
}

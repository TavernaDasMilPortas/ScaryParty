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

        private void OnDestroy()
        {
            UnsubscribeNetworkEvents();
            if (Instance == this) Instance = null;
        }

        private void SubscribeNetworkEvents()
        {
            if (PizzeriaNetworkState.Instance != null)
            {
                PizzeriaNetworkState.Instance.StorageSlots.OnListChanged += OnStorageListChanged;
                PizzeriaNetworkState.Instance.Tools.OnListChanged += OnToolsListChanged;
            }
        }

        private void UnsubscribeNetworkEvents()
        {
            if (PizzeriaNetworkState.Instance != null)
            {
                PizzeriaNetworkState.Instance.StorageSlots.OnListChanged -= OnStorageListChanged;
                PizzeriaNetworkState.Instance.Tools.OnListChanged -= OnToolsListChanged;
            }
        }

        private void OnStorageListChanged(Unity.Netcode.NetworkListEvent<NetStorageDto> changeEvent)
        {
            if (_isPending && _isOpen)
            {
                OnAuthoritativeSuccess();
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
                OnAuthoritativeSuccess();
            }
            else if (_isOpen)
            {
                RefreshItems();
            }
        }

        private void OnAuthoritativeSuccess()
        {
            _isPending = false;
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
                if (_statusLabel != null) _statusLabel.text = "Tempo esgotado. Tente novamente.";
                _storageList?.SetEnabled(true);
                UIFeedbackService.Instance?.PlayError();
            }
            _timeoutCoroutine = null;
        }

        private void ResolveUIDocument()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
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
                var template = Resources.Load<VisualTreeAsset>("StorageModal")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/StorageModal.uxml")
#endif
                    ;
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

            UIToolkitTweenService.PopOut(_storageCard != null ? _storageCard : RootElement, 0.15f, 0.95f, () =>
            {
                RootElement.style.display = DisplayStyle.None;
                if (RootElement.parent != null && RootElement.parent != uiDocument?.rootVisualElement)
                {
                    RootElement.parent.style.display = DisplayStyle.None;
                }
                UIModalStack.Instance?.PopModal(this);
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

            // 1. Ingredientes no armazém
            for (int i = 0; i < netState.StorageSlots.Count; i++)
            {
                var slot = netState.StorageSlots[i];
                if (slot.StorageId == _currentStationId && slot.Quantity > 0)
                {
                    count++;
                    string ingName = catalog != null && catalog.Ingredients.TryGetValue(slot.IngredientDefId, out var def) ? def.Name : $"Ingrediente #{slot.IngredientDefId}";
                    int ingId = slot.IngredientDefId;

                    VisualElement row = null;
                    row = CreateItemRow(ingName, $"Qtd: {slot.Quantity}", () =>
                    {
                        if (_isPending) return;
                        _isPending = true;
                        _storageList?.SetEnabled(false);
                        if (_statusLabel != null) _statusLabel.text = "Retirando ingrediente...";

                        PizzeriaCommandHandler.Instance?.DispenseIngredientServerRpc(_currentStationId, ingId, (byte)_selectedHand);
                        _timeoutCoroutine = StartCoroutine(PendingTimeoutCoroutine());
                    });

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

        private VisualElement CreateItemRow(string title, string badge, Action onTake)
        {
            var row = new VisualElement();
            row.AddToClassList("storage-item-row");

            var info = new VisualElement();
            info.AddToClassList("storage-item-info");

            var titleLbl = new Label(title);
            titleLbl.AddToClassList("storage-item-name");

            var qtyLbl = new Label(badge);
            qtyLbl.AddToClassList("storage-item-qty");

            info.Add(titleLbl);
            info.Add(qtyLbl);

            var takeBtn = new Button(onTake) { text = "PEGAR" };
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

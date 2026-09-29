using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Presentation
{
    public class PizzeriaHudBuilder : MonoBehaviour
    {
        public static PizzeriaHudBuilder Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        
        private VisualElement _storagePanel;
        private VisualElement _stagingPanel;
        private VisualElement _devPanel;

        private int _storageStationId;
        private ulong _stagingBoxId;
        private int _stagingSlotIndex;
        private int _selectedDestinationIndex = 4;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            EnsureUIInitialized();
        }

        private void EnsureUIInitialized()
        {
            if (_storagePanel != null) return;

            // 1. Prioridade: Conectar diretamente ao rootVisualElement do UIManager existente
            if (UIManager.Instance != null && UIManager.Instance.uiDocument != null && UIManager.Instance.uiDocument.rootVisualElement != null)
            {
                _root = UIManager.Instance.uiDocument.rootVisualElement;
            }
            else
            {
                // 2. Fallback: UIDocument próprio com PanelSettings carregado dinamicamente
                if (_doc == null)
                    _doc = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

                if (_doc.panelSettings == null)
                {
                    if (UIManager.Instance != null && UIManager.Instance.uiDocument != null && UIManager.Instance.uiDocument.panelSettings != null)
                    {
                        _doc.panelSettings = UIManager.Instance.uiDocument.panelSettings;
                    }
                    else
                    {
                        var panelSettingsList = Resources.FindObjectsOfTypeAll<PanelSettings>();
                        if (panelSettingsList != null && panelSettingsList.Length > 0)
                        {
                            _doc.panelSettings = panelSettingsList[0];
                        }
                    }
                }

                if (_doc.rootVisualElement != null)
                {
                    _root = _doc.rootVisualElement;
                }
            }

            if (_root == null)
            {
                return; // Tentará novamente quando UIManager estiver pronto
            }

            CreateStoragePanel();
            CreateStagingPanel();
            CreateDevPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_devPanel == null)
            {
                EnsureUIInitialized();
            }

            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
            {
                if (_devPanel != null)
                {
                    _devPanel.style.display = _devPanel.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
                    UpdateUIMode();
                }
            }
        }

        private void CreateStoragePanel()
        {
            _storagePanel = new VisualElement();
            _storagePanel.style.position = Position.Absolute;
            _storagePanel.style.width = 400;
            _storagePanel.style.height = 300;
            _storagePanel.style.left = new Length(50, LengthUnit.Percent);
            _storagePanel.style.top = new Length(50, LengthUnit.Percent);
            _storagePanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
            _storagePanel.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            _storagePanel.style.display = DisplayStyle.None;
            _storagePanel.style.paddingLeft = 10;
            _storagePanel.style.paddingRight = 10;
            _storagePanel.style.paddingTop = 10;
            _storagePanel.style.paddingBottom = 10;

            var title = new Label("ESTOQUE");
            title.style.color = Color.white;
            title.style.fontSize = 20;
            _storagePanel.Add(title);

            var closeBtn = new Button(() => { CloseStoragePicker(); });
            closeBtn.text = "X";
            closeBtn.style.position = Position.Absolute;
            closeBtn.style.right = 10;
            closeBtn.style.top = 10;
            _storagePanel.Add(closeBtn);

            _root.Add(_storagePanel);
        }

        public void OpenStoragePicker(int stationId)
        {
            EnsureUIInitialized();
            _storageStationId = stationId;
            if (_storagePanel == null)
            {
                Debug.LogWarning("[PizzeriaHudBuilder] Impossível abrir estoque: _storagePanel não pôde ser inicializado.");
                return;
            }
            RefreshStoragePanel();
            _storagePanel.style.display = DisplayStyle.Flex;
            SetUIMode(true);
        }

        public void CloseStoragePicker()
        {
            if (_storagePanel != null)
                _storagePanel.style.display = DisplayStyle.None;
            UpdateUIMode();
        }

        private void RefreshStoragePanel()
        {
            _storagePanel.Clear();
            var title = new Label($"ESTOQUE (Armazém #{_storageStationId})");
            title.style.color = Color.white;
            title.style.fontSize = 20;
            _storagePanel.Add(title);

            var closeBtn = new Button(() => { CloseStoragePicker(); });
            closeBtn.text = "X";
            closeBtn.style.position = Position.Absolute;
            closeBtn.style.right = 10;
            closeBtn.style.top = 10;
            _storagePanel.Add(closeBtn);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 20;
            _storagePanel.Add(scroll);

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;
            var catalog = PizzeriaRoot.Instance?.Catalog;

            for (int i = 0; i < netState.StorageSlots.Count; i++)
            {
                var slot = netState.StorageSlots[i];
                if (slot.StorageId == _storageStationId && slot.Quantity > 0)
                {
                    string ingName = catalog != null && catalog.Ingredients.TryGetValue(slot.IngredientDefId, out var def) ? def.Name : $"Ingrediente {slot.IngredientDefId}";
                    
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.marginBottom = 5;

                    var lbl = new Label($"{ingName} (Qtd: {slot.Quantity})");
                    lbl.style.color = Color.white;
                    row.Add(lbl);

                    int capturedIngId = slot.IngredientDefId;
                    int capturedStorageId = _storageStationId;
                    var btn = new Button(() => {
                        var cmd = PizzeriaCommandHandler.Instance;
                        var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
                        var adapter = localPlayer != null ? localPlayer.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>() : null;
                        if (cmd != null && adapter != null)
                        {
                            cmd.DispenseIngredientServerRpc(capturedStorageId, capturedIngId, (byte)adapter.ActiveHand);
                            CloseStoragePicker();
                        }
                    });
                    btn.text = "Pegar";
                    row.Add(btn);

                    scroll.Add(row);
                }
            }

            for (int i = 0; i < netState.Tools.Count; i++)
            {
                var tool = netState.Tools[i];
                if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)_storageStationId)
                {
                    string toolName = tool.DefinitionId == 1 ? "Ralador" : "Faca";
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.marginBottom = 5;

                    var lbl = new Label($"{toolName}");
                    lbl.style.color = Color.white;
                    row.Add(lbl);

                    ulong capturedToolId = tool.ToolId;
                    var btn = new Button(() => {
                        var cmd = PizzeriaCommandHandler.Instance;
                        var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
                        var adapter = localPlayer != null ? localPlayer.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>() : null;
                        if (cmd != null && adapter != null && NetworkManager.Singleton != null)
                        {
                            cmd.TransferToolServerRpc(capturedToolId, (byte)LocationType.Hand, NetworkManager.Singleton.LocalClientId, (int)adapter.ActiveHand);
                            CloseStoragePicker();
                        }
                    });
                    btn.text = "Pegar";
                    row.Add(btn);

                    scroll.Add(row);
                }
            }

            if (scroll.childCount == 0)
            {
                var emptyLbl = new Label("Estoque vazio neste compartimento.");
                emptyLbl.style.color = new Color(0.8f, 0.8f, 0.8f);
                emptyLbl.style.marginTop = 20;
                emptyLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                scroll.Add(emptyLbl);
            }
        }

        private void CreateStagingPanel()
        {
            _stagingPanel = new VisualElement();
            _stagingPanel.style.position = Position.Absolute;
            _stagingPanel.style.width = 350;
            _stagingPanel.style.height = 240;
            _stagingPanel.style.left = new Length(50, LengthUnit.Percent);
            _stagingPanel.style.top = new Length(50, LengthUnit.Percent);
            _stagingPanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
            _stagingPanel.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            _stagingPanel.style.display = DisplayStyle.None;
            _stagingPanel.style.paddingLeft = 10;
            _stagingPanel.style.paddingRight = 10;
            _stagingPanel.style.paddingTop = 10;
            _stagingPanel.style.paddingBottom = 10;
            _stagingPanel.style.alignItems = Align.Center;

            var title = new Label("ETIQUETAR CAIXA");
            title.style.color = Color.white;
            title.style.fontSize = 18;
            _stagingPanel.Add(title);

            var desc = new Label("Escolha o endereço de entrega para esta caixa.");
            desc.style.color = Color.white;
            desc.style.marginTop = 20;
            _stagingPanel.Add(desc);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 20;
            row.style.alignItems = Align.Center;

            var lblDest = new Label($"Endereço: #{_selectedDestinationIndex}");
            lblDest.style.color = Color.white;
            row.Add(lblDest);

            var btnMinus = new Button(() => {
                if (_selectedDestinationIndex > 1) _selectedDestinationIndex--;
                lblDest.text = $"Endereço: #{_selectedDestinationIndex}";
            });
            btnMinus.text = "-";
            row.Add(btnMinus);

            var btnPlus = new Button(() => {
                _selectedDestinationIndex++;
                lblDest.text = $"Endereço: #{_selectedDestinationIndex}";
            });
            btnPlus.text = "+";
            row.Add(btnPlus);

            _stagingPanel.Add(row);

            var btnConfirm = new Button(() => {
                var cmd = PizzeriaCommandHandler.Instance;
                if (cmd != null)
                {
                    cmd.ConfirmStageBoxServerRpc(_stagingBoxId, _stagingSlotIndex, _selectedDestinationIndex);
                }
                _stagingPanel.style.display = DisplayStyle.None;
                UpdateUIMode();
            });
            btnConfirm.text = "Confirmar Etiqueta";
            btnConfirm.style.marginTop = 20;
            _stagingPanel.Add(btnConfirm);

            var btnCancel = new Button(() => {
                _stagingPanel.style.display = DisplayStyle.None;
                UpdateUIMode();
            });
            btnCancel.text = "Cancelar";
            btnCancel.style.marginTop = 10;
            _stagingPanel.Add(btnCancel);

            _root.Add(_stagingPanel);
        }

        public void OpenStagingAddressPicker(ulong boxId, int slotIndex)
        {
            EnsureUIInitialized();
            _stagingBoxId = boxId;
            _stagingSlotIndex = slotIndex;
            if (_stagingPanel != null)
            {
                _stagingPanel.style.display = DisplayStyle.Flex;
                SetUIMode(true);
            }
        }

        private void CreateDevPanel()
        {
            _devPanel = new VisualElement();
            _devPanel.style.position = Position.Absolute;
            _devPanel.style.width = 310;
            _devPanel.style.height = 420;
            _devPanel.style.right = 10;
            _devPanel.style.top = 10;
            _devPanel.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            _devPanel.style.display = DisplayStyle.None;
            _devPanel.style.paddingLeft = 10;
            _devPanel.style.paddingRight = 10;
            _devPanel.style.paddingTop = 10;
            _devPanel.style.paddingBottom = 10;

            var title = new Label("PAINEL DEV - PIZZERIA (F9)");
            title.style.color = Color.white;
            title.style.fontSize = 16;
            _devPanel.Add(title);

            AddDevButton("Atender Pedido Teste (#4: Calabresa+Muss)", () => {
                PizzeriaCommandHandler.Instance?.AnswerPhoneServerRpc(12);
            });
            AddDevButton("Comprar Suprimento Queijo (5x)", () => {
                PizzeriaCommandHandler.Instance?.PurchaseSupplyServerRpc(3, 1);
            });
            AddDevButton("Comprar Suprimento Calabresa (5x)", () => {
                PizzeriaCommandHandler.Instance?.PurchaseSupplyServerRpc(4, 1);
            });

            var upgradesTitle = new Label("Upgrades Demonstráveis:");
            upgradesTitle.style.color = Color.white;
            upgradesTitle.style.marginTop = 10;
            _devPanel.Add(upgradesTitle);

            AddDevButton("Forno Rápido (-20% tempo)", () => { PizzeriaCommandHandler.Instance?.SetUpgradeLevelServerRpc(1, 1); });
            AddDevButton("Slot Forno Extra (+1 slot)", () => { PizzeriaCommandHandler.Instance?.SetUpgradeLevelServerRpc(2, 1); });
            AddDevButton("Mochila Maior (+1 slot)", () => { PizzeriaCommandHandler.Instance?.SetUpgradeLevelServerRpc(3, 1); });
            AddDevButton("Desbloquear Cogumelo", () => { PizzeriaCommandHandler.Instance?.SetUpgradeLevelServerRpc(5, 1); });

            AddDevButton("Fechar Painel (F9)", () => {
                _devPanel.style.display = DisplayStyle.None;
                UpdateUIMode();
            });

            _root.Add(_devPanel);
        }

        private void AddDevButton(string text, System.Action action)
        {
            var btn = new Button(action);
            btn.text = text;
            btn.style.marginTop = 5;
            _devPanel.Add(btn);
        }

        private void SetUIMode(bool interacting)
        {
            if (interacting)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }

        private void UpdateUIMode()
        {
            bool anyOpen = (_storagePanel != null && _storagePanel.style.display == DisplayStyle.Flex) ||
                           (_stagingPanel != null && _stagingPanel.style.display == DisplayStyle.Flex) ||
                           (_devPanel != null && _devPanel.style.display == DisplayStyle.Flex);

            if (!anyOpen)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
            else
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }
    }
}

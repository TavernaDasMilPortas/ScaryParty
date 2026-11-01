using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.UI.Piaria.Presenters
{
    public class StagingPresenter : MonoBehaviour, IModalView
    {
        public static StagingPresenter Instance { get; private set; }

        public bool CanCloseWithEscape => true;
        public VisualElement RootElement { get; private set; }

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;

        private VisualElement _stagingCard;
        private Label _selectedDestLabel;
        private Label _statusLabel;
        private Button _btnPrev;
        private Button _btnNext;
        private Button _btnConfirm;
        private Button _btnCancel;

        private ulong _currentBoxId;
        private int _currentSlotIndex;
        private int _currentDestIndex = 1;
        private readonly List<int> _availableDestinations = new List<int>();
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
                Destroy(this);
                return;
            }

            ResolveUIDocument();
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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void EnsureInitialized()
        {
            if (RootElement != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            RootElement = uiDocument.rootVisualElement.Q<VisualElement>("StagingModalRoot");
            if (RootElement == null)
            {
                var template = Resources.Load<VisualTreeAsset>("StagingModal")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/StagingModal.uxml")
#endif
                    ;
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    RootElement = uiDocument.rootVisualElement.Q<VisualElement>("StagingModalRoot");
                }
            }
            if (RootElement == null) return;

            _stagingCard = RootElement.Q<VisualElement>("StagingCard");
            _selectedDestLabel = RootElement.Q<Label>("SelectedDestLabel");
            _statusLabel = RootElement.Q<Label>("StagingStatusLabel");
            _btnPrev = RootElement.Q<Button>("BtnPrevDest");
            _btnNext = RootElement.Q<Button>("BtnNextDest");
            _btnConfirm = RootElement.Q<Button>("BtnConfirmStaging");
            _btnCancel = RootElement.Q<Button>("BtnCancelStaging");

            ApplyCatalogAssets();
            BindEvents();

            RootElement.style.display = DisplayStyle.None;
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null || _stagingCard == null) return;

            if (catalog.paperPanel2 != null)
                _stagingCard.style.backgroundImage = new StyleBackground(catalog.paperPanel2);
        }

        private void BindEvents()
        {
            if (_btnPrev != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnPrev);
                _btnPrev.clicked += () => ChangeDestination(-1);
            }

            if (_btnNext != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnNext);
                _btnNext.clicked += () => ChangeDestination(1);
            }

            if (_btnConfirm != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnConfirm);
                _btnConfirm.clicked += ConfirmStaging;
            }

            if (_btnCancel != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnCancel);
                _btnCancel.clicked += Close;
            }
        }

        public void Open(ulong boxId, int slotIndex)
        {
            EnsureInitialized();
            if (RootElement == null) return;

            _currentBoxId = boxId;
            _currentSlotIndex = slotIndex;
            _isOpen = true;
            if (_statusLabel != null) _statusLabel.text = "";

            CollectValidDestinations();
            UpdateDestinationDisplay();

            UIModalStack.Instance?.PushModal(this);
            RootElement.style.display = DisplayStyle.Flex;
            UIToolkitTweenService.PopIn(_stagingCard != null ? _stagingCard : RootElement, 0.2f);
        }

        public void Close()
        {
            if (!_isOpen || RootElement == null) return;
            _isOpen = false;

            UIToolkitTweenService.PopOut(_stagingCard != null ? _stagingCard : RootElement, 0.15f, 0.95f, () =>
            {
                RootElement.style.display = DisplayStyle.None;
                UIModalStack.Instance?.PopModal(this);
            });
        }

        private void CollectValidDestinations()
        {
            _availableDestinations.Clear();

            // Coleta os destinos das comandas ativas
            var netState = PizzeriaNetworkState.Instance;
            if (netState != null && netState.Orders != null)
            {
                for (int i = 0; i < netState.Orders.Count; i++)
                {
                    var o = netState.Orders[i];
                    if (o.DestinationId > 0 && !_availableDestinations.Contains(o.DestinationId))
                    {
                        _availableDestinations.Add(o.DestinationId);
                    }
                }
            }

            // Fallback: se nenhuma comanda estiver ativa, oferece opções padrão 1..8
            if (_availableDestinations.Count == 0)
            {
                for (int i = 1; i <= 8; i++) _availableDestinations.Add(i);
            }

            _availableDestinations.Sort();
            _currentDestIndex = 0;
        }

        private void ChangeDestination(int delta)
        {
            if (_availableDestinations.Count == 0) return;
            _currentDestIndex = (_currentDestIndex + delta + _availableDestinations.Count) % _availableDestinations.Count;
            UpdateDestinationDisplay();
        }

        private void UpdateDestinationDisplay()
        {
            if (_selectedDestLabel == null) return;
            if (_availableDestinations.Count > 0 && _currentDestIndex >= 0 && _currentDestIndex < _availableDestinations.Count)
            {
                int destId = _availableDestinations[_currentDestIndex];
                _selectedDestLabel.text = $"Endereço #{destId}";
            }
            else
            {
                _selectedDestLabel.text = "Destino #1";
            }
        }

        private void ConfirmStaging()
        {
            if (_availableDestinations.Count == 0) return;
            int destId = _availableDestinations[_currentDestIndex];

            if (_statusLabel != null) _statusLabel.text = "Afixando etiqueta...";
            var cmd = PizzeriaCommandHandler.Instance;
            if (cmd != null)
            {
                cmd.ConfirmStageBoxServerRpc(_currentBoxId, _currentSlotIndex, destId);
                UIFeedbackService.Instance?.PlaySuccess();
            }

            Close();
        }
    }
}

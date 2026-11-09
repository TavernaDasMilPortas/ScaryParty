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
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/StagingModal.uxml")
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

            var boxArt = RootElement.Q<VisualElement>("VisualPizzaBox");
            if (boxArt != null && catalog.GetBackpackPizzaSprite(1) != null)
            {
                boxArt.style.backgroundImage = new StyleBackground(catalog.GetBackpackPizzaSprite(1));
            }
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

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            UIModalStack.Instance?.PushModal(this);
            var curr = RootElement;
            while (curr != null && curr != uiDocument?.rootVisualElement)
            {
                curr.style.display = DisplayStyle.Flex;
                curr = curr.parent;
            }
            UIToolkitTweenService.PopIn(_stagingCard != null ? _stagingCard : RootElement, 0.2f);
        }

        public void Close()
        {
            if (!_isOpen || RootElement == null) return;
            _isOpen = false;

            UIToolkitTweenService.PopOut(_stagingCard != null ? _stagingCard : RootElement, 0.15f, 0.95f, () =>
            {
                RootElement.style.display = DisplayStyle.None;
                if (RootElement.parent != null && RootElement.parent != uiDocument?.rootVisualElement)
                {
                    RootElement.parent.style.display = DisplayStyle.None;
                }
                UIModalStack.Instance?.PopModal(this);
                if (UIModalStack.Instance == null || !UIModalStack.Instance.HasActiveModal)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            });
        }

        private void CollectValidDestinations()
        {
            _availableDestinations.Clear();

            // Coleta todos os endereços registrados na cidade (para permitir etiquetar para qualquer ponto, inclusive errado)
            var root = PizzeriaRoot.Instance;
            if (root != null && root.AddressRegistry != null && root.AddressRegistry.GetAllIds().Count > 0)
            {
                foreach (var addr in root.AddressRegistry.GetAllIds())
                {
                    if (addr.Value >= 0 && !_availableDestinations.Contains(addr.Value))
                        _availableDestinations.Add(addr.Value);
                }
            }

            // Fallback para DeliveryPoints presentes na cena
            if (_availableDestinations.Count == 0)
            {
                var points = FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None);
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] != null && points[i].AddressId >= 0 && !_availableDestinations.Contains(points[i].AddressId))
                        _availableDestinations.Add(points[i].AddressId);
                }
            }

            // Inclui também destinos de comandas ativas caso ainda não estejam na lista
            var netState = PizzeriaNetworkState.Instance;
            if (netState != null && netState.Orders != null)
            {
                for (int i = 0; i < netState.Orders.Count; i++)
                {
                    var o = netState.Orders[i];
                    if (o.DestinationId >= 0 && !_availableDestinations.Contains(o.DestinationId))
                    {
                        _availableDestinations.Add(o.DestinationId);
                    }
                }
            }

            // Fallback: se nenhum endereço foi encontrado, oferece opções padrão 0..7 (#1..#8)
            if (_availableDestinations.Count == 0)
            {
                for (int i = 0; i < 8; i++) _availableDestinations.Add(i);
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
                _selectedDestLabel.text = $"Endereço #{destId + 1}";
            }
            else
            {
                _selectedDestLabel.text = "Endereço #1";
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

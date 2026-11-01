using System;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    public class ModalPresenter : MonoBehaviour, IModalView
    {
        public static ModalPresenter Instance { get; private set; }

        public bool CanCloseWithEscape => true;
        public VisualElement RootElement { get; private set; }

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;

        private VisualElement _modalCard;
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _btnCancel;
        private Button _btnConfirm;

        private Action _onConfirmAction;
        private Action _onCancelAction;
        private bool _isOpen = false;

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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (RootElement != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            RootElement = uiDocument.rootVisualElement.Q<VisualElement>("GenericModalRoot");
            if (RootElement == null)
            {
                var template = Resources.Load<VisualTreeAsset>("GenericModal")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/GenericModal.uxml")
#endif
                    ;
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    RootElement = uiDocument.rootVisualElement.Q<VisualElement>("GenericModalRoot");
                }
            }
            if (RootElement == null) return;

            _modalCard = RootElement.Q<VisualElement>("GenericModalCard");
            _titleLabel = RootElement.Q<Label>("ModalTitle");
            _messageLabel = RootElement.Q<Label>("ModalMessage");
            _btnCancel = RootElement.Q<Button>("BtnModalCancel");
            _btnConfirm = RootElement.Q<Button>("BtnModalConfirm");

            ApplyCatalogAssets();
            BindEvents();

            RootElement.style.display = DisplayStyle.None;
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null || _modalCard == null) return;

            if (catalog.paperPanel2 != null)
                _modalCard.style.backgroundImage = new StyleBackground(catalog.paperPanel2);
        }

        private void BindEvents()
        {
            if (_btnConfirm != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnConfirm);
                _btnConfirm.clicked += () =>
                {
                    var cb = _onConfirmAction;
                    Close();
                    cb?.Invoke();
                };
            }

            if (_btnCancel != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnCancel);
                _btnCancel.clicked += () =>
                {
                    var cb = _onCancelAction;
                    Close();
                    cb?.Invoke();
                };
            }
        }

        public void ShowConfirmation(string message, Action onConfirm, Action onCancel = null, string confirmText = "CONFIRMAR", string cancelText = "CANCELAR", string title = "CONFIRMAÇÃO")
        {
            EnsureInitialized();
            if (RootElement == null)
            {
                onConfirm?.Invoke();
                return;
            }

            _onConfirmAction = onConfirm;
            _onCancelAction = onCancel;

            if (_titleLabel != null) _titleLabel.text = title;
            if (_messageLabel != null) _messageLabel.text = message;
            if (_btnConfirm != null) { _btnConfirm.text = confirmText; _btnConfirm.style.display = DisplayStyle.Flex; }
            if (_btnCancel != null) { _btnCancel.text = cancelText; _btnCancel.style.display = DisplayStyle.Flex; }

            _isOpen = true;
            UIModalStack.Instance?.PushModal(this);
            RootElement.style.display = DisplayStyle.Flex;
            UIToolkitTweenService.PopIn(_modalCard != null ? _modalCard : RootElement, 0.2f);
        }

        public void ShowAlert(string title, string message, Action onClose = null, string okText = "OK")
        {
            EnsureInitialized();
            if (RootElement == null)
            {
                onClose?.Invoke();
                return;
            }

            _onConfirmAction = onClose;
            _onCancelAction = onClose;

            if (_titleLabel != null) _titleLabel.text = title;
            if (_messageLabel != null) _messageLabel.text = message;
            if (_btnConfirm != null) { _btnConfirm.text = okText; _btnConfirm.style.display = DisplayStyle.Flex; }
            if (_btnCancel != null) { _btnCancel.style.display = DisplayStyle.None; }

            _isOpen = true;
            UIModalStack.Instance?.PushModal(this);
            RootElement.style.display = DisplayStyle.Flex;
            UIToolkitTweenService.PopIn(_modalCard != null ? _modalCard : RootElement, 0.2f);
        }

        public void Close()
        {
            if (!_isOpen || RootElement == null) return;
            _isOpen = false;

            UIToolkitTweenService.PopOut(_modalCard != null ? _modalCard : RootElement, 0.15f, 0.95f, () =>
            {
                RootElement.style.display = DisplayStyle.None;
                UIModalStack.Instance?.PopModal(this);
            });
        }
    }
}

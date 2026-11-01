using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Apresentador de notificações Toast em UI Toolkit.
    /// Substitui o antigo OnGUI do NotificationManager com animações suaves de entrada/saída,
    /// limite de fila e cores de acordo com a categoria da notificação.
    /// </summary>
    public class ToastPresenter : MonoBehaviour
    {
        public static ToastPresenter Instance { get; private set; }

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;

        private VisualElement _toastContainer;
        private readonly List<VisualElement> _activeToasts = new List<VisualElement>();
        private const int MaxActiveToasts = 4;
        private const float DefaultToastDuration = 3.2f;

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
            PiariaPresenterUtils.ResolveUIDocument(this, ref uiDocument, "Assets/UI/Piaria/UXML/LobbySceneContainer.uxml");
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
            NotificationManager.OnNotificationShown += HandleNotification;
        }

        private void OnDestroy()
        {
            NotificationManager.OnNotificationShown -= HandleNotification;
            if (Instance == this) Instance = null;
        }

        public void EnsureInitialized()
        {
            if (_toastContainer != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            _toastContainer = uiDocument.rootVisualElement.Q<VisualElement>("ToastContainerRoot");
            if (_toastContainer == null)
            {
                var template = Resources.Load<VisualTreeAsset>("ToastContainer")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/ToastContainer.uxml")
#endif
                    ;
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    _toastContainer = uiDocument.rootVisualElement.Q<VisualElement>("ToastContainerRoot");
                }
            }
        }

        private void HandleNotification(string message, NotificationType type)
        {
            EnsureInitialized();
            if (_toastContainer == null) return;

            // Se atingir o limite, remove o mais antigo
            if (_activeToasts.Count >= MaxActiveToasts)
            {
                var oldest = _activeToasts[0];
                _activeToasts.RemoveAt(0);
                UIToolkitTweenService.FadeOut(oldest, 0.1f, () => oldest.RemoveFromHierarchy());
            }

            var toast = new VisualElement();
            toast.AddToClassList("toast-item");

            string iconStr = "ℹ️";
            switch (type)
            {
                case NotificationType.Success:
                    toast.AddToClassList("toast-item-success");
                    iconStr = "✅";
                    break;
                case NotificationType.Warning:
                    toast.AddToClassList("toast-item-warning");
                    iconStr = "⚠️";
                    break;
                case NotificationType.Error:
                    toast.AddToClassList("toast-item-error");
                    iconStr = "❌";
                    break;
                default:
                    toast.AddToClassList("toast-item-info");
                    iconStr = "🍕";
                    break;
            }

            var iconLbl = new Label(iconStr);
            iconLbl.AddToClassList("toast-icon");

            var msgLbl = new Label(message);
            msgLbl.AddToClassList("toast-message");

            toast.Add(iconLbl);
            toast.Add(msgLbl);

            _toastContainer.Add(toast);
            _activeToasts.Add(toast);

            UIToolkitTweenService.SlideIn(toast, new Vector2(80, 0), 0.18f);
            StartCoroutine(DismissToastRoutine(toast, DefaultToastDuration));
        }

        private IEnumerator DismissToastRoutine(VisualElement toast, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (toast != null && _activeToasts.Contains(toast))
            {
                _activeToasts.Remove(toast);
                UIToolkitTweenService.SlideOut(toast, new Vector2(80, 0), 0.15f, () =>
                {
                    toast.RemoveFromHierarchy();
                });
            }
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

namespace ScaryParty.Pizzeria.Presentation
{
    public enum NotificationType { Info, Success, Warning, Error }

    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }
        public static event System.Action<string, NotificationType> OnNotificationShown;

        private class Toast
        {
            public string Message;
            public NotificationType Type;
            public float TimeRemaining;
        }

        private List<Toast> toasts = new List<Toast>();
        private const int MaxToasts = 4;
        private const float ToastDuration = 3f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void Show(string message, NotificationType type = NotificationType.Info)
        {
            OnNotificationShown?.Invoke(message, type);
            if (Instance != null)
            {
                Instance.AddToast(message, type);
            }
        }

        private void AddToast(string message, NotificationType type)
        {
            if (toasts.Count >= MaxToasts)
            {
                toasts.RemoveAt(0); // Remove oldest
            }
            toasts.Add(new Toast { Message = message, Type = type, TimeRemaining = ToastDuration });
        }

        private void Update()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].TimeRemaining -= Time.deltaTime;
                if (toasts[i].TimeRemaining <= 0)
                {
                    toasts.RemoveAt(i);
                }
            }
        }

        // OnGUI removido: todas as notificações são apresentadas via ToastPresenter em UI Toolkit.
    }
}

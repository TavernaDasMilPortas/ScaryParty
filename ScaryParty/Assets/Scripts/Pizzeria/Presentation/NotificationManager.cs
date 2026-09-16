using UnityEngine;
using System.Collections.Generic;

namespace ScaryParty.Pizzeria.Presentation
{
    public enum NotificationType { Info, Success, Warning, Error }

    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }

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

        private void OnGUI()
        {
            if (toasts.Count == 0) return;

            int toastHeight = 30;
            int spacing = 5;
            int width = 300;
            int startY = Screen.height - 50 - (toasts.Count * (toastHeight + spacing));

            for (int i = 0; i < toasts.Count; i++)
            {
                var toast = toasts[i];
                Color bgColor = Color.white;
                switch (toast.Type)
                {
                    case NotificationType.Success: bgColor = Color.green; break;
                    case NotificationType.Error: bgColor = Color.red; break;
                    case NotificationType.Warning: bgColor = Color.yellow; break;
                    case NotificationType.Info: bgColor = Color.white; break;
                }

                bgColor.a = 0.8f;
                GUI.backgroundColor = bgColor;

                GUIStyle style = new GUIStyle(GUI.skin.box);
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.black;
                style.fontStyle = FontStyle.Bold;

                float alpha = Mathf.Clamp01(toast.TimeRemaining / 0.5f);
                Color originalColor = GUI.color;
                GUI.color = new Color(1, 1, 1, alpha);

                Rect rect = new Rect((Screen.width - width) / 2, startY + (i * (toastHeight + spacing)), width, toastHeight);
                GUI.Box(rect, toast.Message, style);

                GUI.color = originalColor;
            }
        }
    }
}

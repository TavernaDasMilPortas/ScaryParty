using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Stations
{
    /// <summary>
    /// Ponto de atendimento de clientes presenciais no balcão (distinto de CounterStation que é bancada de cozinha).
    /// Também contém as âncoras de espera da fila correspondente (slots 0..N).
    /// Segurar [F] por 0,6 s atende o cliente no slot 0 da fila.
    /// </summary>
    public class CustomerCounterStation : MonoBehaviour, IInteractable
    {
        [Header("Configuração da Fila de Atendimento")]
        [Tooltip("Índice da fila (0 = Fila 1, 1 = Fila 2)")]
        public int queueIndex = 0;
        public string counterName = "Balcão de Atendimento";

        [Tooltip("Âncoras de posição na fila (índice 0 = frente do balcão, 1 = segundo da fila, etc.)")]
        public Transform[] queueSlotAnchors;

        private StationProgressBar _progressBar;

        private void Start()
        {
            EnsureQueueSlotAnchors();
            _progressBar = StationProgressBar.Create(transform);
            _progressBar.transform.position = GetQueueSlotPosition(0) + Vector3.up * 2.0f;
            _progressBar.Hide();
        }

        public void EnsureQueueSlotAnchors(int defaultSlots = 3)
        {
            if (queueSlotAnchors != null && queueSlotAnchors.Length > 0)
            {
                bool allValid = true;
                for (int i = 0; i < queueSlotAnchors.Length; i++)
                {
                    if (queueSlotAnchors[i] == null) { allValid = false; break; }
                }
                if (allValid) return;
            }

            var list = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < defaultSlots; i++)
            {
                string childName = $"QueueSlot_{i}";
                Transform existing = transform.Find(childName);
                if (existing == null)
                {
                    var go = new GameObject(childName);
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 0f, -1.1f * (i + 1));
                    go.transform.localRotation = Quaternion.identity;
                    existing = go.transform;
                }
                list.Add(existing);
            }
            queueSlotAnchors = list.ToArray();
        }

        public Vector3 GetQueueSlotPosition(int slotIndex)
        {
            EnsureQueueSlotAnchors();
            Vector3 rawPos;
            if (queueSlotAnchors != null && queueSlotAnchors.Length > 0)
            {
                int clamped = Mathf.Clamp(slotIndex, 0, queueSlotAnchors.Length - 1);
                rawPos = queueSlotAnchors[clamped] != null ? queueSlotAnchors[clamped].position : transform.position - transform.forward * (1.1f * (slotIndex + 1));
            }
            else
            {
                rawPos = transform.position - transform.forward * (1.1f * (slotIndex + 1));
            }
            return Presentation.CustomerSpawner.SnapToFloor(rawPos);
        }

        public Quaternion GetQueueSlotRotation(int slotIndex)
        {
            EnsureQueueSlotAnchors();
            if (queueSlotAnchors != null && queueSlotAnchors.Length > 0)
            {
                int clamped = Mathf.Clamp(slotIndex, 0, queueSlotAnchors.Length - 1);
                if (queueSlotAnchors[clamped] != null)
                    return queueSlotAnchors[clamped].rotation;
            }
            return transform.rotation;
        }

        public bool TryGetFrontCustomer(out NetCustomerDto frontCustomer)
        {
            frontCustomer = default;
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Customers == null) return false;

            for (int i = 0; i < netState.Customers.Count; i++)
            {
                var c = netState.Customers[i];
                if (c.QueueIndex == queueIndex && c.IsInQueue && c.QueueSlotIndex == 0)
                {
                    frontCustomer = c;
                    return true;
                }
            }
            return false;
        }

        public string InteractPrompt
        {
            get
            {
                if (TryGetFrontCustomer(out var front))
                {
                    var spec = front.RequestedSpec.ToDomain();
                    var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Catalog;
                    string summary = PizzaOrderFormatter.FormatShortSummary(spec, catalog);
                    return $"[Segure F] Atender Cliente (Fila {queueIndex + 1}) — {summary}";
                }
                return $"{counterName} (Fila {queueIndex + 1} vazia)";
            }
        }

        public void OnInteract(GameObject interactor)
        {
            if (TryGetFrontCustomer(out _))
            {
                NotificationManager.Show("Segure [F] por 0,6 s para anotar o pedido do cliente!", NotificationType.Info);
            }
            else
            {
                NotificationManager.Show($"Nenhum cliente aguardando na Fila {queueIndex + 1}.", NotificationType.Info);
            }
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        private void Update()
        {
            if (_progressBar == null) return;
            if (TryGetFrontCustomer(out var front) && front.AttendProgressNormalized > 0.01f && front.AttendProgressNormalized < 1f)
            {
                _progressBar.transform.position = GetQueueSlotPosition(0) + Vector3.up * 2.05f;
                _progressBar.SetProgress(front.AttendProgressNormalized, new Color(0.25f, 0.85f, 0.4f));
            }
            else
            {
                _progressBar.Hide();
            }
        }
    }
}

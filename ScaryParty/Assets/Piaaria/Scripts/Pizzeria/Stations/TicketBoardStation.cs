using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Stations
{
    public class TicketBoardStation : StationView
    {
        [Header("Quadro de Comandas")]
        public Transform[] ticketSlots;
        public int maxVisibleTickets = 4;

        private readonly List<TicketSheetView> _sheets = new List<TicketSheetView>();

        public override string InteractPrompt => string.Empty;

        protected override void Start()
        {
            base.Start();
            stationName = "Quadro de Comandas";

            // Desativar o colisor da madeira do quadro para que o foco e clique ocorram exclusivamente em cada comanda individual
            var boardCol = GetComponent<Collider>();
            if (boardCol != null) boardCol.enabled = false;

            // Assegurar inicialização dos slots visuais se não atribuídos (ou se todos estiverem nulos no Inspector)
            if (!HasAnyAssignedSlot(ticketSlots))
            {
                var slotsParent = transform.Find("TicketSlots");
                if (slotsParent == null)
                {
                    var go = new GameObject("TicketSlots");
                    go.transform.SetParent(transform, false);
                    slotsParent = go.transform;
                }

                // Detecta a direção que aponta para o interior da sala (em relação ao prédio/sala)
                Vector3 roomDir = transform.parent != null ? -transform.parent.forward : Vector3.back;
                Vector3 localRoomDir = transform.InverseTransformDirection(roomDir);
                float signZ = localRoomDir.z >= 0 ? 1f : -1f;

                // Neutraliza a escala não-uniforme do pai para que os tickets tenham medidas reais em metros
                Vector3 s = transform.localScale;
                slotsParent.localPosition = new Vector3(0f, 0f, signZ * 0.55f);
                slotsParent.localRotation = localRoomDir.z >= 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                slotsParent.localScale = new Vector3(
                    s.x > 0.001f ? 1f / s.x : 1f,
                    s.y > 0.001f ? 1f / s.y : 1f,
                    s.z > 0.001f ? 1f / s.z : 1f
                );

                ticketSlots = new Transform[maxVisibleTickets];
                float spacing = 0.48f;
                float startX = -((maxVisibleTickets - 1) * spacing) * 0.5f;

                for (int i = 0; i < maxVisibleTickets; i++)
                {
                    var slotGo = new GameObject($"Slot_{i}");
                    slotGo.transform.SetParent(slotsParent, false);
                    slotGo.transform.localPosition = new Vector3(startX + (i * spacing), 0f, 0f);
                    slotGo.transform.localRotation = Quaternion.identity;
                    slotGo.transform.localScale = Vector3.one;
                    ticketSlots[i] = slotGo.transform;
                }
            }

            // Criar ou conectar TicketSheetView em cada slot
            _sheets.Clear();
            for (int i = 0; i < ticketSlots.Length; i++)
            {
                var slot = ticketSlots[i];
                if (slot == null) continue;
                var sheet = slot.GetComponentInChildren<TicketSheetView>();
                if (sheet == null)
                {
                    sheet = CreateDefaultTicketSheet(slot, i);
                }
                sheet.Clear();
                _sheets.Add(sheet);
            }
        }

        private static bool HasAnyAssignedSlot(Transform[] slots)
        {
            if (slots == null || slots.Length == 0) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) return true;
            }
            return false;
        }

        private TicketSheetView CreateDefaultTicketSheet(Transform parentSlot, int index)
        {
            var sheetObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sheetObj.name = $"TicketSheet_{index}";
            sheetObj.transform.SetParent(parentSlot, false);
            sheetObj.transform.localPosition = Vector3.zero;
            sheetObj.transform.localRotation = Quaternion.identity;
            sheetObj.transform.localScale = new Vector3(0.38f, 0.55f, 1f);

            // Remover MeshCollider padrão do Quad e adicionar BoxCollider frontal para raycast preciso
            var meshCol = sheetObj.GetComponent<Collider>();
            if (meshCol != null) Destroy(meshCol);

            var boxCol = sheetObj.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(1f, 1f, 0.08f);
            boxCol.center = new Vector3(0f, 0f, -0.02f);

            var meshRenderer = sheetObj.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = new Color(0.96f, 0.94f, 0.88f);
            meshRenderer.sharedMaterial = mat;

            // Criar TextMeshPro 3D na frente do quad
            var textObj = new GameObject("SheetText");
            textObj.transform.SetParent(sheetObj.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one * 0.14f;

            var tmp = textObj.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 2.4f;
            tmp.color = Color.black;
            var rectTransform = textObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(2.4f, 3.6f);

            var sheetView = sheetObj.AddComponent<TicketSheetView>();
            sheetView.textMesh = tmp;
            sheetView.paperRenderer = meshRenderer;

            return sheetView;
        }

        public static bool IsOrderClaimedByActivePizzaiolo(PizzeriaNetworkState netState, ulong orderId, out int employeeId)
        {
            employeeId = 0;
            if (orderId == 0) return false;

            if (netState != null && netState.Employees != null)
            {
                for (int i = 0; i < netState.Employees.Count; i++)
                {
                    var emp = netState.Employees[i];
                    if (emp.Role == (byte)ScaryParty.Pizzeria.Domain.Services.EmployeeRole.Pizzaiolo &&
                        emp.IsAlive &&
                        emp.AssignedTaskId > 0 &&
                        emp.TargetOrderId == (int)orderId &&
                        emp.TaskState != (byte)ScaryParty.Pizzeria.Domain.Services.EmployeeTaskState.Completed &&
                        emp.TaskState != (byte)ScaryParty.Pizzeria.Domain.Services.EmployeeTaskState.Cancelled)
                    {
                        employeeId = emp.EmployeeId;
                        return true;
                    }
                }
            }

            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsServer && root.EmployeeTaskService != null)
            {
                foreach (var kvp in root.EmployeeTaskService.Employees)
                {
                    var emp = kvp.Value;
                    if (emp == null ||
                        emp.Role != ScaryParty.Pizzeria.Domain.Services.EmployeeRole.Pizzaiolo ||
                        !emp.IsAlive ||
                        emp.AssignedTaskId <= 0)
                    {
                        continue;
                    }

                    if (root.EmployeeTaskService.Tasks.TryGetValue(emp.AssignedTaskId, out var task) &&
                        task != null &&
                        task.TargetOrderId.Value == orderId &&
                        task.State != ScaryParty.Pizzeria.Domain.Services.EmployeeTaskState.Completed &&
                        task.State != ScaryParty.Pizzeria.Domain.Services.EmployeeTaskState.Cancelled)
                    {
                        employeeId = emp.EmployeeId;
                        return true;
                    }
                }
            }

            return false;
        }

        protected override void Update()
        {
            base.Update();

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Orders == null) return;

            var catalog = PizzeriaRoot.Instance?.Catalog;
            double now = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                ? NetworkManager.Singleton.ServerTime.Time
                : Time.time;

            int sheetIndex = 0;
            for (int i = 0; i < netState.Orders.Count; i++)
            {
                var order = netState.Orders[i];
                if (order.Lifecycle != (byte)ScaryParty.Pizzeria.Domain.Types.OrderLifecycle.Accepted || order.TicketArchived) continue;
                if (IsOrderClaimedByActivePizzaiolo(netState, order.OrderId, out _)) continue;

                if (sheetIndex < _sheets.Count)
                {
                    _sheets[sheetIndex].BindOrder(order, now, catalog);
                    sheetIndex++;
                }
            }

            // Limpar slots excedentes
            for (int i = sheetIndex; i < _sheets.Count; i++)
            {
                _sheets[i].Clear();
            }
        }

        public override void OnInteract(GameObject interactor)
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Orders == null || netState.Orders.Count == 0)
            {
                NotificationManager.Show("Nenhuma comanda ativa no momento.", NotificationType.Warning);
                return;
            }

            // Se interagiu diretamente com a madeira do quadro, abrir a primeira comanda ativa não arquivada
            for (int i = 0; i < netState.Orders.Count; i++)
            {
                var order = netState.Orders[i];
                if (order.Lifecycle == (byte)ScaryParty.Pizzeria.Domain.Types.OrderLifecycle.Accepted && !order.TicketArchived && !IsOrderClaimedByActivePizzaiolo(netState, order.OrderId, out _))
                {
                    if (TicketModalPresenter.Instance != null)
                    {
                        TicketModalPresenter.Instance.Open(order);
                    }
                    return;
                }
            }

            NotificationManager.Show("Todas as comandas disponíveis estão com os pizzaiolos ou foram arquivadas.", NotificationType.Info);
        }
    }
}

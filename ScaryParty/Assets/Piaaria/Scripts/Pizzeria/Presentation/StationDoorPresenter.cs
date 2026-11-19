using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Pizzeria.Presentation
{
    [DisallowMultipleComponent]
    public class StationDoorPresenter : MonoBehaviour
    {
        [Header("Folhas / Portas Móveis")]
        [SerializeField] private TransformDoorView[] _doors;

        private bool _isHovered;
        private bool _isSessionOpen;
        private bool _isTransferActive;
        private bool _hoverSuppressed;

        public bool IsSessionOpen => _isSessionOpen;
        public bool IsTransferActive => _isTransferActive;
        public bool IsHovered => _isHovered;
        public bool IsHoverSuppressed => _hoverSuppressed;

        private void Awake()
        {
            if (_doors == null || _doors.Length == 0)
            {
                _doors = GetComponentsInChildren<TransformDoorView>(true);
            }
        }

        public void SetDoors(TransformDoorView[] doors)
        {
            _doors = doors;
        }

        public void OnHoverEnter()
        {
            _isHovered = true;
            EvaluateAndApplyTarget();
        }

        public void OnHoverExit()
        {
            _isHovered = false;
            // Retirar a mira limpa a supressão de hover
            _hoverSuppressed = false;
            EvaluateAndApplyTarget();
        }

        public void SetSessionOpen(bool open)
        {
            _isSessionOpen = open;
            if (!open)
            {
                // Encerramento de sessão suprime hover imediato até retirar a mira
                _hoverSuppressed = true;
            }
            EvaluateAndApplyTarget();
        }

        public void SetTransferActive(bool active)
        {
            _isTransferActive = active;
            if (!active)
            {
                // Encerramento de transferência de forno suprime hover imediato
                _hoverSuppressed = true;
            }
            EvaluateAndApplyTarget();
        }

        public void CloseAndSuppressHover()
        {
            _isSessionOpen = false;
            _isTransferActive = false;
            _hoverSuppressed = true;
            EvaluateAndApplyTarget();
        }

        public void SnapClosed()
        {
            _isSessionOpen = false;
            _isTransferActive = false;
            _isHovered = false;
            _hoverSuppressed = false;

            if (_doors == null) return;
            for (int i = 0; i < _doors.Length; i++)
            {
                if (_doors[i] != null)
                {
                    _doors[i].SetTarget(DoorVisualTarget.Closed, immediate: true);
                }
            }
        }

        private void EvaluateAndApplyTarget()
        {
            DoorVisualTarget resolvedTarget;

            if (_isSessionOpen || _isTransferActive)
            {
                resolvedTarget = DoorVisualTarget.Open;
            }
            else if (_isHovered && !_hoverSuppressed)
            {
                resolvedTarget = DoorVisualTarget.Hover;
            }
            else
            {
                resolvedTarget = DoorVisualTarget.Closed;
            }

            if (_doors == null) return;
            for (int i = 0; i < _doors.Length; i++)
            {
                if (_doors[i] != null)
                {
                    _doors[i].SetTarget(resolvedTarget, immediate: false);
                }
            }
        }

        private void OnDisable()
        {
            _isHovered = false;
            _hoverSuppressed = false;
        }
    }

    /// <summary>
    /// Controla a abertura e fechamento automáticos da portinhola/tampo de passagem do balcão
    /// quando qualquer jogador ou funcionário contratado se aproxima para atravessar.
    /// </summary>
    [DisallowMultipleComponent]
    public class CounterPassageDoorController : MonoBehaviour
    {
        private const float TriggerRadiusMeters = 1.85f;
        private const float MaxVerticalDeltaMeters = 2.2f;
        private const float OpenAngleDegrees = 85f;
        private const float OpenSpeedDegPerSec = 380f;
        private const float HoldOpenGraceSeconds = 0.35f;

        private Transform _leaf;
        private Collider _leafCollider;
        private Vector3 _passageWorldCenter;
        private Quaternion _closedLocalRotation = Quaternion.identity;
        private Vector3 _localHingeAxis = Vector3.forward;
        private float _openSign = 1f;
        private float _currentAngle;
        private float _holdTimer;
        private bool _initialized;

        public Vector3 PassageWorldCenter => _passageWorldCenter;

        public static void EnsureCounterPassageDoorsConfigured(Transform root)
        {
            if (root == null) return;

            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            var openingLeaves = new List<Transform>();
            var openingCounterParts = new List<Transform>();

            for (int i = 0; i < allTransforms.Length; i++)
            {
                var t = allTransforms[i];
                if (t == null) continue;
                string lower = t.name.ToLowerInvariant();
                if (lower.StartsWith("abertura"))
                {
                    if (t.GetComponent<MeshRenderer>() != null)
                        openingLeaves.Add(t);

                    var obs = t.GetComponent<NavMeshObstacle>();
                    if (obs != null) DestroySafe(obs);
                }
                else if (lower.StartsWith("balcaopabertura"))
                {
                    openingCounterParts.Add(t);
                }
            }

            if (openingLeaves.Count == 0) return;

            for (int i = 0; i < openingLeaves.Count; i++)
            {
                var leaf = openingLeaves[i];
                if (leaf == null) continue;

                var leafRenderer = leaf.GetComponent<MeshRenderer>();
                if (leafRenderer == null) continue;
                Bounds closedBounds = leafRenderer.bounds;

                // Se balcaopabertura tiver NavMeshObstacle que invada os bounds de abertura, remove para liberar a passagem no NavMesh
                for (int j = 0; j < openingCounterParts.Count; j++)
                {
                    var part = openingCounterParts[j];
                    if (part == null) continue;
                    var obs = part.GetComponent<NavMeshObstacle>();
                    if (obs != null)
                    {
                        Vector3 obsWorldCenter = part.TransformPoint(obs.center);
                        Vector3 obsWorldSize = Vector3.Scale(obs.size, AbsVector(part.lossyScale));
                        var obsBounds = new Bounds(obsWorldCenter, obsWorldSize);
                        if (obsBounds.Intersects(closedBounds))
                        {
                            DestroySafe(obs);
                        }
                    }
                }

                if (leaf.GetComponentInParent<CounterPassageDoorController>() != null)
                    continue;

                Collider leafCollider = leaf.GetComponent<Collider>();
                if (leafCollider == null)
                {
                    var box = leaf.gameObject.AddComponent<BoxCollider>();
                    var mf = leaf.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        box.center = mf.sharedMesh.bounds.center;
                        box.size = mf.sharedMesh.bounds.size;
                    }
                    leafCollider = box;
                }

                Transform closestBalcao = null;
                float closestDistSqr = float.MaxValue;
                for (int j = 0; j < openingCounterParts.Count; j++)
                {
                    var cand = openingCounterParts[j];
                    if (cand == null) continue;
                    var candRend = cand.GetComponentInChildren<Renderer>(true);
                    Vector3 candCenter = candRend != null ? candRend.bounds.center : cand.position;
                    float d = (candCenter - closedBounds.center).sqrMagnitude;
                    if (d < closestDistSqr)
                    {
                        closestDistSqr = d;
                        closestBalcao = cand;
                    }
                }

                Transform leafParent = leaf.parent;
                Vector3 balcaoCenter = closedBounds.center + Vector3.left;
                if (closestBalcao != null)
                {
                    var rend = closestBalcao.GetComponentInChildren<Renderer>(true);
                    balcaoCenter = rend != null ? rend.bounds.center : closestBalcao.position;
                }

                // Determinar orientação da dobradiça no espaço do pai da folha
                Vector3 localMin = leafParent != null ? leafParent.InverseTransformPoint(closedBounds.min) : closedBounds.min;
                Vector3 localMax = leafParent != null ? leafParent.InverseTransformPoint(closedBounds.max) : closedBounds.max;
                Vector3 bMin = Vector3.Min(localMin, localMax);
                Vector3 bMax = Vector3.Max(localMin, localMax);
                Vector3 localBalcao = leafParent != null ? leafParent.InverseTransformPoint(balcaoCenter) : balcaoCenter;
                Vector3 localCenter = (bMin + bMax) * 0.5f;

                float spanX = bMax.x - bMin.x;
                float spanZ = bMax.z - bMin.z;
                bool hingeAlongZAxis = spanX >= spanZ;
                Vector3 localHingeAxis = hingeAlongZAxis ? Vector3.forward : Vector3.right;

                Vector3 hingeLocalPos;
                if (hingeAlongZAxis)
                {
                    Vector3 leftEdge = new Vector3(bMin.x, localCenter.y, localCenter.z);
                    Vector3 rightEdge = new Vector3(bMax.x, localCenter.y, localCenter.z);
                    float dLeft = (leftEdge - localBalcao).sqrMagnitude;
                    float dRight = (rightEdge - localBalcao).sqrMagnitude;
                    hingeLocalPos = dLeft <= dRight ? leftEdge : rightEdge;
                }
                else
                {
                    Vector3 backEdge = new Vector3(localCenter.x, localCenter.y, bMin.z);
                    Vector3 frontEdge = new Vector3(localCenter.x, localCenter.y, bMax.z);
                    float dBack = (backEdge - localBalcao).sqrMagnitude;
                    float dFront = (frontEdge - localBalcao).sqrMagnitude;
                    hingeLocalPos = dBack <= dFront ? backEdge : frontEdge;
                }

                Vector3 hingeWorldPos = leafParent != null ? leafParent.TransformPoint(hingeLocalPos) : hingeLocalPos;

                var pivotGo = new GameObject("CounterDoorPivot");
                pivotGo.transform.SetParent(leafParent, false);
                pivotGo.transform.position = hingeWorldPos;
                pivotGo.transform.rotation = leafParent != null ? leafParent.rotation : Quaternion.identity;
                pivotGo.transform.localScale = Vector3.one;

                leaf.SetParent(pivotGo.transform, true);

                var ctrl = pivotGo.AddComponent<CounterPassageDoorController>();
                ctrl.Initialize(leaf, leafCollider, closedBounds.center, localHingeAxis);
            }
        }

        public void Initialize(Transform leaf, Collider leafCollider, Vector3 passageWorldCenter, Vector3 localHingeAxis)
        {
            _leaf = leaf;
            _leafCollider = leafCollider;
            _passageWorldCenter = passageWorldCenter;
            _closedLocalRotation = transform.localRotation;
            _localHingeAxis = localHingeAxis.sqrMagnitude > 0.001f ? localHingeAxis.normalized : Vector3.forward;

            // Determinar o sinal que levanta a tampa para cima
            _openSign = 1f;
            if (_leaf != null)
            {
                var rend = _leaf.GetComponent<Renderer>();
                Vector3 closedCenter = rend != null ? rend.bounds.center : _leaf.position;

                transform.localRotation = _closedLocalRotation * Quaternion.AngleAxis(OpenAngleDegrees, _localHingeAxis);
                float posY = rend != null ? rend.bounds.center.y : _leaf.position.y;

                transform.localRotation = _closedLocalRotation * Quaternion.AngleAxis(-OpenAngleDegrees, _localHingeAxis);
                float negY = rend != null ? rend.bounds.center.y : _leaf.position.y;

                transform.localRotation = _closedLocalRotation;
                _openSign = posY >= negY ? 1f : -1f;
            }

            _currentAngle = 0f;
            _holdTimer = 0f;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            bool shouldOpen = EmployeeAgentController.IsAnyActorNear(_passageWorldCenter, TriggerRadiusMeters, MaxVerticalDeltaMeters);
            if (shouldOpen)
            {
                _holdTimer = HoldOpenGraceSeconds;
            }
            else if (_holdTimer > 0f)
            {
                _holdTimer = Mathf.Max(0f, _holdTimer - Time.deltaTime);
            }

            float targetAngle = _holdTimer > 0f ? OpenAngleDegrees : 0f;
            if (!Mathf.Approximately(_currentAngle, targetAngle))
            {
                _currentAngle = Mathf.MoveTowards(_currentAngle, targetAngle, Time.deltaTime * OpenSpeedDegPerSec);
                transform.localRotation = _closedLocalRotation * Quaternion.AngleAxis(_currentAngle * _openSign, _localHingeAxis);
            }

            if (_leafCollider != null)
            {
                bool solid = _currentAngle < 8f;
                if (_leafCollider.enabled != solid)
                {
                    _leafCollider.enabled = solid;
                }
            }
        }

        private static Vector3 AbsVector(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
    }
}

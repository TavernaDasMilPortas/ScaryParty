using System.Collections.Generic;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using StarterAssets;
using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.City
{
    /// <summary>
    /// Moto de entregas estacionada no lote lateral da pizzaria (MOTO + O13 buzina).
    /// Restrita às vias da cidade e à área do estacionamento, exigindo mãos vazias e sem arma equipada.
    /// </summary>
    public class DeliveryMotorcycleController : MonoBehaviour, IInteractable
    {
        public static DeliveryMotorcycleController Instance { get; private set; }

        [Header("Movimento da Moto")]
        [SerializeField] private float maxForwardSpeed = 14f;
        [SerializeField] private float maxReverseSpeed = 5f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float brakeDeceleration = 28f;
        [SerializeField] private float turnSpeedDegrees = 95f;

        private Vector3 _parkingPos;
        private Quaternion _parkingRot = Quaternion.identity;
        private Bounds _parkingBounds;
        private Transform _riderSeatAnchor;
        public Transform HandlebarLeftAnchor { get; private set; }
        public Transform HandlebarRightAnchor { get; private set; }
        public Transform LeftExitAnchor { get; private set; }
        public Transform RightExitAnchor { get; private set; }
        public Transform CargoRackAnchor { get; private set; }
        public Transform StreetExitAnchor { get; private set; }
        private CityGenerator _cityGen;
        private float _localSpeed;
        private float _netSendTimer;
        private Vector3 _lastSentPos;
        private Quaternion _lastSentRot = Quaternion.identity;
        private bool _hasSentPose;
        private bool _wasMovingLastSend;
        private uint _lastHornCounter;
        private bool _wasLocalDriver;
        private ThirdPersonController _lockedLocalTpc;
        private Transform _riderRootTransform;
        private CharacterController _disabledSeatedCc;
        private ulong _disabledSeatedRiderClientId = ulong.MaxValue;
        private uint _impactReportedForMountRevision;
        private uint _nextImpactEpisodeId = 1;
        private static readonly RaycastHit[] _sweepHits = new RaycastHit[16];

        public Vector3 ParkingSpawnPos => _parkingPos;
        public Quaternion ParkingSpawnRot => _parkingRot;
        public Bounds ParkingLotBounds => _parkingBounds;

        public string InteractPrompt
        {
            get
            {
                var net = PizzeriaNetworkState.Instance;
                if (net != null)
                {
                    var st = net.MotorcycleState.Value;
                    if (st.IsMounted)
                    {
                        ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
                        return st.RiderClientId == localId
                            ? "[E] Descer da Moto | [WASD] Pilotar | [Espaço] Frear | [H] Buzinar"
                            : "Moto em uso por outro entregador";
                    }
                }
                return "[E] Montar na Moto (requer mão esquerda livre; permite pistola na direita)";
            }
        }

        private void Awake()
        {
            Instance = this;
            EnsureImpactTriggerSensor();
        }

        private void EnsureImpactTriggerSensor()
        {
            var cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null && cols[i].isTrigger) return;
            }
            var sensorGo = new GameObject("ImpactSensor");
            sensorGo.transform.SetParent(transform, false);
            var triggerSensor = sensorGo.AddComponent<BoxCollider>();
            triggerSensor.isTrigger = true;
            triggerSensor.center = new Vector3(0f, 0.65f, 0.05f);
            triggerSensor.size = new Vector3(1.05f, 1.15f, 2.25f);
        }

        private void OnDestroy()
        {
            ReleaseLocalDrivingState();
            ReleaseSeatedRiderAvatar();
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            ReleaseLocalDrivingState();
            ReleaseSeatedRiderAvatar();
        }

        public void ConfigureParking(
            Vector3 parkPos,
            Quaternion parkRot,
            Bounds lotBounds,
            Transform seatAnchor,
            Transform handlebarLeft = null,
            Transform handlebarRight = null,
            Transform leftExit = null,
            Transform rightExit = null,
            Transform cargoRack = null,
            Transform streetExit = null)
        {
            _parkingPos = parkPos;
            _parkingRot = parkRot;
            _parkingBounds = lotBounds;
            _riderSeatAnchor = seatAnchor;
            HandlebarLeftAnchor = handlebarLeft;
            HandlebarRightAnchor = handlebarRight;
            LeftExitAnchor = leftExit;
            RightExitAnchor = rightExit;
            CargoRackAnchor = cargoRack;
            StreetExitAnchor = streetExit;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && PizzeriaNetworkState.Instance != null)
            {
                var st = PizzeriaNetworkState.Instance.MotorcycleState.Value;
                if (!st.IsActive)
                {
                    ResetServerPoseToParking();
                }
            }
        }

        public void ResetServerPoseToParking()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            var net = PizzeriaNetworkState.Instance;
            if (net == null) return;

            transform.SetPositionAndRotation(_parkingPos, _parkingRot);
            _localSpeed = 0f;

            var prev = net.MotorcycleState.Value;
            net.MotorcycleState.Value = new NetMotorcycleDto
            {
                IsActive = true,
                IsMounted = false,
                RiderClientId = 0,
                PosX = _parkingPos.x,
                PosY = _parkingPos.y,
                PosZ = _parkingPos.z,
                RotX = _parkingRot.x,
                RotY = _parkingRot.y,
                RotZ = _parkingRot.z,
                RotW = _parkingRot.w,
                Speed = 0f,
                HornCounter = prev.HornCounter,
                Revision = prev.Revision + 1
            };
        }

        private void Update()
        {
            var net = PizzeriaNetworkState.Instance;
            if (net == null) return;

            var st = net.MotorcycleState.Value;
            if (!st.IsActive) return;

            if (st.HornCounter != _lastHornCounter)
            {
                _lastHornCounter = st.HornCounter;
                PlayHornVisualPulse();
            }

            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            bool isLocalDriver = st.IsMounted && st.RiderClientId == localId;

            if (isLocalDriver)
            {
                if (!_wasLocalDriver)
                {
                    _wasLocalDriver = true;
                    _localSpeed = 0f;
                    _netSendTimer = 0f;
                    _hasSentPose = false;
                    _wasMovingLastSend = false;
                    ContextualTutorialPresenter.NotifyContextualTip(
                        "motorcycle_drive",
                        "Moto de Entregas",
                        "Use [WASD] para pilotar, [Espaço] para frear, [H] para buzinar e [E] para descer. A moto circula pelas ruas e estacionamento.");
                }

                WeaponController.LocalDrivingMotorcycle = true;
                UpdateLocalDriver(localId);
                SyncRiderAvatarToSeat(localId);
            }
            else
            {
                if (_wasLocalDriver)
                {
                    ReleaseLocalDrivingState();
                }

                Vector3 targetPos = new Vector3(st.PosX, st.PosY, st.PosZ);
                Quaternion targetRot = new Quaternion(st.RotX, st.RotY, st.RotZ, st.RotW);
                if (targetRot.x == 0f && targetRot.y == 0f && targetRot.z == 0f && targetRot.w == 0f)
                    targetRot = Quaternion.identity;

                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 14f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 14f);

                if (st.IsMounted)
                {
                    SyncRiderAvatarToSeat(st.RiderClientId);
                }
                else
                {
                    ReleaseSeatedRiderAvatar();
                }
            }
        }

        private void UpdateLocalDriver(ulong localId)
        {
            if (GameplayInputGate.IsLocked)
            {
                _localSpeed = Mathf.MoveTowards(_localSpeed, 0f, brakeDeceleration * Time.deltaTime);
                return;
            }

            float throttle = 0f;
            float steer = 0f;
            bool brake = false;
            bool dismountPressed = false;
            bool hornPressed = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                brake = kb.spaceKey.isPressed;
                dismountPressed = kb.eKey.wasPressedThisFrame;
                hornPressed = kb.hKey.wasPressedThisFrame;
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.E)) dismountPressed = true;
                if (Input.GetKeyDown(KeyCode.H)) hornPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.E)) dismountPressed = true;
            if (Input.GetKeyDown(KeyCode.H)) hornPressed = true;
#endif

            if (dismountPressed)
            {
                if (_wasMovingLastSend || Vector3.Distance(transform.position, _lastSentPos) > 0.015f)
                {
                    PizzeriaCommandHandler.Instance?.UpdateMotorcyclePoseServerRpc(transform.position, transform.rotation, 0f);
                    _lastSentPos = transform.position;
                    _lastSentRot = transform.rotation;
                    _wasMovingLastSend = false;
                }
                PizzeriaCommandHandler.Instance?.ToggleMotorcycleMountServerRpc();
                return;
            }

            if (hornPressed)
            {
                PizzeriaCommandHandler.Instance?.HonkMotorcycleServerRpc();
            }

            if (brake)
            {
                _localSpeed = Mathf.MoveTowards(_localSpeed, 0f, brakeDeceleration * Time.deltaTime);
            }
            else if (Mathf.Abs(throttle) > 0.05f)
            {
                float targetMax = throttle > 0f ? maxForwardSpeed : -maxReverseSpeed;
                _localSpeed = Mathf.MoveTowards(_localSpeed, targetMax * Mathf.Abs(throttle), acceleration * Time.deltaTime);
            }
            else
            {
                _localSpeed = Mathf.MoveTowards(_localSpeed, 0f, acceleration * 0.55f * Time.deltaTime);
            }

            if (Mathf.Abs(_localSpeed) > 0.25f && Mathf.Abs(steer) > 0.05f)
            {
                float steerSign = _localSpeed >= 0f ? 1f : -1f;
                float speedRatio = Mathf.Clamp01(Mathf.Abs(_localSpeed) / 4f);
                float yaw = steer * steerSign * turnSpeedDegrees * speedRatio * Time.deltaTime;
                transform.Rotate(0f, yaw, 0f, Space.World);
            }

            Vector3 currentPos = transform.position;
            Vector3 moveDelta = transform.forward * (_localSpeed * Time.deltaTime);
            Vector3 desiredPos = ResolveSolidCollisionSweep(currentPos, moveDelta);
            Vector3 clampedPos = ClampToAllowedSurface(currentPos, desiredPos, out bool hitCurb);
            if (hitCurb)
            {
                _localSpeed *= 0.8f;
            }

            transform.position = clampedPos;

            _netSendTimer += Time.deltaTime;
            if (_netSendTimer >= 0.08f)
            {
                _netSendTimer = 0f;
                bool isMoving = Mathf.Abs(_localSpeed) > 0.05f;
                bool movedEnough = !_hasSentPose ||
                                   Vector3.Distance(transform.position, _lastSentPos) > 0.015f ||
                                   Quaternion.Angle(transform.rotation, _lastSentRot) > 0.5f ||
                                   (!isMoving && _wasMovingLastSend);
                if (movedEnough)
                {
                    PizzeriaCommandHandler.Instance?.UpdateMotorcyclePoseServerRpc(transform.position, transform.rotation, _localSpeed);
                    _lastSentPos = transform.position;
                    _lastSentRot = transform.rotation;
                    _hasSentPose = true;
                    _wasMovingLastSend = isMoving;
                }
            }
        }

        private Vector3 ResolveSolidCollisionSweep(Vector3 currentPos, Vector3 moveDelta)
        {
            float dist = moveDelta.magnitude;
            if (dist < 0.001f) return currentPos + moveDelta;

            Vector3 dir = moveDelta / dist;
            Vector3 center = currentPos + Vector3.up * 0.65f;
            Vector3 halfExtents = new Vector3(0.42f, 0.48f, 0.90f);
            int hits = Physics.BoxCastNonAlloc(center, halfExtents, dir, _sweepHits, transform.rotation, dist + 0.08f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float closestSafeDist = dist;
            bool blocked = false;
            Vector3 blockNormal = Vector3.zero;

            for (int i = 0; i < hits; i++)
            {
                var h = _sweepHits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
                if (_riderRootTransform != null && h.collider.transform.IsChildOf(_riderRootTransform)) continue;
                if (h.normal.y > 0.65f) continue;

                if (Mathf.Abs(_localSpeed) >= 3.5f && TryReportImpactFromCollider(h.collider, h.point))
                    return currentPos;

                if (h.distance < closestSafeDist)
                {
                    closestSafeDist = Mathf.Max(0f, h.distance - 0.06f);
                    blockNormal = new Vector3(h.normal.x, 0f, h.normal.z);
                    blocked = true;
                }
            }

            if (!blocked) return currentPos + moveDelta;

            _localSpeed *= 0.45f;
            Vector3 remaining = moveDelta - dir * closestSafeDist;
            if (blockNormal.sqrMagnitude > 0.001f)
            {
                blockNormal.Normalize();
                Vector3 slide = Vector3.ProjectOnPlane(remaining, blockNormal);
                return currentPos + dir * closestSafeDist + slide * 0.5f;
            }
            return currentPos + dir * closestSafeDist;
        }

        private void SyncRiderAvatarToSeat(ulong clientId)
        {
            Transform po = null;
            if (PlayerState.TryGetByClientId(clientId, out var riderPs) && riderPs != null)
            {
                po = riderPs.transform;
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer &&
                     NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
                     client.PlayerObject != null)
            {
                po = client.PlayerObject.transform;
            }
            if (po == null) return;

            if (_disabledSeatedRiderClientId != clientId)
                ReleaseSeatedRiderAvatar();

            var no = po.GetComponent<NetworkObject>();
            if (no != null && no.IsOwner)
            {
                var tpc = po.GetComponent<ThirdPersonController>();
                if (tpc != null && tpc.enabled)
                {
                    _lockedLocalTpc = tpc;
                    tpc.enabled = false;
                }
            }

            var cc = po.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                cc.enabled = false;
                _disabledSeatedCc = cc;
                _disabledSeatedRiderClientId = clientId;
            }
            _riderRootTransform = po;

            Vector3 seatPos = _riderSeatAnchor != null ? _riderSeatAnchor.position : transform.position + Vector3.up * 0.55f;
            po.SetPositionAndRotation(seatPos, transform.rotation);
        }

        private void ReleaseSeatedRiderAvatar()
        {
            if (_disabledSeatedCc != null)
            {
                if (!_disabledSeatedCc.enabled) _disabledSeatedCc.enabled = true;
                _disabledSeatedCc = null;
            }
            _riderRootTransform = null;
            _disabledSeatedRiderClientId = ulong.MaxValue;
        }

        private void ReleaseLocalDrivingState()
        {
            _wasLocalDriver = false;
            _hasSentPose = false;
            _wasMovingLastSend = false;
            WeaponController.LocalDrivingMotorcycle = false;

            if (_lockedLocalTpc != null)
            {
                _lockedLocalTpc.enabled = true;
                _lockedLocalTpc = null;
            }

            ReleaseSeatedRiderAvatar();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var cc = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<CharacterController>();
                if (cc != null && !cc.enabled) cc.enabled = true;
            }
        }

        public Vector3 ClampToAllowedSurface(Vector3 currentPos, Vector3 desiredPos, out bool hitCurb)
        {
            hitCurb = false;
            if (IsPointOnDrivableSurface(desiredPos))
                return desiredPos;

            hitCurb = true;

            // Curb slide em X ou Z antes de travar contra o meio-fio
            Vector3 slideX = new Vector3(desiredPos.x, desiredPos.y, currentPos.z);
            if (IsPointOnDrivableSurface(slideX))
                return slideX;

            Vector3 slideZ = new Vector3(currentPos.x, desiredPos.y, desiredPos.z);
            if (IsPointOnDrivableSurface(slideZ))
                return slideZ;

            if (IsPointOnDrivableSurface(currentPos))
                return currentPos;

            return FindNearestAllowedPoint(desiredPos);
        }

        public static bool IsPointInExitDriveway(
            Vector3 pos,
            Vector3 parkingPos,
            Quaternion parkingRot,
            Vector3? streetExitAnchorPos,
            float sidewalkWidth)
        {
            Vector3 fwd = parkingRot * Vector3.forward;
            if (streetExitAnchorPos.HasValue)
            {
                Vector3 toExit = streetExitAnchorPos.Value - parkingPos;
                toExit.y = 0f;
                if (toExit.sqrMagnitude > 0.25f) fwd = toExit.normalized;
            }
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 start = streetExitAnchorPos ?? (parkingPos + fwd * 2.0f);
            start -= fwd * 1.5f;
            float reach = Mathf.Clamp(sidewalkWidth + 4.5f, 6.5f, 13.5f);
            Vector3 end = start + fwd * reach;
            return SqrDistanceToSegmentXZ(pos, start, end) <= 2.15f * 2.15f;
        }

        public bool IsPointOnDrivableSurface(Vector3 pos)
        {
            Vector3 flat = new Vector3(pos.x, _parkingPos.y, pos.z);
            if (_parkingBounds.size.sqrMagnitude > 0.1f)
            {
                Bounds padded = _parkingBounds;
                padded.Expand(new Vector3(0.4f, 6f, 0.4f));
                if (padded.Contains(flat)) return true;
            }

            if (_cityGen == null) _cityGen = FindFirstObjectByType<CityGenerator>();
            float sidewalkWidth = _cityGen != null && _cityGen.config != null ? _cityGen.config.sidewalkWidth : 2.5f;
            if (_parkingBounds.size.sqrMagnitude > 0.1f &&
                IsPointInExitDriveway(pos, _parkingPos, _parkingRot, StreetExitAnchor != null ? StreetExitAnchor.position : (Vector3?)null, sidewalkWidth))
            {
                return true;
            }

            var graph = _cityGen != null && _cityGen.CityData != null ? _cityGen.CityData.streetGraph : null;
            if (graph == null || graph.edges == null || graph.nodes == null) return true;

            float streetWidth = _cityGen.config != null ? _cityGen.config.streetWidth : 10f;
            float halfRadius = Mathf.Max(2.5f, streetWidth * 0.5f - 0.35f);
            float maxSqr = halfRadius * halfRadius;

            for (int i = 0; i < graph.edges.Count; i++)
            {
                var e = graph.edges[i];
                if (e.nodeA < 0 || e.nodeA >= graph.nodes.Count || e.nodeB < 0 || e.nodeB >= graph.nodes.Count)
                    continue;
                Vector3 a = graph.nodes[e.nodeA].worldPosition;
                Vector3 b = graph.nodes[e.nodeB].worldPosition;
                if (SqrDistanceToSegmentXZ(pos, a, b) <= maxSqr)
                    return true;
            }

            return false;
        }

        private Vector3 FindNearestAllowedPoint(Vector3 pos)
        {
            if (_cityGen == null) _cityGen = FindFirstObjectByType<CityGenerator>();
            var graph = _cityGen != null && _cityGen.CityData != null ? _cityGen.CityData.streetGraph : null;
            Vector3 best = _parkingPos;
            float bestDistSqr = (new Vector2(pos.x - _parkingPos.x, pos.z - _parkingPos.z)).sqrMagnitude;

            if (graph != null && graph.edges != null && graph.nodes != null)
            {
                for (int i = 0; i < graph.edges.Count; i++)
                {
                    var e = graph.edges[i];
                    if (e.nodeA < 0 || e.nodeA >= graph.nodes.Count || e.nodeB < 0 || e.nodeB >= graph.nodes.Count)
                        continue;
                    Vector3 projected = ClosestPointOnSegmentXZ(pos, graph.nodes[e.nodeA].worldPosition, graph.nodes[e.nodeB].worldPosition);
                    projected.y = pos.y;
                    float dSqr = (new Vector2(pos.x - projected.x, pos.z - projected.z)).sqrMagnitude;
                    if (dSqr < bestDistSqr)
                    {
                        bestDistSqr = dSqr;
                        best = projected;
                    }
                }
            }

            return best;
        }

        private static float SqrDistanceToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 c = ClosestPointOnSegmentXZ(p, a, b);
            float dx = p.x - c.x;
            float dz = p.z - c.z;
            return dx * dx + dz * dz;
        }

        private static Vector3 ClosestPointOnSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float denom = abx * abx + abz * abz;
            if (denom < 0.0001f) return a;
            float t = Mathf.Clamp01(((p.x - a.x) * abx + (p.z - a.z) * abz) / denom);
            return new Vector3(a.x + abx * t, a.y, a.z + abz * t);
        }

        private void PlayHornVisualPulse()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "MotorcycleHornPulse";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = transform.position + Vector3.up * 0.4f;
            ring.transform.localScale = new Vector3(1.2f, 0.04f, 1.2f);
            var r = ring.GetComponent<Renderer>();
            if (r != null && r.material != null)
                r.material.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            Destroy(ring, 0.35f);
        }

        public void OnInteract(GameObject interactor)
        {
            PizzeriaCommandHandler.Instance?.ToggleMotorcycleMountServerRpc();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_wasLocalDriver || Mathf.Abs(_localSpeed) < 3.5f || other == null) return;
            TryReportImpactFromCollider(other, other.ClosestPoint(transform.position));
        }

        private bool TryReportImpactFromCollider(Collider other, Vector3 hitPoint)
        {
            if (other == null || other.transform.IsChildOf(transform)) return false;
            var net = PizzeriaNetworkState.Instance;
            if (net == null) return false;

            var st = net.MotorcycleState.Value;
            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            if (!st.IsActive || !st.IsMounted || st.RiderClientId != localId) return false;
            if (_impactReportedForMountRevision == st.Revision) return false;

            // Sem friendly-fire contra jogadores
            if (other.GetComponentInParent<PlayerState>() != null ||
                other.GetComponentInParent<ScaryParty.Combat.PlayerHealth>() != null)
            {
                return false;
            }

            // Suporte a monstros replicados por MonsterId (sem NetworkObject próprio)
            var monster = other.GetComponentInParent<ScaryParty.Combat.Monsters.MonsterNpcController>();
            if (monster != null)
            {
                if (!monster.IsAlive) return false;
                _impactReportedForMountRevision = st.Revision;
                PizzeriaCommandHandler.Instance?.ReportMotorcycleMonsterImpactServerRpc(
                    monster.MonsterId,
                    _nextImpactEpisodeId++,
                    hitPoint);
                return true;
            }

            var targetDmg = other.GetComponentInParent<ScaryParty.Combat.IDamageable>();
            var targetNet = other.GetComponentInParent<NetworkObject>();
            if (targetDmg == null || !targetDmg.IsAlive || targetNet == null)
                return false;

            _impactReportedForMountRevision = st.Revision;
            PizzeriaCommandHandler.Instance?.ReportMotorcycleImpactServerRpc(
                targetNet.NetworkObjectId,
                _nextImpactEpisodeId++,
                hitPoint);
            return true;
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }
    }
}

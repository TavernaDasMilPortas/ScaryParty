using System.Collections.Generic;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Presentation.Feedback;
using ScaryParty.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Piaaria.TrailerStudio
{
    public class TrailerActor : MonoBehaviour
    {
        public TrailerActorId actorId;
        public CharacterVisualController visualController;
        public Animator animator;

        private GameObject _rightHandProp;
        private GameObject _leftHandProp;
        private GameObject _equippedBackpack;
        private GameFeelVisualPivot _backpackPivot;

        private readonly List<Vector3> _waypoints = new List<Vector3>();
        private int _waypointIndex;
        private float _moveSpeed = 2.0f;
        private bool _isMoving;
        private bool _isPaused;

        private Transform _mountedSeatAnchor;
        private Transform _mountedBikeRoot;
        private Vector3 _seatLocalOffset = new Vector3(0f, -0.36f, -0.04f);
        private bool _isMounted;
        private float _fallbackFloorY;

        public bool IsMoving => _isMoving;
        public bool IsMounted => _isMounted;
        public bool HasBackpack => _equippedBackpack != null && _equippedBackpack.activeSelf;

        public void Initialize(TrailerActorId id, CharacterVisualController visual, Color color)
        {
            actorId = id;
            visualController = visual;
            if (visualController != null)
            {
                visualController.ResolveReferences();
                visualController.EnsureAnimatorControllerAssigned();
                visualController.EnsureFeelPivot();
                animator = visualController.Animator;
            }
            else
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            if (visualController != null)
            {
                visualController.SetColor(color);
            }
        }

        public void SetFallbackFloorY(float floorY)
        {
            _fallbackFloorY = floorY;
        }

        public void ResetActor(Vector3 position, Quaternion rotation, Color color, bool snapToFloor = true)
        {
            _isMoving = false;
            _isPaused = false;
            _waypoints.Clear();
            _waypointIndex = 0;
            _mountedSeatAnchor = null;
            _mountedBikeRoot = null;
            _isMounted = false;

            if (snapToFloor)
                position = SnapPositionToFloor(position, _fallbackFloorY);

            transform.SetPositionAndRotation(position, rotation);
            ClearAllAttachedProps();
            UnequipBackpack();

            if (visualController != null)
            {
                visualController.SetColor(color);
                visualController.SetSitting(false, false);
                visualController.SetPreparing(false);
                visualController.SetLocomotion(0f, 1f, true, false, false);
                var pivot = visualController.EnsureFeelPivot();
                pivot?.StopAndReset();
                if (visualController.HeadBone != null)
                {
                    visualController.HeadBone.localRotation = Quaternion.identity;
                }
            }

            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
            }
        }

        public void StartMove(Vector3 targetPosition, float duration, float speedOverride = 0f)
        {
            if (_isMounted) return;
            targetPosition = SnapPositionToFloor(targetPosition, transform.position.y);

            _waypoints.Clear();
            _waypointIndex = 0;

            bool usedNavPath = false;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 1.5f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(targetPosition, out NavMeshHit endHit, 1.8f, NavMesh.AllAreas))
            {
                var navPath = new NavMeshPath();
                if (NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, navPath) &&
                    navPath.status == NavMeshPathStatus.PathComplete &&
                    navPath.corners != null && navPath.corners.Length >= 2)
                {
                    for (int i = 1; i < navPath.corners.Length; i++)
                    {
                        _waypoints.Add(SnapPositionToFloor(navPath.corners[i], targetPosition.y));
                    }
                    usedNavPath = _waypoints.Count > 0;
                }
            }

            if (!usedNavPath)
            {
                _waypoints.Add(targetPosition);
            }

            float totalDist = 0f;
            Vector3 prev = transform.position;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                totalDist += Vector3.Distance(prev, _waypoints[i]);
                prev = _waypoints[i];
            }

            if (speedOverride > 0.1f)
            {
                _moveSpeed = speedOverride;
            }
            else
            {
                _moveSpeed = duration > 0.05f ? Mathf.Clamp(totalDist / duration, 1.4f, 4.8f) : 2.1f;
            }

            _isMoving = totalDist > 0.04f;
            if (visualController != null)
            {
                visualController.SetLocomotion(_isMoving ? _moveSpeed : 0f, 1f, true);
            }
        }

        public void TickMove(float dt)
        {
            if (_isPaused) return;

            if (_isMounted)
            {
                SyncMountedPose();
                return;
            }

            if (!_isMoving || _waypointIndex >= _waypoints.Count) return;

            Vector3 current = transform.position;
            Vector3 target = _waypoints[_waypointIndex];
            Vector3 toTarget = target - current;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist <= 0.04f)
            {
                _waypointIndex++;
                if (_waypointIndex >= _waypoints.Count)
                {
                    _isMoving = false;
                    transform.position = SnapPositionToFloor(target, current.y);
                    if (visualController != null)
                        visualController.SetLocomotion(0f, 1f, true);
                }
                return;
            }

            Vector3 dir = toTarget / dist;
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 420f * dt);

            float step = Mathf.Min(dist, _moveSpeed * dt);
            Vector3 nextPos = current + dir * step;
            nextPos = SnapPositionToFloor(nextPos, current.y);
            transform.position = nextPos;

            if (visualController != null)
            {
                visualController.SetLocomotion(_moveSpeed, 1f, true);
            }
        }

        private void LateUpdate()
        {
            if (_isMounted)
            {
                SyncMountedPose();
            }
        }

        public void FacePosition(Vector3 targetPosition)
        {
            Vector3 dir = targetPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        public void SetPreparing(bool preparing)
        {
            if (visualController != null)
                visualController.SetPreparing(preparing);
        }

        public void SetSitting(bool sitting, bool triggerTransition = false)
        {
            if (visualController != null)
                visualController.SetSitting(sitting, triggerTransition);
        }

        public void MountToMotorcycle(Transform seatAnchor, Transform bikeRoot)
        {
            _isMoving = false;
            _waypoints.Clear();
            _mountedSeatAnchor = seatAnchor;
            _mountedBikeRoot = bikeRoot;
            _isMounted = seatAnchor != null || bikeRoot != null;

            if (visualController != null)
            {
                visualController.SetPreparing(false);
                visualController.SetLocomotion(0f, 1f, true);
                visualController.SetSitting(true, false);
            }
            SyncMountedPose();
        }

        public void DismountFromMotorcycle(Vector3 dismountWorldPos, Quaternion dismountRot)
        {
            _isMounted = false;
            _mountedSeatAnchor = null;
            _mountedBikeRoot = null;

            Vector3 snapped = SnapPositionToFloor(dismountWorldPos, _fallbackFloorY);
            transform.SetPositionAndRotation(snapped, dismountRot);

            if (visualController != null)
            {
                visualController.SetSitting(false, false);
                visualController.SetLocomotion(0f, 1f, true);
            }
        }

        private void SyncMountedPose()
        {
            if (_mountedSeatAnchor != null)
            {
                Quaternion rot = _mountedBikeRoot != null ? _mountedBikeRoot.rotation : _mountedSeatAnchor.rotation;
                transform.SetPositionAndRotation(_mountedSeatAnchor.TransformPoint(_seatLocalOffset), rot);
            }
            else if (_mountedBikeRoot != null)
            {
                transform.SetPositionAndRotation(_mountedBikeRoot.TransformPoint(new Vector3(0f, 0.72f, -0.1f)), _mountedBikeRoot.rotation);
            }
        }

        public Transform GetHandBone(bool rightHand)
        {
            if (visualController != null)
            {
                Transform b = rightHand ? visualController.RightHandBone : visualController.LeftHandBone;
                if (b != null) return b;
            }
            return transform;
        }

        public void AttachProp(GameObject prop, bool rightHand = true, ItemPresentationProfile profile = null)
        {
            if (prop == null) return;
            GameObject previous = TakeProp(rightHand);
            if (previous != null && previous != prop)
            {
                previous.SetActive(false);
                Destroy(previous);
            }

            if (rightHand) _rightHandProp = prop;
            else _leftHandProp = prop;

            Transform bone = GetHandBone(rightHand);
            var wrapper = prop.GetComponent<ItemVisualWrapper>();
            if (wrapper != null)
            {
                var pose = rightHand
                    ? (profile != null ? profile.remoteRightHandPose : ItemPoseOffset.Identity)
                    : (profile != null ? profile.remoteLeftHandPose : ItemPoseOffset.Identity);
                wrapper.AttachTo(bone, pose, 0, useSupportPoint: false);
            }
            else
            {
                prop.transform.SetParent(bone, false);
                prop.transform.localPosition = new Vector3(0f, 0f, 0.08f);
                prop.transform.localRotation = Quaternion.identity;
            }
            prop.SetActive(true);
        }

        public GameObject TakeProp(bool rightHand = true)
        {
            GameObject prop = rightHand ? _rightHandProp : _leftHandProp;
            if (rightHand) _rightHandProp = null;
            else _leftHandProp = null;
            return prop;
        }

        public GameObject PeekProp(bool rightHand = true) => rightHand ? _rightHandProp : _leftHandProp;

        public void ClearAllAttachedProps()
        {
            if (_rightHandProp != null)
            {
                _rightHandProp.SetActive(false);
                Destroy(_rightHandProp);
                _rightHandProp = null;
            }
            if (_leftHandProp != null)
            {
                _leftHandProp.SetActive(false);
                Destroy(_leftHandProp);
                _leftHandProp = null;
            }
        }

        public void EquipBackpack(GameObject bagVisualPrefab)
        {
            if (_equippedBackpack != null)
            {
                _equippedBackpack.SetActive(true);
                return;
            }
            if (bagVisualPrefab == null) return;

            Transform mount = visualController != null && visualController.ChestBone != null
                ? visualController.ChestBone
                : transform;

            var bagRoot = new GameObject("TrailerDeliveryBag");
            bagRoot.transform.SetParent(mount, false);
            bagRoot.transform.localPosition = new Vector3(0f, 0.05f, -0.22f);
            bagRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var pivotGo = new GameObject("CosmeticPivot");
            pivotGo.transform.SetParent(bagRoot.transform, false);
            _backpackPivot = pivotGo.AddComponent<GameFeelVisualPivot>();
            _backpackPivot.SetExplicitBasePose(Vector3.zero, Quaternion.identity, Vector3.one);

            var inst = Instantiate(bagVisualPrefab, pivotGo.transform, false);
            inst.name = "BagVisual";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            foreach (var col in inst.GetComponentsInChildren<Collider>(true))
                Destroy(col);

            var mf = inst.GetComponentInChildren<MeshFilter>(true);
            float scaleFactor = 0.52f;
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 bounds = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                float maxAxis = Mathf.Max(Mathf.Abs(bounds.x), Mathf.Abs(bounds.y), Mathf.Abs(bounds.z));
                if (maxAxis > 0.001f) scaleFactor = 0.62f / maxAxis;
            }
            bagRoot.transform.localScale = Vector3.one * scaleFactor;
            TrailerPropFactory.EnsureUrpMaterialAndTint(inst, "bag");
            _equippedBackpack = bagRoot;
        }

        public void TriggerBackpackPulse()
        {
            if (_backpackPivot == null) return;
            var recipe = GameFeelRecipe.CreateDefault(GameFeelEventId.BackpackStored, GameFeelMaterialFamily.FabricCargo);
            recipe.Duration = 0.25f;
            _backpackPivot.PlayRecipe(in recipe, 1.15f, (int)actorId + 77);
        }

        public void UnequipBackpack()
        {
            if (_equippedBackpack != null)
            {
                _equippedBackpack.SetActive(false);
                Destroy(_equippedBackpack);
                _equippedBackpack = null;
                _backpackPivot = null;
            }
        }

        public void PlayReaction(GameFeelEventId eventId, float intensity = 1.1f)
        {
            if (visualController == null) return;
            visualController.PlayBodyReaction(eventId, intensity, (int)actorId + 13, (ulong)actorId + 1);
        }

        public void SetHeadLookDown(float pitchDegrees)
        {
            if (visualController != null && visualController.HeadBone != null)
            {
                visualController.HeadBone.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            }
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            if (animator != null)
                animator.speed = paused ? 0f : 1f;
        }

        public Vector3 SnapPositionToFloor(Vector3 desiredPos, float fallbackFloorY)
        {
            Vector3 probeStart = desiredPos + Vector3.up * 2.2f;
            RaycastHit[] hits = Physics.RaycastAll(probeStart, Vector3.down, 6.5f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.MinValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (_mountedBikeRoot != null && col.transform.IsChildOf(_mountedBikeRoot)) continue;

                string n = col.name.ToLowerInvariant();
                if (n.Contains("counter") || n.Contains("table") || n.Contains("station") ||
                    n.Contains("bench") || n.Contains("oven") || n.Contains("shelf") || n.Contains("chair"))
                    continue;
                bool isSurface = n.Contains("floor") || n.Contains("piso") || n.Contains("ground") ||
                                 n.Contains("chao") || n.Contains("street") || n.Contains("sidewalk") ||
                                 n.Contains("parkinglotpad") || n.Contains("intersection") || hits[i].normal.y > 0.75f;
                if (!isSurface) continue;

                // Ignora tampos de bancadas/mesas altas acima do nível esperado do piso
                float refFloor = _fallbackFloorY != 0f ? _fallbackFloorY : desiredPos.y;
                if (hits[i].point.y > refFloor + 0.28f) continue;

                if (hits[i].point.y > bestY)
                {
                    bestY = hits[i].point.y;
                    found = true;
                }
            }

            if (found)
            {
                desiredPos.y = bestY;
                return desiredPos;
            }

            desiredPos.y = fallbackFloorY != 0f ? fallbackFloorY : desiredPos.y;
            return desiredPos;
        }
    }
}

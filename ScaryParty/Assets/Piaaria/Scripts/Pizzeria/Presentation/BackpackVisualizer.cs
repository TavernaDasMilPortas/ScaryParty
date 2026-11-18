using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation.Feedback;
using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation
{
    public class BackpackVisualizer : MonoBehaviour
    {
        [Header("Visual da mochila")]
        public GameObject deliveryBagPrefab; // Legacy serialized reference; authored bags provide the mesh used at runtime.
        [SerializeField] private Transform backpackMount;
        [SerializeField] private Vector3 mountOffset = new Vector3(0f, 0.05f, -0.22f);
        [SerializeField] private Vector3 mountScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 mountEuler = new Vector3(0f, 180f, 0f);

        private GameObject _visual;
        private GameFeelVisualPivot _cosmeticPivot;
        private NetworkObject _player;
        private ulong _displayedBagId;
        private bool _hasSampledInitial;
        private bool _wasEquipped;
        private int _lastItemCount;
        private int _lastIntegrity = -1;
        private uint _lastRevision;
        private Renderer[] _bagRenderers;
        private MaterialPropertyBlock _damageMpb;
        private Vector3 _lastPlayerPos;
        private float _smoothedSpeed;
        private float _swayPhase;
        private bool _wasMovingWithCargo;

        private void Awake()
        {
            _player = GetComponent<NetworkObject>();
            _lastPlayerPos = transform.position;
        }

        private void Update()
        {
            ulong owner = _player != null ? _player.OwnerClientId :
                (NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue);
            bool isLocalOwner = _player != null && _player.IsOwner;
            bool equipped = CarriedCargoResolver.TryGetEquippedBackpack(PizzeriaNetworkState.Instance, owner, out var bag);
            if (!equipped)
            {
                if (_visual != null)
                {
                    Destroy(_visual);
                    _visual = null;
                    _cosmeticPivot = null;
                    _displayedBagId = 0;
                }
                _hasSampledInitial = true;
                _wasEquipped = false;
                _lastItemCount = 0;
                _smoothedSpeed = 0f;
                _wasMovingWithCargo = false;
                _lastPlayerPos = transform.position;
                return;
            }
            if (_visual != null && _displayedBagId != bag.BackpackId)
            {
                Destroy(_visual);
                _visual = null;
                _cosmeticPivot = null;
            }
            bool newlyCreatedVisual = false;
            if (_visual == null)
            {
                Vector3 visualScale = mountScale;
                Quaternion visualRotation = Quaternion.Euler(mountEuler);
                _displayedBagId = bag.BackpackId;
                Transform mount = backpackMount;
                if (mount == null)
                {
                    var visualCtrl = GetComponentInChildren<ScaryParty.Player.CharacterVisualController>(true);
                    if (visualCtrl != null && visualCtrl.ChestBone != null)
                    {
                        mount = visualCtrl.ChestBone;
                    }
                    else
                    {
                        var animator = GetComponentInChildren<Animator>();
                        if (animator != null && animator.isHuman)
                            mount = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                        else
                        {
                            var allT = GetComponentsInChildren<Transform>(true);
                            foreach (var t in allT)
                            {
                                if (string.Equals(t.name, "chest", System.StringComparison.OrdinalIgnoreCase))
                                {
                                    mount = t;
                                    break;
                                }
                            }
                        }
                    }
                }
                var authored = FindObjectsByType<ScaryParty.Pizzeria.Authoring.DeliveryBagAuthoring>(FindObjectsSortMode.None);
                foreach (var authoredBag in authored)
                {
                    if (authoredBag.BagRenderer == null || authoredBag.BackpackId != _displayedBagId) continue;
                    var mesh = authoredBag.BagRenderer.GetComponent<MeshFilter>();
                    if (mesh == null || mesh.sharedMesh == null) break;
                    _visual = new GameObject("EquippedDeliveryBag");
                    _visual.transform.SetParent(mount != null ? mount : transform, false);

                    var pivotGo = new GameObject("CosmeticPivot");
                    pivotGo.transform.SetParent(_visual.transform, false);
                    pivotGo.transform.localPosition = Vector3.zero;
                    pivotGo.transform.localRotation = Quaternion.identity;
                    pivotGo.transform.localScale = Vector3.one;
                    _cosmeticPivot = pivotGo.AddComponent<GameFeelVisualPivot>();
                    _cosmeticPivot.SetExplicitBasePose(Vector3.zero, Quaternion.identity, Vector3.one);

                    var meshGo = new GameObject("BagMesh");
                    meshGo.transform.SetParent(pivotGo.transform, false);
                    meshGo.transform.localPosition = Vector3.zero;
                    meshGo.transform.localRotation = Quaternion.identity;
                    meshGo.transform.localScale = Vector3.one;
                    meshGo.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                    meshGo.AddComponent<MeshRenderer>().sharedMaterials = authoredBag.BagRenderer.sharedMaterials;

                    Vector3 sourceScale = authoredBag.BagVisual.lossyScale;
                    visualRotation = Quaternion.Euler(mountEuler) * authoredBag.VisualLocalRotation;
                    // Preserve the authored proportions, then fit the largest axis to a wearable size.
                    Vector3 bounds = Vector3.Scale(mesh.sharedMesh.bounds.size, sourceScale);
                    float maxDimension = Mathf.Max(Mathf.Abs(bounds.x), Mathf.Abs(bounds.y), Mathf.Abs(bounds.z));
                    if (maxDimension > 0.0001f)
                        visualScale = sourceScale * (0.65f / maxDimension);
                    break;
                }
                if (_visual == null) return;
                _visual.name = "EquippedDeliveryBag";
                _visual.transform.localPosition = mountOffset;
                _visual.transform.localRotation = visualRotation;
                _visual.transform.localScale = visualScale;
                foreach (var col in _visual.GetComponentsInChildren<Collider>(true)) Destroy(col);
                int localLayer = LayerMask.NameToLayer("LocalPlayerModel");
                if (_player != null && _player.IsOwner && localLayer >= 0)
                    SetLayer(_visual.transform, localLayer);
                newlyCreatedVisual = true;
            }
            _visual.SetActive(true);

            Transform eventTarget = _cosmeticPivot != null ? _cosmeticPivot.transform : _visual.transform;
            if (!_hasSampledInitial)
            {
                _hasSampledInitial = true;
                _wasEquipped = true;
                _lastItemCount = bag.ItemCount;
                _lastRevision = bag.Revision;
                _lastPlayerPos = transform.position;
            }
            else
            {
                if (!_wasEquipped || newlyCreatedVisual)
                {
                    GameFeelPresenter.EnsureInstance().TriggerSimple(
                        GameFeelEventId.BackpackEquipped,
                        eventTarget,
                        bag.BackpackId,
                        0,
                        bag.Revision,
                        GameFeelMaterialFamily.FabricCargo,
                        1f,
                        isLocalOwner);
                }
                else if (bag.ItemCount != _lastItemCount)
                {
                    var evt = bag.ItemCount > _lastItemCount
                        ? GameFeelEventId.BackpackStored
                        : GameFeelEventId.BackpackRemoved;
                    float intensity = Mathf.Clamp(1f + bag.ItemCount * 0.08f, 1f, 1.35f);
                    GameFeelPresenter.EnsureInstance().TriggerSimple(
                        evt,
                        eventTarget,
                        bag.BackpackId,
                        0,
                        bag.Revision,
                        GameFeelMaterialFamily.FabricCargo,
                        intensity,
                        isLocalOwner);
                }
                _wasEquipped = true;
                _lastItemCount = bag.ItemCount;
                _lastRevision = bag.Revision;
            }

            UpdateCargoSway(bag.ItemCount, bag.IsTorn);
            UpdateBagDamageFeedback(bag, isLocalOwner);
        }

        private void UpdateBagDamageFeedback(NetBackpackDto bag, bool isLocalOwner)
        {
            if (bag.MaxIntegrity <= 0) return;

            if (_lastIntegrity >= 0 && bag.Integrity < _lastIntegrity && isLocalOwner)
            {
                ContextualTutorialPresenter.NotifyContextualTip(
                    "bag_damaged",
                    "Mochila Danificada",
                    "Dano recebido desgasta a mochila. Se chegar a 0%, ela rasga e derruba 1 caixa! Segure [F] (3s) para reparar.");
            }
            _lastIntegrity = bag.Integrity;

            if (_visual == null) return;
            if (_bagRenderers == null) _bagRenderers = _visual.GetComponentsInChildren<Renderer>(true);
            if (_damageMpb == null) _damageMpb = new MaterialPropertyBlock();

            float ratio = Mathf.Clamp01((float)bag.Integrity / bag.MaxIntegrity);
            Color tint = bag.IsTorn
                ? new Color(0.45f, 0.16f, 0.16f, 1f)
                : Color.Lerp(new Color(0.85f, 0.38f, 0.25f, 1f), Color.white, ratio);

            float fillNorm = bag.Capacity > 0 ? Mathf.Clamp01((float)bag.ItemCount / bag.Capacity) : 0f;
            Vector3 loadScale = new Vector3(
                1f + fillNorm * 0.16f,
                bag.IsTorn ? 0.82f : (1f - (1f - ratio) * 0.08f),
                1f + fillNorm * 0.22f);
            if (_cosmeticPivot != null && GameFeelSettings.ReduceMotion)
            {
                _cosmeticPivot.transform.localScale = loadScale;
            }
            else if (_cosmeticPivot == null)
            {
                _visual.transform.localScale = Vector3.Scale(mountScale, loadScale);
            }

            for (int i = 0; i < _bagRenderers.Length; i++)
            {
                var r = _bagRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_damageMpb);
                _damageMpb.SetColor("_BaseColor", tint);
                _damageMpb.SetColor("_Color", tint);
                r.SetPropertyBlock(_damageMpb);
            }
        }

        private void UpdateCargoSway(int itemCount, bool isTorn)
        {
            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - _lastPlayerPos;
            delta.y = 0f;
            float dt = Time.deltaTime;
            float instSpeed = dt > 0.0001f ? delta.magnitude / dt : 0f;
            _lastPlayerPos = currentPos;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, instSpeed, 1f - Mathf.Exp(-10f * Mathf.Max(0.001f, dt)));

            if (_cosmeticPivot == null) return;

            if (GameFeelSettings.ReduceMotion || _smoothedSpeed < 0.15f)
            {
                if (_wasMovingWithCargo)
                {
                    _wasMovingWithCargo = false;
                    _cosmeticPivot.ClearContinuousOffset();
                }
                return;
            }

            _wasMovingWithCargo = true;
            float moveNorm = Mathf.Clamp01(_smoothedSpeed / 5.5f);
            float cargoMult = 1f + Mathf.Clamp(itemCount, 0, 6) * 0.14f;
            float presetMult = GameFeelSettings.GetEffectiveChannelIntensity(GameFeelChannel.Movement);
            float amp = moveNorm * cargoMult * presetMult;

            _swayPhase += dt * Mathf.Lerp(7.5f, 12.5f, moveNorm);
            float roll = Mathf.Sin(_swayPhase) * 4.5f * amp;
            float pitch = Mathf.Cos(_swayPhase * 2f) * 3.0f * amp;
            float bobY = Mathf.Abs(Mathf.Sin(_swayPhase)) * 0.018f * amp;
            float squash = Mathf.Sin(_swayPhase * 2f) * 0.03f * amp;

            _cosmeticPivot.SetContinuousOffset(
                new Vector3(0f, bobY, 0f),
                new Vector3(pitch, 0f, roll),
                new Vector3(1f + squash * 0.5f, (isTorn ? 0.85f : 1f) - squash, 1f + squash * 0.5f));
        }

        private void OnDisable()
        {
            if (_cosmeticPivot != null)
            {
                _cosmeticPivot.StopAndReset();
            }
            _smoothedSpeed = 0f;
            _wasMovingWithCargo = false;
        }

        private void OnDestroy() { if (_visual != null) Destroy(_visual); }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }
    }
}

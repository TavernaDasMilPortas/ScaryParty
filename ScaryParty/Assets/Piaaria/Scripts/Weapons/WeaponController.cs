using UnityEngine;
using Unity.Netcode;
using StarterAssets;
using ScaryParty.Combat;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Presenters;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class WeaponController : MonoBehaviour
{
    public static bool LocalDrivingMotorcycle { get; set; }

    [Header("Weapons")]
    public WeaponBase pistolPrefab;
    public WeaponBase shotgunPrefab;

    [Header("References")]
    public Transform rightHandAttachment;
    public Animator animator;
    public ThirdPersonController tpc;

    private WeaponMode currentMode = WeaponMode.Unarmed;
    private WeaponBase currentWeaponInstance;
    private PistolWeapon pistolInstance;
    private ShotgunWeapon shotgunInstance;
    private GameObject slungShotgunVisual;
    private Transform slingBackAttachment;
    private NetworkObject _netObj;
    private WeaponInputHandler _inputHandler;
    private PlayerHealth _health;

    private bool isAiming = false;
    private bool _isReloading = false;
    private float _reloadRemaining = 0f;
    private float lastFireTime = 0f;
    private CharacterController _cc;

    private readonly int AnimWeaponMode = Animator.StringToHash("WeaponMode");
    private readonly int AnimIsAiming = Animator.StringToHash("IsAiming");
    private readonly int AnimFire = Animator.StringToHash("Fire");
    private readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private readonly int AnimIsSprinting = Animator.StringToHash("IsSprinting");

    private void Start()
    {
        _netObj = GetComponent<NetworkObject>();
        _inputHandler = GetComponent<WeaponInputHandler>();
        _health = GetComponent<PlayerHealth>();
        _cc = GetComponent<CharacterController>();

        var visualCtrl = GetComponentInChildren<ScaryParty.Player.CharacterVisualController>(true);
        if (animator == null)
        {
            if (visualCtrl != null && visualCtrl.Animator != null)
                animator = visualCtrl.Animator;
            else
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
        if (tpc == null) tpc = GetComponent<ThirdPersonController>();

        if (rightHandAttachment == null)
        {
            if (visualCtrl != null && visualCtrl.RightHandBone != null)
            {
                rightHandAttachment = visualCtrl.RightHandBone;
            }
            else if (animator != null)
            {
                if (animator.isHuman)
                    rightHandAttachment = animator.GetBoneTransform(HumanBodyBones.RightHand);

                if (rightHandAttachment == null)
                {
                    Transform[] allTransforms = GetComponentsInChildren<Transform>();
                    foreach (Transform t in allTransforms)
                    {
                        string lowerName = t.name.ToLowerInvariant();
                        if (lowerName == "arm2.r" || lowerName.Contains("righthand") || lowerName.EndsWith("right_hand"))
                        {
                            rightHandAttachment = t;
                            break;
                        }
                    }
                }
            }
        }

        Transform targetAttachment = rightHandAttachment;
        bool isLocal = _netObj != null ? _netObj.IsOwner : (tpc != null && tpc.IsOwner);

        if (isLocal && ScaryParty.Player.FirstPersonCameraController.LocalInstance != null)
        {
            targetAttachment = ScaryParty.Player.FirstPersonCameraController.LocalInstance.RightHandAnchor;
        }
        if (targetAttachment == null) targetAttachment = transform;

        if (pistolPrefab != null)
        {
            pistolInstance = Instantiate(pistolPrefab, targetAttachment) as PistolWeapon;
        }
        else
        {
            pistolInstance = CreatePlaceholderWeapon<PistolWeapon>(targetAttachment, "PlaceholderPistol", false);
        }

        if (pistolInstance != null)
        {
            pistolInstance.transform.localPosition = Vector3.zero;
            pistolInstance.transform.localRotation = Quaternion.identity;
            if (isLocal)
            {
                int fpLayer = LayerMask.NameToLayer("FirstPersonOnly");
                if (fpLayer >= 0) SetLayerRecursively(pistolInstance.gameObject, fpLayer);
            }
            pistolInstance.OnUnequip();
        }

        if (shotgunPrefab != null)
        {
            shotgunInstance = Instantiate(shotgunPrefab, targetAttachment) as ShotgunWeapon;
        }
        else
        {
            shotgunInstance = CreatePlaceholderWeapon<ShotgunWeapon>(targetAttachment, "PlaceholderShotgun", true);
        }

        if (shotgunInstance != null)
        {
            shotgunInstance.transform.localPosition = Vector3.zero;
            shotgunInstance.transform.localRotation = Quaternion.identity;
            if (isLocal)
            {
                int fpLayer = LayerMask.NameToLayer("FirstPersonOnly");
                if (fpLayer >= 0) SetLayerRecursively(shotgunInstance.gameObject, fpLayer);
            }
            shotgunInstance.OnUnequip();
        }

        SetWeaponMode(WeaponMode.Unarmed, forceFromReplication: true);
        EnsureSlingVisualInitialized(visualCtrl, isLocal);
    }

    private void EnsureSlingVisualInitialized(ScaryParty.Player.CharacterVisualController visualCtrl, bool isLocal)
    {
        if (slungShotgunVisual != null) return;

        if (visualCtrl != null && visualCtrl.ChestBone != null)
            slingBackAttachment = visualCtrl.ChestBone;
        else if (animator != null && animator.isHuman)
            slingBackAttachment = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);

        if (slingBackAttachment == null) slingBackAttachment = transform;

        if (slungShotgunVisual == null)
        {
            slungShotgunVisual = new GameObject("SlungShotgun3PVisual");
            slungShotgunVisual.transform.SetParent(slingBackAttachment, false);
            slungShotgunVisual.transform.localPosition = new Vector3(-0.08f, 0.04f, -0.15f);
            slungShotgunVisual.transform.localRotation = Quaternion.Euler(15f, 90f, 38f);

            var slingRig = slungShotgunVisual.AddComponent<ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig>();
            slingRig.rigRole = ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig.RigRole.Weapon;
            slingRig.placeholderId = "weapon_shotgun_sling";
            slingRig.ResolveAnchor(ref slingRig.slingAnchor, "SlingAnchor", Vector3.zero, Vector3.zero);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Mesh";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(slungShotgunVisual.transform, false);
            body.transform.localScale = new Vector3(0.06f, 0.08f, 0.56f);
            var renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (urp != null && (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name == "Standard"))
                    renderer.sharedMaterial = new Material(urp);
                if (renderer.material != null) renderer.material.color = new Color(0.28f, 0.22f, 0.18f);
            }
        }

        if (isLocal)
        {
            int localModelLayer = LayerMask.NameToLayer("LocalPlayerModel");
            if (localModelLayer >= 0) SetLayerRecursively(slungShotgunVisual, localModelLayer);
        }
        slungShotgunVisual.SetActive(false);
    }

    private void Update()
    {
        UpdateAnimatorLocomotionBools();

        ulong ownerId = _netObj != null ? _netObj.OwnerClientId : 0;
        var netState = PizzeriaNetworkState.Instance;
        NetWeaponStateDto repWeapon = default;
        bool hasRepWeapon = netState != null && netState.TryGetEquippedWeapon(ownerId, out repWeapon);
        WeaponMode repMode = hasRepWeapon ? (WeaponMode)repWeapon.WeaponType : WeaponMode.Unarmed;

        if (repMode != currentMode)
        {
            _isReloading = false;
            _reloadRemaining = 0f;
            SetWeaponMode(repMode, forceFromReplication: true);
        }

        if (slungShotgunVisual != null)
        {
            bool isSlinged = netState != null && netState.TryGetSlingedWeapon(ownerId, out _);
            if (slungShotgunVisual.activeSelf != isSlinged)
                slungShotgunVisual.SetActive(isSlinged);
        }

        bool isLocalOwner = _netObj != null ? _netObj.IsOwner : (tpc != null && tpc.IsOwner);
        if (!isLocalOwner)
        {
            if (animator != null && netState != null && netState.TryGetPlayerCombatState(ownerId, out var repCombat))
            {
                animator.SetBool(AnimIsAiming, repCombat.IsAiming);
            }
            return;
        }

        bool lifeBlocked = _health != null && (_health.IsIncapacitated || _health.IsDead);
        bool controlsAllowed = currentMode != WeaponMode.Unarmed && !lifeBlocked && (!LocalDrivingMotorcycle || currentMode == WeaponMode.Pistol);
        if (_inputHandler != null && _inputHandler.enableWeaponControls != controlsAllowed)
        {
            _inputHandler.enableWeaponControls = controlsAllowed;
            if (!controlsAllowed) StopAim();
        }

        if (!lifeBlocked && !GameplayInputGate.IsLocked && IsSlingPressed())
        {
            PizzeriaCommandHandler.Instance?.ToggleSlingShotgunServerRpc();
        }

        if (_isReloading)
        {
            _reloadRemaining -= Time.deltaTime;
            if (_reloadRemaining <= 0f)
            {
                _isReloading = false;
                PizzeriaCommandHandler.Instance?.ReloadWeaponServerRpc();
            }
        }
        else if (controlsAllowed && !GameplayInputGate.IsLocked && IsReloadPressed())
        {
            if (hasRepWeapon && repWeapon.CurrentMagAmmo < repWeapon.MaxMagAmmo && repWeapon.RackReserveAmmo > 0)
            {
                StartLocalReload(repMode);
            }
            else if (hasRepWeapon && repWeapon.RackReserveAmmo <= 0)
            {
                NotificationManager.Show("Sem reserva de munição! Aproxime-se do rack e pressione [R].", NotificationType.Warning);
            }
        }

        string weaponLabel = currentMode switch
        {
            WeaponMode.Pistol => "Pistola",
            WeaponMode.Shotgun => "Escopeta",
            _ => string.Empty
        };

        HudPresenter.Instance?.SetWeaponAmmoHud(
            controlsAllowed,
            weaponLabel,
            hasRepWeapon ? repWeapon.CurrentMagAmmo : 0,
            hasRepWeapon ? repWeapon.RackReserveAmmo : 0,
            _isReloading,
            isAiming);
    }

    private void StartLocalReload(WeaponMode mode)
    {
        _isReloading = true;
        _reloadRemaining = mode == WeaponMode.Shotgun ? 2.0f : 1.4f;
        StopAim();
    }

    private static bool IsReloadPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.R);
    }

    private static bool IsSlingPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.G);
    }

    public bool IsCurrentWeaponAutomatic => currentWeaponInstance != null && currentWeaponInstance.isAutomatic;

    public void CycleWeapon(int direction)
    {
        if (_inputHandler != null && (!_inputHandler.enableWeaponControls || !_inputHandler.enableWeaponScroll))
            return;

        int mode = (int)currentMode + direction;
        if (mode > 2) mode = 0;
        if (mode < 0) mode = 2;
        SetWeaponMode((WeaponMode)mode);
    }

    public void SetWeaponMode(WeaponMode mode, bool forceFromReplication = false)
    {
        if (!forceFromReplication && _inputHandler != null && !_inputHandler.enableWeaponControls && mode != WeaponMode.Unarmed)
        {
            mode = WeaponMode.Unarmed;
        }

        if (currentMode == mode) return;

        if (currentWeaponInstance != null)
        {
            currentWeaponInstance.OnUnequip();
        }

        currentMode = mode;
        if (animator != null)
        {
            animator.SetInteger(AnimWeaponMode, (int)currentMode);
            animator.ResetTrigger(AnimFire);
        }

        switch (currentMode)
        {
            case WeaponMode.Pistol:
                currentWeaponInstance = pistolInstance;
                break;
            case WeaponMode.Shotgun:
                currentWeaponInstance = shotgunInstance;
                break;
            default:
                currentWeaponInstance = null;
                break;
        }

        if (currentWeaponInstance != null)
        {
            currentWeaponInstance.OnEquip();
        }
    }

    public void StartAim()
    {
        if (_inputHandler != null && !_inputHandler.enableWeaponControls) return;
        if (currentMode == WeaponMode.Unarmed || _isReloading) return;
        if (!isAiming)
        {
            isAiming = true;
            if (animator != null) animator.SetBool(AnimIsAiming, true);
            ScaryParty.Player.FirstPersonCameraController.LocalInstance?.SetAimState(true);
            PizzeriaCommandHandler.Instance?.SetWeaponAimServerRpc(true);
        }
    }

    public void StopAim()
    {
        if (isAiming)
        {
            isAiming = false;
            if (animator != null) animator.SetBool(AnimIsAiming, false);
            ScaryParty.Player.FirstPersonCameraController.LocalInstance?.SetAimState(false);
            PizzeriaCommandHandler.Instance?.SetWeaponAimServerRpc(false);
        }
    }

    public void TriggerFire()
    {
        if (_inputHandler != null && !_inputHandler.enableWeaponControls) return;
        if (_isReloading || (LocalDrivingMotorcycle && currentMode != WeaponMode.Pistol)) return;
        if (currentMode == WeaponMode.Unarmed || currentWeaponInstance == null) return;
        if (_health != null && (_health.IsIncapacitated || _health.IsDead)) return;

        if (Time.time < lastFireTime + currentWeaponInstance.fireRate) return;

        ulong ownerId = _netObj != null ? _netObj.OwnerClientId : 0;
        if (PizzeriaNetworkState.Instance != null && PizzeriaNetworkState.Instance.TryGetEquippedWeapon(ownerId, out var repWeapon))
        {
            if (repWeapon.CurrentMagAmmo <= 0)
            {
                lastFireTime = Time.time;
                if (repWeapon.RackReserveAmmo > 0) StartLocalReload(currentMode);
                else NotificationManager.Show("Pente vazio! Aproxime-se do rack e pressione [R].", NotificationType.Warning);
                return;
            }
        }

        lastFireTime = Time.time;
        if (animator != null) animator.SetTrigger(AnimFire);

        Camera cam = Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.55f;
        Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
        PizzeriaCommandHandler.Instance?.FireWeaponServerRpc(origin, dir);
    }

    public void PlayRemoteOrLocalFireVisual(Vector3 origin, Vector3 hitPoint, bool didHit)
    {
        currentWeaponInstance?.Shoot();
        SpawnShotTracer(currentWeaponInstance != null ? currentWeaponInstance.transform.position : origin, hitPoint);
    }

    private void SpawnShotTracer(Vector3 start, Vector3 end)
    {
        var tracerGo = new GameObject("ShotTracer");
        var lr = tracerGo.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth = 0.018f;
        lr.endWidth = 0.008f;
        lr.useWorldSpace = true;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) lr.material = new Material(shader);
        lr.startColor = new Color(1f, 0.88f, 0.45f, 0.9f);
        lr.endColor = new Color(1f, 0.65f, 0.2f, 0.1f);
        Destroy(tracerGo, 0.06f);
    }

    private static T CreatePlaceholderWeapon<T>(Transform parent, string goName, bool isShotgun) where T : WeaponBase
    {
        var root = new GameObject(goName);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0.02f, -0.02f, 0.14f);
        root.transform.localRotation = Quaternion.identity;

        var rig = root.AddComponent<ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig>();
        rig.rigRole = ScaryParty.Pizzeria.Presentation.EntityVisualAnchorRig.RigRole.Weapon;
        rig.placeholderId = isShotgun ? "weapon_shotgun_fp" : "weapon_pistol_fp";
        rig.ResolveAnchor(ref rig.gripAnchor, "GripAnchor", Vector3.zero, Vector3.zero);
        rig.ResolveAnchor(ref rig.aimSightAnchor, "AimSightAnchor", new Vector3(0f, 0.06f, isShotgun ? 0.14f : 0.08f), Vector3.zero);
        rig.ResolveAnchor(ref rig.muzzleAnchor, "MuzzleAnchor", new Vector3(0f, 0.02f, isShotgun ? 0.46f : 0.21f), Vector3.zero);
        if (isShotgun)
            rig.ResolveAnchor(ref rig.slingAnchor, "SlingAnchor", new Vector3(0f, 0f, -0.05f), Vector3.zero);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Mesh";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.02f, isShotgun ? 0.18f : 0.08f);
        body.transform.localScale = isShotgun ? new Vector3(0.06f, 0.08f, 0.56f) : new Vector3(0.04f, 0.06f, 0.24f);

        var renderer = body.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (urp != null && (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name == "Standard"))
                renderer.sharedMaterial = new Material(urp);
            if (renderer.material != null)
                renderer.material.color = isShotgun ? new Color(0.28f, 0.22f, 0.18f) : new Color(0.18f, 0.2f, 0.24f);
        }

        return root.AddComponent<T>();
    }

    private void UpdateAnimatorLocomotionBools()
    {
        if (tpc == null || animator == null || _cc == null) return;
        float speed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
        animator.SetBool(AnimIsMoving, speed > 0.1f);
        animator.SetBool(AnimIsSprinting, speed > tpc.MoveSpeed);
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}

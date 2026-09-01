using UnityEngine;
using StarterAssets;

public class WeaponController : MonoBehaviour
{
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
    
    private bool isAiming = false;
    private float lastFireTime = 0f;

    private readonly int AnimWeaponMode = Animator.StringToHash("WeaponMode");
    private readonly int AnimIsAiming = Animator.StringToHash("IsAiming");
    private readonly int AnimFire = Animator.StringToHash("Fire");
    private readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private readonly int AnimIsSprinting = Animator.StringToHash("IsSprinting");

    private void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (tpc == null) tpc = GetComponent<ThirdPersonController>();
        
        if (rightHandAttachment == null)
        {
            Transform[] allTransforms = GetComponentsInChildren<Transform>();
            foreach (Transform t in allTransforms)
            {
                if (t.name == "Right_Hand" || t.name.EndsWith("Right_Hand"))
                {
                    rightHandAttachment = t;
                    break;
                }
            }
        }

        if (pistolPrefab != null && rightHandAttachment != null)
        {
            pistolInstance = Instantiate(pistolPrefab, rightHandAttachment) as PistolWeapon;
            if (pistolInstance != null)
            {
                pistolInstance.transform.localPosition = Vector3.zero;
                pistolInstance.transform.localRotation = Quaternion.identity;
                pistolInstance.OnUnequip();
            }
        }

        if (shotgunPrefab != null && rightHandAttachment != null)
        {
            shotgunInstance = Instantiate(shotgunPrefab, rightHandAttachment) as ShotgunWeapon;
            if (shotgunInstance != null)
            {
                shotgunInstance.transform.localPosition = Vector3.zero;
                shotgunInstance.transform.localRotation = Quaternion.identity;
                shotgunInstance.OnUnequip();
            }
        }

        SetWeaponMode(WeaponMode.Unarmed);
    }

    private void Update()
    {
        UpdateAnimatorLocomotionBools();
    }

    public void CycleWeapon(int direction)
    {
        int mode = (int)currentMode;
        mode += direction;
        if (mode > 2) mode = 0;
        if (mode < 0) mode = 2;
        
        SetWeaponMode((WeaponMode)mode);
    }

    public void SetWeaponMode(WeaponMode mode)
    {
        if (currentMode == mode) return;

        if (currentWeaponInstance != null)
        {
            currentWeaponInstance.OnUnequip();
        }

        currentMode = mode;
        animator.SetInteger(AnimWeaponMode, (int)currentMode);

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
        if (currentMode == WeaponMode.Unarmed) return;
        isAiming = true;
        animator.SetBool(AnimIsAiming, true);
        // Implementar zoom de câmera ou movimento de ombro aqui futuramente
    }

    public void StopAim()
    {
        isAiming = false;
        animator.SetBool(AnimIsAiming, false);
    }

    public void TriggerFire()
    {
        if (currentMode == WeaponMode.Unarmed || currentWeaponInstance == null) return;

        if (Time.time >= lastFireTime + currentWeaponInstance.fireRate)
        {
            lastFireTime = Time.time;
            animator.SetTrigger(AnimFire);
            currentWeaponInstance.Shoot();
        }
    }

    private void UpdateAnimatorLocomotionBools()
    {
        if (tpc != null)
        {
            // Acessando as velocidades privadas do ThirdPersonController usando reflexão seria o ideal, 
            // mas como é só leitura para animação, podemos basear no input ou em propriedades conhecidas.
            // Aqui vamos assumir uma simplificação baseada nos campos públicos do ThirdPersonController.
            float speed = new Vector3(tpc.GetComponent<CharacterController>().velocity.x, 0, tpc.GetComponent<CharacterController>().velocity.z).magnitude;
            
            bool isMoving = speed > 0.1f;
            bool isSprinting = speed > tpc.MoveSpeed; // Se maior que moveSpeed base, tá correndo

            animator.SetBool(AnimIsMoving, isMoving);
            animator.SetBool(AnimIsSprinting, isSprinting);
        }
    }
}

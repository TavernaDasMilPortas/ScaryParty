using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

[RequireComponent(typeof(WeaponController))]
public class WeaponInputHandler : MonoBehaviour
{
    [Header("Settings (Temporariamente Desativados)")]
    [Tooltip("Habilita ou desabilita os controles de arma por hora.")]
    public bool enableWeaponControls = false;

    [Tooltip("Habilita ou desabilita a troca de arma via scroll do mouse por hora.")]
    public bool enableWeaponScroll = false;

    private WeaponController weaponController;
    private StarterAssetsInputs starterAssetsInputs;

    private void Start()
    {
        weaponController = GetComponent<WeaponController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
    }

    private void Update()
    {
        if (starterAssetsInputs == null) return;

        // Se os controles de armas estiverem desativados por hora:
        if (!enableWeaponControls)
        {
            starterAssetsInputs.scrollWeapon = 0f;
            starterAssetsInputs.aim = false;
            starterAssetsInputs.fire = false;
            return;
        }

        if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || Cursor.lockState != CursorLockMode.Locked || Cursor.visible) return;

        // Scroll (Troca de arma)
        if (enableWeaponScroll)
        {
            if (starterAssetsInputs.scrollWeapon > 0)
            {
                weaponController.CycleWeapon(1);
                starterAssetsInputs.scrollWeapon = 0; // reset
            }
            else if (starterAssetsInputs.scrollWeapon < 0)
            {
                weaponController.CycleWeapon(-1);
                starterAssetsInputs.scrollWeapon = 0; // reset
            }
        }
        else
        {
            starterAssetsInputs.scrollWeapon = 0; // reset para não acumular
        }

        // Aim
        if (starterAssetsInputs.aim)
        {
            weaponController.StartAim();
        }
        else
        {
            weaponController.StopAim();
        }

        // Fire
        if (starterAssetsInputs.fire)
        {
            weaponController.TriggerFire();
            // We do not set starterAssetsInputs.fire = false here. 
            // The new Input System will set it to false when the user releases the button.
            // TriggerFire() already handles the fireRate cooldown.
        }
    }

    public void SetWeaponControlsEnabled(bool enabled)
    {
        enableWeaponControls = enabled;
        if (!enabled && starterAssetsInputs != null)
        {
            starterAssetsInputs.scrollWeapon = 0f;
            starterAssetsInputs.aim = false;
            starterAssetsInputs.fire = false;
            if (weaponController != null)
            {
                weaponController.SetWeaponMode(WeaponMode.Unarmed);
                weaponController.StopAim();
            }
        }
    }

    public void SetWeaponScrollEnabled(bool enabled)
    {
        enableWeaponScroll = enabled;
        if (!enabled && starterAssetsInputs != null)
        {
            starterAssetsInputs.scrollWeapon = 0f;
        }
    }
}

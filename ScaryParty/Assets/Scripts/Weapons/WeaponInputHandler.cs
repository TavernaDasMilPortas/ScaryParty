using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

[RequireComponent(typeof(WeaponController))]
public class WeaponInputHandler : MonoBehaviour
{
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

        // Scroll (Troca de arma)
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
            starterAssetsInputs.fire = false; // Como não é "Hold" e sim trigger
        }
    }
}

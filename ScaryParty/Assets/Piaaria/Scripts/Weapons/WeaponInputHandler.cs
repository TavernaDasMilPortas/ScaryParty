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
    private Unity.Netcode.NetworkObject netObj;
    private bool _wasFirePressed = false;
    private bool _requireFireRelease = false;

    private void Start()
    {
        weaponController = GetComponent<WeaponController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        netObj = GetComponent<Unity.Netcode.NetworkObject>();
    }

    private void Update()
    {
        if (netObj != null && !netObj.IsOwner) return;
        if (starterAssetsInputs == null) return;

        // Se os controles de armas estiverem desativados por hora:
        if (!enableWeaponControls)
        {
            starterAssetsInputs.scrollWeapon = 0f;
            starterAssetsInputs.aim = false;
            starterAssetsInputs.fire = false;
            _wasFirePressed = false;
            _requireFireRelease = true;
            return;
        }

        if (IsUiBlockingWeaponInput())
        {
            starterAssetsInputs.scrollWeapon = 0f;
            starterAssetsInputs.aim = false;
            starterAssetsInputs.fire = false;
            _wasFirePressed = false;
            _requireFireRelease = true;
            weaponController?.StopAim();
            return;
        }

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

        // Fire: semiautomático por borda de pressionamento; automático apenas se configurado na arma
        bool firePressed = starterAssetsInputs.fire;
        if (_requireFireRelease)
        {
            if (!firePressed)
            {
                _requireFireRelease = false;
            }
            _wasFirePressed = firePressed;
            return;
        }

        bool shouldFire = weaponController != null && weaponController.IsCurrentWeaponAutomatic
            ? firePressed
            : (firePressed && !_wasFirePressed);

        if (shouldFire)
        {
            weaponController.TriggerFire();
        }

        _wasFirePressed = firePressed;
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

    private static bool IsUiBlockingWeaponInput()
    {
        if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked ||
            Cursor.lockState != CursorLockMode.Locked ||
            Cursor.visible)
            return true;

        if ((Time.unscaledTime - ScaryParty.Pizzeria.Presentation.GameplayInputGate.LastUnlockUnscaledTime) < 0.12f)
            return true;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return true;

        if (IsPointerOverBlockingUIToolkit())
            return true;

        return false;
    }

    private static bool IsPointerOverBlockingUIToolkit()
    {
        Vector2 screenPos;
#if ENABLE_INPUT_SYSTEM
        screenPos = UnityEngine.InputSystem.Mouse.current != null
            ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;
#else
        screenPos = Input.mousePosition;
#endif
        var docs = Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None);
        if (docs == null) return false;

        for (int i = 0; i < docs.Length; i++)
        {
            var doc = docs[i];
            if (doc == null || !doc.isActiveAndEnabled) continue;
            var root = doc.rootVisualElement;
            if (root == null || root.panel == null) continue;
            Vector2 panelPos = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(
                root.panel,
                new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = root.panel.Pick(panelPos);
            if (picked != null && picked != root && picked.pickingMode != UnityEngine.UIElements.PickingMode.Ignore)
            {
                return true;
            }
        }
        return false;
    }
}

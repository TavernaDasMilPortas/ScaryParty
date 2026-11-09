using UnityEngine;

public class PistolWeapon : WeaponBase
{
    private void Awake()
    {
        fireRate = 0.3f;
        damage = 25;
    }

    public override void Shoot()
    {
        base.Shoot();
        // Implementar raycast de pistola depois
        Debug.Log("Pistol Fired!");
    }
}

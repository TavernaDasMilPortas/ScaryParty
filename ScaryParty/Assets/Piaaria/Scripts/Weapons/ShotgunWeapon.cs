using UnityEngine;

public class ShotgunWeapon : WeaponBase
{
    public int pellets = 8;

    private void Awake()
    {
        fireRate = 0.8f;
        damage = 80;
    }

    public override void Shoot()
    {
        base.Shoot();
        // Implementar cone spread raycast de escopeta depois
        Debug.Log($"Shotgun Fired with {pellets} pellets!");
    }
}

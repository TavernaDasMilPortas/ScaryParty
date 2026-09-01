using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    public float fireRate = 0.5f;
    public int damage = 25;
    public ParticleSystem muzzleFlash;

    public virtual void OnEquip()
    {
        gameObject.SetActive(true);
    }

    public virtual void OnUnequip()
    {
        gameObject.SetActive(false);
    }

    public virtual void Shoot()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }
    }
}

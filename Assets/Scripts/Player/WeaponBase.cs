// WeaponBase.cs

using Controllers;
using UnityEngine;

namespace Player
{
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("Weapon Settings")]
        [SerializeField] protected float fireRate = 0.5f;
        [SerializeField] protected GameObject projectilePrefab;
        [SerializeField] protected Transform projectileSpawnPoint;
        [SerializeField] protected float projectileSpeed = 10f;
        [SerializeField] protected float projectileLifetime = 5f;

        [Header("Recoil")]
        [SerializeField] protected float verticalRecoil = 2f;
        [SerializeField] protected Vector2 horizontalRecoilRange = new Vector2(-0.3f, 0.3f);
        [SerializeField] protected float horizontalRecoilDivider = 4f;

        [Header("Ammo / Reload")]
        [SerializeField] protected int magazineSize = 30;
        [SerializeField] protected int ammoInMagazine = 30;
        [SerializeField] protected int ammoReserve = 90;
        [SerializeField] protected float reloadDuration = 1.6f;
        [SerializeField] protected bool autoReloadOnEmpty;
        
        [Header("Audio")]
        [SerializeField] protected AudioClip fireSound;
        [SerializeField] protected AudioClip reloadSound;
        [SerializeField] protected AudioClip dryFireSound;
        protected AudioSource audioSource;
        
        [Header("Animation")]
        [SerializeField] protected Animator armsAnimator;
        [SerializeField] protected Animator weaponAnimator;
        [SerializeField] protected string reloadingParameter = "Reloading";
        protected global::PlayerController2 ownerController;
        
        protected bool canFire = true;
        protected float fireTimer;
        protected bool isReloading;
        protected bool canPlayDryFire = true;
        
        protected virtual void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        public virtual void Fire()
        {
            if (!canFire) return;
            if (isReloading) return;
            if (ammoInMagazine <= 0)
            {
                // Play dry fire sound once per trigger pull
                if (canPlayDryFire && dryFireSound && audioSource)
                {
                    audioSource.PlayOneShot(dryFireSound);
                    canPlayDryFire = false;
                }
                
                // Reset canFire to prevent dry fire spam when holding button
                canFire = false;
                fireTimer = 0f;
                
                if (autoReloadOnEmpty && ammoReserve > 0)
                {
                    TryStartReload();
                }
                return;
            }
            
            canFire = false;
            fireTimer = 0f;
            canPlayDryFire = true; // Reset dry fire flag when successfully firing
            
            if (!projectilePrefab)
            {
                Debug.LogError("Projectile prefab is not assigned!");
                return;
            }
            
            GameObject projectileGo = Instantiate(projectilePrefab, 
                projectileSpawnPoint.position, 
                projectileSpawnPoint.rotation);
            
            // Prefer cached projectile
            Projectile projComponent = projectileGo.GetComponent<Projectile>();
            if (projComponent)
            {
                projComponent.Launch(projectileSpawnPoint.forward * projectileSpeed);
            }
            else
            {
                Debug.LogWarning("Projectile prefab has no Projectile to apply velocity.");
            }
            
            
            ammoInMagazine = Mathf.Max(0, ammoInMagazine - 1);
            
            Destroy(projectileGo, projectileLifetime);
            ApplyRecoilKick();
            if (fireSound && audioSource)
            {
                audioSource.PlayOneShot(fireSound);
            }


            //Check if we can auto reload
            if (ammoInMagazine <= 0 && autoReloadOnEmpty && ammoReserve > 0)
            {
                TryStartReload();
            }
        }

        public void OnFireInputReleased()
        {
            canPlayDryFire = true;
        }

        public void Reload()
        {
            TryStartReload();
        }

        public virtual void SetOwner(global::PlayerController2 owner)
        {
            ownerController = owner;
        }

        private void TryStartReload()
        {
            if (isReloading) return;
            if (ammoInMagazine >= magazineSize) return; // already full
            if (ammoReserve <= 0) return; // nothing to reload

            StartCoroutine(ReloadRoutine());
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            isReloading = true;
            
            // Set reload animation parameter to true
            if (armsAnimator) armsAnimator.SetBool(reloadingParameter, true);
            if (weaponAnimator) weaponAnimator.SetBool(reloadingParameter, true);
            
            if (reloadSound) audioSource.PlayOneShot(reloadSound);

            // Wait for reload duration
            float t = 0f;
            while (t < reloadDuration)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // Refill from reserve
            int needed = magazineSize - ammoInMagazine;
            int toLoad = Mathf.Min(needed, ammoReserve);
            ammoInMagazine += toLoad;
            ammoReserve -= toLoad;

            // Set reload animation parameter to false
            if (armsAnimator) armsAnimator.SetBool(reloadingParameter, false);
            if (weaponAnimator) weaponAnimator.SetBool(reloadingParameter, false);
            
            isReloading = false;
        }
        
        public virtual void Update()
        {

            if (!canFire)
            {
                fireTimer += Time.deltaTime;
                if (fireTimer >= fireRate)
                {
                    canFire = true;
                }
            }
        }

        protected virtual void ApplyRecoilKick()
        {
            if (!ownerController) return;
            float horizontalKick = Random.Range(horizontalRecoilRange.x, horizontalRecoilRange.y);
            if (Mathf.Abs(horizontalRecoilDivider) > 0.001f)
            {
                horizontalKick /= horizontalRecoilDivider;
            }
            Vector2 recoilDelta = new Vector2(horizontalKick, verticalRecoil);
            ownerController.ApplyRecoil(recoilDelta);
        }
    }
}
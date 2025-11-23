// WeaponBase.cs
using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] protected float fireRate = 0.5f;
    [SerializeField] protected GameObject projectilePrefab;
    [SerializeField] protected Transform projectileSpawnPoint;
    [SerializeField] protected float projectileSpeed = 10f;
    [SerializeField] protected float projectileLifetime = 5f;
    
    [Header("Audio")]
    [SerializeField] protected AudioClip fireSound;
    [SerializeField] protected AudioClip reloadSound;
    protected AudioSource audioSource;
    
    protected bool canFire = true;
    protected float fireTimer = 0f;
    
    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    public virtual void Fire()
    {
        if (!canFire) return;
        
        canFire = false;
        fireTimer = 0f;
        
        // Create projectile
        GameObject projectile = Instantiate(projectilePrefab, 
            projectileSpawnPoint.position, 
            projectileSpawnPoint.rotation);
            
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = projectileSpawnPoint.forward * projectileSpeed;
        }
        
        Destroy(projectile, projectileLifetime);
        
        // Play sound
        if (fireSound != null)
        {
            audioSource.PlayOneShot(fireSound);
        }
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
}
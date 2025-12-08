using UnityEngine;
using System;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerHealthController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth;
    public int MaxHealth => maxHealth;
    [SerializeField] private int currentHealth;
    public int CurrentHealth => currentHealth;

    [Header("Armor")]
    [SerializeField] private int maxArmor;
    public int MaxArmor => maxArmor;
    [SerializeField] private int currentArmor;
    public int CurrentArmor => currentArmor;

    private int damageBlockers = 0;

    public bool CanTakeDamage => damageBlockers <= 0;

    private bool isDead = false;
    public bool IsDead => isDead;

    [Header("Camera Shake")]
    [SerializeField] private float intensity = 0.01f;
    [SerializeField] private float time = 0.25f;
    [SerializeField] private Camera playerCamera;

    //[Header("Player Controller")]
    private PlayerController2 playerController;

    public event Action<int, int> OnHealthChanged;
    public event Action<int, int> OnArmorChanged;

    void Start()
    {
        currentHealth = maxHealth;
        currentArmor = maxArmor;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnArmorChanged?.Invoke(currentArmor, maxArmor);
        
        playerController = GetComponent<PlayerController2>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            TakeDamage(10);
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            Heal(10);
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            AddArmor(10);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (CanTakeDamage)
        {
            //StartCoroutine(ShakeCamera(damageAmount));
            if (currentArmor > 0)
            {
                int damageToArmor = Mathf.Min(damageAmount, currentArmor);
                currentArmor -= damageToArmor;
                damageAmount -= damageToArmor;
                OnArmorChanged?.Invoke(currentArmor, maxArmor);
            }

            if (damageAmount > 0)
            {
                currentHealth -= damageAmount;
                currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);

                if (currentHealth <= 0 && !isDead)
                {
                    isDead = true;
                    Debug.Log("Player has died.");
                    SceneManager.LoadScene("LoseScreen");
                }

                

                // Reduce player's speed by half
                if (playerController)
                {
                    playerController.ReduceSpeedByHalf();
                    StartCoroutine(ResetSpeedAfterDelay());
                }
            }
        }
        else
        {
            Debug.Log($"{gameObject.name} is shielded and cannot take damage.");
        }
    }

    private IEnumerator ResetSpeedAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        if (playerController != null)
        {
            playerController.ResetSpeed();
        }
    }

    private IEnumerator ShakeCamera(float damageAmount)
    {
        Vector3 originalPosition = playerCamera.transform.localPosition;
        Quaternion originalRotation = playerCamera.transform.localRotation;
        float elapsedTime = 0f;

        while (elapsedTime < time)
        {
            float x = UnityEngine.Random.Range(-1f, 1f) * (intensity * damageAmount);
            float y = UnityEngine.Random.Range(-1f, 1f) * (intensity * damageAmount);
            float z = UnityEngine.Random.Range(-1f, 1f) * (intensity * damageAmount);
            
            playerCamera.transform.localRotation = originalRotation * Quaternion.Euler(x, y, z);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        playerCamera.transform.localRotation = originalRotation;
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        currentArmor = maxArmor;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnArmorChanged?.Invoke(currentArmor, maxArmor);
    }

    public void Heal(int healAmount)
    {
        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void AddArmor(int armorAmount)
    {
        currentArmor += armorAmount;
        currentArmor = Mathf.Clamp(currentArmor, 0, maxArmor);
        OnArmorChanged?.Invoke(currentArmor, maxArmor);
    }

    public void AddDamageBlocker()
    {
        damageBlockers++;
        Debug.Log("Damage blocker added. Total blockers: " + damageBlockers);
    }

    public void RemoveDamageBlocker()
    {
        damageBlockers = Mathf.Max(0, damageBlockers - 1);
        Debug.Log("Damage blocker removed. Total blockers: " + damageBlockers);
    }
}
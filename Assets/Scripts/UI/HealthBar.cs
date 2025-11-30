using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Player;

public class HealthBar : MonoBehaviour
{
    [Header("UI Components - Health")]
    public GridIconBar healthGridBar;
    public TextMeshProUGUI healthText;

    [Header("UI Components - Armor")]
    public GridIconBar armorGridBar;
    public TextMeshProUGUI armorText;

    [Header("UI Components - Ammo")]
    public TextMeshProUGUI ammoInMagazineText;  // Current ammo in magazine
    public TextMeshProUGUI ammoReserveText;    // Remaining reserve ammo

    [Header("Health System")]
    [SerializeField] private PlayerHealthController playerHealth;

    [Header("Weapon System")]
    [SerializeField] private PlayerController2 playerController;
    private WeaponBase currentWeapon;

    void Start()
    {
        // Auto-find PlayerHealthController if not assigned
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealthController>();
            if (playerHealth == null)
            {
                Debug.LogError("PlayerHealthController not found! Please assign it in the Inspector or ensure a PlayerHealthController exists in the scene.");
                return;
            }
        }

        // Initialize health display
        if (healthGridBar != null)
        {
            healthGridBar.UpdateIcons(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
        healthText.text = $"{playerHealth.CurrentHealth}/{playerHealth.MaxHealth}";

        // Initialize armor display
        if (armorGridBar != null)
        {
            armorGridBar.UpdateIcons(playerHealth.CurrentArmor, playerHealth.MaxArmor);
        }
        armorText.text = $"{playerHealth.CurrentArmor}/{playerHealth.MaxArmor}";

        // Subscribe to health and armor events
        playerHealth.OnHealthChanged += UpdateHealthBar;
        playerHealth.OnArmorChanged += UpdateArmorBar;

        // Auto-find PlayerController2 if not assigned
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController2>();
            if (playerController == null)
            {
                Debug.LogWarning("PlayerController2 not found! Ammo display will not work. Please assign it in the Inspector or ensure a PlayerController2 exists in the scene.");
            }
        }

        // Initialize ammo display
        if (playerController != null && playerController.CurrentWeapon != null)
        {
            UpdateWeaponReference(playerController.CurrentWeapon);
        }
    }

    void Update()
    {
        // Check if weapon reference needs updating
        if (playerController != null)
        {
            WeaponBase newWeapon = playerController.CurrentWeapon;
            
            if (newWeapon != null && newWeapon != currentWeapon)
            {
                UpdateWeaponReference(newWeapon);
            }
        }
    }

    private void UpdateWeaponReference(WeaponBase newWeapon)
    {
        if (newWeapon == null) return;

        // Unsubscribe from old weapon
        if (currentWeapon != null)
        {
            currentWeapon.OnAmmoChanged -= UpdateAmmoDisplay;
        }

        // Subscribe to new weapon
        currentWeapon = newWeapon;
        currentWeapon.OnAmmoChanged += UpdateAmmoDisplay;

        // Initialize ammo display
        UpdateAmmoDisplay(currentWeapon.AmmoInMagazine, currentWeapon.AmmoReserve, currentWeapon.MagazineSize);
    }

    void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        healthText.text = $"{currentHealth}/{maxHealth}";
        if (healthGridBar != null)
        {
            healthGridBar.UpdateIcons(currentHealth, maxHealth);
        }
    }

    void UpdateArmorBar(int currentArmor, int maxArmor)
    {
        armorText.text = $"{currentArmor}/{maxArmor}";
        if (armorGridBar != null)
        {
            armorGridBar.UpdateIcons(currentArmor, maxArmor);
        }
    }

    void UpdateAmmoDisplay(int ammoInMagazine, int ammoReserve, int magazineSize)
    {
        // Update current magazine ammo count
        if (ammoInMagazineText != null)
        {
            ammoInMagazineText.text = ammoInMagazine.ToString();
        }

        // Update remaining reserve ammo count
        if (ammoReserveText != null)
        {
            ammoReserveText.text = ammoReserve.ToString();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from health events
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthBar;
            playerHealth.OnArmorChanged -= UpdateArmorBar;
        }

        // Unsubscribe from weapon events
        if (currentWeapon != null)
        {
            currentWeapon.OnAmmoChanged -= UpdateAmmoDisplay;
        }
    }
}

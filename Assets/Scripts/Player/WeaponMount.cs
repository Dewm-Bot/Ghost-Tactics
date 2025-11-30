// WeaponMount.cs

using Player;
using UnityEngine;

public class WeaponMount : MonoBehaviour
{
    [SerializeField] private Transform defaultPosition;
    [SerializeField] private Transform adsPosition;
    [SerializeField] private float adsTransitionSpeed = 10f;
    
    [Header("Sight Alignment")]
    [SerializeField] private Transform cameraSightTarget; // Where the camera should look when ADS
    [SerializeField] private bool enableSightAlignment = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;
    
    private bool isAiming = false;
    private WeaponBase currentWeapon;
    private Transform weaponSight; // The weapon's sight point
    private Camera playerCamera;
    
    private void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }
    }

    public void MountWeapon(WeaponBase weapon)
    {
        // Unmount current weapon if one exists
        if (currentWeapon != null)
        {
            currentWeapon.transform.SetParent(null);
        }
        
        currentWeapon = weapon;
        
        // Mount new weapon if provided
        if (weapon != null)
        {
            weapon.transform.SetParent(defaultPosition);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            
            // Find the weapon's sight point (assuming it's named "SightPoint" or similar)
            weaponSight = weapon.transform.Find("SightPoint");
            if (weaponSight == null)
            {
                // If no specific sight point, use the weapon's transform
                weaponSight = weapon.transform;
            }
        }
        else
        {
            weaponSight = null;
        }
    }

    public void ToggleAiming(bool aim)
    {
        isAiming = aim;
    }

    private void Update()
    {
        if (!currentWeapon) return;
    
        Transform targetPosition = isAiming ? adsPosition : defaultPosition;
        
        if (isAiming && enableSightAlignment && weaponSight && cameraSightTarget)
        {
            // Calculate the offset needed to align the weapon sight with camera center
            Vector3 sightOffset = cameraSightTarget.position - weaponSight.position;
            Vector3 alignedPosition = targetPosition.position + sightOffset;
            
            currentWeapon.transform.position = Vector3.Lerp(
                currentWeapon.transform.position, 
                alignedPosition, 
                adsTransitionSpeed * Time.deltaTime);
        }
        else
        {
            currentWeapon.transform.position = Vector3.Lerp(
                currentWeapon.transform.position, 
                targetPosition.position, 
                adsTransitionSpeed * Time.deltaTime);
        }
        
        currentWeapon.transform.rotation = Quaternion.Slerp(
            currentWeapon.transform.rotation, 
            targetPosition.rotation, 
            adsTransitionSpeed * Time.deltaTime);
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Draw default position
        if (defaultPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(defaultPosition.position, 0.05f);
            Gizmos.DrawRay(defaultPosition.position, defaultPosition.forward * 0.2f);
        }
        
        // Draw ADS position
        if (adsPosition != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(adsPosition.position, 0.05f);
            Gizmos.DrawRay(adsPosition.position, adsPosition.forward * 0.2f);
        }
        
        // Draw camera sight target
        if (cameraSightTarget)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(cameraSightTarget.position, 0.03f);
        }
        
        // Draw sight alignment line when ADS
        if (isAiming && weaponSight && cameraSightTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(weaponSight.position, cameraSightTarget.position);
        }
    }
}
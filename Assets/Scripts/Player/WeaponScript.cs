using Player;
using UnityEngine;

public class WeaponScript : WeaponBase
{
    [SerializeField] private string playerControllerName = "Player";
    private PlayerController2 _playerController;
    
    protected override void Awake()
    {
        base.Awake(); // Call base Awake for audio setup
        
        GameObject playerObject = GameObject.Find(playerControllerName);
        if (playerObject != null)
        {
            _playerController = playerObject.GetComponent<PlayerController2>();
            if (_playerController == null)
            {
                Debug.LogError("PlayerController component not found on player object");
            }
        }
        else
        {
            Debug.LogError("Player object not found in scene");
        }
    }
    
    new void Update()
    {
        base.Update(); 
        
        if (_playerController && _playerController.isFiring)
        {
            Fire(); 
        }
    }
}

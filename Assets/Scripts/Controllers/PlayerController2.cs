using System;
using UnityEngine;
using GhostTacticsNS;
using System.Collections;
using Player;

public class PlayerController2 : MonoBehaviour
{
    [Header("Character Controller")] 
    public GhostTactics _inputs;
    private CharacterController _controller;



    [Header("Movement")] 
    [SerializeField] private float _speed = 8.0f;
    private Vector2 _move;
    private float _speedOld;
    [SerializeField] private float _acceleration = 1.0f;
    [SerializeField] private float _deceleration = 1.0f;
    //[SerializeField] private float _strafeModifier = 0.5f;
    [ShowOnly] [SerializeField] private float speedmodifier = 0;
    [SerializeField] private float _gravity = 9.81f;
    [SerializeField] private float _jumpHeight = 6.0f;
    [SerializeField] private float _crouchSpeed = 0.1f;
    [SerializeField] private float _crouchHeight = 0.5f;
    private Vector3 _crouchVector = new Vector3(0, 0.5f, 0);
    [ShowOnly] [SerializeField] private Vector3 _velocity = new Vector3(0, 0, 0);
    private float _originalHeight;
    private Vector3 _originalCenter;


    [Header("Mouse Settings")] 
    [SerializeField] public float mouseSensX = 30f;
    [SerializeField] public float mouseSensY = 30f;
    private Vector2 _mouse;
    private Camera _camera;
    private float _xRotation = 0f;
    private float _yRotation = 0f;
    private Vector2 _cameraOffset = new Vector2(0f, 0f);



    [Header("Lean Settings")]
    [SerializeField] private float _leanAngle = 15f;
    [SerializeField] private float _leanDistance = 0.5f;
    [SerializeField] private float _weaponShiftDistance = 0.7f;
    [SerializeField] private float _leanSpeed = 5f;
    [SerializeField] private GameObject _currentGun = null;
    private Vector2 _lean;
    private Vector3 _targetCameraPosition;
    private Quaternion _targetCameraRotation;
    private Vector3 _targetGunPosition;

    [Header("Gun Movement Settings")] 
    [SerializeField] private float _gunYawModifier = 5f;
    [SerializeField] private float _yawClampMin = 30f;
    [SerializeField] private float _yawClampMax = 30f;
    [SerializeField] private float _gunReturnSpeed = 5f;
    [SerializeField] private float _slerpSpeedX = 5f;
    [SerializeField] private float _slerpSpeedY = 5f;
    [SerializeField] private float _returnSpeed = 5f;
    private float _initialGunYaw = 0f;
    
    [Header("Hip Fire Gun Positioning")]
    [SerializeField] private float _upShiftScaler = 1.0f;           // How much gun moves up when looking up
    [SerializeField] private float _upPositionScaler = 0.3f;        // How much gun moves up positionally when looking up
    [SerializeField] private float _downShiftScaler = 1.0f;         // How much gun moves back when looking down
    [SerializeField] private float _downPositionScaler = 0.5f;      // How much gun moves down positionally when looking down
    
    [Header("ADS Gun Movement")]
    [SerializeField] private float _adsLookUpBackwardPush = 1.0f;     // How far back to push gun when looking up in ADS
    [SerializeField] private float _adsLookUpDownwardPush = 1f;     // How far down to push gun when looking up in ADS
    [SerializeField] private float _adsLookDownDownwardPush = 0.2f;   // How far down to push gun when looking down in ADS
    [SerializeField] private float _adsLookDownForwardPush = 0.1f;    // How far forward to push gun when looking down in ADS
    
    [Header("Weapon System")]
    [SerializeField] private WeaponMount weaponMount;
    [SerializeField] private WeaponBase startingWeapon;
    private WeaponBase _currentWeapon;
    public WeaponBase CurrentWeapon => _currentWeapon; // Public accessor for UI and other systems
    private bool isAiming = false;

    [Header("Debug Mode")]
    [SerializeField] private bool debugMode = false;
    private bool allInputFrozen = false;
    [SerializeField] private bool showCameraCenterGizmos = false;
    [SerializeField] private float gizmoSize = 0.02f;
    [SerializeField] private Color centerGizmoColor = Color.red;
    [SerializeField] public bool previewADSInDebug = false; // Allow ADS preview in debug mode

    //[Header("Projectile Stuff")]
    public bool isFiring = false;

    private void Awake()
    {
        _inputs = new GhostTactics();
        _inputs.Enable();

        //Movement Input Binds
        _inputs.Player.Move.performed += ctx => _move = ctx.ReadValue<Vector2>();
        _inputs.Player.Move.canceled += ctx => _move = Vector2.zero;
        _inputs.Player.Jump.performed += context => Jump();
        _inputs.Player.Crouch.performed += context => SudoCrouch();
        _inputs.Player.Crouch.canceled += context => SudoStand();
        


        //Mouse Movement Input Binds
        _inputs.Player.Look.performed += ctx => _mouse = ctx.ReadValue<Vector2>();
        _inputs.Player.Look.canceled += ctx => _mouse = Vector2.zero;

        // Lean Input Binds
        _inputs.Player.LeftLean.performed += ctx => _lean.x = -1;
        _inputs.Player.LeftLean.canceled += ctx => _lean.x = 0;
        _inputs.Player.RightLean.performed += ctx => _lean.x = 1;
        _inputs.Player.RightLean.canceled += ctx => _lean.x = 0;

        //Variable Binding
        _camera = GetComponentInChildren<Camera>();
        _controller = GetComponent<CharacterController>();
        _originalHeight = _controller.height;
        _originalCenter = _controller.center;
        _crouchVector.y = _crouchHeight;

        //Lean Bindings
        _targetCameraPosition = _camera.transform.localPosition;
        _targetCameraRotation = _camera.transform.localRotation;
        _targetGunPosition = _currentGun.transform.localPosition;

        //Weapon Bindings
        _inputs.Player.Fire.performed += ctx => isFiring = true;
        _inputs.Player.Fire.canceled += ctx => {
            isFiring = false;
            _currentWeapon.OnFireInputReleased(); //Check if we let go on the trigger for dry fire / semi auto
        };
        _inputs.Player.Reload.performed += ctx => _currentWeapon.Reload();
        _inputs.Player.AimDownSights.performed += ctx => ToggleAim(true);
        _inputs.Player.AimDownSights.canceled += ctx => ToggleAim(false);


        _yawClampMin = Mathf.Abs(_yawClampMin);
    }

    private void Update()
    {
        // Handle debug mode toggle (P key should work even when frozen)
        HandleDebugInput();
        
        // Apply gun positioning even when input is frozen for debugging
        ApplyGunPositioning();
        
        // Only process other input if not frozen
        if (allInputFrozen) return;
        HandleMouseInput();
        MovementHandler();
        HandleLeanInput();
            
        // Weapon firing
        if (isFiring && _currentWeapon)
        {
            _currentWeapon.Fire();
        }
    }
    
    private void FixedUpdate()
    {
        PhysicsHandler();
    }


    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _speedOld = _speed;
        if (startingWeapon != null)
        {
            EquipWeapon(startingWeapon);
        }
    }
    
    private void OnDisable()
    {
        DisableInput();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    

    private void HandleMouseInput()
    {
        Vector2 _mouseDelta = _mouse * Time.deltaTime;
        _xRotation -= _mouseDelta.y * mouseSensY;
        _yRotation += _mouseDelta.x * mouseSensX;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        Quaternion cameraRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        _camera.transform.localRotation =
            Quaternion.Slerp(_camera.transform.localRotation, cameraRotation, _leanSpeed * Time.deltaTime);

        _controller.transform.rotation = Quaternion.Euler(0f, _yRotation, 0f);

        ApplyGunPositioning();

        AdjustGunRotation(cameraRotation);
    }

    private void ApplyGunPositioning()
    {
        Vector3 targetGunPosition = new Vector3(_currentGun.transform.localPosition.x,
            ((_camera.transform.localPosition.y - 0.5f) - _currentGun.transform.localPosition.y),
            _camera.transform.localPosition.z);

        if (!isAiming)
        {
            switch (_xRotation)
            {
                // Regular hip fire movement with proper up/down positioning using public variables
                // Looking up
                case > 0f:
                {
                    float upwardAmount = (_xRotation / 90f);
                    targetGunPosition += Vector3.forward * (upwardAmount * _upShiftScaler);        // Move gun up
                    targetGunPosition += Vector3.up * (upwardAmount * _upPositionScaler);     // Additional upward positioning
                    break;
                }
                // Looking down  
                case < 0f:
                {
                    float downwardAmount = (Mathf.Abs(_xRotation) / 90f);
                    targetGunPosition -= Vector3.forward * (downwardAmount * _downShiftScaler);   // Move gun back
                    targetGunPosition -= Vector3.up * (downwardAmount * _downPositionScaler);     // Move gun down
                    break;
                }
            }
        }
        else
        {
            switch (_xRotation)
            {
                // ADS-specific movement to maintain sight picture
                // Looking up
                case > 0f:
                {
                    float lookUpAmount = _xRotation / 90f; // 0 to 1
                    targetGunPosition -= Vector3.forward * (lookUpAmount * _adsLookUpBackwardPush);
                    targetGunPosition -= Vector3.up * (lookUpAmount * _adsLookUpDownwardPush);
                    break;
                }
                // Looking down
                case < 0f:
                {
                    float lookDownAmount = Mathf.Abs(_xRotation) / 90f; // 0 to 1
                    targetGunPosition -= Vector3.up * (lookDownAmount * _adsLookDownDownwardPush);
                    targetGunPosition += Vector3.forward * (lookDownAmount * _adsLookDownForwardPush);
                    break;
                }
            }
        }

        _currentGun.transform.localPosition = Vector3.Slerp(_currentGun.transform.localPosition, targetGunPosition,
            _leanSpeed * Time.deltaTime);
    }

    private void AdjustGunRotation(Quaternion cameraRotation)
    {
        if (_initialGunYaw == 0f)
        {
            _initialGunYaw = _currentGun.transform.localRotation.eulerAngles.y;
        }

        _gunYawModifier += _mouse.x * mouseSensX * Time.deltaTime;
        _gunYawModifier = Mathf.Clamp(_gunYawModifier, -_yawClampMin, _yawClampMax);

        _gunYawModifier = Mathf.Lerp(_gunYawModifier, 0f, Time.deltaTime * _gunReturnSpeed);
        float targetGunYaw = _initialGunYaw + _gunYawModifier;

        Quaternion targetGunRotation = Quaternion.Euler(_xRotation, targetGunYaw, 0f);

        _currentGun.transform.localRotation = Quaternion.Slerp(
            _currentGun.transform.localRotation,
            targetGunRotation,
            _leanSpeed * Time.deltaTime
        );
    }

    //Character movement handler
    private void MovementHandler()
    {
        Vector3 move = transform.right * _move.x + transform.forward * _move.y;
        if (Math.Abs(move.x) > 0.1f || Math.Abs(move.z) > 0.1f)
        {
            if (speedmodifier == 0.0f)
            {
                speedmodifier = 2.0f;
            }

            speedmodifier = speedmodifier + _acceleration * 0.01f;
            speedmodifier = Mathf.Clamp(speedmodifier, 0, _speed);
        }
        else if (_controller.isGrounded)
        {
            speedmodifier = 0;
            _velocity.x -= Mathf.MoveTowards(_velocity.x, 0, _deceleration * Time.deltaTime);
            _velocity.z -= Mathf.MoveTowards(_velocity.z, 0, _deceleration * Time.deltaTime);
        }

        _controller.Move(move * (speedmodifier * Time.deltaTime));
    }

    private void HandleLeanInput()
    {
        float leanAngle = -_lean.x * _leanAngle;
        float leanDistance = _lean.x * _leanDistance;

        _targetCameraRotation = Quaternion.Euler(_xRotation + _cameraOffset.y, _cameraOffset.x, leanAngle);
        _targetCameraPosition = new Vector3(leanDistance, _camera.transform.localPosition.y,
            _camera.transform.localPosition.z);
        _targetGunPosition = new Vector3(leanDistance, _currentGun.transform.localPosition.y,
            _currentGun.transform.localPosition.z);

        _camera.transform.localRotation = Quaternion.Slerp(_camera.transform.localRotation, _targetCameraRotation,
            _leanSpeed * Time.deltaTime);
        _camera.transform.localPosition = Vector3.Lerp(_camera.transform.localPosition, _targetCameraPosition,
            _leanSpeed * Time.deltaTime);

        //Shift the gun when leaning
        Vector3 gunShift = Vector3.zero;
        if (_lean.x < 0)
        {
            gunShift = Vector3.left * _weaponShiftDistance;
        }

        _currentGun.transform.localPosition = Vector3.Lerp(_currentGun.transform.localPosition,
            _targetGunPosition + gunShift, _leanSpeed * Time.deltaTime);
    }

    //Character physics handler
    private void PhysicsHandler()
    {
        if (!_controller.isGrounded)
        {
            _velocity.y += -_gravity * Time.smoothDeltaTime;
        }

        // Ensure _velocity does not contain NaN values
        if (float.IsNaN(_velocity.x) || float.IsNaN(_velocity.y) || float.IsNaN(_velocity.z))
        {
            Debug.LogError("Velocity contains NaN values: " + _velocity);
            _velocity = Vector3.zero;
        }

        _controller.Move(_velocity * Time.deltaTime);
    }

    private void Jump()
    {
        _velocity.y = (_jumpHeight);
    }


    //Mouse state handlers
    private void DisableInput()
    {
        //Disable all input
        _inputs.Disable();
    }

    private void EnableInput()
    {
        //Enable all input
        _inputs.Enable();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            //if the application is focused, lock the mouse and enable input
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            EnableInput();
        }
        else
        {
            //If the application is not focused, unlock the mouse and disable input
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            DisableInput();
        }
    }

    //Reserved Area for external interactions

    public void ReduceSpeedByHalf()
    {
        _speed *= 0.5f;
    }

    public void ResetSpeed()
    {
        {
            _speed = _speedOld;
        }
    }
    
        public void SudoCrouch()
        {
            Debug.Log("Crouching!");
            _controller.height = _crouchHeight;
            _controller.center = _crouchVector;
        }

        private void SudoStand()
        {
            Debug.Log("Standing!");
            _controller.height = _originalHeight;
            _controller.center = _originalCenter;
        }
        
        private void ToggleAim(bool aiming)
        {
            // Allow ADS preview in debug mode if previewADSInDebug is enabled
            if (allInputFrozen && !previewADSInDebug)
            {
                return; // Block ADS when input is frozen unless preview is enabled
            }
            
            isAiming = aiming;
    
            if (weaponMount != null)
            {
                weaponMount.ToggleAiming(aiming);
            }

            // Optional: Adjust camera FOV or sensitivity when aiming
            if (isAiming)
            {
                _camera.fieldOfView = 40f; // Zoomed in FOV
                // Store original sensitivity values if you want to restore them later
            }
            else
            {
                _camera.fieldOfView = 60f; // Normal FOV
                // Restore original sensitivity values if needed
            }
        }

        private void EquipWeapon(WeaponBase newWeapon)
        {
            if (_currentWeapon != null)
            {
                // Properly unequip the current weapon
                if (weaponMount != null)
                {
                    weaponMount.MountWeapon(null);
                }
                _currentWeapon = null;
            }

            // Use the existing weapon instance instead of instantiating
            _currentWeapon = newWeapon;
            if (weaponMount != null)
            {
                weaponMount.MountWeapon(_currentWeapon);
            }
            else
            {
                Debug.LogError("WeaponMount reference is missing!");
            }
        }

        private void HandleDebugInput()
    {
        // Toggle weapon input freeze with P key
        if (Input.GetKeyDown(KeyCode.P))
        {
            allInputFrozen = !allInputFrozen;
            Debug.Log($"All input {(allInputFrozen ? "FROZEN" : "UNFROZEN")}");
            
            // Also toggle gizmos visibility
            showCameraCenterGizmos = allInputFrozen;

            // Removed time freezing - debug mode no longer affects Time.timeScale
        }
    }

    // Draw debug gizmos for camera center
    private void OnDrawGizmos()
    {
        if (!showCameraCenterGizmos || _camera == null) return;
        
        Gizmos.color = centerGizmoColor;
        
        // Draw crosshair at camera center
        Vector3 cameraCenter = _camera.transform.position + _camera.transform.forward * 2f;
        
        // Horizontal line
        Vector3 left = cameraCenter - _camera.transform.right * gizmoSize;
        Vector3 right = cameraCenter + _camera.transform.right * gizmoSize;
        Gizmos.DrawLine(left, right);
        
        // Vertical line
        Vector3 up = cameraCenter + _camera.transform.up * gizmoSize;
        Vector3 down = cameraCenter - _camera.transform.up * gizmoSize;
        Gizmos.DrawLine(up, down);
        
        // Center dot
        Gizmos.DrawSphere(cameraCenter, gizmoSize * 0.1f);
        
        // Draw ray from camera forward
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(_camera.transform.position, _camera.transform.forward * 5f);
    }
}

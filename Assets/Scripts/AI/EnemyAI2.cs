using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class EnemyAI2 : MonoBehaviour
{
    [Header("General Variables")]
    private StateMachine _state;
    private NavMeshAgent _agent;
    private Transform _player;
    private Sight _sight;
    private float _preAttackStoppingDistance;
    
    // Anti-Stuck Variables
    private Vector3 _myPosition;
    private float _stuckTimer;
    private float _stuckTime = 5f;
    
    // Distance to a goal before switching goals
    [SerializeField] private float closeEnough = 1f;
    
    // Patrolling variables
    [Header("Patrolling Variables")]
    [ShowOnly, SerializeField] private Vector3 walkPoint;
    [SerializeField] private float walkPointRange = 10f;
    [SerializeField] private Vector2 patrolRepathInterval = new Vector2(4f, 8f);
    private float patrolRepathTimer;
    
    // Attacking variables
    [Header("Attacking Variables")]
    [SerializeField] private float burstShotInterval = 0.12f;
    [SerializeField] private int maxBurstSize = 6;
    [SerializeField] private Vector2 burstPauseRange = new Vector2(0.3f, 1.5f);
    [SerializeField] private float attackRange = 10f;
    [SerializeField] private float attackDistance = 5f;
    public GameObject projectile;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private float projectileSpeed = 45f;
    [ShowOnly, SerializeField] private int remainingBurstShots;
    [ShowOnly, SerializeField] private float shotCooldownTimer;
    [ShowOnly, SerializeField] private float burstPauseTimer;

    [Header("Evasion Settings")]
    [SerializeField] private bool enableEvasiveMovement = true;
    [SerializeField] private float strafeRadius = 3f;
    [SerializeField] private Vector2 strafeDurationRange = new Vector2(0.8f, 1.5f);
    [SerializeField] private float strafeCooldown = 0.9f;
    [SerializeField] private float strafeSpeedMultiplier = 1.1f;
    [SerializeField] private float strafeObstacleCheckDistance = 1.5f;
    [SerializeField] private float reengageDistanceMultiplier = 1.2f;
    private bool strafeActive;
    private Vector3 strafeDestination;
    private float strafeTimer;
    private float strafeCooldownTimer;
    private float baseAgentSpeed;
    
    // Alarm variables
    [Header("Alarm Variables")]
    [ShowOnly, SerializeField] private bool alarmSounded;
    [ShowOnly, SerializeField] private Vector3 alarmPosition;
    
    // Idle variables
    [Header("Idle Variables")]
    [SerializeField] private float idleTime = 2f;
    [ShowOnly, SerializeField] private float currentIdleTime;
    
    [Header("Awareness Variables")]
    [SerializeField] private Vector2 lookAroundInterval = new Vector2(2f, 4f);
    [SerializeField] private float lookAroundAngle = 45f;
    private float lookAroundTimer;
    
    [Header("Accuracy Settings")]
    [SerializeField] private float aimLeadTime = 0.2f;
    [SerializeField] [Range(0f, 10f)] private float spreadAngleDegrees = 1.5f;
    private Vector3 playerLastPosition;
    private Vector3 playerVelocity;
    
    [Header("Audio")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField] [Range(0f, 1f)] private float fireVolume = 1f;
    private AudioSource audioSource;

    // Epic Debug Variables
    [Header("Debug Variables")] 
    [ShowOnly, SerializeField] private string _currentState;
    
    // State Machine States
    private StateMachine.State _patrol;
    private StateMachine.State _attack;
    private StateMachine.State _chase;
    private StateMachine.State _investigate;
    private StateMachine.State _idle;
    

    // Epic Cool Functions

    private float DistanceToGoal(Vector3 goal)
    {
        return Vector3.Distance(transform.position, goal);
    }
    
    private bool AtGoal(Vector3 goal)
    {
        return DistanceToGoal(goal) < closeEnough;
    }
    
    private Vector3 RandomPoint(Vector3 origin, float range)
    {
        Vector3 randomPoint = origin + UnityEngine.Random.insideUnitSphere * range;
        NavMeshHit hit;
        NavMesh.SamplePosition(randomPoint, out hit, range, 1);
        Console.WriteLine("New walk point: " + hit.position);
        return hit.position;
    }
    
    private bool IsPlayerInSight()
    {
        return _sight.detected;
    }
    
    private bool IsPlayerInAttackRange()
    {
        return DistanceToGoal(_player.position) < attackRange;
    }

    private void CheckStuck()
    {
        if (_myPosition == transform.position)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= _stuckTime)
            {
                // Pick a new walk point
                Debug.Log("I got stuck! Picking a new walk point.");
                walkPoint = RandomPoint(transform.position, walkPointRange);
                _stuckTimer = 0;
            }
        }
        else
        {
            _myPosition = transform.position;
            _stuckTimer = 0;
        }
    }
    
    
    // State On Frames

    private void Patrol()
    {
        CheckStuck();
        patrolRepathTimer -= Time.deltaTime;
        if (patrolRepathTimer <= 0f)
        {
            walkPoint = RandomPoint(transform.position, walkPointRange);
            ResetPatrolRepathTimer();
        }
        if (AtGoal(walkPoint))
        {
            _state.TransitionTo(_idle);
        }
        else
        {
            _agent.SetDestination(walkPoint);
        }
    }

    private void StartAttack()
    {
        _preAttackStoppingDistance = _agent.stoppingDistance;
        _agent.stoppingDistance = attackDistance;
        ResetBurstState();
        PrepareNextBurst();
        ResetStrafeState();
    }
    
    private void Attack()
    {
        alarmSounded = false; // If the player is in sight, the alarm is no longer relevant
        float distanceToPlayer = DistanceToGoal(_player.position);
        bool shouldCloseDistance = distanceToPlayer > attackDistance * reengageDistanceMultiplier;
        if (shouldCloseDistance)
        {
            ResetStrafeState();
            _agent.speed = baseAgentSpeed;
            _agent.SetDestination(_player.position);
        }
        else if (enableEvasiveMovement)
        {
            UpdateEvasiveMovement();
        }
        else
        {
            _agent.SetDestination(_player.position);
        }
        
        // Make sure the enemy is facing the player
        Vector3 directionToPlayer = (_player.position - transform.position).normalized;
        directionToPlayer.y = 0;
        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(directionToPlayer), Time.deltaTime * 10f);
        }
        
        UpdateBurstFire();
        
        if (!IsPlayerInSight())
        {
            _state.TransitionTo(_patrol);
        }
    }
    
    private void EndAttack()
    {
        _agent.stoppingDistance = _preAttackStoppingDistance;
        ResetBurstState();
        ResetStrafeState();
    }
    
    private void Chase()
    {
        alarmSounded = false; // If the player is in sight, the alarm is no longer relevant
        _agent.SetDestination(_player.position);
        
        if (!IsPlayerInSight())
        {
            // Stop chasing
            _state.TransitionTo(_patrol);
        }
    }
    
    private void Investigate()
    {
        _agent.SetDestination(alarmPosition);

        if (AtGoal(alarmPosition))
        {
            // Stop investigating
            _state.TransitionTo(_patrol);
        }
    }

    private void StartIdle()
    {
        currentIdleTime = idleTime;
    }

    private void Idle()
    {
        if (currentIdleTime <= 0)
        {
            // Stop idling
            _state.TransitionTo(_patrol);
        }
        else
        {
            currentIdleTime -= Time.deltaTime;
        }
    }

    private void EndIdle()
    {
        // Set a new walk point
        walkPoint = RandomPoint(transform.position, walkPointRange);
        ResetPatrolRepathTimer();
    }

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _player = GameObject.FindWithTag("Player").transform;
        _sight = GetComponent<Sight>();
        baseAgentSpeed = _agent != null ? _agent.speed : 0f;
        if (_player != null)
        {
            playerLastPosition = _player.position;
        }
        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        
        // Pick a random walk point
        walkPoint = RandomPoint(transform.position, walkPointRange);
        ResetPatrolRepathTimer();
        ResetLookAroundTimer();
        
        // State Machine (my beloved)
        _state = new StateMachine();
        
        // States
        
        // Patrol
        _patrol = _state.CreateState("Patrol");
        // Attack
        _attack = _state.CreateState("Attack");
        // Chase
        _chase = _state.CreateState("Chase");
        // Investigate (Responding to an alarm)
        _investigate = _state.CreateState("Investigate");
        // Idle
        _idle = _state.CreateState("Idle");
        
        // OnFrames
        _patrol.OnFrame = Patrol;
        _attack.OnFrame = Attack;
        _chase.OnFrame = Chase;
        _investigate.OnFrame = Investigate;
        _idle.OnFrame = Idle;
        
        // OnEnter
        _attack.OnEnter = StartAttack;
        _idle.OnEnter = StartIdle;
        
        // OnExit
        _attack.OnExit = EndAttack;
        _idle.OnExit = EndIdle;
    }

    private void Update()
    {
        _currentState = _state?.currentState?.ToString();
        HandleLookAround();
        UpdatePlayerVelocity();
        
        // If the player is in sight, in any state, chase the player
        if (IsPlayerInSight() && IsPlayerInAttackRange())
        {
            _state.TransitionTo(_attack);
        }
        else if (IsPlayerInSight())
        {
            _state.TransitionTo(_chase);
        }
        
        _state.Update();
    }
    
    private void OnDrawGizmos()
    {
        // Draw the walk point
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(walkPoint, 1f);
        
        // Draw the attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
    }
    
    private void ResetPatrolRepathTimer()
    {
        if (patrolRepathInterval.y < patrolRepathInterval.x)
        {
            (patrolRepathInterval.x, patrolRepathInterval.y) = (patrolRepathInterval.y, patrolRepathInterval.x);
        }
        patrolRepathTimer = UnityEngine.Random.Range(patrolRepathInterval.x, patrolRepathInterval.y);
    }
    
    private void ResetLookAroundTimer()
    {
        if (lookAroundInterval.y < lookAroundInterval.x)
        {
            (lookAroundInterval.x, lookAroundInterval.y) = (lookAroundInterval.y, lookAroundInterval.x);
        }
        lookAroundTimer = UnityEngine.Random.Range(lookAroundInterval.x, lookAroundInterval.y);
    }
    
    private void HandleLookAround()
    {
        if (IsPlayerInSight())
        {
            ResetLookAroundTimer();
            return;
        }
        if (lookAroundTimer > 0f)
        {
            lookAroundTimer -= Time.deltaTime;
            return;
        }
        float yaw = UnityEngine.Random.Range(-lookAroundAngle, lookAroundAngle);
        transform.Rotate(0f, yaw, 0f);
        _sight?.DetectAspect();
        ResetLookAroundTimer();
    }
    
    private void UpdatePlayerVelocity()
    {
        if (!_player)
        {
            playerVelocity = Vector3.zero;
            return;
        }
        if (Time.deltaTime <= Mathf.Epsilon)
        {
            return;
        }
        Vector3 currentPosition = _player.position;
        playerVelocity = (currentPosition - playerLastPosition) / Time.deltaTime;
        playerLastPosition = currentPosition;
    }
    
    private void ResetBurstState()
    {
        remainingBurstShots = 0;
        shotCooldownTimer = 0f;
        burstPauseTimer = 0f;
    }
    
    private void PrepareNextBurst()
    {
        int clampedMax = Mathf.Max(1, maxBurstSize);
        remainingBurstShots = UnityEngine.Random.Range(1, clampedMax + 1);
        shotCooldownTimer = 0f;
    }
    
    private void ResetStrafeState()
    {
        strafeActive = false;
        strafeTimer = 0f;
        strafeCooldownTimer = 0f;
        strafeDestination = transform.position;
        if (_agent != null)
        {
            _agent.speed = baseAgentSpeed;
        }
    }
    
    private void UpdateBurstFire()
    {
        if (!projectile)
        {
            Debug.LogWarning("Projectile prefab missing on EnemyAI2.");
            return;
        }
        if (remainingBurstShots <= 0)
        {
            if (burstPauseTimer <= 0f)
            {
                float pauseMin = Mathf.Min(burstPauseRange.x, burstPauseRange.y);
                float pauseMax = Mathf.Max(burstPauseRange.x, burstPauseRange.y);
                burstPauseTimer = UnityEngine.Random.Range(pauseMin, pauseMax);
            }
            burstPauseTimer -= Time.deltaTime;
            if (burstPauseTimer <= 0f)
            {
                PrepareNextBurst();
            }
            return;
        }
        if (shotCooldownTimer > 0f)
        {
            shotCooldownTimer -= Time.deltaTime;
            return;
        }
        FireProjectile();
        remainingBurstShots--;
        shotCooldownTimer = Mathf.Max(0.01f, burstShotInterval);
        if (remainingBurstShots <= 0)
        {
            float pauseMin = Mathf.Min(burstPauseRange.x, burstPauseRange.y);
            float pauseMax = Mathf.Max(burstPauseRange.x, burstPauseRange.y);
            burstPauseTimer = UnityEngine.Random.Range(pauseMin, pauseMax);
        }
    }
    
    private void FireProjectile()
    {
        if (!projectile)
        {
            return;
        }
        Vector3 spawnPosition = projectileSpawnPoint ? projectileSpawnPoint.position : transform.position + transform.forward;
        Vector3 predictedTarget = _player ? _player.position + playerVelocity * aimLeadTime : spawnPosition + transform.forward;
        Vector3 aimDirection = (predictedTarget - spawnPosition).normalized;
        if (aimDirection.sqrMagnitude < 0.001f)
        {
            aimDirection = projectileSpawnPoint ? projectileSpawnPoint.forward : transform.forward;
        }
        aimDirection = ApplySpread(aimDirection);
        Quaternion spawnRotation = Quaternion.LookRotation(aimDirection, Vector3.up);
        GameObject bullet = Instantiate(projectile, spawnPosition, spawnRotation);
        if (bullet.TryGetComponent<Rigidbody>(out var rb))
        {
            Vector3 shotVelocity = aimDirection * projectileSpeed;
            if (_agent)
            {
                shotVelocity += _agent.velocity * 0.25f;
            }
            rb.linearVelocity = shotVelocity;
        }
        else
        {
            Debug.LogWarning("Projectile does not have a Rigidbody component.");
        }
        PlayFireSound();
    }
    
    private void UpdateEvasiveMovement()
    {
        if (_agent == null || _player == null)
        {
            return;
        }
        if (strafeActive)
        {
            strafeTimer -= Time.deltaTime;
            bool reached = Vector3.Distance(transform.position, strafeDestination) <= closeEnough;
            if (strafeTimer <= 0f || reached || ShouldAbortStrafe())
            {
                EndStrafe();
            }
            else
            {
                _agent.SetDestination(strafeDestination);
                return;
            }
        }
        if (strafeCooldownTimer > 0f)
        {
            strafeCooldownTimer -= Time.deltaTime;
            _agent.SetDestination(transform.position);
            return;
        }
        if (TryCreateStrafeDestination(out Vector3 destination))
        {
            strafeDestination = destination;
            strafeActive = true;
            strafeTimer = UnityEngine.Random.Range(Mathf.Max(0.1f, strafeDurationRange.x), Mathf.Max(strafeDurationRange.x, strafeDurationRange.y));
            _agent.speed = baseAgentSpeed * strafeSpeedMultiplier;
            _agent.SetDestination(strafeDestination);
        }
        else
        {
            strafeCooldownTimer = strafeCooldown;
            _agent.SetDestination(_player.position);
        }
    }
    
    private bool TryCreateStrafeDestination(out Vector3 destination)
    {
        destination = transform.position;
        Vector3 toPlayer = _player.position - transform.position;
        if (toPlayer.sqrMagnitude < 0.01f)
        {
            return false;
        }
        Vector3 lateral = Vector3.Cross(Vector3.up, toPlayer.normalized);
        lateral *= UnityEngine.Random.value > 0.5f ? 1f : -1f;
        Vector3 forwardOffset = toPlayer.normalized * UnityEngine.Random.Range(-strafeRadius * 0.5f, strafeRadius * 0.5f);
        Vector3 samplePoint = transform.position + lateral * strafeRadius + forwardOffset;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(samplePoint, out hit, strafeRadius, NavMesh.AllAreas))
        {
            return false;
        }
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 toHit = hit.position - transform.position;
        if (Physics.Raycast(rayOrigin, toHit.normalized, out RaycastHit obstruction, strafeObstacleCheckDistance))
        {
            if (!obstruction.collider.CompareTag("Player"))
            {
                return false;
            }
        }
        destination = hit.position;
        return true;
    }
    
    private bool ShouldAbortStrafe()
    {
        if (!_agent.hasPath)
        {
            return true;
        }
        if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            return true;
        }
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 toDest = strafeDestination - transform.position;
        if (Physics.Raycast(rayOrigin, toDest.normalized, out RaycastHit hit, strafeObstacleCheckDistance))
        {
            if (!hit.collider.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }
    
    private void EndStrafe()
    {
        strafeActive = false;
        strafeCooldownTimer = strafeCooldown;
        if (_agent)
        {
            _agent.speed = baseAgentSpeed;
        }
    }
    
    private Vector3 ApplySpread(Vector3 direction)
    {
        if (spreadAngleDegrees <= 0.01f)
        {
            return direction;
        }
        Vector3 randomAxis = UnityEngine.Random.onUnitSphere;
        randomAxis -= direction * Vector3.Dot(randomAxis, direction);
        if (randomAxis.sqrMagnitude < 1e-4f)
        {
            randomAxis = Vector3.Cross(direction, Vector3.up);
        }
        randomAxis.Normalize();
        float angle = UnityEngine.Random.Range(0f, spreadAngleDegrees);
        Quaternion spreadRotation = Quaternion.AngleAxis(angle, randomAxis);
        return (spreadRotation * direction).normalized;
    }
    
    private void PlayFireSound()
    {
        if (!fireSound || !audioSource)
        {
            return;
        }
        audioSource.PlayOneShot(fireSound, fireVolume);
    }
}

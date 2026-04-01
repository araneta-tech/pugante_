using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class PatrolAI : MonoBehaviour
{
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Waypoint System")]
    public WaypointHolder waypointHolder;

    private Transform[] waypoints;
    private int currentIndex = 0;

    private Transform detectedPlayer;

    [Header("Vision Settings")]
    public float lineOfSightRadius = 10f;
    public float viewAngle = 120f;
    public LayerMask obstacleLayer;

    [Header("Combat Settings")]
    public float attackRadius = 2f;

    [Header("Attack Timing")]
    public float attackDelay = 0.8f;
    private float attackTimer = 0f;

    [Header("Enemy Speed")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;

    [Header("Lose Player Settings")]
    public float loseDistance = 15f;
    public float loseTime = 3f;

    private float loseTimer = 0f;

    [Header("Alert UI (World)")]
    public GameObject alertUIPrefab;
    private GameObject alertUIInstance;
    public Vector3 alertOffset = new Vector3(0, 2f, 0);

    private float alertDisplayTimer = 0f;
    public float alertDisplayDuration = 1.5f;

    [Header("PLAYER UI (SCREEN) 🔥")]
    public GameObject playerDetectionUI; // Assign Canvas UI here

    [Header("Sound Settings")]
    public AudioSource audioSource;
    public AudioClip alertSound;

    private bool hasPlayedAlertSound = false;
    private bool hasBusted = false;

    private enum AIState
    {
        Patrol,
        Alert,
        Chase,
        Attack,
        ReturnToPatrol
    }

    private AIState currentState = AIState.Patrol;

    void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (waypointHolder == null) return;

        waypoints = waypointHolder.GetWaypoints();
        if (waypoints.Length == 0) return;

        agent.speed = patrolSpeed;
        agent.SetDestination(waypoints[currentIndex].position);

        if (alertUIPrefab != null)
        {
            alertUIInstance = Instantiate(alertUIPrefab, transform.position + alertOffset, Quaternion.identity);
            alertUIInstance.transform.SetParent(transform);
            alertUIInstance.transform.localPosition = alertOffset;
            alertUIInstance.SetActive(false);
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Ensure player UI starts hidden
        if (playerDetectionUI != null)
            playerDetectionUI.SetActive(false);
    }

    void Update()
    {
        switch (currentState)
        {
            case AIState.Patrol:
                Patrol();
                DetectPlayer();
                break;

            case AIState.Alert:
                AlertState();
                break;

            case AIState.Chase:
                ChasePlayer();
                break;

            case AIState.Attack:
                AttackPlayer();
                break;

            case AIState.ReturnToPatrol:
                ReturnToPatrol();
                break;
        }

        HandlePlayerDetectionUI(); // 🔥 NEW FUNCTION

        if (alertUIInstance != null && alertUIInstance.activeSelf)
        {
            alertDisplayTimer -= Time.deltaTime;

            if (alertDisplayTimer <= 0f && detectedPlayer == null)
            {
                alertUIInstance.SetActive(false);
            }
        }

        if (animator != null && currentState != AIState.Attack)
            animator.SetBool("isAttacking", false);
    }

    // -------------------------
    // 🔥 PLAYER UI CONTROL
    // -------------------------
    void HandlePlayerDetectionUI()
    {
        if (playerDetectionUI == null) return;

        // Show UI if THIS client is detected
        if (detectedPlayer != null)
        {
            PlayerMovement localPlayer = GetLocalPlayer();

            if (localPlayer != null && detectedPlayer == localPlayer.transform)
            {
                if (!playerDetectionUI.activeSelf)
                    playerDetectionUI.SetActive(true);

                return;
            }
        }

        // Hide if not detected
        if (playerDetectionUI.activeSelf)
            playerDetectionUI.SetActive(false);
    }

    PlayerMovement GetLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerMovement>())
        {
            if (p.IsOwner && p.OwnerClientId == NetworkManager.Singleton.LocalClientId)
                return p;
        }
        return null;
    }

    // -------------------------
    // EXISTING FUNCTIONS (UNCHANGED LOGIC)
    // -------------------------

    void Patrol()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[currentIndex].position);
        }
    }

    void DetectPlayer()
    {
        if (NetworkUI.Instance != null && !NetworkUI.Instance.IsPlaying)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, lineOfSightRadius);

        foreach (Collider hit in hits)
        {
            PlayerMovement player = hit.GetComponent<PlayerMovement>();
            if (player == null) continue;

            Vector3 dir = (player.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dir);

            if (angle > viewAngle / 2) continue;

            float dist = Vector3.Distance(transform.position, player.transform.position);

            if (!Physics.Raycast(transform.position + Vector3.up, dir, dist, obstacleLayer))
            {
                detectedPlayer = player.transform;
                currentState = AIState.Alert;
                hasPlayedAlertSound = false;
                return;
            }
        }
    }

    void AlertState()
    {
        if (detectedPlayer == null)
        {
            currentState = AIState.ReturnToPatrol;
            return;
        }

        if (alertUIInstance != null && !alertUIInstance.activeSelf)
            alertUIInstance.SetActive(true);

        alertDisplayTimer = alertDisplayDuration;

        if (!hasPlayedAlertSound && audioSource != null && alertSound != null)
        {
            audioSource.PlayOneShot(alertSound);
            hasPlayedAlertSound = true;
        }

        agent.SetDestination(detectedPlayer.position);

        float distance = Vector3.Distance(transform.position, detectedPlayer.position);

        if (distance <= attackRadius)
        {
            hasBusted = false;
            currentState = AIState.Attack;
            return;
        }

        agent.speed = chaseSpeed;
        currentState = AIState.Chase;
    }

    void LateUpdate()
    {
        if (alertUIInstance != null && alertUIInstance.activeSelf)
        {
            alertUIInstance.transform.position = transform.position + alertOffset;

            if (Camera.main != null)
            {
                Vector3 dir = alertUIInstance.transform.position - Camera.main.transform.position;
                alertUIInstance.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    void ChasePlayer()
    {
        if (alertUIInstance != null && !alertUIInstance.activeSelf)
            alertUIInstance.SetActive(true);

        alertDisplayTimer = alertDisplayDuration;

        if (detectedPlayer == null)
        {
            currentState = AIState.ReturnToPatrol;
            return;
        }

        float distance = Vector3.Distance(transform.position, detectedPlayer.position);

        agent.SetDestination(detectedPlayer.position);

        if (distance <= attackRadius)
        {
            hasBusted = false;
            currentState = AIState.Attack;
            return;
        }

        if (distance > loseDistance)
        {
            loseTimer += Time.deltaTime;

            if (loseTimer >= loseTime)
            {
                detectedPlayer = null;
                loseTimer = 0;
                currentState = AIState.ReturnToPatrol;
            }
        }
        else
        {
            loseTimer = 0;
        }
    }

    void AttackPlayer()
    {
        if (detectedPlayer == null)
        {
            currentState = AIState.ReturnToPatrol;
            return;
        }

        float distance = Vector3.Distance(transform.position, detectedPlayer.position);

        agent.SetDestination(transform.position);

        if (animator != null)
            animator.SetBool("isAttacking", true);

        if (!hasBusted && attackTimer == 0f)
            attackTimer = attackDelay;

        if (!hasBusted)
        {
            attackTimer -= Time.deltaTime;

            if (attackTimer <= 0f && distance <= attackRadius)
            {
                hasBusted = true;
                attackTimer = 0f;

                if (GameManager.Instance != null &&
                    GameManager.Instance.IsSpawned &&
                    NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.IsServer)
                {
                    GameManager.Instance.PlayerBusted();
                }
            }
        }

        if (distance > attackRadius)
        {
            hasBusted = false;
            attackTimer = 0f;
            currentState = AIState.Chase;
        }
    }

    void ReturnToPatrol()
    {
        agent.speed = patrolSpeed;
        agent.SetDestination(waypoints[currentIndex].position);

        if (!agent.pathPending && agent.remainingDistance < 1f)
            currentState = AIState.Patrol;

        if (alertUIInstance != null && alertUIInstance.activeSelf)
            alertUIInstance.SetActive(false);
    }
}
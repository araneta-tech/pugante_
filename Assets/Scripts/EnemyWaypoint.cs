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

        if (waypointHolder == null)
        {
            return;
        }

        waypoints = waypointHolder.GetWaypoints();

        if (waypoints.Length == 0)
        {
            return;
        }

        agent.speed = patrolSpeed;

        agent.SetDestination(waypoints[currentIndex].position);
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

        if (animator != null && currentState != AIState.Attack)
            animator.SetBool("isAttacking", false);
    }

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

    void ChasePlayer()
    {
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
        {
            attackTimer = attackDelay;
        }

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
        {
            currentState = AIState.Patrol;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, lineOfSightRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, left * lineOfSightRadius);
        Gizmos.DrawRay(transform.position, right * lineOfSightRadius);
    }
}
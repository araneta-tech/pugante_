using UnityEngine;
using UnityEngine.AI;

public class PatrolAI : MonoBehaviour
{
    public NavMeshAgent agent;

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

    [Header("Enemy Speed")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;

    [Header("Lose Player Settings")]
    public float loseDistance = 15f;
    public float loseTime = 3f;

    private float loseTimer = 0f;

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

        if (waypointHolder == null)
        {
            Debug.LogError("[AI] No WaypointHolder assigned.");
            return;
        }

        waypoints = waypointHolder.GetWaypoints();

        if (waypoints.Length == 0)
        {
            Debug.LogWarning("[AI] No waypoints found.");
            return;
        }

        agent.speed = patrolSpeed;

        Debug.Log("[AI] STATE → PATROL");

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
    }

    void Patrol()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;

            Debug.Log("[AI] Patrol → Waypoint " + currentIndex);

            agent.SetDestination(waypoints[currentIndex].position);
        }
    }

    void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, lineOfSightRadius);

        foreach (Collider hit in hits)
        {
            PlayerMovement player = hit.GetComponent<PlayerMovement>();

            if (player == null)
                continue;

            Vector3 dir = (player.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dir);

            if (angle > viewAngle / 2)
                continue;

            float dist = Vector3.Distance(transform.position, player.transform.position);

            if (!Physics.Raycast(transform.position + Vector3.up, dir, dist, obstacleLayer))
            {
                detectedPlayer = player.transform;

                Debug.Log("[AI] PLAYER SPOTTED → ALERT");

                currentState = AIState.Alert;

                return;
            }
        }
    }

    void AlertState()
    {
        Debug.Log("[AI] STATE → ALERT");

        if (detectedPlayer == null)
        {
            currentState = AIState.ReturnToPatrol;
            return;
        }

        agent.SetDestination(detectedPlayer.position);

        float distance = Vector3.Distance(transform.position, detectedPlayer.position);

        if (distance <= attackRadius)
        {
            Debug.Log("[AI] Player in attack range → ATTACK");
            currentState = AIState.Attack;
            return;
        }

        Debug.Log("[AI] Confirming target → CHASE");

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

        Debug.Log("[AI] CHASING PLAYER. Distance: " + distance);

        if (distance <= attackRadius)
        {
            Debug.Log("[AI] ATTACK RANGE REACHED");
            currentState = AIState.Attack;
            return;
        }

        if (distance > loseDistance)
        {
            loseTimer += Time.deltaTime;

            Debug.Log("[AI] Losing player timer: " + loseTimer);

            if (loseTimer >= loseTime)
            {
                Debug.Log("[AI] PLAYER LOST → RETURN TO PATROL");

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

        Debug.Log("[AI] ATTACKING PLAYER");

        if (distance > attackRadius)
        {
            Debug.Log("[AI] Player escaped attack → CHASE");
            currentState = AIState.Chase;
        }
    }

    void ReturnToPatrol()
    {
        Debug.Log("[AI] Returning to patrol");

        agent.speed = patrolSpeed;

        agent.SetDestination(waypoints[currentIndex].position);

        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            Debug.Log("[AI] STATE → PATROL");

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
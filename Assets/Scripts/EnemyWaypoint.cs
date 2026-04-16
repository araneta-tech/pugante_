using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class EnemyPatrolAI : NetworkBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    [Header("Waypoint Holder")]
    public Transform waypointHolder;
    private Transform[] waypoints;
    private int currentIndex = 0;

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;

    [Header("Detection")]
    public float detectionRadius = 10f;
    public float viewAngle = 120f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Attack")]
    public float attackRadius = 2f;
    public int damageAmount = 10;        
    public float attackCooldown = 1.0f;  
    private float attackTimer;

    private Transform targetPlayer;

    private enum AIState { Patrol, Chase, Attack }
    private NetworkVariable<int> netState = new NetworkVariable<int>();

    [Header("Chase Indicator Child")]
    public GameObject chaseChildObject; 

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        agent.speed = patrolSpeed;

        agent.Warp(transform.position);

        if (waypointHolder != null)
        {
            int count = waypointHolder.childCount;
            waypoints = new Transform[count];
            for (int i = 0; i < count; i++)
                waypoints[i] = waypointHolder.GetChild(i);

            if (waypoints.Length > 0)
                agent.SetDestination(waypoints[currentIndex].position);
        }

        if (chaseChildObject != null)
            chaseChildObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SetState(AIState.Patrol);
        }
    }

    void Update()
    {
        if (IsServer)
        {
            RunServerAI();
        }

        SyncClientVisuals();
        UpdateChaseChildObject(); 
    }

    void RunServerAI()
    {
        switch ((AIState)netState.Value)
        {
            case AIState.Patrol: Patrol(); DetectPlayer(); break;
            case AIState.Chase: Chase(); break;
            case AIState.Attack: Attack(); break;
        }
    }

    void SetState(AIState newState) { if (IsServer) netState.Value = (int)newState; }

    void Patrol()
    {
        agent.speed = patrolSpeed;
        if (waypoints == null || waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[currentIndex].position);
        }
    }

    void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            Vector3 dirToPlayer = (hit.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            if (angle <= viewAngle / 2f)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (!Physics.Raycast(transform.position + Vector3.up, dirToPlayer, dist, obstacleLayer))
                {
                    targetPlayer = hit.transform;
                    SetState(AIState.Chase);
                    agent.speed = chaseSpeed;
                    return;
                }
            }
        }
    }

    void Chase()
    {
        if (targetPlayer == null) { SetState(AIState.Patrol); return; }

        agent.SetDestination(targetPlayer.position);

        Vector3 direction = (targetPlayer.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);
        if (dist <= attackRadius) SetState(AIState.Attack);
        else if (dist > detectionRadius * 1.5f) { targetPlayer = null; SetState(AIState.Patrol); }
    }

    void Attack()
    {
        if (targetPlayer == null) { SetState(AIState.Patrol); return; }

        var health = targetPlayer.GetComponent<PlayerMovement>();
        if (health != null && health.IsDead)   
        {
            targetPlayer = null;
            animator.SetBool("isAttacking", false);
            SetState(AIState.Patrol);
            return;
        }

        agent.SetDestination(transform.position);

        Vector3 direction = (targetPlayer.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 8f);
        }

        if (animator != null) animator.SetBool("isAttacking", true);

        float dist = Vector3.Distance(transform.position, targetPlayer.position);

        if (dist <= attackRadius)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                ApplyDamage(targetPlayer.gameObject);
                attackTimer = attackCooldown;
            }
        }
        else
        {
            animator.SetBool("isAttacking", false);
            SetState(AIState.Chase);
        }
    }

    void ApplyDamage(GameObject playerObj)
    {
        var health = playerObj.GetComponent<PlayerMovement>(); 
        if (health != null)
        {
            health.TakeDamage(damageAmount);

            if (health.IsDead)
            {
                targetPlayer = null;
                animator.SetBool("isAttacking", false);
                SetState(AIState.Patrol);
            }
        }
    }

    void SyncClientVisuals()
    {
        if (animator == null) return;
        bool attacking = (AIState)netState.Value == AIState.Attack;
        animator.SetBool("isAttacking", attacking);
    }

    void UpdateChaseChildObject()
    {
        if (chaseChildObject != null)
        {
            chaseChildObject.SetActive((AIState)netState.Value == AIState.Chase);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftBoundary * detectionRadius);
        Gizmos.DrawRay(transform.position, rightBoundary * detectionRadius);
    }
}
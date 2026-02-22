using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class PatrolAI : NetworkBehaviour
{
    public NavMeshAgent agent;

    [Header("Waypoint Holder")]
    public WaypointHolder waypointHolder;

    private Transform[] waypoints;
    private int currentIndex = 0;

    private Vector3 originalPosition;
    private bool returningToOrigin = false;

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (waypointHolder == null)
        {
            Debug.LogError("No WaypointHolder assigned to PatrolAI.");
            return;
        }

        waypoints = waypointHolder.GetWaypoints();

        if (waypoints.Length == 0)
        {
            Debug.LogWarning("WaypointHolder has no child waypoints.");
            return;
        }

        originalPosition = transform.position;
        agent.SetDestination(waypoints[currentIndex].position);
    }

    private void Update()
    {
        if (returningToOrigin)
        {
            ReturnToOriginUpdate();
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        if (waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[currentIndex].position);
        }
    }

    /// <summary>
    /// Call this method to make the agent return to its original position.
    /// </summary>
    public void ReturnToOrigin()
    {
        returningToOrigin = true;
        agent.SetDestination(originalPosition);
    }

    private void ReturnToOriginUpdate()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            returningToOrigin = false;
            // Optionally, resume patrol after returning
            agent.SetDestination(waypoints[currentIndex].position);
        }
    }
}

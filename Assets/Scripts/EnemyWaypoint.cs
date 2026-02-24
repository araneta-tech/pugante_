using UnityEngine;
using UnityEngine.AI;

public class PatrolAI : MonoBehaviour
{
    public NavMeshAgent agent;

    [Header("Waypoint Holder")]
    public WaypointHolder waypointHolder;

    private Transform[] waypoints;
    private int currentIndex = 0;

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

        agent.SetDestination(waypoints[currentIndex].position);
    }

    private void Update()
    {
        Patrol();
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
}
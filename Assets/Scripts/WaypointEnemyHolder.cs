using UnityEngine;

public class WaypointHolder : MonoBehaviour
{
    public Transform[] GetWaypoints()
    {
        Transform[] points = new Transform[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
        {
            points[i] = transform.GetChild(i);
        }

        return points;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        // Draw spheres + lines between children (waypoints)
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            Gizmos.DrawSphere(child.position, 0.3f);

            int next = (i + 1) % transform.childCount;
            Transform nextChild = transform.GetChild(next);

            Gizmos.DrawLine(child.position, nextChild.position);
        }
    }
}

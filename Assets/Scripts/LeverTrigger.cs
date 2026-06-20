using UnityEngine;
using Unity.Netcode;

public class NetworkLeverTrigger : NetworkBehaviour
{
    [Header("Player Interaction Settings")]
    public BoxCollider interactionCollider; // The box trigger collider assigned in the inspector
    public NetworkLeverController leverController; // Reference to the NetworkLeverController script

    private void OnTriggerEnter(Collider other)
    {
        // Only trigger when the player enters the interaction area
        if (other.CompareTag("Player"))
        {
            leverController.SetPlayerInRange(true); // Set player in range
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Only trigger when the player exits the interaction area
        if (other.CompareTag("Player"))
        {
            leverController.SetPlayerInRange(false); // Set player out of range
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (interactionCollider != null)
        {
            // Draw the box trigger to visualize the interaction range in the editor
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(interactionCollider.bounds.center, interactionCollider.bounds.size);
        }
    }
}
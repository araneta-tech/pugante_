using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.UI;

public class NetworkLeverController : NetworkBehaviour
{
    [Header("Lever Settings")]
    public GameObject targetObject;  // The GameObject to rotate
    public float rotationAngle = 90f; // How much to rotate
    public float rotationSpeed = 2f;  // Speed of rotation
    public Vector3 rotationAxis = Vector3.up; // Axis to rotate around (default: Y axis)

    [Header("Floating Text Settings")]
    public Text floatingText; // Reference to the floating text UI element
    public string interactMessage = "Press F to interact"; // Message to show when the player is close

    private bool isActivated = false; // Whether the lever has been activated
    private bool isPlayerInRange = false; // Is the player in range of the lever?

    // Update is called once per frame
    void Update()
    {
        if (isActivated && IsServer) // Only the server should handle rotation
        {
            // Smoothly rotate the target object over time
            targetObject.transform.Rotate(rotationAxis * rotationAngle * rotationSpeed * Time.deltaTime);
        }

        // Show the floating text if the player is in range and not activated
        if (floatingText != null)
        {
            floatingText.gameObject.SetActive(isPlayerInRange); // Show/hide text based on range

            // Display the interaction message only when the player presses F
            if (isPlayerInRange && Input.GetKeyDown(KeyCode.F))
            {
                ActivateLeverServerRpc(); // Call server-side interaction
            }
        }
    }

    // Call this method when lever interaction happens
    [ServerRpc] // This makes the method run on the server
    public void ActivateLeverServerRpc()
    {
        isActivated = !isActivated; // Toggle activation
        UpdateLeverStateClientRpc(isActivated); // Notify clients about the state change
    }

    // This ClientRpc is called on the server to update all clients
    [ClientRpc]
    void UpdateLeverStateClientRpc(bool activated)
    {
        isActivated = activated;
    }

    // Trigger when the player enters or exits the interaction range
    public void SetPlayerInRange(bool inRange)
    {
        isPlayerInRange = inRange;
    }
}
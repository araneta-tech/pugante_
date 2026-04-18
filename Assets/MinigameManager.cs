using UnityEngine;
using Unity.Netcode;

public class MinigameManager : MonoBehaviour
{
    public GameObject pipeGameCanvas;   // The minigame UI canvas
    public GameObject successUI;        // Success UI that shows upon completion
    public GameObject failureUI;        // Failure UI that shows upon failure
    public GameObject ventDoor;         // Reference to the vent object (you can disable it initially)

    private bool isMinigameActive = false;
    private bool isGameCompleted = false;
    private VentDoor ventScript;

    private void Start()
    {
        // Hide the minigame canvas initially
        pipeGameCanvas.SetActive(false);
        successUI.SetActive(false);
        failureUI.SetActive(false);

        ventScript = ventDoor.GetComponent<VentDoor>();  // Get the vent script
    }

    // Call this when player interacts with the vent
    public void StartMinigame()
    {
        if (!isMinigameActive && !isGameCompleted)
        {
            isMinigameActive = true;
            pipeGameCanvas.SetActive(true);
            ventScript.LockVent();
        }
    }

    // This method should be called when the player completes the pipe game successfully
    public void OnMinigameSuccess()
    {
        isGameCompleted = true;
        isMinigameActive = false;
        pipeGameCanvas.SetActive(false); // Hide minigame UI
        successUI.SetActive(true);       // Show success UI

        ventScript.UnlockVent(); // Unlock vent functionality (e.g., teleportation)

        // Start teleportation after the minigame is completed successfully
        ventScript.InteractServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    // This method should be called when the player fails the minigame
    public void OnMinigameFailure()
    {
        isGameCompleted = false;
        isMinigameActive = false;
        pipeGameCanvas.SetActive(false); // Hide minigame UI
        failureUI.SetActive(true);       // Show failure UI
    }
}
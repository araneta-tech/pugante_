using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Fusebox : NetworkBehaviour
{
    [Header("Door Settings")]
    public float openAngle = -90f; // The rotation in degrees around the Z-axis
    public float rotateDuration = 0.5f;

    [Header("Floating UI")]
    public GameObject floatingUIText;

    [Header("Audio Settings")]
    public AudioClip doorToggleSound;
    private AudioSource audioSource;

    private bool isPlayerNearby = false;
    private bool isOpen = false;
    private bool isAnimating = false;
    private bool hasInteracted = false; // Flag to ensure only one interaction

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Transform detectionTrigger;
    private PlayerMovement currentPlayer;

    void Awake()
    {
        detectionTrigger = transform.Find("DetectionTrigger");
        if (detectionTrigger == null)
            Debug.LogWarning("DetectionTrigger child not found on Key object!");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Start()
    {
        closedRotation = transform.rotation;
        openRotation = Quaternion.Euler(
            transform.eulerAngles.x,
            transform.eulerAngles.y,
            transform.eulerAngles.z + openAngle
        );

        if (floatingUIText != null)
            floatingUIText.SetActive(false);
    }

    void Update()
    {
        if (!IsSpawned) return;

        // Ensure interaction only happens once
        if (isPlayerNearby && !hasInteracted && Input.GetKeyDown(KeyCode.F) && !isAnimating)
        {
            hasInteracted = true; // Prevent further interactions
            ToggleDoorServerRpc();
        }
    }

    public void PlayerNearby(bool nearby)
    {
        isPlayerNearby = nearby;
        if (floatingUIText != null)
            floatingUIText.SetActive(nearby);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.IsOwner)
        {
            currentPlayer = player;

            if (!CanInteract(player))
            {
                isPlayerNearby = true;

                if (floatingUIText != null)
                    floatingUIText.SetActive(false);
                return;
            }

            isPlayerNearby = true;

            if (floatingUIText != null)
                floatingUIText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.IsOwner)
        {
            isPlayerNearby = false;
            if (floatingUIText != null)
                floatingUIText.SetActive(false);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void ToggleDoorServerRpc()
    {
        ToggleDoorClientRpc();
    }

    [ClientRpc]
    void ToggleDoorClientRpc()
    {
        if (!isAnimating)
        {
            PlayDoorToggleSound();
            StartCoroutine(RotateDoor());
        }
    }

    private IEnumerator RotateDoor()
    {
        isAnimating = true;

        Quaternion startRot = transform.rotation;
        Quaternion targetRot = isOpen ? closedRotation : openRotation;

        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            // Slerp only on Z-axis by modifying the rotation
            Vector3 currentRotation = transform.rotation.eulerAngles;
            Vector3 targetRotation = targetRot.eulerAngles;
            float zRotation = Mathf.Lerp(currentRotation.z, targetRotation.z, elapsed / rotateDuration);

            transform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, zRotation);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRot;

        isOpen = !isOpen;
        isAnimating = false;
    }

    private void PlayDoorToggleSound()
    {
        if (audioSource != null && doorToggleSound != null)
        {
            audioSource.PlayOneShot(doorToggleSound);
        }
    }

    bool CanInteract(PlayerMovement player)
    {
        if (player == null) return false;

        if (player.SelectedCharacterIndex == 1)
            return false;

        return true;
    }

    // Reset interaction flag if needed (e.g., for respawn or reset conditions)
    public void ResetInteraction()
    {
        hasInteracted = false;
    }
}
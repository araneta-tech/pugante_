using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class VentDoor : NetworkBehaviour
{
    [Header("Floating UI")]
    public GameObject floatingUIText;

    [Header("Audio Settings")]
    public AudioClip doorToggleSound;
    private AudioSource audioSource;

    [Header("Player Move After Interact")]
    public Transform moveTarget;
    public float moveDelay = 1.0f;

    private bool isPlayerNearby = false;
    private PlayerMovement currentPlayer;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        if (floatingUIText != null)
            floatingUIText.SetActive(false);
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F))
        {
            InteractServerRpc(NetworkManager.Singleton.LocalClientId);
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
    void InteractServerRpc(ulong clientId)
    {
        PlayerMovement player = FindPlayerByClientId(clientId);

        if (player == null)
            return;

        if (player.SelectedCharacterIndex != 0)
        {
            Debug.Log("[VentDoor] Player is not index 0, denied.");
            return;
        }

        if (moveTarget != null)
            StartCoroutine(MovePlayerAfterDelay(clientId, moveDelay));

        InteractClientRpc();
    }

    [ClientRpc]
    void InteractClientRpc()
    {
        PlayDoorToggleSound();
    }

    private IEnumerator MovePlayerAfterDelay(ulong clientId, float delay)
    {
        yield return new WaitForSeconds(delay);

        PlayerMovement player = FindPlayerByClientId(clientId);

        if (player != null && moveTarget != null)
        {
            Debug.Log($"Teleporting player to {moveTarget.position}");

            player.SpawnAtPosition(moveTarget.position);
            player.transform.rotation = moveTarget.rotation;
        }
    }

    private PlayerMovement FindPlayerByClientId(ulong clientId)
    {
        foreach (var player in FindObjectsOfType<PlayerMovement>())
        {
            if (player.OwnerClientId == clientId)
                return player;
        }
        return null;
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
        return player.SelectedCharacterIndex == 0;
    }
}

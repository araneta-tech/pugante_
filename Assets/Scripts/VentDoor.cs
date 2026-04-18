using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
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

    [Header("Fade Settings")]
    public Image fadeImage;  
    public Color fadeColor = Color.black;  
    public float maxAlpha = 0.8f;  
    public float fadeDuration = 1.0f;  
    public float displayDuration = 2.0f;  

    public bool isVentUnlocked = false;

    private MinigameManager minigameManager;
    private bool isPlayerNearby = false;
    private PlayerMovement currentPlayer;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(false);  
        }
    }

    void Start()
    {
        minigameManager = GameObject.Find("MinigameManager").GetComponent<MinigameManager>();

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
    public void InteractServerRpc(ulong clientId)
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
        {
            StartCoroutine(MovePlayerAfterDelay(clientId, moveDelay));
            StartCoroutine(FadeInOut());  
        }

        InteractClientRpc();
    }

    [ClientRpc]
    void InteractClientRpc()
    {
        PlayDoorToggleSound();
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

    private IEnumerator FadeInOut(bool fadeIn = true)
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);

            float elapsedTime = 0f;
            float targetAlpha = fadeIn ? maxAlpha : 0f;
            float startAlpha = fadeIn ? 0f : maxAlpha;

            while (elapsedTime < fadeDuration)
            {
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
                fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, targetAlpha);

            if (!fadeIn)
            {
                fadeImage.gameObject.SetActive(false);
            }
        }
    }

    public void LockVent()
    {
        isVentUnlocked = false;
        Debug.Log("Vent is locked. Complete the minigame to unlock.");
    }

    public void UnlockVent()
    {
        isVentUnlocked = true;
        Debug.Log("Vent is now unlocked!");
    }

    private void OnMouseDown()
    {
        if (!isVentUnlocked)
        {
            minigameManager.StartMinigame();
        }
        else
        {
            TeleportPlayer();
        }
    }

    private void TeleportPlayer()
    {
        Debug.Log("Vent unlocked! Teleporting player...");
        InteractServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public IEnumerator MovePlayerAfterDelay(ulong clientId, float delay)
    {
        yield return new WaitForSeconds(delay);

        PlayerMovement player = FindPlayerByClientId(clientId);

        if (player != null && moveTarget != null)
        {
            Debug.Log($"Teleporting player to {moveTarget.position}");

            player.SpawnAtPosition(moveTarget.position);

            Vector3 currentRotation = player.transform.eulerAngles;
            float targetYRotation = moveTarget.rotation.eulerAngles.y;

            player.transform.rotation = Quaternion.Euler(currentRotation.x, targetYRotation, currentRotation.z);

            yield return new WaitForSeconds(displayDuration);

            yield return StartCoroutine(FadeInOut(false));
        }
    }

}
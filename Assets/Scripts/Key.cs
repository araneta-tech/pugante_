using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Key : NetworkBehaviour
{
    [Header("Key Settings")]
    public float riseHeight = 2f;
    public float riseDuration = 1f;

    [Header("Floating UI")]
    public GameObject floatingUIText;

    [Header("Audio Settings")]
    public AudioClip keyPressSound;  
    private AudioSource audioSource; 

    [HideInInspector]
    public bool isPlayerNearby = false;

    private Transform detectionTrigger;

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
        if (floatingUIText != null)
            floatingUIText.SetActive(false);
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F))
        {
            CollectKeyServerRpc();
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
    void CollectKeyServerRpc()
    {
        if (!IsSpawned) return;

        PlayKeyPressSound();

        if (floatingUIText != null)
            floatingUIText.SetActive(false);

        StartCoroutine(RiseAndDestroy());
    }

    private void PlayKeyPressSound()
    {
        if (audioSource != null && keyPressSound != null)
        {
            audioSource.PlayOneShot(keyPressSound); 
        }
    }

    private IEnumerator RiseAndDestroy()
    {
        if (floatingUIText != null)
            floatingUIText.SetActive(false);

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.up * riseHeight;
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / riseDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();

        Destroy(gameObject);
    }
}
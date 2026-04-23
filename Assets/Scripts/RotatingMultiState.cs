using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class RotatingMultiState : NetworkBehaviour
{
    [Header("Target")]
    public Transform targetObject;

    [Header("Rotation States (XYZ per state)")]
    public Vector3[] rotationStates;

    [Header("Animation")]
    public float rotateDuration = 0.5f;

    public enum EaseType
    {
        Linear,
        EaseOut,
        EaseInOut
    }

    public EaseType easeType = EaseType.EaseInOut;

    [Header("UI")]
    public GameObject floatingUIText;

    [Header("Audio")]
    public AudioClip rotateSound;
    private AudioSource audioSource;

    private int currentState = 0;
    private bool isAnimating = false;
    private bool isPlayerNearby = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    void Start()
    {
        if (targetObject == null)
        {
            Debug.LogError("[RotatingMultiState] No target object assigned!");
            return;
        }

        if (rotationStates.Length == 0)
        {
            Debug.LogError("[RotatingMultiState] No rotation states defined!");
            return;
        }

        // Set initial rotation
        targetObject.rotation = Quaternion.Euler(rotationStates[0]);

        if (floatingUIText != null)
            floatingUIText.SetActive(false);
    }

    void Update()
    {
        if (!IsSpawned) return;
        if (!IsClient) return;

        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F) && !isAnimating)
        {
            CycleStateServerRpc();
        }
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
    void CycleStateServerRpc()
    {
        int nextState = (currentState + 1) % rotationStates.Length;

        Debug.Log($"[Rotating] Server switching {currentState} → {nextState}");

        currentState = nextState;

        ApplyStateClientRpc(currentState);
    }

    [ClientRpc]
    void ApplyStateClientRpc(int stateIndex)
    {
        if (!isAnimating)
        {
            PlaySound();
            StartCoroutine(RotateToState(stateIndex));
        }
    }

    IEnumerator RotateToState(int stateIndex)
    {
        isAnimating = true;

        Quaternion startRot = targetObject.rotation;
        Quaternion targetRot = Quaternion.Euler(rotationStates[stateIndex]);

        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            float t = ApplyEasing(elapsed / rotateDuration);

            targetObject.rotation = Quaternion.Slerp(startRot, targetRot, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        targetObject.rotation = targetRot;

        isAnimating = false;
    }

    float ApplyEasing(float t)
    {
        switch (easeType)
        {
            case EaseType.EaseOut:
                return 1f - Mathf.Pow(1f - t, 3f);

            case EaseType.EaseInOut:
                return t * t * (3f - 2f * t);

            default:
                return t;
        }
    }

    void PlaySound()
    {
        if (audioSource != null && rotateSound != null)
        {
            audioSource.PlayOneShot(rotateSound);
        }
    }
}
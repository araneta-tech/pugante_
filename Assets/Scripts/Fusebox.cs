using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Fusebox : NetworkBehaviour
{
    [Header("Animation Settings")]
    public float rotateDuration = 0.5f;

    public enum EaseType
    {
        Linear,
        EaseOut,     // fast → slow (snappy switch)
        EaseInOut    // smooth both ends (heavy door)
    }

    public EaseType easeType = EaseType.EaseInOut;

    [Header("Per Object Settings")]
    public Transform[] objectsToRotate;
    public float[] openAngles;

    [Header("Floating UI")]
    public GameObject floatingUIText;

    [Header("Audio Settings")]
    public AudioClip doorToggleSound;
    private AudioSource audioSource;

    private bool isPlayerNearby = false;
    private bool isOpen = false;
    private bool isAnimating = false;

    private Quaternion[] closedRotations;
    private Quaternion[] openRotations;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        int count = objectsToRotate.Length;

        closedRotations = new Quaternion[count];
        openRotations = new Quaternion[count];

        for (int i = 0; i < count; i++)
        {
            closedRotations[i] = objectsToRotate[i].rotation;

            float angle = (i < openAngles.Length) ? openAngles[i] : 0f;

            openRotations[i] = Quaternion.Euler(
                objectsToRotate[i].eulerAngles.x,
                objectsToRotate[i].eulerAngles.y,
                objectsToRotate[i].eulerAngles.z + angle
            );
        }

        if (floatingUIText != null)
            floatingUIText.SetActive(false);
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F) && !isAnimating)
        {
            ToggleDoorServerRpc();
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
            StartCoroutine(RotateObjects());
        }
    }

    private IEnumerator RotateObjects()
    {
        isAnimating = true;

        int count = objectsToRotate.Length;

        Quaternion[] startRotations = new Quaternion[count];
        Quaternion[] targetRotations = new Quaternion[count];

        for (int i = 0; i < count; i++)
        {
            startRotations[i] = objectsToRotate[i].rotation;
            targetRotations[i] = isOpen ? closedRotations[i] : openRotations[i];
        }

        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            float t = elapsed / rotateDuration;
            t = ApplyEasing(t);

            for (int i = 0; i < count; i++)
            {
                objectsToRotate[i].rotation = Quaternion.Slerp(
                    startRotations[i],
                    targetRotations[i],
                    t
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap to final rotation
        for (int i = 0; i < count; i++)
        {
            objectsToRotate[i].rotation = targetRotations[i];
        }

        isOpen = !isOpen;
        isAnimating = false;
    }

    float ApplyEasing(float t)
    {
        switch (easeType)
        {
            case EaseType.EaseOut:
                return 1f - Mathf.Pow(1f - t, 3f); // fast → slow

            case EaseType.EaseInOut:
                return t * t * (3f - 2f * t); // smooth both ends

            default:
                return t; // linear
        }
    }

    private void PlayDoorToggleSound()
    {
        if (audioSource != null && doorToggleSound != null)
        {
            audioSource.PlayOneShot(doorToggleSound);
        }
    }

    public void PlayerNearby(bool nearby)
    {
        isPlayerNearby = nearby;

        if (floatingUIText != null)
            floatingUIText.SetActive(nearby);
    }
}
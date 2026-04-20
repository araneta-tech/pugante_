using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class GateButton : NetworkBehaviour
{
    [Header("References")]
    public GateController gate;
    public Transform buttonMesh;

    [Header("Visuals")]
    public Renderer buttonRenderer;
    public Light buttonLight;
    public Material redMat;
    public Material greenMat;

    [Header("Audio")]
    public AudioClip clickSound;
    public AudioClip successSound;

    private AudioSource audioSource;

    [Header("Animation")]
    public float pressDepth = 0.02f;
    public float pressDuration = 0.15f;

    [Header("Debug")]
    public int buttonIndex = 1;

    private Vector3 startPos;
    private Vector3 pressedPos;

    private bool isPlayerNearby = false;
    private bool isAnimating = false;
    private bool isPressed = false;

    void Start()
    {
        startPos = buttonMesh.localPosition;

        // 🔥 X-axis only movement
        pressedPos = startPos + new Vector3(-pressDepth, 0f, 0f);

        // 🔊 Auto-create AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.maxDistance = 10f;
        }

        SetVisual(false);

        Debug.Log($"[Button {buttonIndex}] Initialized");
    }

    void Update()
    {
        if (!IsSpawned) return;
        if (!IsClient) return;

        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F) && !isAnimating && !isPressed)
        {
            Debug.Log($"[Button {buttonIndex}] Input detected → CLICK");

            PlaySound(clickSound);

            PressButtonServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void PressButtonServerRpc()
    {
        if (isPressed) return;

        isPressed = true;

        Debug.Log($"[Button {buttonIndex}] PRESSED on server");

        if (gate != null)
        {
            gate.ActivateButtonServerRpc();
        }
        else
        {
            Debug.LogError($"[Button {buttonIndex}] Gate reference missing!");
        }

        PressButtonClientRpc();
    }

    [ClientRpc]
    void PressButtonClientRpc()
    {
        SetVisual(true);

        PlaySound(successSound);

        if (!isAnimating)
        {
            StartCoroutine(PressAnimation());
        }
    }

    // 🎨 Visual state (red → green)
    void SetVisual(bool pressed)
    {
        if (buttonRenderer != null)
        {
            buttonRenderer.material = pressed ? greenMat : redMat;
        }

        if (buttonLight != null)
        {
            buttonLight.color = pressed ? Color.green : Color.red;
            buttonLight.intensity = pressed ? 2f : 1f;
        }
    }

    // 🎬 X-axis press animation
    IEnumerator PressAnimation()
    {
        isAnimating = true;

        float t = 0f;

        while (t < pressDuration)
        {
            buttonMesh.localPosition = Vector3.Lerp(startPos, pressedPos, t / pressDuration);
            t += Time.deltaTime;
            yield return null;
        }

        buttonMesh.localPosition = pressedPos;

        isAnimating = false;
    }

    // 🔊 Safe audio playback
    void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;

        audioSource.PlayOneShot(clip, 1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();

        if (player != null && player.IsOwner)
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();

        if (player != null && player.IsOwner)
        {
            isPlayerNearby = false;
        }
    }
}
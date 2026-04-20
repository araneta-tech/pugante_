using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class GateController : NetworkBehaviour
{
    [Header("Gate Requirement")]
    public int requiredButtons = 2;

    private int activatedCount = 0;
    private bool gateOpened = false;
    private bool isAnimating = false;

    [Header("Animation Settings")]
    public float rotateDuration = 0.5f;

    public enum EaseType
    {
        Linear,
        EaseOut,
        EaseInOut
    }

    public EaseType easeType = EaseType.EaseInOut;

    [Header("Per Object Settings")]
    public Transform[] objectsToRotate;
    public float[] openAngles;

    [Header("Audio")]
    public AudioClip doorToggleSound;
    private AudioSource audioSource;

    private Quaternion[] closedRotations;
    private Quaternion[] openRotations;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
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
    }

    [ServerRpc(RequireOwnership = false)]
    public void ActivateButtonServerRpc()
    {
        if (gateOpened) return;

        activatedCount++;

        if (activatedCount >= requiredButtons)
        {
            gateOpened = true;
            OpenGateClientRpc();
        }
    }

    [ClientRpc]
    void OpenGateClientRpc()
    {
        if (!isAnimating)
        {
            PlaySound();
            StartCoroutine(RotateObjects());
        }
    }

    private IEnumerator RotateObjects()
    {
        isAnimating = true;

        int count = objectsToRotate.Length;
        Quaternion[] startRotations = new Quaternion[count];

        for (int i = 0; i < count; i++)
        {
            startRotations[i] = objectsToRotate[i].rotation;
        }

        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            float t = ApplyEasing(elapsed / rotateDuration);

            for (int i = 0; i < count; i++)
            {
                objectsToRotate[i].rotation = Quaternion.Slerp(
                    startRotations[i],
                    openRotations[i],
                    t
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            objectsToRotate[i].rotation = openRotations[i];
        }

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
        if (audioSource != null && doorToggleSound != null)
        {
            audioSource.PlayOneShot(doorToggleSound);
        }
    }
}
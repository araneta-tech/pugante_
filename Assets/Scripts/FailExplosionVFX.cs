using UnityEngine;
using Unity.Netcode;
using Cinemachine;

public class FailExplosionVFX : NetworkBehaviour
{
    [Header("VFX")]
    public GameObject explosionPrefab;

    [Header("Attach Settings")]
    public string boneName = "Spine"; 
    public Vector3 localOffset;

    [Header("Audio")]
    public AudioClip explosionSound;
    public float volume = 1f;

    [Header("Camera Shake (Cinemachine)")]
    public float shakeAmplitude = 2f;
    public float shakeFrequency = 2f;
    public float shakeDuration = 0.5f;

    public void PlayExplosion()
    {
        if (IsServer)
        {
            PlayExplosionClientRpc();
        }
    }

    [ClientRpc]
    void PlayExplosionClientRpc()
    {
        Transform attachPoint = GetBoneTransform();

        if (attachPoint == null)
            attachPoint = transform;

        Vector3 spawnPos = attachPoint.position + attachPoint.TransformDirection(localOffset);

        if (explosionPrefab != null)
        {
            GameObject vfx = Instantiate(explosionPrefab, spawnPos, Quaternion.identity);

            Destroy(vfx, 5f);
        }

        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, spawnPos, volume);
        }

        if (IsOwner)
        {
            DoCameraShake();
        }
    }

    Transform GetBoneTransform()
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim == null) return null;

        foreach (Transform t in anim.GetComponentsInChildren<Transform>())
        {
            if (t.name == boneName)
                return t;
        }

        return null;
    }

    void DoCameraShake()
    {
        var cam = FindObjectOfType<CinemachineVirtualCamera>();
        if (cam == null) return;

        var noise = cam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise == null) return;

        StartCoroutine(ShakeRoutine(noise));
    }

    System.Collections.IEnumerator ShakeRoutine(CinemachineBasicMultiChannelPerlin noise)
    {
        float timer = 0f;

        float originalAmp = noise.m_AmplitudeGain;
        float originalFreq = noise.m_FrequencyGain;

        noise.m_AmplitudeGain = shakeAmplitude;
        noise.m_FrequencyGain = shakeFrequency;

        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        noise.m_AmplitudeGain = originalAmp;
        noise.m_FrequencyGain = originalFreq;
    }
}
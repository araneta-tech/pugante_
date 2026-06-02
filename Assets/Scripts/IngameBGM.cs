using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class IngameBGM : MonoBehaviour
{
    [Header("Background Music")]
    public AudioClip bgmClip;

    [Range(0f, 1f)]
    public float volume = 0.7f;

    [Header("Fade Settings")]
    public float fadeInDuration = 3f; 

    private AudioSource audioSource;
    private bool hasStarted = false;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = bgmClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0f; 
    }

    private void Update()
    {
        if (hasStarted)
            return;

        if (NetworkManager.Singleton == null)
            return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                StartBGM();
                break;
            }
        }
    }

    private void StartBGM()
    {
        hasStarted = true;

        if (bgmClip != null)
        {
            audioSource.Play();
            StartCoroutine(FadeInMusic());
        }
    }

    private IEnumerator FadeInMusic()
    {
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, volume, elapsed / fadeInDuration);
            yield return null;
        }

        audioSource.volume = volume; 
    }
}
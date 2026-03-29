using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UISoundManager : MonoBehaviour
{
    public static UISoundManager Instance;

    [Header("UI Sounds")]
    public AudioSource audioSource;
    public AudioClip clickSound;
    public AudioClip closeSound;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void PlayClick()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);
    }

    public void PlayClose()
    {
        if (audioSource != null && closeSound != null)
            audioSource.PlayOneShot(closeSound);
    }
}
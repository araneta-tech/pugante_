using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFade : MonoBehaviour
{
    public Image fadeImage;
    public float fadeDuration = 0.5f;

    private static ScreenFade instance;

    void Awake()
    {
        instance = this;
    }

    public static ScreenFade Instance => instance;

    public IEnumerator FadeOut()
    {
        float t = 0f;
        Color c = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float smooth = t / fadeDuration;
            smooth = smooth * smooth * (3f - 2f * smooth);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 1f;
        fadeImage.color = c;
    }

    public IEnumerator FadeIn()
    {
        float t = 0f;
        Color c = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float smooth = t / fadeDuration;
            smooth = smooth * smooth * (3f - 2f * smooth);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 0f;
        fadeImage.color = c;
    }
}
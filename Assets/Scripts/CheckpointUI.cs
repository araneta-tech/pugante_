using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CheckpointUI : MonoBehaviour
{
    public Image fadeImage;
    public TextMeshProUGUI checkpointText;

    [Header("Fade Settings")]
    public Color fadeColor = Color.black;
    [Range(0f, 1f)] public float maxAlpha = 0.3f;
    public float fadeDuration = 0.5f;
    public float displayDuration = 1.5f;

    public static CheckpointUI Instance;

    private void Awake()
    {
        // ✅ Prevent duplicates
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        SetAlpha(fadeImage, 0f);
        SetTextAlpha(0f);
        checkpointText.gameObject.SetActive(false);
    }

    public void ShowCheckpointUI()
    {
        StopAllCoroutines();
        StartCoroutine(FadeSequence());
    }

    private IEnumerator FadeSequence()
    {
        yield return StartCoroutine(Fade(0f, maxAlpha));

        checkpointText.gameObject.SetActive(true);
        SetTextAlpha(1f);

        yield return new WaitForSeconds(displayDuration);

        yield return StartCoroutine(Fade(maxAlpha, 0f));

        SetTextAlpha(0f);
        checkpointText.gameObject.SetActive(false);
    }

    private IEnumerator Fade(float start, float end)
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            float alpha = Mathf.Lerp(start, end, t);
            SetAlpha(fadeImage, alpha);

            yield return null;
        }

        SetAlpha(fadeImage, end);
    }

    private void SetAlpha(Image img, float alpha)
    {
        Color c = fadeColor;
        c.a = alpha;
        img.color = c;
    }

    private void SetTextAlpha(float alpha)
    {
        Color c = checkpointText.color;
        c.a = alpha;
        checkpointText.color = c;
    }
}
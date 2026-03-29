using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BustedUI : MonoBehaviour
{
    public static BustedUI Instance;

    public GameObject panel;
    public float displayTime = 2f;

    private Coroutine currentRoutine;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (panel != null)
            panel.SetActive(false);
    }

    public void Show()
    {
        if (panel == null) return;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        panel.SetActive(true);

        yield return new WaitForSeconds(displayTime);

        panel.SetActive(false);

        currentRoutine = null;
    }
}
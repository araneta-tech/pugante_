using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BustedUI : MonoBehaviour
{
    public static BustedUI Instance;

    public GameObject panel;
    public float displayTime = 2f;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        panel.SetActive(false);
    }

    public void Show()
    {
        StopAllCoroutines();
        StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        panel.SetActive(true);

        yield return new WaitForSeconds(displayTime);

        panel.SetActive(false);
    }
}
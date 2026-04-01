using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class PlayerDistanceManager : NetworkBehaviour
{
    [Header("Distance Settings")]
    public float maxGroupDistance = 8f;

    [Header("Fail Timer")]
    public float failTimeLimit = 10f;

    [Header("UI")]
    public CanvasGroup failUI;
    public float fadeSpeed = 2f;

    [Header("Scene")]
    public string menuSceneName = "NetworkUI";

    private float failTimer = 0f;
    private bool isFailing = false;
    private bool hasTriggeredFail = false;

    void Update()
    {
        if (!IsServer) return;

        List<Transform> players = GetPlayerTransforms();

        if (players.Count <= 1) return;

        bool isGroupTooFar = CheckGroupDistance(players);

        if (isGroupTooFar)
        {
            failTimer += Time.deltaTime;

            if (!isFailing)
                isFailing = true;

            if (failTimer >= failTimeLimit && !hasTriggeredFail)
            {
                hasTriggeredFail = true;
                StartCoroutine(FailSequence());
            }
        }
        else
        {
            failTimer = 0f;
            isFailing = false;
        }
    }

    List<Transform> GetPlayerTransforms()
    {
        List<Transform> list = new List<Transform>();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                list.Add(client.PlayerObject.transform);
            }
        }

        return list;
    }

    bool CheckGroupDistance(List<Transform> players)
    {
        Vector3 center = Vector3.zero;

        foreach (var p in players)
            center += p.position;

        center /= players.Count;

        foreach (var p in players)
        {
            if (Vector3.Distance(p.position, center) > maxGroupDistance)
                return true; // group is too spread out
        }

        return false;
    }

    IEnumerator FailSequence()
    {
        // 1. Despawn all players
        DespawnAllPlayers();

        // 2. Show UI (fade in)
        yield return StartCoroutine(FadeUI(1f));

        // 3. Wait 5 seconds
        yield return new WaitForSeconds(5f);

        // 4. Shutdown network + return to menu
        NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(menuSceneName);
    }

    void DespawnAllPlayers()
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.Despawn(true);
            }
        }
    }

    IEnumerator FadeUI(float targetAlpha)
    {
        if (failUI == null) yield break;

        failUI.gameObject.SetActive(true);

        while (!Mathf.Approximately(failUI.alpha, targetAlpha))
        {
            failUI.alpha = Mathf.MoveTowards(
                failUI.alpha,
                targetAlpha,
                Time.deltaTime * fadeSpeed
            );

            yield return null;
        }
    }
}
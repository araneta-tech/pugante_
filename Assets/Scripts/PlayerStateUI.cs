using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class PlayerStateUI : MonoBehaviour
{
    [Header("Local Player UI")]
    public GameObject bustedUI;
    public GameObject failUI;
    public GameObject distanceWarningUI;

    [Header("Other Players UI Prefabs")]
    public GameObject otherPlayerBustedPrefab;
    public GameObject otherPlayerFailPrefab;
    public Transform otherPlayersUIParent;

    private PlayerMovement localPlayer;
    private readonly Dictionary<PlayerMovement, (GameObject bustedUI, GameObject failUI)> otherPlayerUIs
        = new Dictionary<PlayerMovement, (GameObject, GameObject)>();

    void Start()
    {
        SetAllHidden();
    }

    void Update()
    {
        if (localPlayer == null)
        {
            TryFindLocalPlayer();
            SetAllHidden();
            return;
        }

        if (!localPlayer.IsSpawned)
        {
            SetAllHidden();
            return;
        }

        UpdateLocalUI(localPlayer);
        UpdateOtherPlayersUI();
    }

    void TryFindLocalPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        }
    }

    void UpdateLocalUI(PlayerMovement player)
    {
        if (bustedUI != null)
            bustedUI.SetActive(player.IsBusted);

        if (failUI != null)
            failUI.SetActive(player.IsFailed);

        if (distanceWarningUI != null)
            distanceWarningUI.SetActive(player.IsDistanceWarning);
    }

    void UpdateOtherPlayersUI()
    {
        PlayerMovement[] allPlayers = FindObjectsOfType<PlayerMovement>();

        HashSet<PlayerMovement> aliveEntries = new HashSet<PlayerMovement>();

        foreach (var netPlayer in allPlayers)
        {
            if (netPlayer == null || netPlayer == localPlayer)
                continue;

            aliveEntries.Add(netPlayer);

            if (!otherPlayerUIs.ContainsKey(netPlayer))
            {
                GameObject bustedObj = null;
                GameObject failObj = null;

                if (otherPlayerBustedPrefab != null && otherPlayersUIParent != null)
                    bustedObj = Instantiate(otherPlayerBustedPrefab, otherPlayersUIParent);

                if (otherPlayerFailPrefab != null && otherPlayersUIParent != null)
                    failObj = Instantiate(otherPlayerFailPrefab, otherPlayersUIParent);

                otherPlayerUIs[netPlayer] = (bustedObj, failObj);
            }

            var uiPair = otherPlayerUIs[netPlayer];

            if (uiPair.bustedUI != null)
                uiPair.bustedUI.SetActive(netPlayer.IsBusted);

            if (uiPair.failUI != null)
                uiPair.failUI.SetActive(netPlayer.IsFailed);
        }

        List<PlayerMovement> toRemove = new List<PlayerMovement>();

        foreach (var entry in otherPlayerUIs)
        {
            if (entry.Key == null || !aliveEntries.Contains(entry.Key))
            {
                if (entry.Value.bustedUI != null)
                    Destroy(entry.Value.bustedUI);

                if (entry.Value.failUI != null)
                    Destroy(entry.Value.failUI);

                toRemove.Add(entry.Key);
            }
        }

        foreach (var key in toRemove)
            otherPlayerUIs.Remove(key);
    }

    void SetAllHidden()
    {
        if (bustedUI != null) bustedUI.SetActive(false);
        if (failUI != null) failUI.SetActive(false);
        if (distanceWarningUI != null) distanceWarningUI.SetActive(false);

        foreach (var uiPair in otherPlayerUIs.Values)
        {
            if (uiPair.bustedUI != null) uiPair.bustedUI.SetActive(false);
            if (uiPair.failUI != null) uiPair.failUI.SetActive(false);
        }
    }
}
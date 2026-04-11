using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class PlayerStateUI : MonoBehaviour
{
    [Header("Local Player UI")]
    public GameObject bustedUI;   // Local player's busted UI
    public GameObject failUI;     // Local player's fail UI

    [Header("Other Players UI Prefabs")]
    public GameObject otherPlayerBustedPrefab; // Prefab for busted notification
    public GameObject otherPlayerFailPrefab;   // Prefab for fail notification
    public Transform otherPlayersUIParent;     // Container panel for other players' UI

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

        // Update local player UI
        UpdateLocalUI(localPlayer);

        // Update other players UI
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
    }

    void UpdateOtherPlayersUI()
    {
        foreach (var netPlayer in FindObjectsOfType<PlayerMovement>())
        {
            if (netPlayer == localPlayer) continue; // skip local player

            if (!otherPlayerUIs.ContainsKey(netPlayer))
            {
                // Create UI entries for this player
                GameObject bustedObj = null;
                GameObject failObj = null;

                if (otherPlayerBustedPrefab != null && otherPlayersUIParent != null)
                    bustedObj = Instantiate(otherPlayerBustedPrefab, otherPlayersUIParent);

                if (otherPlayerFailPrefab != null && otherPlayersUIParent != null)
                    failObj = Instantiate(otherPlayerFailPrefab, otherPlayersUIParent);

                otherPlayerUIs[netPlayer] = (bustedObj, failObj);
            }

            // Update visibility
            var uiPair = otherPlayerUIs[netPlayer];
            if (uiPair.bustedUI != null)
                uiPair.bustedUI.SetActive(netPlayer.IsBusted);

            if (uiPair.failUI != null)
                uiPair.failUI.SetActive(netPlayer.IsFailed);
        }
    }

    void SetAllHidden()
    {
        if (bustedUI != null) bustedUI.SetActive(false);
        if (failUI != null) failUI.SetActive(false);

        foreach (var uiPair in otherPlayerUIs.Values)
        {
            if (uiPair.bustedUI != null) uiPair.bustedUI.SetActive(false);
            if (uiPair.failUI != null) uiPair.failUI.SetActive(false);
        }
    }
}
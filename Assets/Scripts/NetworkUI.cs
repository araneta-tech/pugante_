using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    [Header("Network Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button stopHostButton;

    [Header("Network Mode Panel (Host / Client UI)")]
    [SerializeField] private GameObject networkModePanel;

    [Header("Character Selection UI")]
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private CharacterSelectUI characterSelectUI;

    private enum NetworkMode { None, Host, Client }
    private NetworkMode pendingMode = NetworkMode.None;

    private bool hasSelectedCharacter = false;
    private bool isPlaying = false;

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
        stopHostButton.onClick.AddListener(StopHostButtonOnClick);

        stopHostButton.gameObject.SetActive(false);
        characterSelectPanel.SetActive(false);

        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null)
            stopHostButton.gameObject.SetActive(NetworkManager.Singleton.IsHost && Application.isPlaying);

        if (hasSelectedCharacter)
        {
            hostButton.interactable = false;
            clientButton.interactable = false;
        }
        else
        {
            hostButton.interactable = true;
            clientButton.interactable = true;
        }
    }

    private void OnHostButtonClicked()
    {
        pendingMode = NetworkMode.Host;
        DisableNetworkModePanel();
        ShowCharacterSelect();
    }

    private void OnClientButtonClicked()
    {
        pendingMode = NetworkMode.Client;
        DisableNetworkModePanel();
        ShowCharacterSelect();
    }

    private void DisableNetworkModePanel()
    {
        if (networkModePanel != null)
            networkModePanel.SetActive(false);
    }

    private void ShowCharacterSelect()
    {
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(true);

        hasSelectedCharacter = false;
        isPlaying = false;
    }

    public void OnCharacterSelected(int characterIndex)
    {
        hasSelectedCharacter = true;

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        if (pendingMode == NetworkMode.Host)
            NetworkManager.Singleton.StartHost();
        else if (pendingMode == NetworkMode.Client)
            NetworkManager.Singleton.StartClient();

        StartCoroutine(SetCharacterIndexWhenReady(characterIndex));

        isPlaying = true;
    }

    private IEnumerator SetCharacterIndexWhenReady(int characterIndex)
    {
        while (NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            yield return null;
        }

        var playerMovement = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();

        if (playerMovement != null)
            playerMovement.SelectCharacter(characterIndex);
    }

    private void StopHostButtonOnClick()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        ReturnToHostClientSelection();
    }

    private void ReturnToHostClientSelection()
    {
        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);

        if (networkModePanel != null)
            networkModePanel.SetActive(true);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        pendingMode = NetworkMode.None;
        hasSelectedCharacter = false;
        isPlaying = false;
    }
}
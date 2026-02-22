using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button stopHostButton;
    [Header("Character Selection UI")]
    [SerializeField] private GameObject characterSelectPanel; // Assign your character selection UI panel here
    [SerializeField] private CharacterSelectUI characterSelectUI; // Reference to your character select UI script
    [Header("Back Button")]
    public Button backButton; // Now public for assignment from other scripts or Inspector

    private enum NetworkMode { None, Host, Client }
    private NetworkMode pendingMode = NetworkMode.None;
    private bool hasSelectedCharacter = false;
    private bool isPlaying = false;

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
        stopHostButton.onClick.AddListener(StopHostButtonOnClick);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);

        stopHostButton.gameObject.SetActive(false);
        characterSelectPanel.SetActive(false);

        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);

        if (backButton != null)
            backButton.gameObject.SetActive(false);
    }

    private void Update()
    {
        stopHostButton.gameObject.SetActive(NetworkManager.Singleton.IsHost && Application.isPlaying);

        // Back button logic
        if (backButton != null)
        {
            // Show back button only during character selection or while playing
            if (characterSelectPanel.activeSelf || isPlaying)
                backButton.gameObject.SetActive(true);
            else
                backButton.gameObject.SetActive(false);
        }
    }

    private void OnHostButtonClicked()
    {
        pendingMode = NetworkMode.Host;
        ShowCharacterSelect();
    }

    private void OnClientButtonClicked()
    {
        pendingMode = NetworkMode.Client;
        ShowCharacterSelect();
    }

    private void ShowCharacterSelect()
    {
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
        characterSelectPanel.SetActive(true);
        hasSelectedCharacter = false;
        isPlaying = false;
        // Optionally, reset character selection UI state here
    }

    // This should be called by your CharacterSelectUI when a character is chosen
    public void OnCharacterSelected(int characterIndex)
    {
        hasSelectedCharacter = true;
        characterSelectPanel.SetActive(false);

        // Start the network session after character selection
        if (pendingMode == NetworkMode.Host)
            NetworkManager.Singleton.StartHost();
        else if (pendingMode == NetworkMode.Client)
            NetworkManager.Singleton.StartClient();

        // Set the character index for the local player after network spawn
        StartCoroutine(SetCharacterIndexWhenReady(characterIndex));

        isPlaying = true;
    }

    private System.Collections.IEnumerator SetCharacterIndexWhenReady(int characterIndex)
    {
        // Wait until the local player object is spawned
        while (NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            yield return null;
        }

        var playerMovement = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.SelectCharacter(characterIndex);
        }
    }

    private void StopHostButtonOnClick()
    {
        NetworkManager.Singleton.Shutdown();
        ReturnToHostClientSelection();
    }

    // Public method for the back button
    public void OnBackButtonClicked()
    {
        if (characterSelectPanel.activeSelf)
        {
            // If in character selection, go back to host/client selection
            characterSelectPanel.SetActive(false);
            hostButton.gameObject.SetActive(true);
            clientButton.gameObject.SetActive(true);
            pendingMode = NetworkMode.None;
            hasSelectedCharacter = false;
            isPlaying = false;
        }
        else if (isPlaying)
        {
            // If in game, stop the network session and return to host/client selection
            NetworkManager.Singleton.Shutdown();
            ReturnToHostClientSelection();
        }
    }

    private void ReturnToHostClientSelection()
    {
        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);
        characterSelectPanel.SetActive(false);
        pendingMode = NetworkMode.None;
        hasSelectedCharacter = false;
        isPlaying = false;
    }
}

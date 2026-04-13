using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class NetworkUI : MonoBehaviour
{
    public static NetworkUI Instance;

    [Header("Network Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button stopHostButton;

    [Header("Network Mode Panel (Host / Client UI)")]
    [SerializeField] private GameObject networkModePanel;

    [Header("Waiting Room UI")]
    [SerializeField] private GameObject waitingRoomPanel;

    [Header("Character Selection UI")]
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private CharacterSelectUI characterSelectUI;

    [Header("Session Requirements")]
    [SerializeField] private int requiredPlayersToStart = 2;

    private enum NetworkMode { None, Host, Client }
    private NetworkMode pendingMode = NetworkMode.None;

    private bool hasSelectedCharacter = false;
    private bool isPlaying = false;
    private bool connectionStarted = false;
    private bool characterSelectionOpened = false;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
        stopHostButton.onClick.AddListener(StopHostButtonOnClick);

        stopHostButton.gameObject.SetActive(false);

        if (networkModePanel != null)
            networkModePanel.SetActive(true);

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(false);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }

        UpdateCursorState();
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null)
            stopHostButton.gameObject.SetActive(NetworkManager.Singleton.IsHost && Application.isPlaying);

        hostButton.interactable = !connectionStarted && !hasSelectedCharacter;
        clientButton.interactable = !connectionStarted && !hasSelectedCharacter;

        HandleCursorInput();

        if (connectionStarted && !characterSelectionOpened && HasRequiredPlayersConnected())
        {
            ShowCharacterSelect();
        }
    }

    private void HandleCursorInput()
    {
        bool isAltHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        if (isAltHeld)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            UpdateCursorState();
        }
    }

    private void UpdateCursorState()
    {
        if (isPlaying)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void OnHostButtonClicked()
    {
        pendingMode = NetworkMode.Host;
        DisableNetworkModePanel();
        BeginConnection();
    }

    private void OnClientButtonClicked()
    {
        pendingMode = NetworkMode.Client;
        DisableNetworkModePanel();
        BeginConnection();
    }

    private void BeginConnection()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is missing.");
            ReturnToTitleMenu();
            return;
        }

        connectionStarted = true;
        hasSelectedCharacter = false;
        isPlaying = false;
        characterSelectionOpened = false;

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(true);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        UpdateCursorState();

        if (pendingMode == NetworkMode.Host)
        {
            NetworkManager.Singleton.StartHost();
        }
        else if (pendingMode == NetworkMode.Client)
        {
            NetworkManager.Singleton.StartClient();
        }
    }

    private void DisableNetworkModePanel()
    {
        if (networkModePanel != null)
            networkModePanel.SetActive(false);
    }

    private bool HasRequiredPlayersConnected()
    {
        if (NetworkManager.Singleton == null)
            return false;

        return NetworkManager.Singleton.ConnectedClientsList != null &&
               NetworkManager.Singleton.ConnectedClientsList.Count >= requiredPlayersToStart;
    }

    private void ShowWaitingRoom()
    {
        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(true);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        isPlaying = false;
        UpdateCursorState();
    }

    private void ShowCharacterSelect()
    {
        if (characterSelectionOpened)
            return;

        characterSelectionOpened = true;

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(false);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(true);

        hasSelectedCharacter = false;
        isPlaying = false;

        UpdateCursorState();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!connectionStarted)
            return;

        if (HasRequiredPlayersConnected())
        {
            ShowCharacterSelect();
        }
        else
        {
            ShowWaitingRoom();
        }
    }

    public void OnCharacterSelected(int characterIndex)
    {
        if (!connectionStarted || NetworkManager.Singleton == null)
            return;

        if (!HasRequiredPlayersConnected())
        {
            Debug.LogWarning("Character selection is locked until both players are connected.");
            return;
        }

        hasSelectedCharacter = true;

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(false);

        StartCoroutine(SetCharacterIndexWhenReady(characterIndex));

        isPlaying = true;
        UpdateCursorState();
    }

    private IEnumerator SetCharacterIndexWhenReady(int characterIndex)
    {
        while (NetworkManager.Singleton == null ||
               NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            yield return null;
        }

        var playerMovement = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();

        if (playerMovement != null)
        {
            playerMovement.SelectCharacter(characterIndex);

            if (GameManager.Instance != null)
                playerMovement.SpawnAtPosition(GameManager.Instance.GetChapterStartPosition());
        }
    }

    private void StopHostButtonOnClick()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        ReturnToTitleMenu();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        ReturnToTitleMenu();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        }
    }

    private void OnServerStopped(bool _)
    {
        ReturnToTitleMenu();
    }

    private void ReturnToTitleMenu()
    {
        connectionStarted = false;
        hasSelectedCharacter = false;
        isPlaying = false;
        characterSelectionOpened = false;
        pendingMode = NetworkMode.None;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SceneManager.LoadScene("TitleMenu", LoadSceneMode.Single);
    }
}
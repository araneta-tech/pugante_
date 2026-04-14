using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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

    [Header("Character Selection UI")]
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private CharacterSelectUI characterSelectUI;

    [Header("Client Connection UI")]
    [SerializeField] private InputField ipInputField;
    [SerializeField] private Button confirmIPButton; // NEW
    [SerializeField] private Text connectionStatusText;

    private enum NetworkMode { None, Host, Client }
    private NetworkMode pendingMode = NetworkMode.None;

    private bool hasSelectedCharacter = false;
    private bool isPlaying = false;

    private string confirmedIP = ""; // NEW

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
        stopHostButton.onClick.AddListener(StopHostButtonOnClick);
        confirmIPButton.onClick.AddListener(OnConfirmIPClicked); // NEW

        stopHostButton.gameObject.SetActive(false);
        characterSelectPanel.SetActive(false);

        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);

        confirmIPButton.gameObject.SetActive(false); // hidden initially

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }

        UpdateCursorState();
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null)
            stopHostButton.gameObject.SetActive(NetworkManager.Singleton.IsHost && Application.isPlaying);

        hostButton.interactable = !hasSelectedCharacter;
        clientButton.interactable = !hasSelectedCharacter;

        HandleCursorInput();
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
        ShowCharacterSelect();
    }

    private void OnClientButtonClicked()
    {
        pendingMode = NetworkMode.Client;
        DisableNetworkModePanel();

        // SHOW IP INPUT FIRST
        confirmIPButton.gameObject.SetActive(true);
        connectionStatusText.text = "Enter Host IP and Confirm";
    }

    private void OnConfirmIPClicked() // NEW
    {
        if (string.IsNullOrEmpty(ipInputField.text))
        {
            connectionStatusText.text = "Please enter a valid IP.";
            return;
        }

        confirmedIP = ipInputField.text;
        connectionStatusText.text = $"IP Confirmed: {confirmedIP}";

        confirmIPButton.gameObject.SetActive(false);

        // Proceed to character select AFTER confirming IP
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
        UpdateCursorState();
    }

    public void OnCharacterSelected(int characterIndex)
    {
        hasSelectedCharacter = true;

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        if (pendingMode == NetworkMode.Host)
        {
            NetworkManager.Singleton.StartHost();
        }
        else if (pendingMode == NetworkMode.Client)
        {
            StartCoroutine(TryConnectClient(confirmedIP, 7777, 180f));
        }

        StartCoroutine(SetCharacterIndexWhenReady(characterIndex));

        isPlaying = true;
        UpdateCursorState();
    }

    private IEnumerator TryConnectClient(string ip, ushort port, float timeoutSeconds)
    {
        float startTime = Time.time;
        bool connected = false;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, port);

        while (!connected && Time.time - startTime < timeoutSeconds)
        {
            Debug.Log($"[NetworkUI] Attempting to connect to {ip}:{port}...");
            if (connectionStatusText != null)
                connectionStatusText.text = $"Connecting to {ip}:{port}...";

            NetworkManager.Singleton.StartClient();

            float attemptStart = Time.time;
            while (Time.time - attemptStart < 5f)
            {
                if (NetworkManager.Singleton.IsConnectedClient)
                {
                    connected = true;
                    break;
                }
                yield return null;
            }

            if (!connected)
            {
                Debug.Log("[NetworkUI] Connection attempt failed, retrying...");
                if (connectionStatusText != null)
                    connectionStatusText.text = "Connection failed, retrying...";

                NetworkManager.Singleton.Shutdown();
                yield return new WaitForSeconds(2f);
            }
        }

        if (!connected)
        {
            Debug.LogError("[NetworkUI] Could not connect to host after retries.");
            if (connectionStatusText != null)
                connectionStatusText.text = "Failed to connect after retries.";

            ReturnToTitleMenu();
        }
        else
        {
            if (connectionStatusText != null)
                connectionStatusText.text = "Connected!";
        }
    }

    private IEnumerator SetCharacterIndexWhenReady(int characterIndex)
    {
        float timer = 0f;

        while (NetworkManager.Singleton == null ||
               NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            timer += Time.deltaTime;
            if (timer > 10f)
            {
                Debug.LogError("PlayerObject not found!");
                yield break;
            }

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
            ReturnToTitleMenu();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ReturnToTitleMenu();
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
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
        isPlaying = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SceneManager.LoadScene("TitleMenu", LoadSceneMode.Single);
    }
}
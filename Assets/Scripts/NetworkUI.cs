using System.Collections;
using System.Net;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    public static NetworkUI Instance;

    [Header("Network Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button stopHostButton;

    [Header("Host & Client Panel")]
    [SerializeField] private GameObject hostClientPanel;

    [Header("Character Selection UI")]
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private CharacterSelectUI characterSelectUI;

    [Header("Client Connection UI")]
    [SerializeField] private GameObject ipInputPanel;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private Button confirmIPButton;
    [SerializeField] private Text connectionStatusText;

    private enum NetworkMode
    {
        None,
        Host,
        Client
    }

    private NetworkMode pendingMode = NetworkMode.None;
    private bool hasSelectedCharacter = false;
    private bool isPlaying = false;
    private string confirmedIP = "";

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
        if (hostButton != null)
            hostButton.onClick.AddListener(OnHostButtonClicked);

        if (clientButton != null)
            clientButton.onClick.AddListener(OnClientButtonClicked);

        if (stopHostButton != null)
            stopHostButton.onClick.AddListener(StopHostButtonOnClick);

        if (confirmIPButton != null)
            confirmIPButton.onClick.AddListener(OnConfirmIPClicked);

        if (stopHostButton != null)
            stopHostButton.gameObject.SetActive(false);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        if (hostClientPanel != null)
            hostClientPanel.SetActive(true);

        if (hostButton != null)
            hostButton.gameObject.SetActive(true);

        if (clientButton != null)
            clientButton.gameObject.SetActive(true);

        HideIPInputPanel();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }

        UpdateCursorState();
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null && stopHostButton != null)
            stopHostButton.gameObject.SetActive(NetworkManager.Singleton.IsHost && Application.isPlaying);

        if (hostButton != null)
            hostButton.interactable = !hasSelectedCharacter;

        if (clientButton != null)
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
            return;
        }

        UpdateCursorState();
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
        HideHostClientPanel();
        HideIPInputPanel();
        ShowCharacterSelect();
    }

    private void OnClientButtonClicked()
    {
        pendingMode = NetworkMode.Client;
        HideHostClientPanel();
        ShowIPInputPanel();
    }

    private void OnConfirmIPClicked()
    {
        if (ipInputField == null)
        {
            if (connectionStatusText != null)
                connectionStatusText.text = "IP input field is missing.";
            return;
        }

        string rawIP = ipInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(rawIP))
        {
            if (connectionStatusText != null)
                connectionStatusText.text = "Please enter a valid IP.";
            CancelIPInput();
            return;
        }

        if (!IsValidIPAddress(rawIP))
        {
            if (connectionStatusText != null)
                connectionStatusText.text = "Invalid IP address. Input canceled.";
            CancelIPInput();
            return;
        }

        confirmedIP = rawIP;

        if (connectionStatusText != null)
            connectionStatusText.text = $"IP Confirmed: {confirmedIP}";

        HideIPInputPanel();
        ShowCharacterSelect();
    }

    private bool IsValidIPAddress(string ip)
    {
        return IPAddress.TryParse(ip, out _);
    }

    private void CancelIPInput()
    {
        confirmedIP = "";

        if (ipInputField != null)
            ipInputField.text = "";

        HideIPInputPanel();
        ShowHostClientPanel();
    }

    private void ShowIPInputPanel()
    {
        if (ipInputPanel != null)
            ipInputPanel.SetActive(true);

        if (confirmIPButton != null)
            confirmIPButton.gameObject.SetActive(true);

        if (connectionStatusText != null)
            connectionStatusText.text = "Enter Host IP and Confirm";

        if (ipInputField != null)
        {
            ipInputField.text = "";
            ipInputField.ActivateInputField();
        }
    }

    private void HideIPInputPanel()
    {
        if (ipInputPanel != null)
            ipInputPanel.SetActive(false);

        if (confirmIPButton != null)
            confirmIPButton.gameObject.SetActive(false);
    }

    private void HideHostClientPanel()
    {
        if (hostClientPanel != null)
            hostClientPanel.SetActive(false);
    }

    private void ShowHostClientPanel()
    {
        if (hostClientPanel != null)
            hostClientPanel.SetActive(true);
    }

    private void ShowCharacterSelect()
    {
        if (hostClientPanel != null)
            hostClientPanel.SetActive(false);

        if (hostButton != null)
            hostButton.gameObject.SetActive(false);

        if (clientButton != null)
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
            if (NetworkManager.Singleton != null)
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
        if (NetworkManager.Singleton == null)
        {
            if (connectionStatusText != null)
                connectionStatusText.text = "NetworkManager is missing.";

            yield break;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[NetworkUI] UnityTransport component not found.");
            if (connectionStatusText != null)
                connectionStatusText.text = "Transport is missing.";
            yield break;
        }

        if (!IsValidIPAddress(ip))
        {
            if (connectionStatusText != null)
                connectionStatusText.text = "Invalid IP address. Input canceled.";
            CancelIPInput();
            yield break;
        }

        float startTime = Time.time;
        bool connected = false;

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

            CancelIPInput();
            ReturnToTitleMenu();
        }
        else
        {
            if (connectionStatusText != null)
                connectionStatusText.text = "Connected!";

            HideIPInputPanel();
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

        PlayerMovement playerMovement = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();

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

        HideIPInputPanel();
        ShowHostClientPanel();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SceneManager.LoadScene("TitleMenu", LoadSceneMode.Single);
    }
}
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Fail UI (Game Over)")]
    [SerializeField] private RawImage failRawImage;
    [SerializeField] private float failFadeSpeed = 1f;

    private Coroutine failRoutine;

    private enum NetworkMode { None, Host, Client }
    private NetworkMode pendingMode = NetworkMode.None;

    private bool hasSelectedCharacter = false;
    private bool isPlaying = false;

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
        characterSelectPanel.SetActive(false);

        if (failRawImage != null)
            failRawImage.gameObject.SetActive(false);

        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);

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
            NetworkManager.Singleton.StartHost();
        else if (pendingMode == NetworkMode.Client)
            NetworkManager.Singleton.StartClient();

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
            playerMovement.SelectCharacter(characterIndex);
    }

    private void StopHostButtonOnClick()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ReturnToHostClientSelection();
        }
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

        if (failRawImage != null)
            failRawImage.gameObject.SetActive(false);

        UpdateCursorState();
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
        ReturnToHostClientSelection();
    }

    public void StartFailUISequence(float waitSeconds)
    {
        if (failRoutine != null)
            StopCoroutine(failRoutine);

        failRoutine = StartCoroutine(FailRoutine(waitSeconds));
    }

    private IEnumerator FailRoutine(float waitSeconds)
    {
        if (failRawImage != null)
        {
            failRawImage.gameObject.SetActive(true);

            Color c = failRawImage.color;
            c.a = 0f;
            failRawImage.color = c;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * failFadeSpeed;
                c.a = Mathf.Clamp01(t);
                failRawImage.color = c;
                yield return null;
            }

            c.a = 1f;
            failRawImage.color = c;
        }

        yield return new WaitForSeconds(waitSeconds);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        ReturnToHostClientSelection();
    }
}
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button stopHostButton; // Add this in the Inspector

   
    private void Start()
    {
        hostButton.onClick.AddListener(HostButtonOnClick);
        clientButton.onClick.AddListener(ClientButtonOnClick);
        stopHostButton.onClick.AddListener(StopHostButtonOnClick);

        stopHostButton.gameObject.SetActive(false); // Hide by default
    }

    private void Update()
    {
        // Show stopHostButton only if we are hosting and the game is running
        if (NetworkManager.Singleton.IsHost && Application.isPlaying)
        {
            stopHostButton.gameObject.SetActive(true);
        }
        else
        {
            stopHostButton.gameObject.SetActive(false);
        }
    }

    private void HostButtonOnClick()
    {
        NetworkManager.Singleton.StartHost();
    }

    private void ClientButtonOnClick()
    {
        NetworkManager.Singleton.StartClient();
    }

    private void StopHostButtonOnClick()
    {
        NetworkManager.Singleton.Shutdown();
    }
}

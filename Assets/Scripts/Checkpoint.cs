using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class Checkpoint : NetworkBehaviour
{
    public int checkpointIndex;

    private HashSet<ulong> playersInside = new HashSet<ulong>();
    private bool checkpointActivated = false;
    private Vector3 checkpointPosition;
    private Coroutine failCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player == null) return;

        ulong clientId = player.GetComponent<NetworkObject>().OwnerClientId;
        playersInside.Add(clientId);

        // Activate checkpoint only if all connected players are inside
        if (!checkpointActivated && playersInside.Count == NetworkManager.Singleton.ConnectedClientsList.Count)
        {
            checkpointActivated = true;
            checkpointPosition = transform.position;

            if (GameManager.Instance != null && GameManager.Instance.IsSpawned)
            {
                GameManager.Instance.ActivateCheckpoint(checkpointIndex);
            }

            ShowCheckpointUIClientRpc();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player == null) return;

        ulong clientId = player.GetComponent<NetworkObject>().OwnerClientId;
        playersInside.Remove(clientId);
    }

    [ClientRpc]
    private void ShowCheckpointUIClientRpc()
    {
        if (CheckpointUI.Instance != null)
            CheckpointUI.Instance.ShowCheckpointUI();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            PlayerMovement.OnPlayerDespawned += OnPlayerDespawned;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            PlayerMovement.OnPlayerDespawned -= OnPlayerDespawned;
        }
    }

    // Called when a player despawns (must be invoked from PlayerMovement)
    private void OnPlayerDespawned(PlayerMovement player)
    {
        if (!IsServer) return;

        if (checkpointActivated)
        {
            // Respawn the player at the checkpoint
            player.RespawnAtCheckpoint(checkpointPosition);
        }
        else
        {
            // No checkpoint: fail after 2 seconds
            if (failCoroutine == null)
                failCoroutine = StartCoroutine(FailAndShutdownAfterDelay());
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Also handle disconnects as despawns
        if (!checkpointActivated)
        {
            if (failCoroutine == null)
                failCoroutine = StartCoroutine(FailAndShutdownAfterDelay());
        }
    }

    private IEnumerator FailAndShutdownAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.Shutdown();
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

using UnityEngine;
using Unity.Netcode;

public class Checkpoint : NetworkBehaviour
{
    public int checkpointIndex;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player == null) return;

        if (GameManager.Instance == null || !GameManager.Instance.IsSpawned)
            return;

        if (!GameManager.Instance.HasActivatedCheckpoint(checkpointIndex))
        {
            GameManager.Instance.ActivateCheckpoint(checkpointIndex);

            ShowCheckpointUIClientRpc();
        }
    }

    [ClientRpc]
    private void ShowCheckpointUIClientRpc()
    {
        if (CheckpointUI.Instance != null)
            CheckpointUI.Instance.ShowCheckpointUI();
    }
}
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Lives Settings")]
    public int maxLives = 3;
    private NetworkVariable<int> currentLives = new NetworkVariable<int>();

    [Header("Checkpoints")]
    public Transform[] checkpoints;
    private NetworkVariable<int> currentCheckpointIndex = new NetworkVariable<int>(0);
    private HashSet<int> activatedCheckpoints = new HashSet<int>();

    [Header("Chapter Start Points")]
    public Transform[] chapterStartPoints;

    [Header("Reset Delay")]
    public float resetDelay = 2f;

    private List<PlayerMovement> players = new List<PlayerMovement>();

    public override void OnNetworkSpawn()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (IsServer)
        {
            currentLives.Value = maxLives;
            currentCheckpointIndex.Value = 0;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterPlayer(PlayerMovement player)
    {
        if (!players.Contains(player)) players.Add(player);
    }

    public void UnregisterPlayer(PlayerMovement player)
    {
        if (players.Contains(player)) players.Remove(player);
    }

    public int PlayersCount => players.Count;

    public int GetCurrentCheckpointIndex() => currentCheckpointIndex.Value;

    public bool HasActivatedCheckpoint(int index) => activatedCheckpoints.Contains(index);

    public void ActivateCheckpoint(int index)
    {
        if (!IsServer) return;

        if (!activatedCheckpoints.Contains(index))
            activatedCheckpoints.Add(index);

        if (index > currentCheckpointIndex.Value)
        {
            currentCheckpointIndex.Value = index;
            currentLives.Value = maxLives;
            Debug.Log($"[GameManager] Checkpoint reached → Lives reset to {maxLives}");
        }
    }

    public void PlayerBusted()
    {
        if (!IsServer) return;

        currentLives.Value--;
        Debug.Log($"[GameManager] Player busted. Lives left: {currentLives.Value}");

        if (currentLives.Value <= 0)
        {
            ShowBustedClientRpc();
            StartCoroutine(ResetChapterAfterDelay());
        }
        else
        {
            RespawnPlayersAtCheckpoint();
        }
    }

    [ClientRpc]
    void ShowBustedClientRpc()
    {
        if (BustedUI.Instance != null) BustedUI.Instance.Show();
    }

    public void RespawnPlayersAtCheckpoint()
    {
        Transform spawn;

        if (currentCheckpointIndex.Value > 0 && checkpoints.Length > currentCheckpointIndex.Value)
        {
            spawn = checkpoints[currentCheckpointIndex.Value];
        }
        else
        {
            int chapterIndex = GetChapterIndex();
            spawn = chapterStartPoints.Length > chapterIndex ? chapterStartPoints[chapterIndex] : transform;
        }

        foreach (var p in players)
        {
            if (p != null)
            {
                p.RespawnAtCheckpoint(spawn.position);
            }
        }

        Debug.Log($"[GameManager] Players respawned at {spawn.name}");
    }

    IEnumerator ResetChapterAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);
        ResetChapter();
    }

    void ResetChapter()
    {
        Debug.Log("[GameManager] Resetting chapter");

        currentLives.Value = maxLives;
        activatedCheckpoints.Clear();
        currentCheckpointIndex.Value = 0;

        RespawnPlayersAtCheckpoint();
    }

    public void EndChapter()
    {
        Debug.Log("[GameManager] Chapter Completed!");

        currentCheckpointIndex.Value = 0;
        currentLives.Value = maxLives;
        activatedCheckpoints.Clear();
    }

    int GetChapterIndex()
    {
        if (checkpoints.Length == 0 || chapterStartPoints.Length == 0) return 0;
        int checkpointsPerChapter = checkpoints.Length / chapterStartPoints.Length;
        return Mathf.Clamp(currentCheckpointIndex.Value / checkpointsPerChapter, 0, chapterStartPoints.Length - 1);
    }
}
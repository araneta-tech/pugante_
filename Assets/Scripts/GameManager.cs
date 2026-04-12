using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Checkpoints")]
    public Transform[] checkpoints;
    private NetworkVariable<int> currentCheckpointIndex = new NetworkVariable<int>(0);
    private HashSet<int> activatedCheckpoints = new HashSet<int>();

    [Header("Chapter Start Points")]
    public Transform[] chapterStartPoints;

    private int nextChapterStartIndex = 0;

    [Header("Reset Delay")]
    public float resetDelay = 2f;

    private List<PlayerMovement> players = new List<PlayerMovement>();

    private bool gameOverTriggered = false; // prevent multiple triggers

    public override void OnNetworkSpawn()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (IsServer)
        {
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
            Debug.Log($"[GameManager] Checkpoint reached → Updated to {index}");
        }
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

        activatedCheckpoints.Clear();
        currentCheckpointIndex.Value = 0;

        RespawnPlayersAtCheckpoint();
        nextChapterStartIndex = 0;
    }

    public void EndChapter()
    {
        Debug.Log("[GameManager] Chapter Completed!");

        currentCheckpointIndex.Value = 0;
        activatedCheckpoints.Clear();
    }

    int GetChapterIndex()
    {
        if (checkpoints.Length == 0 || chapterStartPoints.Length == 0) return 0;
        int checkpointsPerChapter = checkpoints.Length / chapterStartPoints.Length;
        return Mathf.Clamp(currentCheckpointIndex.Value / checkpointsPerChapter, 0, chapterStartPoints.Length - 1);
    }

    public Vector3 GetChapterStartPosition()
    {
        int chapterIndex = GetChapterIndex();
        if (chapterStartPoints.Length > chapterIndex && chapterStartPoints[chapterIndex] != null)
            return chapterStartPoints[chapterIndex].position;

        return transform.position;
    }

    public Vector3 GetChapterStartPositionForPlayer(PlayerMovement player)
    {
        int chapterIndex = GetChapterIndex();

        if (chapterStartPoints.Length == 0)
            return transform.position;

        int playerIndex = players.IndexOf(player);

        // Wrap around if more players than start points
        int spawnIndex = playerIndex % chapterStartPoints.Length;

        // Offset for chapter
        int finalIndex = spawnIndex + (chapterIndex * chapterStartPoints.Length);
        finalIndex = Mathf.Min(finalIndex, chapterStartPoints.Length - 1);

        return chapterStartPoints[finalIndex].position;
    }

    public Vector3 GetNextChapterStartPosition()
    {
        if (chapterStartPoints.Length == 0)
            return transform.position;

        Vector3 spawnPos = chapterStartPoints[nextChapterStartIndex].position;

        nextChapterStartIndex = (nextChapterStartIndex + 1) % chapterStartPoints.Length;

        return spawnPos;
    }

    // ---------------- GAME OVER HANDLING ----------------
    public void CheckForGameOverConditions()
    {
        if (players.Count == 0 || gameOverTriggered) return;

        bool allFailedOrBusted = true;
        foreach (var p in players)
        {
            if (p != null && !p.IsFailed && !p.IsBusted)
            {
                allFailedOrBusted = false;
                break;
            }
        }

        if (allFailedOrBusted)
        {
            gameOverTriggered = true;
            StartCoroutine(GameOverRoutine());
        }
    }

    private IEnumerator GameOverRoutine()
    {
        Debug.Log("[GameManager] Game Over detected. Waiting 5 seconds before stopping...");

        yield return new WaitForSeconds(5f);

        HandleGameOver();
    }

    public void HandleGameOver()
    {
        Debug.Log("[GameManager] Ending game and returning to Title menu.");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        StopAllCoroutines();

        SceneManager.LoadScene("TitleMenu", LoadSceneMode.Single);
    }
}

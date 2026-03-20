using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Lives")]
    public int maxLives = 3;
    private NetworkVariable<int> currentLives = new NetworkVariable<int>();

    [Header("Checkpoints")]
    public Transform[] checkpoints;
    private NetworkVariable<int> currentCheckpointIndex = new NetworkVariable<int>(0);

    private HashSet<int> activatedCheckpoints = new HashSet<int>();
    private List<PlayerMovement> players = new List<PlayerMovement>();

    // ❌ REMOVED Awake()

    public override void OnNetworkSpawn()
    {
        Debug.Log("GameManager spawned. IsServer: " + IsServer);

        // ✅ SAFE singleton setup (Netcode-ready)
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // ✅ Initialize ONLY on server
        if (IsServer)
        {
            currentLives.Value = maxLives;
            currentCheckpointIndex.Value = 0;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    // ✅ REGISTER PLAYERS PROPERLY
    public void RegisterPlayer(PlayerMovement player)
    {
        if (!players.Contains(player))
            players.Add(player);
    }

    public void UnregisterPlayer(PlayerMovement player)
    {
        if (players.Contains(player))
            players.Remove(player);
    }

    public int GetCurrentCheckpointIndex() => currentCheckpointIndex.Value;

    public bool HasActivatedCheckpoint(int index)
    {
        return activatedCheckpoints.Contains(index);
    }

    public void ActivateCheckpoint(int index)
    {
        if (!IsServer) return;

        if (!activatedCheckpoints.Contains(index))
            activatedCheckpoints.Add(index);

        if (index > currentCheckpointIndex.Value)
            currentCheckpointIndex.Value = index;
    }

    public void PlayerBusted()
    {
        if (NetworkUI.Instance != null && !NetworkUI.Instance.IsPlaying)
            return; // Game hasn't started yet

        if (!IsServer) return;

        currentLives.Value--;
        ShowBustedClientRpc();

        if (currentLives.Value <= 0)
            ResetChapter();
        else
            RespawnPlayers();
    }

    [ClientRpc]
    void ShowBustedClientRpc()
    {
        if (BustedUI.Instance != null)
            BustedUI.Instance.Show();
    }

    void RespawnPlayers()
    {
        if (checkpoints.Length == 0) return;

        Transform spawn = checkpoints[currentCheckpointIndex.Value];

        foreach (var p in players)
        {
            if (p != null)
            {
                p.transform.position = spawn.position;

                Rigidbody rb = p.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.velocity = Vector3.zero;
            }
        }
    }

    void ResetChapter()
    {
        currentLives.Value = maxLives;

        int chapterStart = (currentCheckpointIndex.Value / 2) * 2;
        currentCheckpointIndex.Value = chapterStart;

        activatedCheckpoints.Clear();

        RespawnPlayers();
    }
}
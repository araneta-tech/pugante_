using UnityEngine;
using Unity.Netcode;

public class MovableObject : NetworkBehaviour
{
    [Header("Interaction Settings")]
    public float detectRange = 3f;
    public float holdDistance = 2.5f;
    public float holdHeight = 1.0f;

    [Header("Follow Settings")]
    public float followSpeed = 10f;

    [Header("Floating UI")]
    public bool showFloatingF = true;
    [SerializeField] private GameObject floatingUIText;

    private NetworkVariable<bool> isHeld = new NetworkVariable<bool>(false);
    private NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);

    private Rigidbody rb;
    private Collider objectCollider;
    private Collider playerCollider;

    private bool isPlayerNearby = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectCollider = GetComponent<Collider>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        if (!IsSpawned) return;

        isPlayerNearby = CheckPlayerProximity();

        if (showFloatingF)
            UpdateFloatingUIText();

        HandleInput();

        if (IsServer && isHeld.Value)
        {
            FollowPlayerServer();
        }
    }

    // -------------------------
    // 🔥 NEW: INTERACTION RESTRICTION
    // -------------------------
    bool CanInteract(PlayerMovement player)
    {
        if (player == null) return false;

        // ❌ Restrict character index 0
        if (player.SelectedCharacterIndex == 0)
            return false;

        return true;
    }

    // -------------------------
    // PROXIMITY CHECK
    // -------------------------
    bool CheckPlayerProximity()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange);

        foreach (var hit in hits)
        {
            PlayerMovement player = hit.GetComponent<PlayerMovement>();

            if (player != null && player.IsOwner)
                return true;
        }

        return false;
    }

    void HandleInput()
    {
        if (!isPlayerNearby) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            PlayerMovement player = GetLocalPlayer();
            if (player == null) return;

            // 🔥 APPLY RESTRICTION HERE
            if (!CanInteract(player))
            {
                Debug.Log("[Movable] Interaction blocked: Character 0 cannot use this.");
                return;
            }

            if (isHeld.Value)
                ReleaseServerRpc(player.OwnerClientId);
            else
                GrabServerRpc(player.OwnerClientId);
        }
    }

    PlayerMovement GetLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerMovement>())
        {
            if (p.IsOwner && p.OwnerClientId == NetworkManager.Singleton.LocalClientId)
                return p;
        }
        return null;
    }

    // -------------------------
    // SERVER LOGIC
    // -------------------------
    [ServerRpc(RequireOwnership = false)]
    void GrabServerRpc(ulong playerId)
    {
        if (isHeld.Value) return;
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(playerId)) return;

        var playerObj = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
        if (playerObj == null) return;

        PlayerMovement player = playerObj.GetComponent<PlayerMovement>();
        
        // 🔥 SERVER-SIDE VALIDATION (IMPORTANT)
        if (!CanInteract(player)) return;

        playerCollider = playerObj.GetComponent<Collider>();

        isHeld.Value = true;
        holderId.Value = playerId;

        rb.isKinematic = true;

        if (playerCollider != null && objectCollider != null)
            Physics.IgnoreCollision(playerCollider, objectCollider, true);

        player.SetSpeedMultiplier(0.5f);

        Debug.Log($"[Movable] Grabbed by {playerId}");
    }

    [ServerRpc(RequireOwnership = false)]
    void ReleaseServerRpc(ulong playerId)
    {
        if (!isHeld.Value) return;
        if (holderId.Value != playerId) return;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(playerId)) return;

        var playerObj = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
        if (playerObj == null) return;

        PlayerMovement player = playerObj.GetComponent<PlayerMovement>();

        isHeld.Value = false;
        holderId.Value = ulong.MaxValue;

        if (playerCollider != null && objectCollider != null)
            Physics.IgnoreCollision(playerCollider, objectCollider, false);

        playerCollider = null;

        player.RestoreSpeed();

        rb.isKinematic = true;

        Debug.Log($"[Movable] Released by {playerId}");
    }

    // -------------------------
    // SERVER MOVEMENT
    // -------------------------
    void FollowPlayerServer()
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(holderId.Value)) return;

        var playerObj = NetworkManager.Singleton.ConnectedClients[holderId.Value].PlayerObject;
        if (playerObj == null) return;

        Vector3 targetPosition =
            playerObj.transform.position +
            playerObj.transform.forward * holdDistance +
            Vector3.up * holdHeight;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );
    }

    // -------------------------
    // UI
    // -------------------------
    void UpdateFloatingUIText()
    {
        if (floatingUIText == null) return;

        PlayerMovement player = GetLocalPlayer();

        // 🔥 Hide UI if restricted
        if (player != null && !CanInteract(player))
        {
            floatingUIText.SetActive(false);
            return;
        }

        if (isPlayerNearby)
        {
            floatingUIText.SetActive(true);
        }
        else
        {
            floatingUIText.SetActive(false);
        }
    }
}
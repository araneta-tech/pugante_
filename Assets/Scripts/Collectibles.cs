using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CollectibleItem : NetworkBehaviour
{
    // Global list of all active collectible items
    public static readonly List<CollectibleItem> ActiveItems = new List<CollectibleItem>();

    [Header("Item Settings")]
    public float detectRange = 2f;

    [Header("Floating UI")]
    public bool showFloatingF = true;

    // Assign your UI text object here
    [SerializeField] private GameObject floatingUIText;

    [HideInInspector]
    public bool isPlayerNearby = false;

    void OnEnable()
    {
        if (!ActiveItems.Contains(this))
            ActiveItems.Add(this);

        if (floatingUIText != null)
            floatingUIText.SetActive(false);
    }

    void OnDisable()
    {
        ActiveItems.Remove(this);

        if (floatingUIText != null)
            floatingUIText.SetActive(false);
    }

    void Update()
    {
        if (!IsSpawned) return;

        isPlayerNearby = CheckPlayerProximity();

        if (showFloatingF)
        {
            UpdateFloatingUIText();
        }
    }

    // --------------------------
    // PLAYER DETECTION
    // --------------------------
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

    // --------------------------
    // FLOATING UI CONTROL
    // --------------------------
    void UpdateFloatingUIText()
    {
        if (floatingUIText == null) return;

        if (isPlayerNearby)
        {
            floatingUIText.SetActive(true);
        }
        else
        {
            floatingUIText.SetActive(false);
        }
    }

    // --------------------------
    // SERVER-SIDE COLLECTION
    // --------------------------
    [ServerRpc(RequireOwnership = false)]
    public void CollectServerRpc()
    {
        if (!IsSpawned) return;

        Debug.Log($"[Item] Collected: {name}");

        if (floatingUIText != null)
            floatingUIText.SetActive(false);

        ActiveItems.Remove(this);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();

        Destroy(gameObject);
    }
}
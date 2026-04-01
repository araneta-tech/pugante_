using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CollectibleItem : NetworkBehaviour
{
    public static readonly List<CollectibleItem> ActiveItems = new List<CollectibleItem>();

    [Header("Item Settings")]
    public float detectRange = 2f;

    [Header("Floating UI")]
    public bool showFloatingF = true;

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

        if (isPlayerNearby && IsOwner && Input.GetKeyDown(KeyCode.F))
        {
            CollectServerRpc();
        }
    }

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
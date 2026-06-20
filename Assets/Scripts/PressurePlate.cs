using UnityEngine;
using Unity.Netcode;

public class PressurePlate : NetworkBehaviour
{
    [Header("Player Detection")]
    public string playerTag = "Player";

    [Header("Animator")]
    public Animator targetAnimator;

    [Header("UI Prompt")]
    public GameObject interactUI;

    private int localPlayerInsideCount = 0;

    private NetworkVariable<bool> isPressedNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (interactUI != null)
            interactUI.SetActive(false);

        ApplyState(isPressedNet.Value);
        isPressedNet.OnValueChanged += OnStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        isPressedNet.OnValueChanged -= OnStateChanged;
    }

    void Update()
    {
        bool localPlayerInside = localPlayerInsideCount > 0;
        bool canInteract = IsLocalPlayerAvailable() && localPlayerInside;

        if (interactUI != null)
            interactUI.SetActive(canInteract);

        if (!canInteract)
            return;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsLocalPlayerCollider(other))
            return;

        localPlayerInsideCount++;

        Debug.Log("[PressurePlate] Local player ENTER");

        SetPressedServerRpc(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsLocalPlayerCollider(other))
            return;

        localPlayerInsideCount = Mathf.Max(0, localPlayerInsideCount - 1);

        Debug.Log("[PressurePlate] Local player EXIT");

        if (localPlayerInsideCount == 0)
        {
            SetPressedServerRpc(false);
        }
    }

    private bool IsLocalPlayerAvailable()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsClient &&
               NetworkManager.Singleton.LocalClient != null &&
               NetworkManager.Singleton.LocalClient.PlayerObject != null;
    }

    private bool IsLocalPlayerCollider(Collider other)
    {
        if (other == null || !IsLocalPlayerAvailable())
            return false;

        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj != null)
        {
            return netObj == NetworkManager.Singleton.LocalClient.PlayerObject;
        }

        return other.CompareTag(playerTag);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPressedServerRpc(bool pressed)
    {
        if (isPressedNet.Value != pressed)
        {
            Debug.Log($"[PressurePlate] Server state → {pressed}");
            isPressedNet.Value = pressed;
        }
    }

    private void OnStateChanged(bool oldValue, bool newValue)
    {
        ApplyState(newValue);
    }

    private void ApplyState(bool pressed)
    {
        if (targetAnimator != null)
        {
            // 🔥 Animator expects "isPressed" bool
            targetAnimator.SetBool("isPressed", pressed);
        }
    }
}
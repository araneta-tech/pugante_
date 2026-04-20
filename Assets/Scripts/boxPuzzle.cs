using UnityEngine;
using Unity.Netcode;

public class HoldFToOpen : NetworkBehaviour
{
    [Header("Player Detection")]
    public string playerTag = "Player";

    [Header("Animator")]
    public Animator targetAnimator;

    [Header("UI Prompt")]
    public GameObject interactUI;

    [Header("Settings")]
    public KeyCode holdKey = KeyCode.F;

    private int localPlayerInsideCount = 0;

    private NetworkVariable<bool> isClosedNet = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (interactUI != null)
            interactUI.SetActive(false);

        ApplyClosedState(isClosedNet.Value);
        isClosedNet.OnValueChanged += OnClosedStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        isClosedNet.OnValueChanged -= OnClosedStateChanged;
    }

    void Update()
    {
        bool localPlayerInside = localPlayerInsideCount > 0;
        bool canInteract = IsLocalPlayerAvailable() && localPlayerInside;

        if (interactUI != null)
            interactUI.SetActive(canInteract);

        if (!canInteract)
            return;

        bool shouldBeClosed = !Input.GetKey(holdKey);

        if (isClosedNet.Value != shouldBeClosed)
        {
            SetClosedServerRpc(shouldBeClosed);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsLocalPlayerCollider(other))
            return;

        localPlayerInsideCount++;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsLocalPlayerCollider(other))
            return;

        localPlayerInsideCount = Mathf.Max(0, localPlayerInsideCount - 1);
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
    private void SetClosedServerRpc(bool closed)
    {
        if (isClosedNet.Value != closed)
            isClosedNet.Value = closed;
    }

    private void OnClosedStateChanged(bool oldValue, bool newValue)
    {
        ApplyClosedState(newValue);
    }

    private void ApplyClosedState(bool closed)
    {
        if (targetAnimator != null)
            targetAnimator.SetBool("isClosed", closed);
    }
}
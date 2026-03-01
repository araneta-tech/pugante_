using UnityEngine;
using Unity.Netcode;

public class NetworkPushableObject : NetworkBehaviour
{
    [Header("Pseudo Rigidbody Settings")]
    public float mass = 1f;
    public float friction = 4f;
    public float pushForce = 8f;

    [Header("Interaction Settings")]
    public float interactionRange = 3f; // Distance at which "F" appears

    private Vector3 serverVelocity;
    private NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(
        writePerm: NetworkVariableWritePermission.Server);

    private NetworkVariable<Vector3> netVelocity = new NetworkVariable<Vector3>(
        writePerm: NetworkVariableWritePermission.Server);

    // Local floating F for each client
    private GameObject floatingFText;

    private void Start()
    {
        if (IsServer)
        {
            netPosition.Value = transform.position;
            netVelocity.Value = Vector3.zero;
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            ServerSimulatePhysics();
        }

        // Each client independently checks if a local player is near
        ShowFloatingFTextForLocalPlayer();
    }

    private void ServerSimulatePhysics()
    {
        transform.position += serverVelocity * Time.deltaTime;
        serverVelocity = Vector3.Lerp(serverVelocity, Vector3.zero, friction * Time.deltaTime);

        netPosition.Value = transform.position;
        netVelocity.Value = serverVelocity;
    }

    // ------------------------------
    // FLOATING "F" TEXT FOR LOCAL PLAYER NEARBY
    // ------------------------------
    void ShowFloatingFTextForLocalPlayer()
    {
        // Find the local player for this client
        PlayerMovement localPlayer = null;
        foreach (var player in FindObjectsOfType<PlayerMovement>())
        {
            if (player.IsOwner && player.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                localPlayer = player;
                break;
            }
        }

        if (localPlayer == null)
        {
            DestroyFloatingText();
            return;
        }

        float dist = Vector3.Distance(transform.position, localPlayer.transform.position);

        if (dist <= interactionRange)
        {
            if (floatingFText == null)
            {
                floatingFText = new GameObject("PressF_UI");
                var tm = floatingFText.AddComponent<TextMesh>();
                tm.text = "F";
                tm.fontSize = 64;
                tm.characterSize = 0.1f;
                tm.anchor = TextAnchor.MiddleCenter;
            }

            // Position the F above the object
            floatingFText.transform.position = transform.position + Vector3.up * 2f;
        }
        else
        {
            DestroyFloatingText();
        }
    }

    void DestroyFloatingText()
    {
        if (floatingFText != null)
        {
            Destroy(floatingFText);
            floatingFText = null;
        }
    }
}
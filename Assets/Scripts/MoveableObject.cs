using UnityEngine;
using Unity.Netcode;

public class MovableObject : NetworkBehaviour
{
    [Header("Interaction Settings")]
    public float interactionRange = 3f;
    public float holdDistance = 2.5f;
    public float holdHeight = 1.0f;

    [Header("Follow Settings")]
    public float followSpeed = 5f;

    private NetworkVariable<bool> isHeld = new NetworkVariable<bool>(false);
    private NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(0);

    private GameObject floatingF;
    private Rigidbody rb;
    private Collider objectCollider;
    private Collider playerCollider;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectCollider = GetComponent<Collider>();
        rb.isKinematic = true; // Always kinematic to prevent being pushed when not held
    }

    private void Update()
    {
        ShowFloatingF();
        if (IsOwner) CheckInput();
        if (IsServer && isHeld.Value) FollowPlayer();
    }

    private void CheckInput()
    {
        PlayerMovement player = GetLocalPlayer();
        if (player == null) return;

        if (player.SelectedCharacterIndex != 1) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= interactionRange && Input.GetKeyDown(KeyCode.F))
        {
            if (isHeld.Value) ReleaseObjectServerRpc(player.OwnerClientId);
            else ToggleHoldServerRpc(player.OwnerClientId);
        }
    }

    private PlayerMovement GetLocalPlayer()
    {
        foreach (var p in FindObjectsOfType<PlayerMovement>())
        {
            if (p.IsOwner && p.OwnerClientId == NetworkManager.Singleton.LocalClientId)
                return p;
        }
        return null;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleHoldServerRpc(ulong playerId)
    {
        var playerObj = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
        if (playerObj == null) return;

        PlayerMovement player = playerObj.GetComponent<PlayerMovement>();
        playerCollider = playerObj.GetComponent<Collider>();

        if (!isHeld.Value)
        {
            isHeld.Value = true;
            holderId.Value = playerId;

            rb.isKinematic = false; // Enable physics when object is held
            if (playerCollider != null && objectCollider != null)
                Physics.IgnoreCollision(playerCollider, objectCollider, true); // Ignore collisions

            player.SetSpeedMultiplier(0.5f); // Slow down player when holding
        }
        else
        {
            ReleaseObjectServerRpc(playerId); // Release object if already held
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReleaseObjectServerRpc(ulong playerId)
    {
        isHeld.Value = false;
        holderId.Value = 0;

        rb.isKinematic = true; // Keep object kinematic when released
        if (playerCollider != null && objectCollider != null)
            Physics.IgnoreCollision(playerCollider, objectCollider, false); // Restore collision

        var playerObj = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
        if (playerObj != null)
        {
            PlayerMovement player = playerObj.GetComponent<PlayerMovement>();
            player.RestoreSpeed(); // Restore normal player speed
        }
    }

    private void FollowPlayer()
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(holderId.Value)) return;

        var playerObj = NetworkManager.Singleton.ConnectedClients[holderId.Value].PlayerObject;
        if (playerObj == null) return;

        PlayerMovement player = playerObj.GetComponent<PlayerMovement>();
        if (player == null) return;

        Vector3 targetPosition = player.transform.position + player.transform.forward * holdDistance + Vector3.up * holdHeight;
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
    }

    private void ShowFloatingF()
    {
        PlayerMovement player = GetLocalPlayer();
        if (player == null || player.SelectedCharacterIndex != 1)
        {
            DestroyFloatingF();
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        if (dist <= interactionRange)
        {
            if (floatingF == null)
            {
                floatingF = new GameObject("PressF");
                TextMesh text = floatingF.AddComponent<TextMesh>();
                text.fontSize = 50;
                text.characterSize = 0.2f;
                text.anchor = TextAnchor.MiddleCenter;

                floatingF.AddComponent<FaceCamera>();
            }

            floatingF.GetComponent<TextMesh>().text = isHeld.Value ? "[F] Release" : "[F] Grab";
            floatingF.transform.position = transform.position + Vector3.up * 2f;
            floatingF.SetActive(true);
        }
        else
        {
            DestroyFloatingF();
        }
    }

    private void DestroyFloatingF()
    {
        if (floatingF != null)
        {
            floatingF.SetActive(false);
        }
    }
}

public class FaceCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;
    }
}
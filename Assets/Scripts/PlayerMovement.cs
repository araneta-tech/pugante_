using UnityEngine;
using Unity.Netcode;
using Cinemachine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings (Shared)")]
    public float speed = 5f;
    public float jumpForce = 5f;

    [Header("Host Movement Settings")]
    public float hostSpeed = 6f;
    public float hostJumpForce = 6f;

    [Header("Client Movement Settings")]
    public float clientSpeed = 4f;
    public float clientJumpForce = 4f;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 lastInput = Vector3.zero;

    [Header("Model Prefabs")]
    public GameObject hostModelPrefab;
    public GameObject clientModelPrefab;

    private GameObject spawnedModel;

    // ----- Jump Improvements -----
    private float coyoteTime = 0.15f;
    private float coyoteCounter = 0f;
    private float groundCheckDistance = 0.3f;
    // ------------------------------

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = !IsServer;

        ApplyMovementOverrides();
        SpawnCorrectModelForThisPlayer();

        if (IsOwner)
            AssignCamera();
    }

    void ApplyMovementOverrides()
    {
        if (OwnerClientId == NetworkManager.ServerClientId)
        {
            speed = hostSpeed;
            jumpForce = hostJumpForce;
        }
        else
        {
            speed = clientSpeed;
            jumpForce = clientJumpForce;
        }
    }

    void SpawnCorrectModelForThisPlayer()
    {
        GameObject prefabToSpawn = (OwnerClientId == NetworkManager.ServerClientId)
            ? hostModelPrefab
            : clientModelPrefab;

        if (prefabToSpawn != null)
        {
            spawnedModel = Instantiate(prefabToSpawn, transform);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
            spawnedModel.transform.localScale = Vector3.one;

            foreach (var col in spawnedModel.GetComponentsInChildren<Collider>())
                Destroy(col);
        }
    }

    void AssignCamera()
    {
        var vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.Follow = transform;
            vcam.LookAt = transform;
        }
    }

    void FixedUpdate()
    {
        UpdateGroundStatus();

        if (IsOwner)
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            Vector3 input = new Vector3(x, 0f, z);
            SendInputServerRpc(input);

            if (Input.GetKeyDown(KeyCode.K) && coyoteCounter > 0f)
                JumpServerRpc();
        }

        if (IsServer)
        {
            // ------------ ONLY CHANGE YOU ASKED FOR ------------
            float appliedSpeed = isGrounded ? speed : speed * 0.5f;
            Vector3 move = lastInput.normalized * appliedSpeed;
            // -----------------------------------------------------

            rb.velocity = new Vector3(move.x, rb.velocity.y, move.z);
            RotateTowards(lastInput);
        }
    }

    [ServerRpc]
    void SendInputServerRpc(Vector3 input)
    {
        lastInput = input;
    }

    [ServerRpc]
    void JumpServerRpc()
    {
        if (coyoteCounter > 0f)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
            coyoteCounter = 0f;
        }
    }

    void RotateTowards(Vector3 movement)
    {
        if (movement.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.15f);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    void UpdateGroundStatus()
    {
        bool rayHit = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);

        if (rayHit || isGrounded)
        {
            isGrounded = true;
            coyoteCounter = coyoteTime;
        }
        else
        {
            isGrounded = false;
            coyoteCounter -= Time.fixedDeltaTime;
        }
    }
}

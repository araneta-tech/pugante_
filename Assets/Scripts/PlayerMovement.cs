using UnityEngine;
using Unity.Netcode;
using Cinemachine;
using System.Collections.Generic;

[System.Serializable]
public class CharacterConfig
{
    public GameObject prefab;
    public float speed = 5f;
    public float jumpForce = 5f;
}

public class PlayerMovement : NetworkBehaviour
{
    [Header("Character Options")]
    public List<CharacterConfig> characterConfigs;

    [Header("Default Character Index")]
    public int defaultCharacterIndex = 0;

    [Header("Distance Check Settings")]
    public float warningDistance = 15f;
    public float limitDistance = 20f;
    public float outOfRangeDuration = 5f;

    private static float outOfRangeTimer = 0f;
    private static bool timerActive = false;

    private GameObject spawnedModel;
    private Animator animator;
    private Rigidbody rb;

    private bool isGrounded;
    private Vector3 lastInput;

    private NetworkVariable<int> selectedCharacterIndex = new NetworkVariable<int>();
    private NetworkVariable<bool> isWalkingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private NetworkVariable<bool> groundedNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float speed;
    private float jumpForce;

    [Header("Item Collection")]
    public float collectRange = 2f;
    private CollectibleItem currentItem;

    private bool interactingWithObject = false;

    public Vector3 LastInput => lastInput;
    public int SelectedCharacterIndex => selectedCharacterIndex.Value;

    public void SetInteractingWithObject(bool state) => interactingWithObject = state;

    // -----------------------------------------------------
    // NETWORK INITIALIZATION
    // -----------------------------------------------------
    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = !IsServer;

        if (!IsServer)
            rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (IsServer && characterConfigs.Count > 0)
            selectedCharacterIndex.Value = defaultCharacterIndex;

        ApplyCharacterConfig(selectedCharacterIndex.Value);
        SpawnSelectedModel();

        selectedCharacterIndex.OnValueChanged += (oldValue, newValue) =>
        {
            ApplyCharacterConfig(newValue);
            SpawnSelectedModel();
        };

        isWalkingNet.OnValueChanged += (oldValue, newValue) => ApplyAnimationState(newValue);

        if (IsOwner)
            AssignCamera();
    }

    // -----------------------------------------------------
    // CHARACTER SELECTION
    // -----------------------------------------------------
    public void SelectCharacter(int index)
    {
        if (!IsOwner) return;
        SelectCharacterServerRpc(index);
    }

    [ServerRpc]
    void SelectCharacterServerRpc(int index)
    {
        if (index >= 0 && index < characterConfigs.Count)
            selectedCharacterIndex.Value = index;
    }

    void ApplyCharacterConfig(int index)
    {
        if (index < 0 || index >= characterConfigs.Count) return;

        speed = characterConfigs[index].speed;
        jumpForce = characterConfigs[index].jumpForce;
    }

    // -----------------------------------------------------
    // CHARACTER MODEL HANDLING
    // -----------------------------------------------------
    void SpawnSelectedModel()
    {
        if (spawnedModel != null)
            Destroy(spawnedModel);

        int idx = selectedCharacterIndex.Value;
        if (idx < 0 || idx >= characterConfigs.Count) return;

        var prefab = characterConfigs[idx].prefab;
        if (prefab != null)
        {
            spawnedModel = Instantiate(prefab, transform);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;

            foreach (var c in spawnedModel.GetComponentsInChildren<Collider>())
                Destroy(c);

            animator = spawnedModel.GetComponentInChildren<Animator>();
            ApplyAnimationState(false);
        }
    }

    void AssignCamera()
    {
        var cam = FindObjectOfType<CinemachineVirtualCamera>();
        if (cam != null)
        {
            cam.Follow = transform;
            cam.LookAt = transform;
        }
    }

    // -----------------------------------------------------
    // FIXED UPDATE — MOVEMENT + INPUT
    // -----------------------------------------------------
    void FixedUpdate()
    {
        if (!IsSpawned) return;

        if (IsServer)
            ServerMovement();

        if (IsOwner)
            HandleInput();
    }

    void ServerMovement()
    {
        float speedMultiplier = interactingWithObject ? 0.5f : 1f;
        float appliedSpeed = isGrounded ? speed * speedMultiplier : speed * 0.5f * speedMultiplier;

        Vector3 move = lastInput.normalized * appliedSpeed;
        rb.velocity = new Vector3(move.x, rb.velocity.y, move.z);

        RotateTowards(lastInput);

        bool walking = lastInput.magnitude > 0.01f;
        if (isWalkingNet.Value != walking)
            isWalkingNet.Value = walking;

        CheckPlayersDistance();
    }

    void HandleInput()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 input = new Vector3(x, 0f, z);
        SendInputServerRpc(input);

        ApplyAnimationState(input.magnitude > 0.01f);

        // Jump
        if (Input.GetKeyDown(KeyCode.K))
            JumpServerRpc();

        // Detect nearest item
        DetectItem();
    }

    // -----------------------------------------------------
    // ITEM DETECTION + COLLECTION
    // -----------------------------------------------------
    void DetectItem()
    {
        CollectibleItem nearest = null;
        float nearestDist = collectRange + 1f;

        foreach (var item in CollectibleItem.ActiveItems)
        {
            float d = Vector3.Distance(transform.position, item.transform.position);
            if (d < collectRange && d < nearestDist)
            {
                nearest = item;
                nearestDist = d;
            }
        }

        currentItem = nearest;

        if (currentItem != null && Input.GetKeyDown(KeyCode.F))
        {
            currentItem.CollectServerRpc();
        }
    }

    // -----------------------------------------------------
    // NETWORKED SERVER RPCS
    // -----------------------------------------------------
    [ServerRpc]
    void SendInputServerRpc(Vector3 input) => lastInput = input;

    [ServerRpc]
    void JumpServerRpc()
    {
        float groundCheckDistance = 0.2f;
        float groundOffset = 0.9f;

        Vector3 rayOrigin = transform.position + Vector3.down * groundOffset;
        bool canJump = Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance);

        Debug.DrawRay(rayOrigin, Vector3.down * groundCheckDistance, canJump ? Color.green : Color.red, 1f);

        if (!canJump) return;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    // -----------------------------------------------------
    // ROTATION
    // -----------------------------------------------------
    void RotateTowards(Vector3 movement)
    {
        if (movement.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 0.15f);
        }
    }

    // -----------------------------------------------------
    // DISTANCE CHECK BETWEEN PLAYERS
    // -----------------------------------------------------
    void CheckPlayersDistance()
    {
        var players = FindObjectsOfType<PlayerMovement>();
        if (players.Length < 2)
        {
            outOfRangeTimer = 0f;
            timerActive = false;
            return;
        }

        PlayerMovement p1 = players[0];
        PlayerMovement p2 = players[1];

        float distance = Vector3.Distance(p1.transform.position, p2.transform.position);

        if (distance > warningDistance && distance <= limitDistance)
        {
            outOfRangeTimer = 0f;
            timerActive = false;
            return;
        }

        if (distance > limitDistance)
        {
            if (!timerActive)
            {
                timerActive = true;
                outOfRangeTimer = 0f;
            }
            else
            {
                outOfRangeTimer += Time.fixedDeltaTime;

                if (outOfRangeTimer >= outOfRangeDuration)
                {
                    foreach (var p in players)
                    {
                        NetworkObject n = p.GetComponent<NetworkObject>();
                        if (n && n.IsSpawned)
                        {
                            n.Despawn(false);
                            Destroy(n.gameObject);
                        }
                    }

                    timerActive = false;
                    outOfRangeTimer = 0f;
                }
            }
            return;
        }

        timerActive = false;
        outOfRangeTimer = 0f;
    }

    // -----------------------------------------------------
    // ANIMATIONS
    // -----------------------------------------------------
    void ApplyAnimationState(bool walking)
    {
        if (animator != null)
            animator.SetBool("isWalking", walking);
    }
}
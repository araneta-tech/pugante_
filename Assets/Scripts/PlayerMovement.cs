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

    // --- Distance Check Settings ---
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
    private bool lastGroundedState = false; // for debug logs
    private Vector3 lastInput;

    // Network synced vars
    private NetworkVariable<int> selectedCharacterIndex = new NetworkVariable<int>();
    private NetworkVariable<bool> isWalkingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float speed;
    private float jumpForce;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = !IsServer;

        if (!IsServer)
            rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (IsServer && selectedCharacterIndex.Value == 0 && characterConfigs.Count > 0)
            selectedCharacterIndex.Value = defaultCharacterIndex;

        ApplyCharacterConfig(selectedCharacterIndex.Value);
        SpawnSelectedModel();

        selectedCharacterIndex.OnValueChanged += (oldValue, newValue) =>
        {
            ApplyCharacterConfig(newValue);
            SpawnSelectedModel();
        };

        isWalkingNet.OnValueChanged += (oldValue, newValue) =>
        {
            ApplyAnimationState(newValue);
        };

        if (IsOwner)
            AssignCamera();
    }

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
            ApplyAnimationState(false); // idle on spawn
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

    void FixedUpdate()
    {
        if (IsServer)
        {
            // Movement slower in air
            float appliedSpeed = isGrounded ? speed : speed * 0.5f;
            Vector3 move = lastInput.normalized * appliedSpeed;

            rb.velocity = new Vector3(move.x, rb.velocity.y, move.z);

            RotateTowards(lastInput);

            bool walking = lastInput.magnitude > 0.01f;
            if (isWalkingNet.Value != walking)
                isWalkingNet.Value = walking;

            // --- Distance Check Logic ---
            CheckPlayersDistance();
        }

        if (IsOwner)
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            Vector3 input = new Vector3(x, 0f, z);
            SendInputServerRpc(input);

            ApplyAnimationState(input.magnitude > 0.01f);

            // Jump input (grounded required)
            if (Input.GetKeyDown(KeyCode.K) && isGrounded)
                JumpServerRpc();
        }
    }

    // --- Distance Calculation and Handling ---
    void CheckPlayersDistance()
    {
        // Only run on server
        var players = FindObjectsOfType<PlayerMovement>();
        if (players.Length != 2)
        {
            // Only process if exactly 2 players
            outOfRangeTimer = 0f;
            timerActive = false;
            return;
        }

        float distance = Vector3.Distance(players[0].transform.position, players[1].transform.position);

        if (distance > limitDistance)
        {
            if (!timerActive)
            {
                Debug.Log($"[DistanceCheck] Players exceeded limit distance ({distance:F2} > {limitDistance}). Starting timer.");
                timerActive = true;
                outOfRangeTimer = 0f;
            }
            else
            {
                outOfRangeTimer += Time.fixedDeltaTime;
                Debug.Log($"[DistanceCheck] Players still out of range for {outOfRangeTimer:F2} seconds.");
                if (outOfRangeTimer >= outOfRangeDuration)
                {
                    Debug.Log("[DistanceCheck] BOOOOM Players failed to get within range..");
                    foreach (var p in players)
                    {
                        NetworkObject netObj = p.GetComponent<NetworkObject>();
                        if (netObj != null)
                            netObj.Despawn(true); // true = destroy
                        else
                            Destroy(p.gameObject);
                    }
                    timerActive = false;
                    outOfRangeTimer = 0f;
                }
            }
        }
        else if (distance > warningDistance)
        {
            Debug.Log($"[DistanceCheck] Warning: Players are far apart! Distance: {distance:F2}");
            timerActive = false;
            outOfRangeTimer = 0f;
        }
        else
        {
            timerActive = false;
            outOfRangeTimer = 0f;
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
        if (isGrounded)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            isGrounded = false;
        }
    }

    void RotateTowards(Vector3 movement)
    {
        if (movement.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 0.15f);
        }
    }

    // -----------------------------
    // COLLIDER-BASED GROUND DETECTION
    // -----------------------------
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ground"))
            SetGrounded(true);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ground"))
            SetGrounded(false);
    }

    private void SetGrounded(bool grounded)
    {
        isGrounded = grounded;

        // Debug log only when state changes
        if (isGrounded != lastGroundedState)
        {
            Debug.Log(isGrounded ? "GROUND DETECTED" : "LEFT GROUND");
            lastGroundedState = isGrounded;
        }
    }

    void ApplyAnimationState(bool walking)
    {
        if (animator != null)
            animator.SetBool("isWalking", walking);
    }
}

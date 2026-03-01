using UnityEngine;
using Unity.Netcode;
using Cinemachine;
using System.Collections.Generic;

//
// ---------------------------------------------------------
// CHARACTER CONFIG DATA
// ---------------------------------------------------------
[System.Serializable]
public class CharacterConfig
{
    public GameObject prefab;
    public float speed = 5f;
    public float jumpForce = 5f;
}

//
// ---------------------------------------------------------
// PLAYER MOVEMENT CONTROLLER
// ---------------------------------------------------------
public class PlayerMovement : NetworkBehaviour
{
    // -----------------------------------------------------
    // CHARACTER OPTIONS
    // -----------------------------------------------------
    [Header("Character Options")]
    public List<CharacterConfig> characterConfigs;

    [Header("Default Character Index")]
    public int defaultCharacterIndex = 0;

    // -----------------------------------------------------
    // DISTANCE CHECK SYSTEM
    // -----------------------------------------------------
    [Header("Distance Check Settings")]
    public float warningDistance = 15f;
    public float limitDistance = 20f;
    public float outOfRangeDuration = 5f;

    private static float outOfRangeTimer = 0f;
    private static bool timerActive = false;

    // -----------------------------------------------------
    // INTERNAL COMPONENTS
    // -----------------------------------------------------
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

    // -----------------------------------------------------
    // ITEM COLLECTION SYSTEM
    // -----------------------------------------------------
    [Header("Item Collection")]
    public float collectRange = 2f;
    private CollectibleItem currentItem;
    private GameObject floatingFText;

    // -----------------------------------------------------
    // MOVABLE OBJECT INTERACTION (NO MOVEMENT INCLUDED)
    // -----------------------------------------------------
    private bool interactingWithObject = false;
    public Vector3 LastInput => lastInput;
    public int SelectedCharacterIndex => selectedCharacterIndex.Value;

    public void SetInteractingWithObject(bool state)
    {
        interactingWithObject = state;
    }

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

        isWalkingNet.OnValueChanged += (oldValue, newValue) =>
        {
            ApplyAnimationState(newValue);
        };

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
    // FIXED UPDATE — MOVEMENT HANDLING
    // -----------------------------------------------------
    void FixedUpdate()
    {
        if (!IsSpawned) return;

        //
        // SERVER-SIDE MOVEMENT
        //
        if (IsServer)
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

        //
        // CLIENT INPUT
        //
        if (IsOwner)
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            Vector3 input = new Vector3(x, 0f, z);
            SendInputServerRpc(input);

            ApplyAnimationState(input.magnitude > 0.01f);

            //
            // JUMP WITH RAYCAST
            //
            if (Input.GetKeyDown(KeyCode.K))
            {
                float groundCheckDistance = 0.2f;
                float groundOffset = 0.9f;
                Vector3 rayOrigin = transform.position + Vector3.down * groundOffset;

                bool canJump = Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance);
                Debug.DrawRay(rayOrigin, Vector3.down * groundCheckDistance, canJump ? Color.green : Color.red, 1f);

                if (canJump)
                    JumpServerRpc();
            }

            //
            // ITEM HANDLING
            //
            DetectItem();
            if (currentItem != null && Input.GetKeyDown(KeyCode.F))
                CollectItemServerRpc(currentItem.NetworkObject);
        }
    }

    // -----------------------------------------------------
    // ITEM DETECTION + UI
    // -----------------------------------------------------
    void DetectItem()
    {
        CollectibleItem nearest = null;
        float nearestDist = collectRange + 1;

        var allItems = FindObjectsOfType<CollectibleItem>();

        foreach (var item in allItems)
        {
            float d = Vector3.Distance(transform.position, item.transform.position);
            if (d < collectRange && d < nearestDist)
            {
                nearest = item;
                nearestDist = d;
            }
        }

        if (nearest == null && floatingFText != null)
        {
            Destroy(floatingFText);
            floatingFText = null;
        }

        if (nearest != currentItem)
        {
            currentItem = nearest;
            UpdateFloatingUIText();
        }
    }

    void UpdateFloatingUIText()
    {
        if (floatingFText != null)
            Destroy(floatingFText);

        if (currentItem == null) return;

        floatingFText = new GameObject("PressF_UI");
        var tm = floatingFText.AddComponent<TextMesh>();

        tm.text = "F";
        tm.fontSize = 64;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;

        floatingFText.transform.position = currentItem.transform.position + Vector3.up * 2f;
    }

    // -----------------------------------------------------
    // ITEM COLLECTION (SERVER SIDE)
    // -----------------------------------------------------
    [ServerRpc]
    void CollectItemServerRpc(NetworkObjectReference itemRef)
    {
        if (!itemRef.TryGet(out NetworkObject obj)) return;

        Debug.Log("[ITEM] Collected!");

        if (floatingFText != null)
        {
            Destroy(floatingFText);
            floatingFText = null;
        }

        currentItem = null;
        obj.Despawn();
        Destroy(obj.gameObject);
    }

    // -----------------------------------------------------
    // EXISTING CORE FUNCTIONS (UNCHANGED)
    // -----------------------------------------------------
    [ServerRpc]
    void SendInputServerRpc(Vector3 input)
    {
        lastInput = input;
    }

    [ServerRpc]
    void JumpServerRpc()
    {
        float groundCheckDistance = 0.2f;
        float groundOffset = 0.9f;

        Vector3 rayOrigin = transform.position + Vector3.down * groundOffset;
        bool canJump = Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance);

        Debug.DrawRay(rayOrigin, Vector3.down * groundCheckDistance, canJump ? Color.green : Color.red, 1f);
        Debug.Log($"[Jump] Can jump: {canJump}");

        if (!canJump) return;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    void RotateTowards(Vector3 movement)
    {
        if (movement.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 0.15f);
        }
    }

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
            Debug.Log($"[DistanceCheck] Warning! ({distance:F2}m)");
            outOfRangeTimer = 0f;
            timerActive = false;
            return;
        }

        if (distance > limitDistance)
        {
            if (!timerActive)
            {
                Debug.Log($"[DistanceCheck] Limit exceeded ({distance:F2}m). Timer starting...");
                timerActive = true;
                outOfRangeTimer = 0f;
            }
            else
            {
                outOfRangeTimer += Time.fixedDeltaTime;
                Debug.Log($"[DistanceCheck] Out of range for {outOfRangeTimer:F2}/{outOfRangeDuration}");

                if (outOfRangeTimer >= outOfRangeDuration)
                {
                    Debug.Log("[DistanceCheck] DESPAWNING both players");

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

    void ApplyAnimationState(bool walking)
    {
        if (animator != null)
            animator.SetBool("isWalking", walking);
    }
}
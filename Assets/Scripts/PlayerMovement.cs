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

    [Header("Footstep Audio")]
    public AudioSource footstepAudioSource;
    public List<AudioClip> footstepClips;
    public float footstepInterval = 0.5f;

    private float footstepTimer = 0f;

    [Header("Distance Check Settings")]
    public float warningDistance = 15f;
    public float limitDistance = 20f;
    public float outOfRangeDuration = 5f;

    [Header("Explosion Sound")]
    public AudioSource explosionAudioSource;
    public AudioClip explosionSound;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.3f;
    public LayerMask groundLayer;

    [Header("Item Collection")]
    public float collectRange = 2f;

    private static float outOfRangeTimer = 0f;
    private static bool timerActive = false;

    private static List<PlayerMovement> players = new List<PlayerMovement>();

    private GameObject spawnedModel;
    private Animator animator;
    private Rigidbody rb;

    private bool isGrounded;
    private bool isTouchingGroundTag;
    private Vector3 lastInput;

    private bool jumpRequested = false;

    private NetworkVariable<int> selectedCharacterIndex = new NetworkVariable<int>();
    private NetworkVariable<bool> isWalkingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float speed;
    private float jumpForce;

    private CollectibleItem currentItem;
    private bool interactingWithObject = false;

    public Vector3 LastInput => lastInput;
    public int SelectedCharacterIndex => selectedCharacterIndex.Value;

    public void SetInteractingWithObject(bool state) => interactingWithObject = state;

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

        players.Add(this);

        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        players.Remove(this);

        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayer(this);
        }
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

        GameObject prefab = characterConfigs[idx].prefab;
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

    void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        if (Input.GetKeyDown(KeyCode.K))
            RequestJumpServerRpc();

        HandleFootsteps();
    }

    void FixedUpdate()
    {
        if (!IsSpawned) return;

        UpdateGroundCheck();

        if (IsServer)
        {
            ServerMovement();

            if (jumpRequested && (isGrounded || isTouchingGroundTag))
            {
                jumpRequested = false;

                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }

        if (IsOwner)
            HandleInput();
    }

    void UpdateGroundCheck()
    {
        if (groundCheck == null) return;

        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundRadius,
            groundLayer
        );
    }

    void HandleInput()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 input = new Vector3(x, 0f, z);
        SendInputServerRpc(input);

        DetectItem();
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestJumpServerRpc()
    {
        jumpRequested = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isTouchingGroundTag = true;
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isTouchingGroundTag = true;
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isTouchingGroundTag = false;
    }

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
            currentItem.CollectServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void SendInputServerRpc(Vector3 input)
    {
        lastInput = input;
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

    void RotateTowards(Vector3 movement)
    {
        if (movement.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 0.15f);
        }
    }

    void HandleFootsteps()
    {
        if (!IsOwner) return; 

        bool isWalking = isWalkingNet.Value && (isGrounded || isTouchingGroundTag);

        if (isWalking)
        {
            footstepTimer -= Time.deltaTime;

            if (footstepTimer <= 0f)
            {
                PlayFootstep();
                footstepTimer = footstepInterval / Mathf.Clamp(lastInput.magnitude, 0.5f, 1f);
            }
        }
        else
        {
            footstepTimer = 0f;
        }
    }

    void PlayFootstep()
    {
        if (footstepAudioSource == null || footstepClips.Count == 0) return;

        int index = Random.Range(0, footstepClips.Count);
        footstepAudioSource.PlayOneShot(footstepClips[index]);
    }

    void CheckPlayersDistance()
    {
        if (players.Count < 2)
        {
            outOfRangeTimer = 0;
            timerActive = false;
            return;
        }

        float distance = Vector3.Distance(players[0].transform.position, players[1].transform.position);

        if (distance > warningDistance && distance <= limitDistance)
        {
            outOfRangeTimer = 0;
            timerActive = false;
            return;
        }

        if (distance > limitDistance)
        {
            if (!timerActive)
            {
                timerActive = true;
                outOfRangeTimer = 0;
            }
            else
            {
                outOfRangeTimer += Time.fixedDeltaTime;

                if (outOfRangeTimer >= outOfRangeDuration)
                {
                    if (explosionAudioSource != null && explosionSound != null)
                    {
                        explosionAudioSource.PlayOneShot(explosionSound);
                    }

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
                    outOfRangeTimer = 0;
                }
            }
            return;
        }

        timerActive = false;
        outOfRangeTimer = 0;
    }

    void ApplyAnimationState(bool walking)
    {
        if (animator != null)
            animator.SetBool("isWalking", walking);
    }

    public void SelectCharacter(int index)
    {
        if (!IsOwner) return;
        SelectCharacterServerRpc(index);
    }

    [ServerRpc(RequireOwnership = false)]
    void SelectCharacterServerRpc(int index)
    {
        if (index >= 0 && index < characterConfigs.Count)
            selectedCharacterIndex.Value = index;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speed *= multiplier;
    }

    public void RestoreSpeed()
    {
        speed = characterConfigs[selectedCharacterIndex.Value].speed;
    }

    public void RespawnAtCheckpoint(Vector3 position)
    {
        if (!IsServer || !IsSpawned) return;

        lastInput = Vector3.zero;
        jumpRequested = false;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); 
        }

        transform.position = position;

        if (rb != null)
        {
            rb.WakeUp();
        }

        animator?.SetBool("isWalking", false);

        Debug.Log($"[PlayerMovement] Respawned at {position}");
    }
}
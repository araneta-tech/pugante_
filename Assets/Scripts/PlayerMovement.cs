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

    [Header("Fail / Return Settings")]
    public float failUiHoldSeconds = 5f;
    public string menuSceneName = "HostClientMenu";

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.3f;
    public LayerMask groundLayer;

    [Header("Item Collection")]
    public float collectRange = 2f;

    [Header("3rd Person Camera Settings")]
    public float mouseSensitivity = 2f;
    public float cameraDistance = 4f;
    public float cameraHeight = 2f;

    private float yaw;
    private float pitch = 15f;

    private static float outOfRangeTimer = 0f;
    private static bool timerActive = false;
    private static bool failSequenceTriggered = false;

    private static readonly List<PlayerMovement> players = new List<PlayerMovement>();

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

    private CinemachineVirtualCamera virtualCam;

    public Vector3 LastInput => lastInput;
    public int SelectedCharacterIndex => selectedCharacterIndex.Value;

    public static event System.Action<PlayerMovement> OnPlayerDespawned;

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

        if (!players.Contains(this))
            players.Add(this);

        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);
        }

        if (IsServer)
        {
            if (GameManager.Instance != null)
            {
                Vector3 spawnPos = GameManager.Instance.GetChapterStartPosition();

                transform.position = spawnPos;

                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                }

                SetSpawnPositionClientRpc(spawnPos);
            }
        }
    }

    [ClientRpc]
    void SetSpawnPositionClientRpc(Vector3 position)
    {
        transform.position = position;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }

    public void SpawnAtPosition(Vector3 position)
    {
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        transform.position = position;

        SpawnAtPositionClientRpc(position);
    }

    [ClientRpc]
    private void SpawnAtPositionClientRpc(Vector3 position)
    {
        if (!IsOwner)
        {
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }

            transform.position = position;
        }
    }

    public override void OnNetworkDespawn()
    {
        players.Remove(this);

        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayer(this);
        }

        if (IsServer && OnPlayerDespawned != null)
        {
            OnPlayerDespawned(this);
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
        virtualCam = FindObjectOfType<CinemachineVirtualCamera>();
        if (virtualCam != null)
        {
            virtualCam.Follow = transform;
            virtualCam.LookAt = transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        HandleCameraRotation();

        if (Input.GetKeyDown(KeyCode.K))
            RequestJumpServerRpc();
    }

    void HandleCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -30f, 60f);

        if (virtualCam != null)
        {
            virtualCam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    void HandleCameraZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && virtualCam != null)
        {
            var transposer = virtualCam.GetCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
            if (transposer != null)
            {
                transposer.m_CameraDistance = Mathf.Clamp(transposer.m_CameraDistance - scroll, 2f, 6f);
            }
        }
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

        Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Camera.main.transform.right;

        Vector3 input = camForward * z + camRight * x;

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

    void CheckPlayersDistance()
    {
        if (!IsServer || failSequenceTriggered)
            return;

        List<PlayerMovement> serverPlayers = GetServerPlayers();

        if (serverPlayers.Count < 2)
        {
            ResetDistanceTimer();
            return;
        }

        float maxPlayerDistance = GetMaxPlayerDistance(serverPlayers);

        if (maxPlayerDistance <= warningDistance)
        {
            ResetDistanceTimer();
            return;
        }

        if (maxPlayerDistance > limitDistance)
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
                    BeginFailSequence();
                }
            }
        }
        else
        {
            ResetDistanceTimer();
        }
    }

    List<PlayerMovement> GetServerPlayers()
    {
        List<PlayerMovement> result = new List<PlayerMovement>();

        if (NetworkManager.Singleton == null)
            return result;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent(out PlayerMovement pm))
            {
                result.Add(pm);
            }
        }

        return result;
    }

    float GetMaxPlayerDistance(List<PlayerMovement> serverPlayers)
    {
        float maxDistance = 0f;

        for (int i = 0; i < serverPlayers.Count; i++)
        {
            for (int j = i + 1; j < serverPlayers.Count; j++)
            {
                float d = Vector3.Distance(
                    serverPlayers[i].transform.position,
                    serverPlayers[j].transform.position
                );

                if (d > maxDistance)
                    maxDistance = d;
            }
        }

        return maxDistance;
    }

    void ResetDistanceTimer()
    {
        outOfRangeTimer = 0f;
        timerActive = false;
    }

    void BeginFailSequence()
    {
        if (failSequenceTriggered)
            return;

        failSequenceTriggered = true;
        ResetDistanceTimer();

        DespawnAllPlayerObjects();

        if (NetworkUI.Instance != null)
        {
            NetworkUI.Instance.StartFailUISequence(5f);
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] NetworkUI.Instance is missing.");
        }
    }

    void DespawnAllPlayerObjects()
    {
        if (!IsServer || NetworkManager.Singleton == null)
            return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.IsSpawned)
            {
                client.PlayerObject.Despawn(true);
            }
        }
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

    public override void OnDestroy()
    {
        if (players.Contains(this))
            players.Remove(this);

        base.OnDestroy();
    }
}
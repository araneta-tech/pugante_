using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;

[System.Serializable]
public class CharacterConfig
{
    public GameObject prefab;
    public float speed = 5f;
    public float jumpForce = 5f;
}

public class PlayerMovement : NetworkBehaviour
{
    private const int LifeStateAlive = 0;
    private const int LifeStateBusted = 1;
    private const int LifeStateFailed = 2;

    [Header("Character Options")]
    public List<CharacterConfig> characterConfigs;

    [Header("Default Character Index")]
    public int defaultCharacterIndex = 0;

    [Header("Distance Check Settings")]
    public float warningDistance = 15f;
    public float limitDistance = 20f;
    public float outOfRangeDuration = 5f;

    [Header("Fail / Return Settings")]
    public float failUiHoldSeconds = 10f;
    public string menuSceneName = "HostClientMenu";

    [Header("Fail VFX")]
    public GameObject vfxExplosionPrefab;
    public float vfxDelayBeforeFail = 1.0f;

    [Header("Health Settings")]
    public int maxHealth = 100;

    [Header("Revive Settings")]
    public float reviveRange = 2f;
    public float reviveHoldSeconds = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.3f;
    public LayerMask groundLayer;

    [Header("Jump Settings")]
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.12f;
    public float jumpCooldown = 0.05f;

    [Header("Item Collection")]
    public float collectRange = 2f;

    [Header("3rd Person Camera Settings")]
    public float mouseSensitivity = 2f;
    public float cameraDistance = 4f;
    public float cameraHeight = 2f;

    [Header("Footstep Audio")]
    public AudioClip[] footstepClipsIndex0;
    public AudioClip[] footstepClipsIndex1;

    public float footstepInterval = 0.5f;
    public float footstepVolume = 1f;

    [Header("Distance Warning Audio")]
    public AudioClip distanceLoopClip;

    [Tooltip("Minimum volume when warning starts")]
    public float minVolume = 0.05f;

    [Tooltip("Maximum volume at max distance")]
    public float maxVolume = 1f;

    [Tooltip("Optional pitch scaling")]
    public float minPitch = 0.8f;
    public float maxPitch = 1.3f;

    private float yaw;
    private float pitch = 15f;

    private static float outOfRangeTimer = 0f;
    private static bool timerActive = false;
    private static bool failSequenceTriggered = false;
    private static bool titleReturnSequenceTriggered = false;
    private float smoothedDistance = 0f;
    private AudioSource distanceAudioSource;

    private static readonly List<PlayerMovement> players = new List<PlayerMovement>();

    private GameObject spawnedModel;
    private Animator animator;
    private Rigidbody rb;

    private bool isGrounded;
    private bool isTouchingGroundTag;
    private Vector3 lastInput;
    private AudioSource footstepSource;
    private float footstepTimer = 0f;

    private bool jumpRequested = false;
    private float jumpBufferTimer = 0f;
    private float coyoteTimer = 0f;
    private float jumpCooldownTimer = 0f;

    private NetworkVariable<int> selectedCharacterIndex = new NetworkVariable<int>();

    private NetworkVariable<bool> isWalkingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isJumpingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> currentHealthNet = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> lifeStateNet = new NetworkVariable<int>(
        LifeStateAlive,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> distanceWarningNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> distanceNormalizedNet = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float speed;
    private float jumpForce;

    private CollectibleItem currentItem;
    private bool interactingWithObject = false;

    private CinemachineVirtualCamera virtualCam;

    private PlayerMovement reviveTarget;
    private float reviveHoldTimer = 0f;

    public Vector3 LastInput => lastInput;
    public int SelectedCharacterIndex => selectedCharacterIndex.Value;
    public int CurrentHealth => currentHealthNet.Value;
    public bool IsBusted => lifeStateNet.Value == LifeStateBusted;
    public bool IsFailed => lifeStateNet.Value == LifeStateFailed;
    public bool IsDead => lifeStateNet.Value != LifeStateAlive;
    public bool IsDistanceWarning => distanceWarningNet.Value;
    public float DistanceNormalized => distanceNormalizedNet.Value;

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

        if (IsServer)
        {
            currentHealthNet.Value = maxHealth;
            lifeStateNet.Value = LifeStateAlive;
            distanceWarningNet.Value = false;
            isJumpingNet.Value = false;
            ResetJumpState();
        }

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

        isJumpingNet.OnValueChanged += (oldValue, newValue) =>
        {
            ApplyJumpAnimationState(newValue);
        };

        lifeStateNet.OnValueChanged += (oldValue, newValue) =>
        {
            ApplyLifeState(newValue);
        };

        ApplyLifeState(lifeStateNet.Value);
        ApplyJumpAnimationState(isJumpingNet.Value);

        if (IsOwner)
            AssignCamera();

        if (!players.Contains(this))
            players.Add(this);

        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);

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

        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.loop = false;
            footstepSource.spatialBlend = 1f;
            footstepSource.volume = footstepVolume;
        }

        if (distanceLoopClip != null)
        {
            distanceAudioSource = gameObject.AddComponent<AudioSource>();
            distanceAudioSource.clip = distanceLoopClip;
            distanceAudioSource.loop = true;
            distanceAudioSource.playOnAwake = false;
            distanceAudioSource.volume = 0f;
            distanceAudioSource.spatialBlend = 0f;
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

        ResetStaticsIfNoPlayersLeft();
    }

    void ResetStaticsIfNoPlayersLeft()
    {
        if (players.Count > 0)
            return;

        outOfRangeTimer = 0f;
        timerActive = false;
        failSequenceTriggered = false;
        titleReturnSequenceTriggered = false;
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
            ApplyJumpAnimationState(isJumpingNet.Value);
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
        if (!IsSpawned) return;

        HandleDistanceAudio();

        if (!IsOwner)
            return;

        HandleCameraRotation();
        HandleCameraZoom();

        if (IsBusted || IsFailed)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
            RequestJumpServerRpc();

        if (Input.GetKeyDown(KeyCode.M))
            DebugKillServerRpc();

        HandleReviveInput();
    }

    void HandleFootstepsServer()
    {
        if (!IsServer) return;

        bool isMoving = lastInput.magnitude > 0.1f;
        bool grounded = isGrounded || isTouchingGroundTag;

        if (!isMoving || !grounded || IsBusted || IsFailed)
        {
            footstepTimer = 0f;
            return;
        }

        footstepTimer += Time.fixedDeltaTime;

        if (footstepTimer >= footstepInterval)
        {
            footstepTimer = 0f;
            PlayFootstepClientRpc(transform.position, selectedCharacterIndex.Value);
        }
    }

    [ClientRpc]
    void PlayFootstepClientRpc(Vector3 position, int characterIndex)
    {
        if (footstepSource == null)
            return;

        AudioClip clipToPlay = GetFootstepClip(characterIndex);
        if (clipToPlay == null)
            return;

        footstepSource.transform.position = position;
        footstepSource.PlayOneShot(clipToPlay, footstepVolume);
    }

    AudioClip GetFootstepClip(int index)
    {
        AudioClip[] clips = null;

        if (index == 0)
            clips = footstepClipsIndex0;
        else if (index == 1)
            clips = footstepClipsIndex1;

        if (clips == null || clips.Length == 0)
            return null;

        return clips[Random.Range(0, clips.Length)];
    }

    void HandleDistanceAudio()
    {
        if (distanceAudioSource == null || distanceLoopClip == null)
            return;

        if (failSequenceTriggered || IsFailed)
        {
            if (distanceAudioSource.isPlaying)
                distanceAudioSource.Stop();

            return;
        }

        float target = distanceNormalizedNet.Value;
        smoothedDistance = Mathf.Lerp(smoothedDistance, target, Time.deltaTime * 5f);

        if (!distanceWarningNet.Value)
        {
            distanceAudioSource.volume = Mathf.Lerp(distanceAudioSource.volume, 0f, Time.deltaTime * 5f);

            if (distanceAudioSource.volume <= 0.01f && distanceAudioSource.isPlaying)
                distanceAudioSource.Stop();

            return;
        }

        if (!distanceAudioSource.isPlaying)
            distanceAudioSource.Play();

        float targetVolume = Mathf.Lerp(minVolume, maxVolume, smoothedDistance);
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, smoothedDistance);

        distanceAudioSource.volume = Mathf.Lerp(distanceAudioSource.volume, targetVolume, Time.deltaTime * 5f);
        distanceAudioSource.pitch = targetPitch;
    }

    [ClientRpc]
    void StopDistanceAudioClientRpc()
    {
        if (distanceAudioSource != null)
        {
            distanceAudioSource.Stop();
            distanceAudioSource.volume = 0f;
        }
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
        UpdateJumpTimers();
        ApplyJumpAnimationState();

        if (IsBusted || IsFailed)
            return;

        if (IsServer)
        {
            TryHandleServerJump();
            ServerMovement();
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

    bool IsGroundedForJump()
    {
        return isGrounded || isTouchingGroundTag;
    }

    void UpdateJumpTimers()
    {
        if (IsGroundedForJump())
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.fixedDeltaTime;

        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.fixedDeltaTime;

        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.fixedDeltaTime;

        if (coyoteTimer < 0f)
            coyoteTimer = 0f;

        if (jumpBufferTimer < 0f)
            jumpBufferTimer = 0f;

        if (jumpCooldownTimer < 0f)
            jumpCooldownTimer = 0f;

        if (jumpBufferTimer <= 0f)
            jumpRequested = false;
    }

    void TryHandleServerJump()
    {
        if (!jumpRequested || rb == null)
            return;

        if (jumpBufferTimer <= 0f)
        {
            jumpRequested = false;
            return;
        }

        if (jumpCooldownTimer > 0f)
            return;

        if (!IsGroundedForJump() && coyoteTimer <= 0f)
            return;

        jumpRequested = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        jumpCooldownTimer = jumpCooldown;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        isJumpingNet.Value = true;
        ApplyJumpAnimationState(true);
    }

    void HandleInput()
    {
        if (IsBusted || IsFailed)
            return;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Camera.main.transform.right;

        Vector3 input = camForward * z + camRight * x;

        SendInputServerRpc(input);

        if (!HandleReviveInput())
            DetectItem();
    }

    bool HandleReviveInput()
    {
        if (!IsOwner || IsBusted || IsFailed)
            return false;

        PlayerMovement target = FindNearestBustedPlayer();

        if (target != null && Input.GetKey(KeyCode.F))
        {
            if (reviveTarget != target)
            {
                reviveTarget = target;
                reviveHoldTimer = 0f;
            }

            reviveHoldTimer += Time.deltaTime;

            if (reviveHoldTimer >= reviveHoldSeconds)
            {
                ReviveTargetServerRpc(new NetworkObjectReference(reviveTarget.NetworkObject));
                ResetReviveHold();
            }

            return true;
        }

        ResetReviveHold();
        return false;
    }

    void ResetReviveHold()
    {
        reviveTarget = null;
        reviveHoldTimer = 0f;
    }

    PlayerMovement FindNearestBustedPlayer()
    {
        PlayerMovement nearest = null;
        float nearestDist = reviveRange + 1f;

        foreach (var player in players)
        {
            if (player == null || player == this || !player.IsSpawned || !player.IsBusted)
                continue;

            float d = Vector3.Distance(transform.position, player.transform.position);
            if (d < reviveRange && d < nearestDist)
            {
                nearest = player;
                nearestDist = d;
            }
        }

        return nearest;
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestJumpServerRpc()
    {
        jumpRequested = true;
        jumpBufferTimer = jumpBufferTime;
    }

    [ServerRpc(RequireOwnership = false)]
    void DebugKillServerRpc()
    {
        if (lifeStateNet.Value == LifeStateAlive)
        {
            ApplyDamage(maxHealth);
        }
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

        HandleFootstepsServer();

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
            SetDistanceWarning(false);
            distanceNormalizedNet.Value = 0f;
            return;
        }

        float maxPlayerDistance = GetMaxPlayerDistance(serverPlayers);

        float normalized = Mathf.InverseLerp(warningDistance, limitDistance, maxPlayerDistance);
        distanceNormalizedNet.Value = normalized;

        if (maxPlayerDistance > warningDistance)
            SetDistanceWarning(true);
        else
            SetDistanceWarning(false);

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
                    StopDistanceAudioClientRpc();
                    BeginFailSequence();
                }
            }
        }
        else
        {
            ResetDistanceTimer();
        }
    }

    void SetDistanceWarning(bool state)
    {
        if (distanceWarningNet.Value != state)
            distanceWarningNet.Value = state;
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

    void ResetJumpState()
    {
        jumpRequested = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        jumpCooldownTimer = 0f;
    }

    IEnumerator FailSequenceWithDelay()
    {
        yield return new WaitForSeconds(vfxDelayBeforeFail);

        SetAllPlayersFailed();

        BeginTitleReturnSequenceClientRpc(failUiHoldSeconds);
        BeginTitleReturnSequence(failUiHoldSeconds);
    }

    void BeginFailSequence()
    {
        if (failSequenceTriggered)
            return;

        failSequenceTriggered = true;
        ResetDistanceTimer();
        SetDistanceWarning(false);

        StopDistanceAudioClientRpc();

        if (IsServer)
        {
            foreach (var player in players)
            {
                if (player != null)
                {
                    var vfx = player.GetComponent<FailExplosionVFX>();
                    if (vfx != null)
                    {
                        vfx.PlayExplosion();
                    }
                }
            }
            StartCoroutine(FailSequenceWithDelay());
        }
    }

    void SetAllPlayersFailed()
    {
        if (!IsServer || NetworkManager.Singleton == null)
            return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent(out PlayerMovement pm))
            {
                pm.ForceFailedState();
            }
        }
    }

    void ForceFailedState()
    {
        if (lifeStateNet.Value == LifeStateFailed)
            return;

        currentHealthNet.Value = 0;
        lifeStateNet.Value = LifeStateFailed;

        lastInput = Vector3.zero;
        jumpRequested = false;
        reviveTarget = null;
        reviveHoldTimer = 0f;
        ResetJumpState();

        ApplyLifeState(LifeStateFailed);
    }

    [ClientRpc]
    void BeginTitleReturnSequenceClientRpc(float delay)
    {
        BeginTitleReturnSequence(delay);
    }

    void BeginTitleReturnSequence(float delay)
    {
        if (titleReturnSequenceTriggered)
            return;

        titleReturnSequenceTriggered = true;
        StartCoroutine(ReturnToTitleRoutine(delay));
    }

    IEnumerator ReturnToTitleRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        yield return null;
        SceneManager.LoadScene(menuSceneName);
    }

    void ApplyAnimationState(bool walking)
    {
        if (animator != null)
            animator.SetBool("isWalking", walking);
    }

    void ApplyJumpAnimationState()
    {
        if (!IsServer)
            return;

        bool jumping = !isGrounded && !isTouchingGroundTag && lifeStateNet.Value == LifeStateAlive;

        if (isJumpingNet.Value != jumping)
            isJumpingNet.Value = jumping;

        if (animator != null)
            animator.SetBool("isJumping", jumping);
    }

    void ApplyJumpAnimationState(bool jumping)
    {
        if (animator != null)
            animator.SetBool("isJumping", jumping);
    }

    void ApplyLifeState(int state)
    {
        if (rb != null)
        {
            if (state == LifeStateAlive)
            {
                rb.isKinematic = !IsServer;
                rb.useGravity = true;
                rb.WakeUp();
            }
            else
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.Sleep();
            }
        }

        if (animator != null)
        {
            animator.SetBool("isWalking", false);
            animator.SetBool("isJumping", false);
        }

        if (IsServer && state != LifeStateAlive)
        {
            isJumpingNet.Value = false;
            ResetJumpState();
        }
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
        ResetJumpState();

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
        animator?.SetBool("isJumping", false);
        isJumpingNet.Value = false;

        Debug.Log($"[PlayerMovement] Respawned at {position}");
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsBusted || IsFailed)
            return;

        if (IsServer)
        {
            ApplyDamage(amount);
        }
        else
        {
            TakeDamageServerRpc(amount);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void TakeDamageServerRpc(int amount)
    {
        ApplyDamage(amount);
    }

    void ApplyDamage(int amount)
    {
        if (IsBusted || IsFailed)
            return;

        currentHealthNet.Value -= amount;
        Debug.Log("Player took damage, health = " + currentHealthNet.Value);

        if (currentHealthNet.Value <= 0)
        {
            currentHealthNet.Value = 0;
            Bust();
        }
    }

    void Bust()
    {
        if (lifeStateNet.Value != LifeStateAlive)
            return;

        lifeStateNet.Value = LifeStateBusted;
        ResetJumpState();
        ApplyLifeState(LifeStateBusted);
        Debug.Log("Player busted!");
    }

    [ServerRpc(RequireOwnership = false)]
    void ReviveTargetServerRpc(NetworkObjectReference targetRef)
    {
        if (!targetRef.TryGet(out NetworkObject targetObject))
            return;

        if (!targetObject.TryGetComponent(out PlayerMovement target))
            return;

        if (target == null || !target.IsSpawned || !target.IsBusted)
            return;

        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance > reviveRange)
            return;

        target.ReviveFromBusted();
    }

    void ReviveFromBusted()
    {
        if (!IsServer || !IsBusted)
            return;

        lifeStateNet.Value = LifeStateAlive;
        currentHealthNet.Value = maxHealth;

        lastInput = Vector3.zero;
        jumpRequested = false;
        isTouchingGroundTag = false;
        reviveTarget = null;
        reviveHoldTimer = 0f;
        ResetJumpState();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }

        isJumpingNet.Value = false;
        ApplyLifeState(LifeStateAlive);

        Debug.Log("[PlayerMovement] Player revived!");
    }

    public override void OnDestroy()
    {
        if (players.Contains(this))
            players.Remove(this);

        ResetStaticsIfNoPlayersLeft();

        base.OnDestroy();
    }
}
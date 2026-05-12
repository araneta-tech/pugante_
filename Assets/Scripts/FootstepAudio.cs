using UnityEngine;

public class FootstepAudio : MonoBehaviour
{
    [System.Serializable]
    public class FootstepConfig
    {
        public float stepInterval = 0.5f;
        public float moveThreshold = 0.15f;

        public float volume = 1f;

        public float minPitch = 0.95f;
        public float maxPitch = 1.05f;
    }

    [Header("Clips")]
    public AudioClip[] footstepClipsIndex0;
    public AudioClip[] footstepClipsIndex1;

    [Header("Per Character Settings")]
    public FootstepConfig index0;
    public FootstepConfig index1;

    private AudioSource source;
    private PlayerMovement player;

    private float stepTimer;
    private bool stepLocked;

    void Start()
    {
        player = GetComponent<PlayerMovement>();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
    }

    void Update()
    {
        if (player == null || !player.IsSpawned)
            return;

        if (player.IsBusted || player.IsFailed)
        {
            ResetSteps();
            return;
        }

        FootstepConfig cfg = GetConfig(player.SelectedCharacterIndex);

        bool moving = player.LastInput.magnitude > cfg.moveThreshold;
        bool grounded = player.IsGrounded;

        if (!moving || !grounded)
        {
            ResetSteps();
            return;
        }

        stepTimer += Time.deltaTime;

        if (stepTimer >= cfg.stepInterval && !stepLocked)
        {
            PlayFootstep(player.SelectedCharacterIndex, cfg);

            stepLocked = true;
            stepTimer = 0f;

            Invoke(nameof(UnlockStep), cfg.stepInterval * 0.5f);
        }
    }

    void PlayFootstep(int characterIndex, FootstepConfig cfg)
    {
        AudioClip clip = GetClip(characterIndex);
        if (clip == null) return;

        source.pitch = Random.Range(cfg.minPitch, cfg.maxPitch);
        source.volume = cfg.volume;

        source.PlayOneShot(clip);
    }

    void UnlockStep()
    {
        stepLocked = false;
    }

    void ResetSteps()
    {
        stepTimer = 0f;
        stepLocked = false;
    }

    FootstepConfig GetConfig(int index)
    {
        return index == 0 ? index0 : index1;
    }

    AudioClip GetClip(int index)
    {
        AudioClip[] clips = index == 0 ? footstepClipsIndex0 : footstepClipsIndex1;

        if (clips == null || clips.Length == 0)
            return null;

        return clips[Random.Range(0, clips.Length)];
    }
}
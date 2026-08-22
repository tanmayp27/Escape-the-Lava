using System.Collections;
using UnityEngine;

// Manages BGM and SFX playback, dynamic volume ducking, audio transitions,
// and low-pass filter effects based on game state and player events.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM & Game Outcome Audio")]
    [Tooltip("Background music clip played on loop in the Main Menu scene.")]
    public AudioClip mainMenuBgmClip;

    [Tooltip("Background music clip played on loop during gameplay.")]
    public AudioClip bgmClip;

    [Tooltip("Audio clip played when winning a round.")]
    public AudioClip winAudioClip;

    [Tooltip("Audio clip played when losing a round.")]
    public AudioClip loseAudioClip;

    [Header("Tile SFX Audio Clips")]
    [Tooltip("Fallback clip played when collecting a Diamond.")]
    public AudioClip positiveAudioClip;

    [Tooltip("Pool of positive audio clips selected randomly for Diamond collection.")]
    public AudioClip[] positiveAudioClips;

    [Tooltip("Base audio clip played when taking damage from Lava.")]
    public AudioClip damageAudioClip;

    [Tooltip("Pool of hurt audio clips layered over the damage sound when taking damage.")]
    public AudioClip[] hurtAudioClips;

    [Tooltip("Fallback clip played when clicking an Island/Grass tile.")]
    public AudioClip grassRustlingAudioClip;

    [Tooltip("Pool of grass rustling clips selected randomly when clicking an Island/Grass tile.")]
    public AudioClip[] grassRustlingAudioClips;

    [Header("Audio Master & BGM Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 1.0f;

    [Range(0f, 1f)]
    public float bgmVolume = 0.5f;

    [Header("Ducking & Muffle Filter Settings")]
    [Tooltip("BGM volume multiplier applied on Win or Lose outcomes.")]
    public float outcomeDuckedVolumeMultiplier = 0.4f;

    [Tooltip("BGM volume multiplier applied temporarily on tile interaction events.")]
    public float eventDuckedVolumeMultiplier = 0.75f;

    [Tooltip("BGM volume multiplier applied when player lives fall below 3.")]
    public float lowHealthVolumeMultiplier = 0.65f;

    [Tooltip("Low Pass Filter cutoff frequency for round outcome states (Hz).")]
    public float outcomeMuffledCutoffHz = 900f;

    [Tooltip("Low Pass Filter cutoff frequency for low health state (Hz).")]
    public float lowHealthMuffledCutoffHz = 1400f;

    [Tooltip("Low Pass Filter cutoff frequency for mild tile interaction ducking (Hz).")]
    public float eventMuffledCutoffHz = 3500f;

    [Tooltip("Normal cutoff frequency when BGM is unfiltered (Hz).")]
    public float normalCutoffHz = 22000f;

    [Tooltip("Duration in seconds of temporary ducking on tile interaction events.")]
    public float eventDuckDuration = 0.4f;

    [Tooltip("Speed of volume, cutoff frequency, and pitch transitions.")]
    public float filterTransitionSpeed = 8.0f;

    [Header("Timer BGM Pitch Acceleration Settings")]
    [Tooltip("Time threshold in seconds under which BGM pitch and speed gradually accelerate.")]
    public float timerPitchThresholdSeconds = 10f;

    [Tooltip("Maximum pitch multiplier for BGM when the timer reaches 0.")]
    public float maxTimerPitch = 1.35f;

    [Tooltip("Default pitch for BGM when time is above the threshold.")]
    public float normalPitch = 1.0f;

    private float currentRemainingTimeTracked = 60f;

    // Runtime Audio Source and Filter References
    private AudioSource audioSource;
    private AudioSource bgmAudioSource;
    private AudioLowPassFilter bgmLowPassFilter;

    private float currentTargetVolume;
    private float currentTargetCutoff;
    private Coroutine eventDuckCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnLivesChanged -= HandleLivesChanged;
            GameManager.Instance.OnTimerChanged -= HandleTimerChanged;
        }
    }

    private void Start()
    {
        ResetBGMAudioState();
        BindToGameManager();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        BindToGameManager();
    }

    private void BindToGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

            GameManager.Instance.OnLivesChanged -= HandleLivesChanged;
            GameManager.Instance.OnLivesChanged += HandleLivesChanged;

            GameManager.Instance.OnTimerChanged -= HandleTimerChanged;
            GameManager.Instance.OnTimerChanged += HandleTimerChanged;

            HandleGameStateChanged(GameManager.Instance.CurrentState);
            HandleLivesChanged(GameManager.Instance.CurrentLives);
            HandleTimerChanged(GameManager.Instance.RemainingTime);
        }
        else
        {
            ResetBGMAudioState();
            PlayMainMenuBGM();
        }
    }

    private void HandleTimerChanged(float remainingTime)
    {
        currentRemainingTimeTracked = remainingTime;
    }

    private void Update()
    {
        // Smoothly transition BGM volume, cutoff frequency, and pitch
        if (bgmAudioSource != null && bgmAudioSource.clip != null && bgmAudioSource.isPlaying)
        {
            float targetVol = currentTargetVolume * masterVolume;
            bgmAudioSource.volume = Mathf.Lerp(bgmAudioSource.volume, targetVol, Time.deltaTime * filterTransitionSpeed);

            if (bgmLowPassFilter != null && bgmLowPassFilter.enabled)
            {
                bgmLowPassFilter.cutoffFrequency = Mathf.Lerp(bgmLowPassFilter.cutoffFrequency, currentTargetCutoff, Time.deltaTime * filterTransitionSpeed);
            }

            // Accelerate BGM speed and pitch when timer falls below threshold
            float targetPitch = normalPitch;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                if (currentRemainingTimeTracked <= timerPitchThresholdSeconds && currentRemainingTimeTracked > 0f)
                {
                    float progress = 1.0f - Mathf.Clamp01(currentRemainingTimeTracked / timerPitchThresholdSeconds);
                    targetPitch = Mathf.Lerp(normalPitch, maxTimerPitch, progress);
                }
            }

            bgmAudioSource.pitch = Mathf.Lerp(bgmAudioSource.pitch, targetPitch, Time.deltaTime * filterTransitionSpeed);
        }
    }

    // Ensures SFX and BGM AudioSources are configured. BGM resides on a dedicated child object
    // so low-pass filtering applies exclusively to background music.
    private void EnsureAudioSources()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (bgmAudioSource == null)
        {
            Transform bgmChild = transform.Find("BGMHolder");
            if (bgmChild == null)
            {
                GameObject bgmObj = new GameObject("BGMHolder");
                bgmObj.transform.SetParent(transform);
                bgmObj.transform.localPosition = Vector3.zero;
                bgmAudioSource = bgmObj.AddComponent<AudioSource>();
            }
            else
            {
                bgmAudioSource = bgmChild.GetComponent<AudioSource>();
                if (bgmAudioSource == null)
                {
                    bgmAudioSource = bgmChild.gameObject.AddComponent<AudioSource>();
                }
            }
        }

        if (bgmAudioSource != null)
        {
            bgmAudioSource.loop = true;
            bgmAudioSource.playOnAwake = false;

            if (bgmLowPassFilter == null)
            {
                bgmLowPassFilter = bgmAudioSource.GetComponent<AudioLowPassFilter>();
                if (bgmLowPassFilter == null)
                {
                    bgmLowPassFilter = bgmAudioSource.gameObject.AddComponent<AudioLowPassFilter>();
                }
            }

            if (bgmAudioSource.clip == null && mainMenuBgmClip != null)
            {
                bgmAudioSource.clip = mainMenuBgmClip;
            }
            else if (bgmAudioSource.clip == null && bgmClip != null)
            {
                bgmAudioSource.clip = bgmClip;
            }
        }

        // Remove any low-pass filter erroneously attached to the main object
        AudioLowPassFilter mainFilter = GetComponent<AudioLowPassFilter>();
        if (mainFilter != null)
        {
            if (Application.isPlaying)
            {
                Destroy(mainFilter);
            }
            else
            {
                DestroyImmediate(mainFilter);
            }
        }
    }

    private void HandleGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Playing:
                ResetBGMAudioState();
                PlayGameplayBGM();
                break;
            case GameState.LevelComplete:
                PlayWinAudio();
                break;
            case GameState.GameOver:
                PlayLoseAudio();
                break;
        }
    }

    // Plays the Main Menu background music on loop.
    public void PlayMainMenuBGM()
    {
        AudioClip clipToPlay = mainMenuBgmClip != null ? mainMenuBgmClip : bgmClip;
        PlayBGM(clipToPlay);
    }

    // Plays the Gameplay background music on loop.
    public void PlayGameplayBGM()
    {
        AudioClip clipToPlay = bgmClip != null ? bgmClip : mainMenuBgmClip;
        PlayBGM(clipToPlay);
    }

    // Plays specified background music track on loop, transitioning smoothly if the track changes.
    public void PlayBGM(AudioClip clipToPlay = null)
    {
        if (bgmAudioSource == null)
        {
            return;
        }

        AudioClip targetClip = clipToPlay;
        if (targetClip == null)
        {
            targetClip = mainMenuBgmClip != null ? mainMenuBgmClip : bgmClip;
        }

        if (targetClip == null)
        {
            Debug.LogWarning("[AudioManager] BGM AudioClip is not assigned in the Inspector.");
            return;
        }

        bgmAudioSource.loop = true;

        if (bgmAudioSource.clip != targetClip)
        {
            bgmAudioSource.clip = targetClip;
            bgmAudioSource.volume = bgmVolume * masterVolume;
            bgmAudioSource.Play();
        }
        else if (!bgmAudioSource.isPlaying)
        {
            bgmAudioSource.volume = bgmVolume * masterVolume;
            bgmAudioSource.Play();
        }
    }

    // Stops background music playback.
    public void StopBGM()
    {
        if (bgmAudioSource != null && bgmAudioSource.isPlaying)
        {
            bgmAudioSource.Stop();
        }
    }

    // Plays positive collection audio and applies temporary event ducking to BGM.
    public void PlayPositiveAudio(Vector3 position = default)
    {
        TriggerEventDucking();
        AudioClip positiveClip = GetRandomClip(positiveAudioClips, positiveAudioClip);
        PlayClip(positiveClip, position);
    }

    // Plays damage audio and applies temporary event ducking to BGM.
    public void PlayDamageAudio(Vector3 position = default)
    {
        TriggerEventDucking();
        PlayClip(damageAudioClip, position);

        AudioClip hurtClip = GetRandomClip(hurtAudioClips);
        if (hurtClip != null)
        {
            PlayClip(hurtClip, position);
        }
    }

    // Plays grass rustling audio and applies temporary event ducking to BGM.
    public void PlayGrassRustlingAudio(Vector3 position = default)
    {
        TriggerEventDucking();
        AudioClip rustleClip = GetRandomClip(grassRustlingAudioClips, grassRustlingAudioClip);
        PlayClip(rustleClip, position);
    }

    // Plays win audio and ducks/muffles the background music.
    public void PlayWinAudio(Vector3 position = default)
    {
        if (eventDuckCoroutine != null)
        {
            StopCoroutine(eventDuckCoroutine);
            eventDuckCoroutine = null;
        }

        currentTargetVolume = bgmVolume * outcomeDuckedVolumeMultiplier;
        currentTargetCutoff = outcomeMuffledCutoffHz;

        if (winAudioClip != null)
        {
            PlayClip(winAudioClip, position);
        }
    }

    // Plays lose audio and ducks/muffles the background music.
    public void PlayLoseAudio(Vector3 position = default)
    {
        if (eventDuckCoroutine != null)
        {
            StopCoroutine(eventDuckCoroutine);
            eventDuckCoroutine = null;
        }

        currentTargetVolume = bgmVolume * outcomeDuckedVolumeMultiplier;
        currentTargetCutoff = outcomeMuffledCutoffHz;

        if (loseAudioClip != null)
        {
            PlayClip(loseAudioClip, position);
        }
    }

    private void HandleLivesChanged(int lives)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
        {
            return;
        }

        if (lives < 3)
        {
            SetLowHealthAudioState();
        }
        else
        {
            ResetBGMAudioState();
        }
    }

    // Applies BGM volume ducking and muffling for the low health state.
    public void SetLowHealthAudioState()
    {
        if (eventDuckCoroutine != null)
        {
            StopCoroutine(eventDuckCoroutine);
            eventDuckCoroutine = null;
        }

        currentTargetVolume = bgmVolume * lowHealthVolumeMultiplier;
        currentTargetCutoff = lowHealthMuffledCutoffHz;
    }

    // Triggers temporary BGM volume ducking and muffling during tile interaction events.
    public void TriggerEventDucking()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
        {
            return;
        }

        if (eventDuckCoroutine != null)
        {
            StopCoroutine(eventDuckCoroutine);
        }
        eventDuckCoroutine = StartCoroutine(EventDuckRoutine());
    }

    private IEnumerator EventDuckRoutine()
    {
        currentTargetVolume = bgmVolume * eventDuckedVolumeMultiplier;
        currentTargetCutoff = eventMuffledCutoffHz;

        yield return new WaitForSeconds(eventDuckDuration);

        ResetBGMAudioState();
    }

    // Resets BGM volume and filter cutoff frequency back to default gameplay levels.
    public void ResetBGMAudioState()
    {
        if (eventDuckCoroutine != null)
        {
            StopCoroutine(eventDuckCoroutine);
            eventDuckCoroutine = null;
        }

        if (bgmAudioSource != null && (GameManager.Instance == null || GameManager.Instance.RemainingTime > timerPitchThresholdSeconds))
        {
            bgmAudioSource.pitch = normalPitch;
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentLives < 3 && GameManager.Instance.CurrentState == GameState.Playing)
        {
            currentTargetVolume = bgmVolume * lowHealthVolumeMultiplier;
            currentTargetCutoff = lowHealthMuffledCutoffHz;
        }
        else
        {
            currentTargetVolume = bgmVolume;
            currentTargetCutoff = normalCutoffHz;
        }
    }

    // Plays the appropriate sound effect for a specific tile type.
    public void PlayAudioForTile(TileType tileType, Vector3 position = default)
    {
        switch (tileType)
        {
            case TileType.Diamond:
                PlayPositiveAudio(position);
                break;
            case TileType.Lava:
                PlayDamageAudio(position);
                break;
            case TileType.Island:
                PlayGrassRustlingAudio(position);
                break;
        }
    }

    // Picks a random clip from an array, falling back to a default clip if empty or null.
    private AudioClip GetRandomClip(AudioClip[] clips, AudioClip fallback = null)
    {
        if (clips != null && clips.Length > 0)
        {
            int randomIndex = Random.Range(0, clips.Length);
            if (clips[randomIndex] != null)
            {
                return clips[randomIndex];
            }
        }

        return fallback;
    }

    // Plays an AudioClip at a given position using the primary SFX AudioSource.
    public void PlayClip(AudioClip clip, Vector3 position = default)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource != null && audioSource.enabled)
        {
            audioSource.PlayOneShot(clip, masterVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, position, masterVolume);
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Centralized audio system for the entire game.
///
/// ALL sounds in the game go through this singleton.
/// No other script should have its own AudioSource for game sounds.
///
/// SOUND SLOTS (assign clips in Inspector):
///   DoorOpen, DoorClose, ButtonClick
///   RightAnswer, WrongAnswer
///   CellphoneRing, OfficephoneRing
///   CallCutOff, CallbackUnavailable
///   PaperSound, DoorKnock
///   BreathingHeavy, Inhale, Exhale
///   Heartbeat, PeopleJudging
///   NpcVoice, NarratorVoice
///   NegativeVoice (multiple clips supported — plays random one)
///   OpenRoomMusic, MainMusic, TenseMusic
///
/// MUSIC STATE MACHINE:
///   Normal → MainMusic (from game start)
///   Tense  → TenseMusic (when anxiety ≥ 50%)
///   Open   → OpenRoomMusic (one-shot, after all rooms done)
///   Stopped→ silence
///
/// NEGATIVE VOICES:
///   Schedules random NegativeVoice clips when anxiety ≥ 50%.
///   Frequency increases as anxiety rises. Stops below 50%.
///
/// SETUP:
///   1. Create a GameObject "AudioManager" and attach this script.
///   2. Assign every clip slot in the Inspector.
///   3. Ensure AnxietyManager exists in the scene.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — One-Shot Clips
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Door Sounds")]
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] [Range(0f, 1f)] private float doorOpenVolume = 1f;
    [SerializeField] private AudioClip doorCloseClip;
    [SerializeField] [Range(0f, 1f)] private float doorCloseVolume = 1f;

    [Header("UI / Puzzle Sounds")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] [Range(0f, 1f)] private float buttonClickVolume = 1f;
    [SerializeField] private AudioClip rightAnswerClip;
    [SerializeField] [Range(0f, 1f)] private float rightAnswerVolume = 1f;
    [SerializeField] private AudioClip wrongAnswerClip;
    [SerializeField] [Range(0f, 1f)] private float wrongAnswerVolume = 1f;

    [Header("Phone Sounds")]
    [SerializeField] private AudioClip cellphoneRingClip;
    [SerializeField] private AudioClip officephoneRingClip;
    [SerializeField] private AudioClip callCutOffClip;
    [SerializeField] [Range(0f, 1f)] private float callCutOffVolume = 1f;
    [SerializeField] private AudioClip callbackUnavailableClip;
    [SerializeField] [Range(0f, 1f)] private float callbackUnavailableVolume = 1f;

    [Tooltip("Specific volume multiplier for when the phone is ringing. 1.0 is full volume, 0.3 is quiet.")]
    [SerializeField] [Range(0f, 1f)] private float phoneRingVolume = 0.4f;

    [Header("Environment Sounds")]
    [SerializeField] private AudioClip paperSoundClip;
    [SerializeField] [Range(0f, 1f)] private float paperSoundVolume = 1f;
    [SerializeField] private AudioClip doorKnockClip;
    [SerializeField] [Range(0f, 1f)] private float doorKnockVolume = 1f;

    [Header("Breathing / Panic Sounds")]
    [SerializeField] private AudioClip breathingHeavyClip;
    [SerializeField] [Range(0f, 1f)] private float breathingHeavyVolume = 1f;
    [SerializeField] private AudioClip inhaleClip;
    [SerializeField] [Range(0f, 1f)] private float inhaleVolume = 1f;
    [SerializeField] private AudioClip exhaleClip;
    [SerializeField] [Range(0f, 1f)] private float exhaleVolume = 1f;
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] [Range(0f, 1f)] private float heartbeatVolume = 1f;
    [SerializeField] private AudioClip peopleJudgingClip;
    [SerializeField] [Range(0f, 1f)] private float peopleJudgingVolume = 1f;

    [Header("Voice")]
    [Tooltip("AudioSource used for NPC voice clips (assigned at runtime via PlayNpcClip).")]
    [SerializeField] private AudioSource npcVoiceSource;

    [Tooltip("AudioSource used for Narrator voice clips (assigned at runtime via PlayNarratorClip).")]
    [SerializeField] private AudioSource narratorVoiceSource;

    [Tooltip("Player inner-voice clip: played after the NPC call cuts off.")]
    [SerializeField] private AudioClip playerVoiceCallBackClip;
    [SerializeField] [Range(0f, 1f)] private float playerVoiceCallBackVolume = 1f;

    [Tooltip("Player inner-voice clip: played when the player enters the Illusion Room.")]
    [SerializeField] private AudioClip playerVoiceIllusionBathroomClip;
    [SerializeField] [Range(0f, 1f)] private float playerVoiceIllusionBathroomVolume = 1f;

    [Tooltip("Dedicated AudioSource for special one-shot player voice lines (callback, bathroom). " +
             "Set Output → InnerVoice mixer group for the in-head DSP effect.")]
    [SerializeField] private AudioSource playerInnerVoiceSource;

    [Header("Negative Voices")]
    [Tooltip("A pool of negative inner-voice clips. A random one is chosen each time.")]
    [SerializeField] private AudioClip[] negativeVoiceClips;
    [Tooltip("Volume modifier for negative voice clips.")]
    [SerializeField] [Range(0f, 1f)] private float negativeVoiceVolume = 1f;
    [Tooltip("AudioSource for negative voice clips. Output → InnerVoice mixer group.")]
    [SerializeField] private AudioSource negativeVoiceSource;

    [Tooltip("Minimum seconds between negative voice triggers at 50% anxiety.")]
    [SerializeField] private float negativeVoiceMinInterval = 12f;
    [Tooltip("Minimum seconds between negative voice triggers at 100% anxiety.")]
    [SerializeField] private float negativeVoiceMaxFreqInterval = 3f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Audio Mixer & Volume Control
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Audio Mixer")]
    [Tooltip("The GameAudioMixer asset. Create via Window → Audio → Audio Mixer. " +
             "Expose mixer group volumes as parameters named: MusicVol, SFXVol, VoiceVol, InnerVoiceVol.")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Volume Control (0 = silent, 1 = full)")]
    [Tooltip("Overall master volume scale.")]
    [SerializeField] [Range(0f, 1f)] private float masterVolume = 1f;
    [Tooltip("Music tracks volume.")]
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 1f;
    [Tooltip("Sound effects volume.")]
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;
    [Tooltip("NPC and Narrator voice volume.")]
    [SerializeField] [Range(0f, 1f)] private float voiceVolume = 1f;
    [Tooltip("Inner voice / intrusive thoughts volume.")]
    [SerializeField] [Range(0f, 1f)] private float innerVoiceVolume = 0.9f;

    // cached to detect changes
    private float _prevMaster = -1f, _prevMusic = -1f, _prevSfx = -1f,
                  _prevVoice = -1f, _prevInnerVoice = -1f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Music
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Music")]
    [SerializeField] private AudioSource mainMusicSource;
    [SerializeField] private AudioSource tenseMusicSource;
    [SerializeField] private AudioSource openRoomMusicSource;

    [Tooltip("Seconds to cross-fade between music tracks.")]
    [SerializeField] private float musicFadeDuration = 2.5f;

    private float TenseMusicThreshold => AnxietyManager.Instance != null ? AnxietyManager.Instance.NormalizedExtremeThreshold : 0.65f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — General AudioSource (for one-shots)
    // ═══════════════════════════════════════════════════════════════════════

    [Header("General AudioSource (one-shots & loops)")]
    [Tooltip("A generic AudioSource for playing one-shot sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("AudioSource for looping sounds (breathing heavy, ringing, etc.).")]
    [SerializeField] private AudioSource loopSource;

    // Public properties to expose specific clip and source reference safely
    public AudioClip PeopleJudgingClip => peopleJudgingClip;
    public AudioSource SfxSource => sfxSource;

    [Tooltip("Dedicated AudioSource for heartbeat loop during panic (separate so it can overlap breathing).")]
    [SerializeField] private AudioSource heartbeatLoopSource;

    // ═══════════════════════════════════════════════════════════════════════
    //  MUSIC STATE
    // ═══════════════════════════════════════════════════════════════════════

    public enum MusicState { Normal, Tense, Open, Stopped }
    private MusicState _musicState = MusicState.Stopped;
    private Coroutine _musicFadeRoutine;

    // ═══════════════════════════════════════════════════════════════════════
    //  NEGATIVE VOICE STATE
    // ═══════════════════════════════════════════════════════════════════════

    private Coroutine _negativeVoiceRoutine;
    private bool _negativeVoicesActive;

    // ═══════════════════════════════════════════════════════════════════════
    //  CURRENT LOOP TRACKING
    // ═══════════════════════════════════════════════════════════════════════

    private SoundID _currentLoop = SoundID.None;

    /// <summary>The SoundID currently playing on the loop source. SoundID.None if silent.</summary>
    public SoundID CurrentLoop => _currentLoop;

    //  MUSIC TARGET VOLUMES
    // ═══════════════════════════════════════════════════════════════════════

    private float _mainMusicTargetVol = 1f;
    private float _tenseMusicTargetVol = 1f;
    private float _openRoomMusicTargetVol = 1f;

    private float _defaultLoopVol       = 1f;
    private float _defaultHeartbeatVol  = 1f;

    // Master volume scale (used by blackout fade — multiplied against target vols)
    private float _masterScale = 1f;
    private Coroutine _allAudioFadeRoutine;

    // ═══════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Store original volumes set in Inspector to use as max limits
        if (mainMusicSource     != null) _mainMusicTargetVol     = mainMusicSource.volume;
        if (tenseMusicSource    != null) _tenseMusicTargetVol    = tenseMusicSource.volume;
        if (openRoomMusicSource != null) _openRoomMusicTargetVol = openRoomMusicSource.volume;
        if (loopSource          != null) _defaultLoopVol         = loopSource.volume;
        if (heartbeatLoopSource != null) _defaultHeartbeatVol    = heartbeatLoopSource.volume;
    }

    void Start()
    {
        // Subscribe to anxiety changes for music state machine + negative voices
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;
            AnxietyManager.Instance.OnPanicAttackStarted += HandlePanicStarted;
            AnxietyManager.Instance.OnPanicAttackEnded   += HandlePanicEnded;
        }

        // Apply initial mixer volumes
        ApplyMixerVolumes();

        // Start main music
        SetMusicState(MusicState.Normal);
    }

    void Update()
    {
        // Detect Inspector slider changes at runtime and apply to mixer
        if (!Mathf.Approximately(masterVolume,     _prevMaster)     ||
            !Mathf.Approximately(musicVolume,      _prevMusic)      ||
            !Mathf.Approximately(sfxVolume,        _prevSfx)        ||
            !Mathf.Approximately(voiceVolume,      _prevVoice)      ||
            !Mathf.Approximately(innerVoiceVolume, _prevInnerVoice))
        {
            ApplyMixerVolumes();
        }
    }

    void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnAnxietyChanged -= HandleAnxietyChanged;
            AnxietyManager.Instance.OnPanicAttackStarted -= HandlePanicStarted;
            AnxietyManager.Instance.OnPanicAttackEnded   -= HandlePanicEnded;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Sound Effects
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Plays a one-shot sound effect (fire and forget).
    /// Player inner-voice IDs are routed to playerInnerVoiceSource (InnerVoice mixer group).
    /// </summary>
    public void PlayOneShot(SoundID id)
    {
        AudioClip clip = GetClip(id);
        float volume = GetClipVolume(id);
        if (clip == null) { LogMissing(id); return; }

        // Route player inner-voice lines through dedicated source (InnerVoice mixer group)
        if ((id == SoundID.PlayerVoiceCallBack || id == SoundID.PlayerVoiceIllusionBathroom)
             && playerInnerVoiceSource != null)
        {
            playerInnerVoiceSource.PlayOneShot(clip, volume);
            return;
        }

        sfxSource.PlayOneShot(clip, volume);
    }

    /// <summary>Gets the duration in seconds of a specific SoundID clip.</summary>
    public float GetClipLength(SoundID id)
    {
        AudioClip clip = GetClip(id);
        return clip != null ? clip.length : 0f;
    }

    /// <summary>
    /// Starts a looping sound on the loop AudioSource.
    /// Only one loop can be active at a time on the loop source.
    /// Use Stop(id) to end it.
    /// </summary>
    public void PlayLoop(SoundID id)
    {
        AudioClip clip = GetClip(id);
        if (clip == null) { LogMissing(id); return; }

        if (_currentLoop == id && loopSource.isPlaying) return; // already playing

        loopSource.clip = id == SoundID.None ? null : clip;
        loopSource.loop = true;
        
        // Adjust volume specifically based on the inspector setting
        loopSource.volume = GetClipVolume(id) * _defaultLoopVol;

        loopSource.Play();
        _currentLoop = id;
    }

    /// <summary>Stops the current looping sound if it matches the given ID.</summary>
    public void Stop(SoundID id)
    {
        if (_currentLoop != id) return;
        loopSource.Stop();
        loopSource.clip = null;
        _currentLoop = SoundID.None;
    }

    /// <summary>Stops any currently looping sound unconditionally.</summary>
    public void StopLoop()
    {
        if (loopSource != null) { loopSource.Stop(); loopSource.clip = null; }
        _currentLoop = SoundID.None;

        // Also stop heartbeat loop and restore its volume
        if (heartbeatLoopSource != null)
        {
            heartbeatLoopSource.Stop();
            heartbeatLoopSource.volume = _defaultHeartbeatVol;
        }
    }

    /// <summary>Stops all looping environmental sounds (like door knocks or judging voices).</summary>
    public void StopAllEnvironmentalLoops()
    {
        StopLoop();
        if (IllusionRoomManager.Instance != null)
        {
            IllusionRoomManager.Instance.StopVoices();
        }
    }

    /// <summary>
    /// Directly set the volume of BOTH loop sources (breathing + heartbeat).
    /// Used for gradual fade during blackout.
    /// </summary>
    public void SetLoopVolume(float volume)
    {
        if (loopSource          != null) loopSource.volume          = volume * _masterScale;
        if (heartbeatLoopSource != null) heartbeatLoopSource.volume = volume * _masterScale;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Panic Audio (Breathing + Heartbeat together)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts BOTH heavy breathing (loopSource) and heartbeat (heartbeatLoopSource) for panic.
    /// Stops any previous loop on the loop source first so they always play cleanly.
    /// </summary>
    public void PlayPanicAudio()
    {
        // --- Breathing ---
        AudioClip breathClip = GetClip(SoundID.BreathingHeavy);
        if (breathClip != null && loopSource != null)
        {
            loopSource.Stop();
            loopSource.clip   = breathClip;
            loopSource.loop   = true;
            loopSource.volume = GetClipVolume(SoundID.BreathingHeavy) * _defaultLoopVol * _masterScale;
            loopSource.Play();
            _currentLoop = SoundID.BreathingHeavy;
        }
        else LogMissing(SoundID.BreathingHeavy);

        // --- Heartbeat ---
        AudioClip heartClip = GetClip(SoundID.Heartbeat);
        if (heartClip != null && heartbeatLoopSource != null)
        {
            heartbeatLoopSource.Stop();
            heartbeatLoopSource.clip   = heartClip;
            heartbeatLoopSource.loop   = true;
            heartbeatLoopSource.volume = GetClipVolume(SoundID.Heartbeat) * _defaultHeartbeatVol * _masterScale;
            heartbeatLoopSource.Play();
        }
        else LogMissing(SoundID.Heartbeat);

        Debug.Log("[AudioManager] Panic audio started (breathing + heartbeat).");
    }

    /// <summary>
    /// Stops both breathing and heartbeat loops cleanly.
    /// </summary>
    public void StopPanicAudio()
    {
        StopLoop(); // handles both loopSource and heartbeatLoopSource
        Debug.Log("[AudioManager] Panic audio stopped.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Global Audio Fade (Blackout)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fades ALL audio sources (music, loops, voices, sfx) to targetVolume over duration.
    /// Use target=0 for blackout fade-out, target=1 for wake-up fade-in.
    /// </summary>
    public void FadeAllAudio(float targetVolume, float duration)
    {
        if (_allAudioFadeRoutine != null) StopCoroutine(_allAudioFadeRoutine);
        _allAudioFadeRoutine = StartCoroutine(FadeAllAudioRoutine(targetVolume, duration));
    }

    /// <summary>
    /// Instantly set all audio to a volume scale (0=silent, 1=full).
    /// </summary>
    public void SetAllVolumesImmediate(float scale)
    {
        _masterScale = Mathf.Clamp01(scale);
        ApplyMasterScale();
    }

    IEnumerator FadeAllAudioRoutine(float targetScale, float duration)
    {
        float startScale = _masterScale;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _masterScale = Mathf.Lerp(startScale, targetScale, Mathf.Clamp01(elapsed / duration));
            ApplyMasterScale();
            yield return null;
        }

        _masterScale = targetScale;
        ApplyMasterScale();
        _allAudioFadeRoutine = null;
    }

    void ApplyMasterScale()
    {
        // Music
        if (mainMusicSource     != null && mainMusicSource.isPlaying)
            mainMusicSource.volume     = _mainMusicTargetVol     * _masterScale;
        if (tenseMusicSource    != null && tenseMusicSource.isPlaying)
            tenseMusicSource.volume    = _tenseMusicTargetVol    * _masterScale;
        if (openRoomMusicSource != null && openRoomMusicSource.isPlaying)
            openRoomMusicSource.volume = _openRoomMusicTargetVol * _masterScale;

        // Loops
        if (loopSource          != null) loopSource.volume          = _defaultLoopVol      * _masterScale;
        if (heartbeatLoopSource != null) heartbeatLoopSource.volume = _defaultHeartbeatVol * _masterScale;

        // Voices
        if (npcVoiceSource      != null) npcVoiceSource.volume      = 1f * _masterScale;
        if (narratorVoiceSource != null) narratorVoiceSource.volume  = 1f * _masterScale;
        if (negativeVoiceSource != null) negativeVoiceSource.volume  = 1f * _masterScale;

        // Environmental sounds (IllusionRoom people judging voices)
        if (IllusionRoomManager.Instance != null)
        {
            // Access and fade the voices source indirectly via IllusionRoomManager
            AudioSource voicesSource = IllusionRoomManager.Instance.GetVoicesSource();
            if (voicesSource != null)
                voicesSource.volume = 1f * _masterScale;
        }

        // SFX
        if (sfxSource           != null) sfxSource.volume           = 1f * _masterScale;
    }

    /// <summary>
    /// Plays a one-shot on the SFX source (used internally by PanicAttackController
    /// to start the heartbeat loop as a separate channel).
    /// </summary>
    public void PlayOneShotOnSfx(SoundID id)
    {
        AudioClip clip = GetClip(id);
        if (clip == null) { LogMissing(id); return; }

        if (id == SoundID.Heartbeat && heartbeatLoopSource != null)
        {
            heartbeatLoopSource.clip = clip;
            heartbeatLoopSource.loop = true;
            heartbeatLoopSource.Play();
            return;
        }

        sfxSource.PlayOneShot(clip, GetClipVolume(id));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Voice
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Plays an NPC voice clip on the dedicated NPC voice AudioSource.
    /// Returns the clip length so callers can wait for it.
    /// </summary>
    public float PlayNpcClip(AudioClip clip)
    {
        if (clip == null) return 0f;
        if (npcVoiceSource == null) { Debug.LogWarning("[AudioManager] NPC voice source not assigned."); return 0f; }
        npcVoiceSource.Stop();
        npcVoiceSource.clip = clip;
        npcVoiceSource.Play();
        return clip.length;
    }

    /// <summary>
    /// Plays a Narrator voice clip on the dedicated Narrator voice AudioSource.
    /// Returns the clip length so callers can await it.
    /// </summary>
    public float PlayNarratorClip(AudioClip clip)
    {
        if (clip == null) return 0f;
        if (narratorVoiceSource == null) { Debug.LogWarning("[AudioManager] Narrator voice source not assigned."); return 0f; }
        narratorVoiceSource.Stop();
        narratorVoiceSource.clip = clip;
        narratorVoiceSource.Play();
        return clip.length;
    }

    /// <summary>True if the Narrator is currently speaking.</summary>
    public bool IsNarratorSpeaking => narratorVoiceSource != null && narratorVoiceSource.isPlaying;

    /// <summary>True if the NPC voice is currently playing.</summary>
    public bool IsNpcSpeaking => npcVoiceSource != null && npcVoiceSource.isPlaying;

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Volume
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the master volume scale (0 to 1). Hook this up to a UI Slider's OnValueChanged event!
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyMixerVolumes();
    }

    /// <summary>
    /// Fade the looping sound source volume over time.
    /// Used by PanicAttackController to ramp breathing/heartbeat intensity.
    /// </summary>
    public void FadeLoopVolume(float targetVolume, float duration)
    {
        StartCoroutine(FadeVolumeRoutine(loopSource, targetVolume, duration));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Music
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Switch to a new music state, cross-fading tracks as needed.
    /// </summary>
    public void SetMusicState(MusicState state)
    {
        if (_musicState == state) return;
        _musicState = state;

        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(MusicTransitionRoutine(state));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ANXIETY CALLBACKS
    // ═══════════════════════════════════════════════════════════════════════

    void HandleAnxietyChanged(float value)
    {
        if (AnxietyManager.Instance == null) return;
        float normalized = AnxietyManager.Instance.NormalizedAnxiety;

        // Music state machine
        if (_musicState != MusicState.Open) // don't override open room music
        {
            if (normalized >= TenseMusicThreshold && _musicState != MusicState.Tense)
                SetMusicState(MusicState.Tense);
            else if (normalized < TenseMusicThreshold && _musicState == MusicState.Tense)
                SetMusicState(MusicState.Normal);
        }

        // Negative voices — start scheduler above threshold, stop below
        if (normalized >= TenseMusicThreshold && !_negativeVoicesActive)
            StartNegativeVoices();
        else if (normalized < TenseMusicThreshold && _negativeVoicesActive)
            StopNegativeVoices();
    }

    void HandlePanicStarted()
    {
        // During panic, ensure negative voices are running and music is tense
        if (!_negativeVoicesActive) StartNegativeVoices();
    }

    void HandlePanicEnded()
    {
        // After panic, let HandleAnxietyChanged decide naturally based on new value
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  NEGATIVE VOICE SCHEDULER
    // ═══════════════════════════════════════════════════════════════════════

    void StartNegativeVoices()
    {
        if (_negativeVoicesActive) return;
        _negativeVoicesActive = true;
        _negativeVoiceRoutine = StartCoroutine(NegativeVoiceRoutine());
        Debug.Log("[AudioManager] Negative voices started.");
    }

    void StopNegativeVoices()
    {
        _negativeVoicesActive = false;
        if (_negativeVoiceRoutine != null) { StopCoroutine(_negativeVoiceRoutine); _negativeVoiceRoutine = null; }
        if (negativeVoiceSource != null) negativeVoiceSource.Stop();
        Debug.Log("[AudioManager] Negative voices stopped.");
    }

    IEnumerator NegativeVoiceRoutine()
    {
        // Initial short delay before first voice
        yield return new WaitForSeconds(2f);

        while (_negativeVoicesActive)
        {
            // Play a random clip
            if (negativeVoiceClips != null && negativeVoiceClips.Length > 0 && negativeVoiceSource != null)
            {
                AudioClip clip = negativeVoiceClips[Random.Range(0, negativeVoiceClips.Length)];
                if (clip != null)
                {
                    negativeVoiceSource.PlayOneShot(clip, negativeVoiceVolume);
                    Debug.Log($"[AudioManager] Negative voice played: {clip.name}");
                }
            }

            // Interval shrinks as anxiety rises (more frequent at high anxiety)
            float normalized = AnxietyManager.Instance != null ? AnxietyManager.Instance.NormalizedAnxiety : 0.5f;
            float t = Mathf.Clamp01((normalized - TenseMusicThreshold) / (1f - TenseMusicThreshold));
            float interval = Mathf.Lerp(negativeVoiceMinInterval, negativeVoiceMaxFreqInterval, t);

            // Add a little randomness so it never feels mechanical
            interval += Random.Range(-interval * 0.2f, interval * 0.2f);

            yield return new WaitForSeconds(Mathf.Max(1f, interval));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  MUSIC TRANSITION
    // ═══════════════════════════════════════════════════════════════════════

    IEnumerator MusicTransitionRoutine(MusicState state)
    {
        // Determine which source becomes active
        AudioSource incoming = state switch
        {
            MusicState.Normal  => mainMusicSource,
            MusicState.Tense   => tenseMusicSource,
            MusicState.Open    => openRoomMusicSource,
            _                  => null
        };

        // Collect all music sources to fade out
        AudioSource[] allMusic = { mainMusicSource, tenseMusicSource, openRoomMusicSource };

        // Start incoming source (at vol 0 if cross-fading)
        if (incoming != null)
        {
            incoming.loop = (state != MusicState.Open); // open room plays once
            
            if (!incoming.isPlaying)
            {
                incoming.volume = 0f;
                incoming.Play();
            }
        }

        float elapsed = 0f;
        float startVol = incoming != null ? incoming.volume : 0f;
        float targetVol = incoming != null ? GetTargetVolume(incoming) : 1f;

        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / musicFadeDuration);

            foreach (var src in allMusic)
            {
                if (src == null) continue;
                if (src == incoming)
                    src.volume = Mathf.Lerp(startVol, targetVol, t);
                else
                    src.volume = Mathf.Lerp(src.volume, 0f, t);
            }

            yield return null;
        }

        // Snap & stop inactive sources
        foreach (var src in allMusic)
        {
            if (src == null) continue;
            if (src == incoming)
                src.volume = targetVol;
            else
            {
                src.volume = 0f;
                src.Stop();
            }
        }

        Debug.Log($"[AudioManager] Music → {state}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    float GetTargetVolume(AudioSource src)
    {
        if (src == mainMusicSource) return _mainMusicTargetVol;
        if (src == tenseMusicSource) return _tenseMusicTargetVol;
        if (src == openRoomMusicSource) return _openRoomMusicTargetVol;
        return 1f;
    }

    AudioClip GetClip(SoundID id) => id switch
    {
        SoundID.DoorOpen                    => doorOpenClip,
        SoundID.DoorClose                   => doorCloseClip,
        SoundID.ButtonClick                 => buttonClickClip,
        SoundID.RightAnswer                 => rightAnswerClip,
        SoundID.WrongAnswer                 => wrongAnswerClip,
        SoundID.CellphoneRing               => cellphoneRingClip,
        SoundID.OfficephoneRing             => officephoneRingClip,
        SoundID.CallCutOff                  => callCutOffClip,
        SoundID.CallbackUnavailable         => callbackUnavailableClip,
        SoundID.PaperSound                  => paperSoundClip,
        SoundID.DoorKnock                   => doorKnockClip,
        SoundID.BreathingHeavy              => breathingHeavyClip,
        SoundID.Inhale                      => inhaleClip,
        SoundID.Exhale                      => exhaleClip,
        SoundID.Heartbeat                   => heartbeatClip,
        SoundID.PeopleJudging               => peopleJudgingClip,
        SoundID.PlayerVoiceCallBack         => playerVoiceCallBackClip,
        SoundID.PlayerVoiceIllusionBathroom => playerVoiceIllusionBathroomClip,
        _                                   => null
    };

    float GetClipVolume(SoundID id) => id switch
    {
        SoundID.DoorOpen                    => doorOpenVolume,
        SoundID.DoorClose                   => doorCloseVolume,
        SoundID.ButtonClick                 => buttonClickVolume,
        SoundID.RightAnswer                 => rightAnswerVolume,
        SoundID.WrongAnswer                 => wrongAnswerVolume,
        SoundID.CellphoneRing               => phoneRingVolume,
        SoundID.OfficephoneRing             => phoneRingVolume,
        SoundID.CallCutOff                  => callCutOffVolume,
        SoundID.CallbackUnavailable         => callbackUnavailableVolume,
        SoundID.PaperSound                  => paperSoundVolume,
        SoundID.DoorKnock                   => doorKnockVolume,
        SoundID.BreathingHeavy              => breathingHeavyVolume,
        SoundID.Inhale                      => inhaleVolume,
        SoundID.Exhale                      => exhaleVolume,
        SoundID.Heartbeat                   => heartbeatVolume,
        SoundID.PeopleJudging               => peopleJudgingVolume,
        SoundID.PlayerVoiceCallBack         => playerVoiceCallBackVolume,
        SoundID.PlayerVoiceIllusionBathroom => playerVoiceIllusionBathroomVolume,
        _                                   => 1f
    };

    IEnumerator FadeVolumeRoutine(AudioSource src, float target, float duration)
    {
        if (src == null) yield break;
        float start   = src.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed   += Time.deltaTime;
            src.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        src.volume = target;
    }

    void ApplyMixerVolumes()
    {
        _prevMaster     = masterVolume;
        _prevMusic      = musicVolume;
        _prevSfx        = sfxVolume;
        _prevVoice      = voiceVolume;
        _prevInnerVoice = innerVoiceVolume;

        // AudioMixer volumes are in decibels. Convert linear 0-1 to dB.
        // Clamp to avoid log(0). -80dB is effectively silent.
        if (audioMixer == null) return;

        audioMixer.SetFloat("MasterVol",     LinearToDecibel(masterVolume));
        audioMixer.SetFloat("MusicVol",      LinearToDecibel(musicVolume      * masterVolume));
        audioMixer.SetFloat("SFXVol",        LinearToDecibel(sfxVolume        * masterVolume));
        audioMixer.SetFloat("VoiceVol",      LinearToDecibel(voiceVolume      * masterVolume));
        audioMixer.SetFloat("InnerVoiceVol", LinearToDecibel(innerVoiceVolume * masterVolume));
    }

    static float LinearToDecibel(float linear)
    {
        return linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f;
    }

    void LogMissing(SoundID id) =>
        Debug.LogWarning($"[AudioManager] Clip not assigned for SoundID.{id}. " +
                         "Assign it in the AudioManager Inspector.");

    // ═══════════════════════════════════════════════════════════════════════
    //  EDITOR TEST HELPERS
    // ═══════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Test: Play DoorOpen")]        void T_DoorOpen()         => PlayOneShot(SoundID.DoorOpen);
    [ContextMenu("Test: Play DoorClose")]       void T_DoorClose()        => PlayOneShot(SoundID.DoorClose);
    [ContextMenu("Test: Play ButtonClick")]     void T_Button()           => PlayOneShot(SoundID.ButtonClick);
    [ContextMenu("Test: Play RightAnswer")]     void T_Right()            => PlayOneShot(SoundID.RightAnswer);
    [ContextMenu("Test: Play WrongAnswer")]     void T_Wrong()            => PlayOneShot(SoundID.WrongAnswer);
    [ContextMenu("Test: Play CellphoneRing")]   void T_CellRing()         => PlayLoop(SoundID.CellphoneRing);
    [ContextMenu("Test: Play OfficephoneRing")] void T_OfficeRing()       => PlayLoop(SoundID.OfficephoneRing);
    [ContextMenu("Test: Play CallCutOff")]      void T_CutOff()           => PlayOneShot(SoundID.CallCutOff);
    [ContextMenu("Test: Play CallbackUnavail")] void T_CallbackUnavail()  => PlayOneShot(SoundID.CallbackUnavailable);
    [ContextMenu("Test: Play PaperSound")]      void T_Paper()            => PlayOneShot(SoundID.PaperSound);
    [ContextMenu("Test: Play DoorKnock")]       void T_Knock()            => PlayOneShot(SoundID.DoorKnock);
    [ContextMenu("Test: Play BreathingHeavy")]  void T_HeavyBreath()      => PlayLoop(SoundID.BreathingHeavy);
    [ContextMenu("Test: Play Inhale")]          void T_Inhale()           => PlayOneShot(SoundID.Inhale);
    [ContextMenu("Test: Play Exhale")]          void T_Exhale()           => PlayOneShot(SoundID.Exhale);
    [ContextMenu("Test: Play Heartbeat")]       void T_Heart()            => PlayLoop(SoundID.Heartbeat);
    [ContextMenu("Test: Play PeopleJudging")]   void T_Judging()          => PlayOneShot(SoundID.PeopleJudging);
    [ContextMenu("Test: Stop Loop")]            void T_StopLoop()         => StopLoop();
    [ContextMenu("Test: Music → Normal")]       void T_MusicNormal()      => SetMusicState(MusicState.Normal);
    [ContextMenu("Test: Music → Tense")]        void T_MusicTense()       => SetMusicState(MusicState.Tense);
    [ContextMenu("Test: Music → Open Room")]    void T_MusicOpen()        => SetMusicState(MusicState.Open);
    [ContextMenu("Test: Music → Stopped")]      void T_MusicStop()        => SetMusicState(MusicState.Stopped);
    [ContextMenu("Test: Start Negative Voices")]void T_NegVoiceStart()    => StartNegativeVoices();
    [ContextMenu("Test: Stop Negative Voices")] void T_NegVoiceStop()     => StopNegativeVoices();
#endif
}

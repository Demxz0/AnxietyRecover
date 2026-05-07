using System.Collections;
using UnityEngine;

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
    [SerializeField] private AudioClip doorCloseClip;

    [Header("UI / Puzzle Sounds")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip rightAnswerClip;
    [SerializeField] private AudioClip wrongAnswerClip;

    [Header("Phone Sounds")]
    [SerializeField] private AudioClip cellphoneRingClip;
    [SerializeField] private AudioClip officephoneRingClip;
    [SerializeField] private AudioClip callCutOffClip;
    [SerializeField] private AudioClip callbackUnavailableClip;

    [Header("Environment Sounds")]
    [SerializeField] private AudioClip paperSoundClip;
    [SerializeField] private AudioClip doorKnockClip;

    [Header("Breathing / Panic Sounds")]
    [SerializeField] private AudioClip breathingHeavyClip;
    [SerializeField] private AudioClip inhaleClip;
    [SerializeField] private AudioClip exhaleClip;
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private AudioClip peopleJudgingClip;

    [Header("Voice")]
    [Tooltip("AudioSource used for NPC voice clips (assigned at runtime via PlayNpcClip).")]
    [SerializeField] private AudioSource npcVoiceSource;

    [Tooltip("AudioSource used for Narrator voice clips (assigned at runtime via PlayNarratorClip).")]
    [SerializeField] private AudioSource narratorVoiceSource;

    [Header("Negative Voices")]
    [Tooltip("A pool of negative inner-voice clips. A random one is chosen each time.")]
    [SerializeField] private AudioClip[] negativeVoiceClips;
    [SerializeField] private AudioSource negativeVoiceSource;

    [Tooltip("Minimum seconds between negative voice triggers at 50% anxiety.")]
    [SerializeField] private float negativeVoiceMinInterval = 12f;
    [Tooltip("Minimum seconds between negative voice triggers at 100% anxiety.")]
    [SerializeField] private float negativeVoiceMaxFreqInterval = 3f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Music
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Music")]
    [SerializeField] private AudioSource mainMusicSource;
    [SerializeField] private AudioSource tenseMusicSource;
    [SerializeField] private AudioSource openRoomMusicSource;

    [Tooltip("Seconds to cross-fade between music tracks.")]
    [SerializeField] private float musicFadeDuration = 2.5f;

    [Tooltip("Anxiety normalized value (0–1) at which music switches to tense. 0.5 = 50%.")]
    [SerializeField] [Range(0f, 1f)] private float tenseMusicThreshold = 0.5f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — General AudioSource (for one-shots)
    // ═══════════════════════════════════════════════════════════════════════

    [Header("General AudioSource (one-shots & loops)")]
    [Tooltip("A generic AudioSource for playing one-shot sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("AudioSource for looping sounds (breathing heavy, ringing, etc.).")]
    [SerializeField] private AudioSource loopSource;

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

    // ═══════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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

        // Start main music
        SetMusicState(MusicState.Normal);
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

    /// <summary>Plays a one-shot sound effect (fire and forget).</summary>
    public void PlayOneShot(SoundID id)
    {
        AudioClip clip = GetClip(id);
        if (clip == null) { LogMissing(id); return; }
        sfxSource.PlayOneShot(clip);
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
        loopSource.Stop();
        loopSource.clip = null;
        _currentLoop = SoundID.None;

        // Also stop heartbeat loop
        if (heartbeatLoopSource != null) heartbeatLoopSource.Stop();
    }

    /// <summary>
    /// Directly set the volume of the loop source (used for fade-out during blackout).
    /// </summary>
    public void SetLoopVolume(float volume)
    {
        if (loopSource != null) loopSource.volume = volume;
        if (heartbeatLoopSource != null) heartbeatLoopSource.volume = volume;
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

        sfxSource.PlayOneShot(clip);
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
            if (normalized >= tenseMusicThreshold && _musicState != MusicState.Tense)
                SetMusicState(MusicState.Tense);
            else if (normalized < tenseMusicThreshold && _musicState == MusicState.Tense)
                SetMusicState(MusicState.Normal);
        }

        // Negative voices — start scheduler above threshold, stop below
        if (normalized >= tenseMusicThreshold && !_negativeVoicesActive)
            StartNegativeVoices();
        else if (normalized < tenseMusicThreshold && _negativeVoicesActive)
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
                    negativeVoiceSource.PlayOneShot(clip);
                    Debug.Log($"[AudioManager] Negative voice played: {clip.name}");
                }
            }

            // Interval shrinks as anxiety rises (more frequent at high anxiety)
            float normalized = AnxietyManager.Instance != null ? AnxietyManager.Instance.NormalizedAnxiety : 0.5f;
            float t = Mathf.Clamp01((normalized - tenseMusicThreshold) / (1f - tenseMusicThreshold));
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
        if (incoming != null && !incoming.isPlaying)
        {
            incoming.volume = 0f;
            incoming.loop   = (state != MusicState.Open); // open room plays once
            incoming.Play();
        }

        float elapsed = 0f;
        float startVol = incoming != null ? incoming.volume : 0f;

        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / musicFadeDuration);

            foreach (var src in allMusic)
            {
                if (src == null) continue;
                if (src == incoming)
                    src.volume = Mathf.Lerp(startVol, 1f, t);
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
                src.volume = 1f;
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

    AudioClip GetClip(SoundID id) => id switch
    {
        SoundID.DoorOpen              => doorOpenClip,
        SoundID.DoorClose             => doorCloseClip,
        SoundID.ButtonClick           => buttonClickClip,
        SoundID.RightAnswer           => rightAnswerClip,
        SoundID.WrongAnswer           => wrongAnswerClip,
        SoundID.CellphoneRing         => cellphoneRingClip,
        SoundID.OfficephoneRing       => officephoneRingClip,
        SoundID.CallCutOff            => callCutOffClip,
        SoundID.CallbackUnavailable   => callbackUnavailableClip,
        SoundID.PaperSound            => paperSoundClip,
        SoundID.DoorKnock             => doorKnockClip,
        SoundID.BreathingHeavy        => breathingHeavyClip,
        SoundID.Inhale                => inhaleClip,
        SoundID.Exhale                => exhaleClip,
        SoundID.Heartbeat             => heartbeatClip,
        SoundID.PeopleJudging         => peopleJudgingClip,
        _                             => null
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

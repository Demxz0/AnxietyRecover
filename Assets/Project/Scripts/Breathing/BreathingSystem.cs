using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-driven breathing system — nothing happens until the player acts.
///
/// The cycle is entirely controlled by the player's input:
///   1. Hold F for 3 seconds (inhale) — circle expands as they hold
///   2. Release F, then hold G for 3 seconds (exhale) — circle shrinks
///   3. During each phase, the heart icon lights up twice — player must right-click
///
/// Any mistake resets the cycle:
///   • Releasing the key before the phase completes
///   • Pressing the wrong key (G during inhale, F during exhale)
///   • Missing a heartbeat right-click window
///
/// Success:
///   • 2 complete cycles → reduce anxiety by one level
///   • 5 complete cycles during panic → call CalmDown()
/// </summary>
public class BreathingSystem : MonoBehaviour
{
    public static BreathingSystem Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Timing
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Cycle Timing")]
    [Tooltip("How long the player must hold the inhale key (seconds).")]
    [SerializeField] private float inhaleDuration = 3f;

    [Tooltip("How long the player must hold the exhale key (seconds).")]
    [SerializeField] private float exhaleDuration = 3f;

    [Tooltip("Grace period after inhale completes for the player to start exhale (seconds).")]
    [SerializeField] private float transitionGrace = 1.5f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Input Keys
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Input Keys")]
    [Tooltip("Key to hold during inhale.")]
    [SerializeField] private Key inhaleKey = Key.F;

    [Tooltip("Key to hold during exhale.")]
    [SerializeField] private Key exhaleKey = Key.G;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Success Thresholds
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Success Thresholds")]
    [Tooltip("Consecutive successful cycles needed to reduce anxiety by one level.")]
    [SerializeField] private int cyclesToReduceAnxiety = 2;

    [Tooltip("Consecutive successful cycles needed to calm down from a panic attack.")]
    [SerializeField] private int cyclesToCalmPanic = 5;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Heartbeat Windows
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Heartbeat Windows")]
    [Tooltip("How long each heartbeat window stays open (seconds).")]
    [SerializeField] private float heartbeatWindowDuration = 1.0f;

    [Tooltip("When the 1st heartbeat window opens, as fraction of phase duration (0–1).")]
    [SerializeField] [Range(0f, 0.5f)] private float beat1StartFraction = 0.17f;

    [Tooltip("When the 2nd heartbeat window opens, as fraction of phase duration (0–1).")]
    [SerializeField] [Range(0.4f, 1f)] private float beat2StartFraction = 0.60f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Audio
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Audio (assign clips later — no errors if empty)")]
    [Tooltip("Played while the player inhales.")]
    [SerializeField] private AudioSource inhaleAudio;

    [Tooltip("Played while the player exhales.")]
    [SerializeField] private AudioSource exhaleAudio;

    [Tooltip("Played on each successful heartbeat right-click.")]
    [SerializeField] private AudioSource heartbeatAudio;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — References
    // ═══════════════════════════════════════════════════════════════════════

    [Header("References")]
    [SerializeField] private BreathingCircleUI circleUI;

    // ═══════════════════════════════════════════════════════════════════════
    //  STATE MACHINE
    // ═══════════════════════════════════════════════════════════════════════

    public enum State { Idle, Inhaling, WaitingForExhale, Exhaling }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC STATE (read by UI)
    // ═══════════════════════════════════════════════════════════════════════

    public State  CurrentState             => _state;
    public float  PhaseTimer               => _phaseTimer;
    public float  CurrentPhaseDuration     => (_state == State.Inhaling) ? inhaleDuration : exhaleDuration;
    public float  PhaseProgress            => (_state == State.Inhaling || _state == State.Exhaling)
                                              ? Mathf.Clamp01(_phaseTimer / CurrentPhaseDuration) : 0f;
    public bool   IsHeartbeatWindowActive  => _isWindowActive;
    public int    ActiveBeatIndex          => _activeBeatIndex;
    public int    BeatsHitThisPhase        => _beatsHitThisPhase;
    public int    ConsecutiveSuccessfulCycles => _consecutiveSuccessfulCycles;
    public int    CyclesToReduce           => cyclesToReduceAnxiety;
    public int    CyclesToCalm             => cyclesToCalmPanic;

    // ═══════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ═══════════════════════════════════════════════════════════════════════

    private State _state = State.Idle;
    private float _phaseTimer;
    private float _transitionTimer;

    // Heartbeat tracking
    private bool _isWindowActive;
    private int  _activeBeatIndex;   // 0 = none, 1 = first, 2 = second
    private int  _beatsHitThisPhase;
    private bool _beat1Hit;
    private bool _beat2Hit;
    private bool _beat1WindowPassed; // true once window 1 has closed
    private bool _beat2WindowPassed; // true once window 2 has closed

    // Cycle tracking
    private bool _inhaleCompleted;
    private int  _consecutiveSuccessfulCycles;

    // ═══════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        bool fHeld = kb[inhaleKey].isPressed;
        bool gHeld = kb[exhaleKey].isPressed;
        bool fDown = kb[inhaleKey].wasPressedThisFrame;
        bool gDown = kb[exhaleKey].wasPressedThisFrame;
        bool rightClick = mouse.rightButton.wasPressedThisFrame;

        switch (_state)
        {
            case State.Idle:
                UpdateIdle(fDown, gDown);
                break;

            case State.Inhaling:
                UpdateInhaling(fHeld, gDown, rightClick);
                break;

            case State.WaitingForExhale:
                UpdateWaitingForExhale(gDown, fDown);
                break;

            case State.Exhaling:
                UpdateExhaling(gHeld, fDown, rightClick);
                break;
        }

        UpdateAudio();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  STATE: IDLE
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateIdle(bool fDown, bool gDown)
    {
        // Player pressed F → start inhaling
        if (fDown)
        {
            EnterInhaling();
            return;
        }

        // Player pressed G without inhaling first → wrong, do nothing
        if (gDown)
        {
            Debug.Log("[BreathingSystem] ✗ Must inhale (F) before exhale (G).");
            FailCycle();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  STATE: INHALING
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateInhaling(bool fHeld, bool gDown, bool rightClick)
    {
        // Wrong key pressed during inhale → fail
        if (gDown)
        {
            Debug.Log("[BreathingSystem] ✗ Pressed exhale key during inhale — cycle reset.");
            FailCycle();
            return;
        }

        // Player released F before completing → fail
        if (!fHeld)
        {
            Debug.Log($"[BreathingSystem] ✗ Released inhale key early ({_phaseTimer:F1}s / {inhaleDuration}s) — cycle reset.");
            FailCycle();
            return;
        }

        // Advance timer
        _phaseTimer += Time.deltaTime;

        // Update heartbeat windows
        UpdateHeartbeatWindows(inhaleDuration, rightClick);

        // Check if a beat window was missed
        if (CheckMissedBeat())
        {
            Debug.Log("[BreathingSystem] ✗ Missed heartbeat window — cycle reset.");
            FailCycle();
            return;
        }

        // Inhale complete!
        if (_phaseTimer >= inhaleDuration)
        {
            if (_beatsHitThisPhase >= 2)
            {
                Debug.Log("[BreathingSystem] ✓ Inhale complete!");
                _inhaleCompleted = true;
                EnterWaitingForExhale();
            }
            else
            {
                Debug.Log("[BreathingSystem] ✗ Inhale time done but missed heartbeats — cycle reset.");
                FailCycle();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  STATE: WAITING FOR EXHALE
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateWaitingForExhale(bool gDown, bool fDown)
    {
        _transitionTimer += Time.deltaTime;

        // Player pressed G → start exhaling
        if (gDown)
        {
            EnterExhaling();
            return;
        }

        // Player pressed F again → wrong, fail
        if (fDown)
        {
            Debug.Log("[BreathingSystem] ✗ Pressed inhale key again — cycle reset.");
            FailCycle();
            return;
        }

        // Grace period expired
        if (_transitionTimer >= transitionGrace)
        {
            Debug.Log("[BreathingSystem] ✗ Too slow to start exhale — cycle reset.");
            FailCycle();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  STATE: EXHALING
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateExhaling(bool gHeld, bool fDown, bool rightClick)
    {
        // Wrong key pressed during exhale → fail
        if (fDown)
        {
            Debug.Log("[BreathingSystem] ✗ Pressed inhale key during exhale — cycle reset.");
            FailCycle();
            return;
        }

        // Player released G before completing → fail
        if (!gHeld)
        {
            Debug.Log($"[BreathingSystem] ✗ Released exhale key early ({_phaseTimer:F1}s / {exhaleDuration}s) — cycle reset.");
            FailCycle();
            return;
        }

        // Advance timer
        _phaseTimer += Time.deltaTime;

        // Update heartbeat windows
        UpdateHeartbeatWindows(exhaleDuration, rightClick);

        // Check if a beat window was missed
        if (CheckMissedBeat())
        {
            Debug.Log("[BreathingSystem] ✗ Missed heartbeat window — cycle reset.");
            FailCycle();
            return;
        }

        // Exhale complete!
        if (_phaseTimer >= exhaleDuration)
        {
            if (_beatsHitThisPhase >= 2)
            {
                Debug.Log("[BreathingSystem] ✓ Exhale complete — full cycle done!");
                CompleteCycle();
            }
            else
            {
                Debug.Log("[BreathingSystem] ✗ Exhale time done but missed heartbeats — cycle reset.");
                FailCycle();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HEARTBEAT WINDOWS
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateHeartbeatWindows(float phaseDuration, bool rightClick)
    {
        float beat1Start = phaseDuration * beat1StartFraction;
        float beat1End   = beat1Start + heartbeatWindowDuration;
        float beat2Start = phaseDuration * beat2StartFraction;
        float beat2End   = beat2Start + heartbeatWindowDuration;

        bool inWindow1 = _phaseTimer >= beat1Start && _phaseTimer < beat1End;
        bool inWindow2 = _phaseTimer >= beat2Start && _phaseTimer < beat2End;

        // Track windows closing
        if (_phaseTimer >= beat1End) _beat1WindowPassed = true;
        if (_phaseTimer >= beat2End) _beat2WindowPassed = true;

        if (inWindow1)
        {
            _isWindowActive = true;
            _activeBeatIndex = 1;
        }
        else if (inWindow2)
        {
            _isWindowActive = true;
            _activeBeatIndex = 2;
        }
        else
        {
            _isWindowActive = false;
            _activeBeatIndex = 0;
        }

        // Handle right-click during active window
        if (rightClick && _isWindowActive)
        {
            if (_activeBeatIndex == 1 && !_beat1Hit)
            {
                _beat1Hit = true;
                _beatsHitThisPhase++;
                Debug.Log("[BreathingSystem] ♥ Beat 1 hit!");
                PlayHeartbeat();
            }
            else if (_activeBeatIndex == 2 && !_beat2Hit)
            {
                _beat2Hit = true;
                _beatsHitThisPhase++;
                Debug.Log("[BreathingSystem] ♥ Beat 2 hit!");
                PlayHeartbeat();
            }
        }
    }

    /// <summary>Returns true if a beat window has closed without being hit.</summary>
    bool CheckMissedBeat()
    {
        if (_beat1WindowPassed && !_beat1Hit) return true;
        if (_beat2WindowPassed && !_beat2Hit) return true;
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  STATE TRANSITIONS
    // ═══════════════════════════════════════════════════════════════════════

    void EnterInhaling()
    {
        _state = State.Inhaling;
        ResetPhaseTracking();
        Debug.Log("[BreathingSystem] → Inhaling (hold F for 3s)");
    }

    void EnterWaitingForExhale()
    {
        _state = State.WaitingForExhale;
        _transitionTimer = 0f;
        _isWindowActive = false;
        _activeBeatIndex = 0;
    }

    void EnterExhaling()
    {
        _state = State.Exhaling;
        ResetPhaseTracking();
        Debug.Log("[BreathingSystem] → Exhaling (hold G for 3s)");
    }

    void ResetPhaseTracking()
    {
        _phaseTimer = 0f;
        _beatsHitThisPhase = 0;
        _beat1Hit = false;
        _beat2Hit = false;
        _beat1WindowPassed = false;
        _beat2WindowPassed = false;
        _isWindowActive = false;
        _activeBeatIndex = 0;
    }

    void FailCycle()
    {
        if (_consecutiveSuccessfulCycles > 0)
            Debug.Log("[BreathingSystem] Streak reset.");

        _consecutiveSuccessfulCycles = 0;
        _inhaleCompleted = false;
        _state = State.Idle;
        ResetPhaseTracking();

        StopIfPlaying(inhaleAudio);
        StopIfPlaying(exhaleAudio);

        if (circleUI != null) circleUI.OnCycleFailed();
    }

    void CompleteCycle()
    {
        _consecutiveSuccessfulCycles++;
        _inhaleCompleted = false;
        _state = State.Idle;
        ResetPhaseTracking();

        StopIfPlaying(inhaleAudio);
        StopIfPlaying(exhaleAudio);

        Debug.Log($"[BreathingSystem] ★ Cycle complete! ({_consecutiveSuccessfulCycles} consecutive)");

        if (circleUI != null) circleUI.OnCycleSuccess();

        CheckRewards();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  REWARDS
    // ═══════════════════════════════════════════════════════════════════════

    void CheckRewards()
    {
        if (AnxietyManager.Instance == null) return;

        bool isPanic = AnxietyManager.Instance.IsPanicActive;

        // During panic: need 5 cycles to calm down
        if (isPanic && _consecutiveSuccessfulCycles >= cyclesToCalmPanic)
        {
            Debug.Log("[BreathingSystem] ★★ Panic attack calmed through breathing!");

            PanicAttackController pac = FindFirstObjectByType<PanicAttackController>();
            if (pac != null) pac.CalmDown();

            AnxietyManager.Instance.ReduceOneLevel();
            _consecutiveSuccessfulCycles = 0;
            return;
        }

        // Normal: need 2 cycles to reduce one level
        if (!isPanic && _consecutiveSuccessfulCycles >= cyclesToReduceAnxiety)
        {
            Debug.Log("[BreathingSystem] ★★ Anxiety reduced by one level through breathing!");
            AnxietyManager.Instance.ReduceOneLevel();
            _consecutiveSuccessfulCycles = 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AUDIO
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateAudio()
    {
        if (_state == State.Inhaling)
        {
            PlayLoopIfNotPlaying(inhaleAudio);
            StopIfPlaying(exhaleAudio);
        }
        else if (_state == State.Exhaling)
        {
            PlayLoopIfNotPlaying(exhaleAudio);
            StopIfPlaying(inhaleAudio);
        }
        else
        {
            StopIfPlaying(inhaleAudio);
            StopIfPlaying(exhaleAudio);
        }
    }

    void PlayHeartbeat()
    {
        if (heartbeatAudio != null && heartbeatAudio.clip != null)
            heartbeatAudio.PlayOneShot(heartbeatAudio.clip);
    }

    void PlayLoopIfNotPlaying(AudioSource src)
    {
        if (src == null || src.clip == null) return;
        if (!src.isPlaying) { src.loop = true; src.Play(); }
    }

    void StopIfPlaying(AudioSource src)
    {
        if (src == null) return;
        if (src.isPlaying) src.Stop();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EDITOR TEST HELPERS
    // ═══════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Test: Print State")]
    void TestPrintState()
    {
        Debug.Log($"State: {_state} | " +
                  $"Timer: {_phaseTimer:F1}s | " +
                  $"Window: {_isWindowActive} (beat {_activeBeatIndex}) | " +
                  $"Beats: {_beatsHitThisPhase}/2 | " +
                  $"Streak: {_consecutiveSuccessfulCycles}");
    }
#endif
}

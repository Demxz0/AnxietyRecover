using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-driven breathing system — nothing happens until the player acts.
///
/// The cycle is entirely controlled by the player's input:
///   1. Hold F for 3 seconds (inhale) — circle expands as they hold
///   2. Release F, then hold G for 3 seconds (exhale) — circle shrinks
///
/// Any mistake resets the cycle:
///   • Releasing the key before the phase completes
///   • Pressing the wrong key (G during inhale, F during exhale)
///
/// Success:
///   • 2 complete cycles → reduce anxiety by one level
///   • 5 complete cycles during panic → call CalmDown()
///
/// NOTE: The heartbeat right-click mechanic has been removed.
/// Breathing now works purely by holding F then G for the required duration.
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
    //  INSPECTOR — Audio
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Audio (assign clips later — no errors if empty)")]
    [Tooltip("Played while the player inhales.")]
    [SerializeField] private AudioSource inhaleAudio;

    [Tooltip("Played while the player exhales.")]
    [SerializeField] private AudioSource exhaleAudio;

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

    public State  CurrentState               => _state;
    public float  PhaseTimer                 => _phaseTimer;
    public float  CurrentPhaseDuration       => (_state == State.Inhaling) ? inhaleDuration : exhaleDuration;
    public float  PhaseProgress              => (_state == State.Inhaling || _state == State.Exhaling)
                                               ? Mathf.Clamp01(_phaseTimer / CurrentPhaseDuration) : 0f;
    public int    ConsecutiveSuccessfulCycles => _consecutiveSuccessfulCycles;
    public int    CyclesToReduce             => cyclesToReduceAnxiety;
    public int    CyclesToCalm               => cyclesToCalmPanic;

    // ═══════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ═══════════════════════════════════════════════════════════════════════

    private State _state = State.Idle;
    private float _phaseTimer;
    private float _transitionTimer;

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
        // Do not process breathing input while any UI canvas is open.
        if (UIInputMode.IsInUI) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        bool fHeld = kb[inhaleKey].isPressed;
        bool gHeld = kb[exhaleKey].isPressed;
        bool fDown = kb[inhaleKey].wasPressedThisFrame;
        bool gDown = kb[exhaleKey].wasPressedThisFrame;

        switch (_state)
        {
            case State.Idle:
                UpdateIdle(fDown, gDown);
                break;

            case State.Inhaling:
                UpdateInhaling(fHeld, gDown);
                break;

            case State.WaitingForExhale:
                UpdateWaitingForExhale(gDown, fDown);
                break;

            case State.Exhaling:
                UpdateExhaling(gHeld, fDown);
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

    void UpdateInhaling(bool fHeld, bool gDown)
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

        // Inhale complete!
        if (_phaseTimer >= inhaleDuration)
        {
            Debug.Log("[BreathingSystem] ✓ Inhale complete!");
            _inhaleCompleted = true;
            EnterWaitingForExhale();
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

    void UpdateExhaling(bool gHeld, bool fDown)
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

        // Exhale complete!
        if (_phaseTimer >= exhaleDuration)
        {
            Debug.Log("[BreathingSystem] ✓ Exhale complete — full cycle done!");
            CompleteCycle();
        }
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
                  $"Streak: {_consecutiveSuccessfulCycles}");
    }
#endif
}

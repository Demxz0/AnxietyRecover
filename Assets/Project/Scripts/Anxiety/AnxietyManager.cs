using System;
using System.Collections;
using UnityEngine;

public enum AnxietyLevel
{
    Calm,
    MildAnxiety,
    HighAnxiety,
    ExtremeAnxiety,
    Panic
}

public class AnxietyManager : MonoBehaviour
{
    public static AnxietyManager Instance { get; private set; }

    [Header("Anxiety Values")]
    [SerializeField] private float maxAnxiety      = 100f;
    [SerializeField] private float startingAnxiety = 0f;

    [Header("Level Thresholds (0 – 100)")]
    [Tooltip("Calm → Mild Anxiety")]
    [SerializeField] private float mildThreshold    = 20f;
    [Tooltip("Mild → High Anxiety")]
    [SerializeField] private float highThreshold    = 40f;
    [Tooltip("High → Extreme Anxiety")]
    [SerializeField] private float extremeThreshold = 65f;
    [Tooltip("Extreme → Panic level (bar turns red, but panic ATTACK not yet triggered)")]
    [SerializeField] private float panicThreshold   = 85f;

    public float MildThreshold => mildThreshold;
    public float ExtremeThreshold => extremeThreshold;
    
    public float NormalizedMildThreshold => mildThreshold / maxAnxiety;
    public float NormalizedExtremeThreshold => extremeThreshold / maxAnxiety;

    [Header("Panic Attack — Bar Triggered")]
    [Tooltip("Anxiety must reach this value to trigger a bar-based panic attack. " +
             "Medication lowers this over time.")]
    [SerializeField] private float panicAttackThreshold = 100f;

    [Header("Panic Attack — Spontaneous")]
    [Tooltip("Chance per minute (%) of a spontaneous panic attack. " +
             "This can happen even at Calm/Mild anxiety, like a real panic attack. " +
             "Keep this very low (1–3%).")]
    [SerializeField] [Range(0f, 10f)] private float spontaneousPanicChancePerMinute = 2f;

    [Tooltip("After any panic attack ends, spontaneous panics are blocked for this many seconds. " +
             "Prevents two panic attacks in quick succession.")]
    [SerializeField] private float postPanicCooldown = 240f; // 4 minutes

    [Tooltip("Seconds to wait at game start before spontaneous panics can fire. " +
             "Gives the player time to settle in.")]
    [SerializeField] private float initialGracePeriod = 60f;


    public float        AnxietyValue         => _anxietyValue;
    public float        MaxAnxiety           => maxAnxiety;
    public float        NormalizedAnxiety    => _anxietyValue / maxAnxiety;
    public AnxietyLevel CurrentLevel         => _currentLevel;
    public float        PanicAttackThreshold => panicAttackThreshold;
    public bool         IsPanicActive        => _panicActive;

    /// <summary>True if any system has blocked panic attacks (scripted event running).</summary>
    public bool IsPanicBlocked => _panicBlockCount > 0;

    // ─── Events ────────────────────────────────────────────────────────────

    /// <summary>Fires every time the anxiety value changes. Passes the new float value.</summary>
    public event Action<float> OnAnxietyChanged;

    /// <summary>Fires when the anxiety level category changes (e.g., Calm → MildAnxiety).</summary>
    public event Action<AnxietyLevel> OnLevelChanged;

    /// <summary>Fires when a panic attack begins. PanicAttackController listens to this.</summary>
    public event Action OnPanicAttackStarted;

    /// <summary>Fires when a panic attack fully ends (success or blackout). </summary>
    public event Action OnPanicAttackEnded;

    // ─── Private State ─────────────────────────────────────────────────────

    private float        _anxietyValue;
    private AnxietyLevel _currentLevel;
    private bool         _panicActive;

    /// <summary>
    /// Counter of active panic blocks. Panic fires only when this is 0.
    /// Multiple systems can block simultaneously — each must unblock independently.
    /// </summary>
    private int _panicBlockCount = 0;

    /// <summary>Time.time when the last panic attack ended. Used for cooldown.</summary>
    private float _lastPanicEndTime = float.NegativeInfinity;

    // ─── Unity Lifecycle ───────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _anxietyValue = Mathf.Clamp(startingAnxiety, 0f, maxAnxiety);
        _currentLevel = EvaluateLevel(_anxietyValue);
    }

    void Start()
    {
        StartCoroutine(SpontaneousPanicRoutine());
    }

    // ─── Public API — Anxiety ──────────────────────────────────────────────

    /// <summary>
    /// Increase anxiety by <paramref name="amount"/>.
    /// Call this from any event, trigger, or action script.
    /// Each caller should define its own amount (e.g., darkness = 3/s, failed puzzle = 15 flat).
    /// Does nothing during an active panic attack.
    /// </summary>
    public void AddAnxiety(float amount)
    {
        if (_panicActive) return;

        _anxietyValue = Mathf.Clamp(_anxietyValue + amount, 0f, maxAnxiety);
        NotifyChange();

        // Check bar-based panic threshold
        if (_anxietyValue >= panicAttackThreshold)
            TriggerPanicAttack();
    }

    /// <summary>
    /// Decrease anxiety by <paramref name="amount"/>.
    /// Call from: breathing mini-game success, puzzle solve, calming interactions.
    /// </summary>
    public void ReduceAnxiety(float amount)
    {
        _anxietyValue = Mathf.Clamp(_anxietyValue - amount, 0f, maxAnxiety);
        NotifyChange();
    }

    /// <summary>
    /// Reduces anxiety to a specific target over a given duration.
    /// Useful for fast drops after major puzzle completions or reaching safe areas.
    /// </summary>
    public void StartGradualReduction(float targetAnxiety, float duration)
    {
        StartCoroutine(GradualReductionRoutine(targetAnxiety, duration));
    }

    private IEnumerator GradualReductionRoutine(float targetAnxiety, float duration)
    {
        float startAnxiety = _anxietyValue;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _anxietyValue = Mathf.Lerp(startAnxiety, targetAnxiety, elapsed / duration);
            NotifyChange();
            yield return null;
        }
        _anxietyValue = targetAnxiety;
        NotifyChange();
    }

    /// <summary>
    /// Set anxiety to an exact value. Used by the Panic Attack blackout recovery
    /// to reset the player to a safe level when they "wake up."
    /// </summary>
    public void SetAnxiety(float value)
    {
        _anxietyValue = Mathf.Clamp(value, 0f, maxAnxiety);
        NotifyChange();
    }

    /// <summary>
    /// Drop anxiety by one full level — used by the Breathing System.
    ///   Panic → just below panicThreshold
    ///   High  → just below highThreshold
    ///   Mild  → just below mildThreshold
    ///   Calm  → stays at 0
    /// </summary>
    public void ReduceOneLevel()
    {
        float target = _currentLevel switch
        {
            AnxietyLevel.Panic          => panicThreshold   - 1f,
            AnxietyLevel.ExtremeAnxiety => extremeThreshold - 1f,
            AnxietyLevel.HighAnxiety    => highThreshold    - 1f,
            AnxietyLevel.MildAnxiety    => mildThreshold    - 1f,
            _                           => 0f
        };

        Debug.Log($"[AnxietyManager] ReduceOneLevel: {_currentLevel} ({_anxietyValue:F0}) → {target:F0}");
        SetAnxiety(target);
    }

    // ─── Public API — Panic Attack ─────────────────────────────────────────

    /// <summary>
    /// Attempt to trigger a panic attack.
    /// Will silently fail if: panic is already active, or panic is blocked.
    /// </summary>
    public void TriggerPanicAttack()
    {
        if (_panicActive)
        {
            Debug.Log("[AnxietyManager] Panic already active — skipped.");
            return;
        }

        if (_panicBlockCount > 0)
        {
            Debug.Log($"[AnxietyManager] Panic is blocked ({_panicBlockCount} blockers) — skipped.");
            return;
        }

        _panicActive = true;
        Debug.Log("[AnxietyManager] ⚠ Panic attack started.");
        OnPanicAttackStarted?.Invoke();
    }

    /// <summary>
    /// Call this when a panic attack fully ends — either resolved by breathing, or blackout fail.
    /// Starts the cooldown timer for spontaneous panics.
    /// </summary>
    public void NotifyPanicAttackEnded()
    {
        _panicActive    = false;
        _lastPanicEndTime = Time.time;

        float cooldownRemaining = postPanicCooldown;
        Debug.Log($"[AnxietyManager] Panic attack ended. Spontaneous cooldown: {cooldownRemaining}s");

        OnPanicAttackEnded?.Invoke();
    }

    // ─── Public API — Panic Block ──────────────────────────────────────────

    /// <summary>
    /// Prevent panic attacks from triggering while a scripted event is running.
    /// IMPORTANT: Every BlockPanicAttack() MUST have a matching UnblockPanicAttack() call.
    /// </summary>
    public void BlockPanicAttack()
    {
        _panicBlockCount++;
        Debug.Log($"[AnxietyManager] Panic blocked. Active blocks: {_panicBlockCount}");
    }

    /// <summary>
    /// Remove one panic block. Panic attacks re-enable when block count reaches 0.
    /// </summary>
    public void UnblockPanicAttack()
    {
        _panicBlockCount = Mathf.Max(0, _panicBlockCount - 1);
        Debug.Log($"[AnxietyManager] Panic unblocked. Active blocks: {_panicBlockCount}");
    }

    // ─── Public API — Medication ───────────────────────────────────────────

    /// <summary>
    /// Medication penalty: lowers the bar threshold so future panic attacks come sooner.
    /// </summary>
    public void LowerPanicThreshold(float amount)
    {
        panicAttackThreshold = Mathf.Max(20f, panicAttackThreshold - amount);
        Debug.Log($"[AnxietyManager] Panic threshold lowered to {panicAttackThreshold}");
    }

    // ─── Private ───────────────────────────────────────────────────────────

    void NotifyChange()
    {
        OnAnxietyChanged?.Invoke(_anxietyValue);

        AnxietyLevel newLevel = EvaluateLevel(_anxietyValue);
        if (newLevel != _currentLevel)
        {
            _currentLevel = newLevel;
            OnLevelChanged?.Invoke(_currentLevel);
        }
    }

    AnxietyLevel EvaluateLevel(float value)
    {
        if (value >= panicThreshold)   return AnxietyLevel.Panic;
        if (value >= extremeThreshold) return AnxietyLevel.ExtremeAnxiety;
        if (value >= highThreshold)    return AnxietyLevel.HighAnxiety;
        if (value >= mildThreshold)    return AnxietyLevel.MildAnxiety;
        return AnxietyLevel.Calm;
    }

    /// <summary>
    /// Runs in the background, checking once per minute if a spontaneous panic
    /// attack should fire. This represents real-life unexpected panic attacks
    /// that can happen regardless of current anxiety level.
    ///
    /// CONDITIONS (all must pass):
    ///   • Initial grace period has elapsed
    ///   • No panic is currently active
    ///   • No panic block is in place
    ///   • Cooldown since last panic has expired
    ///   • Random roll beats the configured chance %
    /// </summary>
    IEnumerator SpontaneousPanicRoutine()
    {
        // Wait at game start — let the player settle in
        yield return new WaitForSeconds(initialGracePeriod);

        while (true)
        {
            yield return new WaitForSeconds(60f); // evaluate once per minute

            // Gate 1: no panic active
            if (_panicActive) continue;

            // Gate 2: not blocked by a scripted event
            if (_panicBlockCount > 0) continue;

            // Gate 3: cooldown after previous panic has expired
            float timeSinceLastPanic = Time.time - _lastPanicEndTime;
            if (timeSinceLastPanic < postPanicCooldown)
            {
                float remaining = postPanicCooldown - timeSinceLastPanic;
                Debug.Log($"[AnxietyManager] Spontaneous panic on cooldown. {remaining:F0}s remaining.");
                continue;
            }

            // Gate 4: roll the dice
            float roll = UnityEngine.Random.value * 100f;
            if (roll < spontaneousPanicChancePerMinute)
            {
                Debug.Log($"[AnxietyManager] 🎲 Spontaneous panic! (rolled {roll:F1} < {spontaneousPanicChancePerMinute}%)");
                TriggerPanicAttack();
            }
        }
    }

    // ─── Editor Test Helpers ───────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Test: Add 20 Anxiety")]
    void TestAdd() => AddAnxiety(20f);

    [ContextMenu("Test: Reduce 20 Anxiety")]
    void TestReduce() => ReduceAnxiety(20f);

    [ContextMenu("Test: Force Panic Attack")]
    void TestPanic() => TriggerPanicAttack();

    [ContextMenu("Test: End Panic Attack")]
    void TestEndPanic() => NotifyPanicAttackEnded();

    [ContextMenu("Test: Block Panic")]
    void TestBlock() => BlockPanicAttack();

    [ContextMenu("Test: Unblock Panic")]
    void TestUnblock() => UnblockPanicAttack();
#endif
}

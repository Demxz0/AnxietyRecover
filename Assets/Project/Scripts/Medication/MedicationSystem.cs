using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Medication System — a tempting but costly emergency tool.
///
/// The player has a LIMITED number of pills (default: 3).
/// Each use instantly stops a panic attack and lowers anxiety,
/// but with escalating penalties:
///
///   TOLERANCE — each successive pill is LESS effective:
///     • 1st pill → anxiety drops to Calm (0)
///     • 2nd pill → anxiety drops to Mild only (~24)
///     • 3rd pill → anxiety drops to High only (~59)
///
///   THRESHOLD PENALTY — each use lowers the panic-attack threshold:
///     • Panic attacks trigger sooner (threshold: 100 → 90 → 80)
///
///   DROWSINESS — 15 seconds of:
///     • Blurred/vignetted vision
///     • Slowed movement (50% speed)
///
///   COOLDOWN — can't take another pill for 60 seconds.
///
/// SETUP:
///   1. Create a GameObject "MedicationSystem" and attach this script.
///   2. Assign references: PlayerMovement, URP Volume, drowsiness overlay Image.
///   3. Default input key: 1 (configurable).
/// </summary>
public class MedicationSystem : MonoBehaviour
{
    public static MedicationSystem Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Supply
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Supply")]
    [Tooltip("Total pills available for the entire game.")]
    [SerializeField] private int totalPills = 3;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Tolerance
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Tolerance (anxiety level after each use)")]
    [Tooltip("Anxiety value after the 1st pill (Calm).")]
    [SerializeField] private float pill1AnxietyTarget = 0f;

    [Tooltip("Anxiety value after the 2nd pill (Mild).")]
    [SerializeField] private float pill2AnxietyTarget = 24f;

    [Tooltip("Anxiety value after the 3rd pill (High — barely helps).")]
    [SerializeField] private float pill3AnxietyTarget = 59f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Penalties
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Penalties")]
    [Tooltip("How much the panic threshold drops per pill use.")]
    [SerializeField] private float thresholdPenaltyPerUse = 10f;

    [Tooltip("Cooldown between doses (seconds).")]
    [SerializeField] private float cooldownDuration = 60f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Drowsiness
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Drowsiness Effect")]
    [Tooltip("How long the drowsiness lasts after taking medication (seconds).")]
    [SerializeField] private float drowsinessDuration = 15f;

    [Tooltip("Movement speed multiplier during drowsiness (0.5 = half speed).")]
    [SerializeField] [Range(0.2f, 1f)] private float drowsinessSpeedMultiplier = 0.5f;

    [Tooltip("Vignette intensity during drowsiness (for visual blur).")]
    [SerializeField] private float drowsinessVignetteIntensity = 0.4f;

    [Tooltip("Optional overlay Image for drowsiness tint (semi-transparent dark/blue).")]
    [SerializeField] private Image drowsinessOverlay;

    [Tooltip("Color of the drowsiness overlay.")]
    [SerializeField] private Color drowsinessOverlayColor = new Color(0.02f, 0.02f, 0.1f, 0.25f);

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Input
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Input")]
    [Tooltip("Key to take medication.")]
    [SerializeField] private Key medicationKey = Key.Digit1;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — References
    // ═══════════════════════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("The player's movement script (for speed reduction during drowsiness).")]
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("The URP Volume (same one used by PanicAttackController) for vignette during drowsiness.")]
    [SerializeField] private Volume postProcessVolume;

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC STATE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Pills remaining.</summary>
    public int PillsRemaining => _pillsRemaining;

    /// <summary>Total pills the player started with.</summary>
    public int TotalPills => totalPills;

    /// <summary>How many pills have been used (0, 1, 2, 3).</summary>
    public int PillsUsed => totalPills - _pillsRemaining;

    /// <summary>True while the cooldown is active (can't take another pill).</summary>
    public bool IsOnCooldown => _cooldownTimer > 0f;

    /// <summary>Remaining cooldown time in seconds.</summary>
    public float CooldownRemaining => _cooldownTimer;

    /// <summary>True while drowsiness effect is active.</summary>
    public bool IsDrowsy => _isDrowsy;

    // ═══════════════════════════════════════════════════════════════════════
    //  EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Fires when a pill is taken. Passes pills remaining.</summary>
    public event System.Action<int> OnPillTaken;

    /// <summary>
    /// Fires the first time the player picks up the pills container in the start room.
    /// The MedicationHUD listens to this to show itself.
    /// </summary>
    public event System.Action OnUnlocked;

    // ═══════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ═══════════════════════════════════════════════════════════════════════

    private int   _pillsRemaining;
    private float _cooldownTimer;
    private bool  _isDrowsy;
    private bool  _isUnlocked;          // false until the pills container is picked up
    private Vignette _vignette;
    private Coroutine _drowsinessRoutine;
    private float _baseVignetteIntensity; // store pre-drowsiness vignette

    // ═══════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _pillsRemaining = totalPills;

        // Cache vignette reference
        if (postProcessVolume != null && postProcessVolume.profile != null)
            postProcessVolume.profile.TryGet(out _vignette);

        // Start drowsiness overlay hidden
        if (drowsinessOverlay != null)
            SetOverlayAlpha(0f);
    }

    void Update()
    {
        // Cooldown timer
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        // Block input until the pills container has been picked up
        if (!_isUnlocked) return;

        // Input check — do not consume medication while a canvas is open
        Keyboard kb = Keyboard.current;
        if (kb != null && !UIInputMode.IsInUI && kb[medicationKey].wasPressedThisFrame)
            TryTakeMedication();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by PillsContainerPickup when the player picks up the pills container
    /// for the first time. Activates the medication system and shows the HUD.
    /// Safe to call multiple times — only unlocks once.
    /// </summary>
    public void Unlock()
    {
        if (_isUnlocked) return;
        _isUnlocked = true;
        Debug.Log("[MedicationSystem] Unlocked — pills container picked up.");
        OnUnlocked?.Invoke();
    }

    /// <summary>True once the pills container has been picked up.</summary>
    public bool IsUnlocked => _isUnlocked;

    /// <summary>
    /// Attempt to take medication. Called by input or by other systems.
    /// Returns true if a pill was successfully taken.
    /// </summary>
    public bool TryTakeMedication()
    {
        // ── Guard checks ─────────────────────────────────────────────────
        if (_pillsRemaining <= 0)
        {
            Debug.Log("[MedicationSystem] ✗ No pills remaining.");
            return false;
        }

        if (_cooldownTimer > 0f)
        {
            Debug.Log($"[MedicationSystem] ✗ Cooldown active ({_cooldownTimer:F0}s remaining).");
            return false;
        }

        // ── Take the pill ────────────────────────────────────────────────
        _pillsRemaining--;
        int pillNumber = totalPills - _pillsRemaining; // 1st, 2nd, or 3rd
        _cooldownTimer = cooldownDuration;

        Debug.Log($"[MedicationSystem] 💊 Pill #{pillNumber} taken. {_pillsRemaining} remaining.");

        // ── Effect 1: Stop panic attack ──────────────────────────────────
        if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsPanicActive)
        {
            PanicAttackController pac = FindFirstObjectByType<PanicAttackController>();
            if (pac != null) pac.CalmDown();
            Debug.Log("[MedicationSystem] → Panic attack stopped.");
        }

        // ── Effect 2: Lower anxiety (with tolerance) ─────────────────────
        float targetAnxiety = GetToleranceTarget(pillNumber);
        if (AnxietyManager.Instance != null)
        {
            float before = AnxietyManager.Instance.AnxietyValue;
            AnxietyManager.Instance.SetAnxiety(targetAnxiety);
            Debug.Log($"[MedicationSystem] → Anxiety: {before:F0} → {targetAnxiety:F0} (tolerance level {pillNumber})");
        }

        // ── Penalty: Drowsiness ──────────────────────────────────────────
        if (_drowsinessRoutine != null)
            StopCoroutine(_drowsinessRoutine);
        _drowsinessRoutine = StartCoroutine(DrowsinessEffect());

        // ── Notify ───────────────────────────────────────────────────────
        OnPillTaken?.Invoke(_pillsRemaining);

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TOLERANCE
    // ═══════════════════════════════════════════════════════════════════════

    float GetToleranceTarget(int pillNumber)
    {
        return pillNumber switch
        {
            1 => pill1AnxietyTarget,  // Calm
            2 => pill2AnxietyTarget,  // Mild
            3 => pill3AnxietyTarget,  // High — barely helps
            _ => pill3AnxietyTarget   // any extras (if totalPills > 3)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DROWSINESS EFFECT
    // ═══════════════════════════════════════════════════════════════════════

    IEnumerator DrowsinessEffect()
    {
        _isDrowsy = true;
        Debug.Log($"[MedicationSystem] 😵 Drowsiness started ({drowsinessDuration}s)");

        // Save current vignette intensity
        _baseVignetteIntensity = (_vignette != null) ? _vignette.intensity.value : 0f;

        // ── Ramp IN (1 second) ───────────────────────────────────────────
        float rampDuration = 1f;
        float elapsed = 0f;
        while (elapsed < rampDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / rampDuration);
            ApplyDrowsiness(t);
            yield return null;
        }
        ApplyDrowsiness(1f);

        // ── Hold ─────────────────────────────────────────────────────────
        yield return new WaitForSeconds(drowsinessDuration - 2f); // subtract ramp in/out

        // ── Ramp OUT (1 second) ──────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < rampDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.SmoothStep(0f, 1f, elapsed / rampDuration);
            ApplyDrowsiness(t);
            yield return null;
        }
        ApplyDrowsiness(0f);

        _isDrowsy = false;
        Debug.Log("[MedicationSystem] Drowsiness ended.");
    }

    /// <summary>Apply drowsiness effects at intensity t (0–1).</summary>
    void ApplyDrowsiness(float t)
    {
        // Slow movement
        if (playerMovement != null)
            playerMovement.SpeedMultiplier = Mathf.Lerp(1f, drowsinessSpeedMultiplier, t);

        // Vignette
        if (_vignette != null)
        {
            float vignetteTarget = Mathf.Lerp(_baseVignetteIntensity, drowsinessVignetteIntensity, t);
            _vignette.intensity.Override(vignetteTarget);
        }

        // Overlay
        SetOverlayAlpha(drowsinessOverlayColor.a * t);
    }

    void SetOverlayAlpha(float alpha)
    {
        if (drowsinessOverlay == null) return;
        Color c = drowsinessOverlayColor;
        drowsinessOverlay.color = new Color(c.r, c.g, c.b, alpha);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EDITOR TEST HELPERS
    // ═══════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Test: Take Medication")]
    void TestTake() => TryTakeMedication();

    [ContextMenu("Test: Reset Pills")]
    void TestReset()
    {
        _pillsRemaining = totalPills;
        _cooldownTimer = 0f;
        Debug.Log("[MedicationSystem] Pills reset for testing.");
    }
#endif
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Always-visible breathing circle UI — stays still until the player acts.
///
/// Circle only moves when the player holds the correct key:
///   • Inhaling (F held): circle expands proportionally to hold time
///   • Exhaling (G held): circle shrinks proportionally to hold time
///   • Idle: circle sits at rest (minScale)
///
/// NOTE: The heartbeat mechanic and heart icon have been removed.
/// </summary>
public class BreathingCircleUI : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Circle
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Circle")]
    [Tooltip("The circle Image that scales with breathing.")]
    [SerializeField] private RectTransform circleRect;

    [Tooltip("The Image component of the circle (for color changes).")]
    [SerializeField] private Image circleImage;

    [Tooltip("How much smaller than the editor size the circle shrinks to at exhale end. (1 = no change)")]
    [SerializeField] private float minScale = 1f;

    [Tooltip("How much larger than the editor size the circle grows to at full inhale.")]
    [SerializeField] private float maxScale = 1.4f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Labels
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Labels")]
    [Tooltip("Shows 'شهيق' / 'زفير' / idle text.")]
    [SerializeField] private TextMeshProUGUI phaseLabel;

    [Tooltip("Shows the current key hint.")]
    [SerializeField] private TextMeshProUGUI keyHintLabel;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Colors
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Circle Colors")]
    [SerializeField] private Color idleColor     = new Color(0.7f, 0.7f, 0.7f, 0.4f);
    [SerializeField] private Color inhaleColor   = new Color(0.3f, 0.6f, 1f, 0.8f);
    [SerializeField] private Color exhaleColor   = new Color(0.3f, 0.9f, 0.5f, 0.8f);
    [SerializeField] private Color waitColor     = new Color(0.5f, 0.8f, 1f, 0.6f);
    [SerializeField] private Color successColor  = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private Color failColor     = new Color(1f, 0.2f, 0.2f, 1f);

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Streak
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Streak Display")]
    [SerializeField] private TextMeshProUGUI streakLabel;

    // ═══════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ═══════════════════════════════════════════════════════════════════════

    private float _flashTimer;
    private bool  _isFlashing;

    /// <summary>The localScale read from the editor at Start. Used as the idle/rest size.</summary>
    private Vector3 _editorScale;

    /// <summary>The color read from the editor at Start. Restored when idle and breathing is inactive.</summary>
    private Color   _editorColor;

    private const string InhaleArabic = "شهيق";
    private const string ExhaleArabic = "زفير";
    private const string IdleArabic   = "تنفّس";  // "Breathe"

    // ═══════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    void Start()
    {
        // Remember whatever size and color were set in the editor
        if (circleRect  != null) _editorScale = circleRect.localScale;
        if (circleImage != null) _editorColor = circleImage.color;
    }

    void Update()
    {
        if (BreathingSystem.Instance == null) return;

        UpdateCircleScale();
        UpdateCircleColor();
        UpdateLabels();
        UpdateFlash();
        UpdateStreak();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CIRCLE SCALE — only moves when player is holding a key
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateCircleScale()
    {
        if (circleRect == null) return;

        var state = BreathingSystem.Instance.CurrentState;

        // Calculate the rest size as a scaled version of the editor size
        Vector3 restScale   = _editorScale * minScale;
        Vector3 expandScale = _editorScale * maxScale;

        Vector3 targetScale = restScale; // default: idle / resting

        switch (state)
        {
            case BreathingSystem.State.Inhaling:
                float inhaleProgress = BreathingSystem.Instance.PhaseProgress;
                targetScale = Vector3.Lerp(restScale, expandScale, Mathf.SmoothStep(0f, 1f, inhaleProgress));
                break;

            case BreathingSystem.State.WaitingForExhale:
                targetScale = expandScale;
                break;

            case BreathingSystem.State.Exhaling:
                float exhaleProgress = BreathingSystem.Instance.PhaseProgress;
                targetScale = Vector3.Lerp(expandScale, restScale, Mathf.SmoothStep(0f, 1f, exhaleProgress));
                break;

            default: // Idle — stay exactly at the editor-defined size
                targetScale = _editorScale;
                break;
        }

        circleRect.localScale = targetScale;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CIRCLE COLOR
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateCircleColor()
    {
        if (circleImage == null || _isFlashing) return;

        var state = BreathingSystem.Instance.CurrentState;

        // When idle, restore the original editor color — don't override it
        Color target = state switch
        {
            BreathingSystem.State.Inhaling         => inhaleColor,
            BreathingSystem.State.WaitingForExhale => waitColor,
            BreathingSystem.State.Exhaling          => exhaleColor,
            _                                       => _editorColor   // idle → keep editor color
        };

        circleImage.color = Color.Lerp(circleImage.color, target, Time.deltaTime * 8f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  LABELS
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateLabels()
    {
        var state = BreathingSystem.Instance.CurrentState;

        if (phaseLabel != null)
        {
            phaseLabel.text = state switch
            {
                BreathingSystem.State.Inhaling         => InhaleArabic,
                BreathingSystem.State.WaitingForExhale => ExhaleArabic + "...",
                BreathingSystem.State.Exhaling          => ExhaleArabic,
                _                                       => IdleArabic
            };
        }

        if (keyHintLabel != null)
        {
            keyHintLabel.text = state switch
            {
                BreathingSystem.State.Inhaling         => "F",
                BreathingSystem.State.WaitingForExhale => "G",
                BreathingSystem.State.Exhaling          => "G",
                _                                       => "F"
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  STREAK
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateStreak()
    {
        if (streakLabel == null) return;

        int streak = BreathingSystem.Instance.ConsecutiveSuccessfulCycles;
        if (streak > 0)
        {
            bool isPanic = AnxietyManager.Instance != null && AnxietyManager.Instance.IsPanicActive;
            int goal = isPanic ? BreathingSystem.Instance.CyclesToCalm : BreathingSystem.Instance.CyclesToReduce;
            streakLabel.text = $"{streak}/{goal}";
            streakLabel.gameObject.SetActive(true);
        }
        else
        {
            streakLabel.gameObject.SetActive(false);
        }
    }

    public void OnCycleSuccess()
    {
        _isFlashing = true;
        _flashTimer = 0f;
        if (circleImage != null)
            circleImage.color = successColor;
    }

    public void OnCycleFailed()
    {
        _isFlashing = true;
        _flashTimer = 0f;
        if (circleImage != null)
            circleImage.color = failColor;
    }

    void UpdateFlash()
    {
        if (!_isFlashing) return;
        _flashTimer += Time.deltaTime;
        if (_flashTimer >= 0.4f)
            _isFlashing = false;
    }
}

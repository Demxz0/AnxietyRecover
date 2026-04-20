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
/// Heart icon only activates during heartbeat windows while the player is breathing.
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

    [Tooltip("Scale of the circle at rest and at exhale end.")]
    [SerializeField] private float minScale = 0.5f;

    [Tooltip("Scale of the circle at full inhale.")]
    [SerializeField] private float maxScale = 1.2f;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Labels
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Labels")]
    [Tooltip("Shows 'شهيق' / 'زفير' / idle text.")]
    [SerializeField] private TextMeshProUGUI phaseLabel;

    [Tooltip("Shows the current key hint.")]
    [SerializeField] private TextMeshProUGUI keyHintLabel;

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Heart Icon
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Heart Icon")]
    [Tooltip("Heart icon Image — gray when inactive, colored during heartbeat windows.")]
    [SerializeField] private Image heartIcon;

    [Tooltip("Heart color when inactive (grayed out).")]
    [SerializeField] private Color heartInactiveColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

    [Tooltip("Heart color when window is ACTIVE (player should right-click now!).")]
    [SerializeField] private Color heartActiveColor = new Color(0.9f, 0.15f, 0.15f, 1f);

    [Tooltip("Heart color after a successful beat hit.")]
    [SerializeField] private Color heartHitColor = new Color(1f, 0.4f, 0.6f, 1f);

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR — Colors
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Circle Colors")]
    [SerializeField] private Color idleColor     = new Color(0.7f, 0.7f, 0.7f, 0.4f);
    [SerializeField] private Color inhaleColor   = new Color(0.3f, 0.6f, 1f, 0.8f);
    [SerializeField] private Color exhaleColor   = new Color(0.3f, 0.9f, 0.5f, 0.8f);
    [SerializeField] private Color waitColor     = new Color(0.5f, 0.8f, 1f, 0.6f);
    [SerializeField] private Color successColor  = new Color(0.2f, 1f, 0.4f, 1f);

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

    private const string InhaleArabic = "شهيق";
    private const string ExhaleArabic = "زفير";
    private const string IdleArabic   = "تنفّس";  // "Breathe"

    // ═══════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    void Update()
    {
        if (BreathingSystem.Instance == null) return;

        UpdateCircleScale();
        UpdateCircleColor();
        UpdateLabels();
        UpdateHeartIcon();
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
        float scale = minScale; // default: resting

        switch (state)
        {
            case BreathingSystem.State.Inhaling:
                // Expand proportionally to how long the player has held F
                float inhaleProgress = BreathingSystem.Instance.PhaseProgress;
                scale = Mathf.Lerp(minScale, maxScale, Mathf.SmoothStep(0f, 1f, inhaleProgress));
                break;

            case BreathingSystem.State.WaitingForExhale:
                // Stay at full size — inhale just completed
                scale = maxScale;
                break;

            case BreathingSystem.State.Exhaling:
                // Shrink proportionally to how long the player has held G
                float exhaleProgress = BreathingSystem.Instance.PhaseProgress;
                scale = Mathf.Lerp(maxScale, minScale, Mathf.SmoothStep(0f, 1f, exhaleProgress));
                break;

            default: // Idle
                scale = minScale;
                break;
        }

        circleRect.localScale = Vector3.one * scale;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CIRCLE COLOR
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateCircleColor()
    {
        if (circleImage == null || _isFlashing) return;

        var state = BreathingSystem.Instance.CurrentState;
        Color target = state switch
        {
            BreathingSystem.State.Inhaling        => inhaleColor,
            BreathingSystem.State.WaitingForExhale => waitColor,
            BreathingSystem.State.Exhaling         => exhaleColor,
            _                                      => idleColor
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
    //  HEART ICON — only active during heartbeat windows while breathing
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateHeartIcon()
    {
        if (heartIcon == null) return;

        var state = BreathingSystem.Instance.CurrentState;

        // Heart only does anything while player is actively inhaling or exhaling
        if (state != BreathingSystem.State.Inhaling && state != BreathingSystem.State.Exhaling)
        {
            heartIcon.color = Color.Lerp(heartIcon.color, heartInactiveColor, Time.deltaTime * 6f);
            return;
        }

        bool windowActive = BreathingSystem.Instance.IsHeartbeatWindowActive;
        int beatIndex = BreathingSystem.Instance.ActiveBeatIndex;
        int beatsHit = BreathingSystem.Instance.BeatsHitThisPhase;

        if (windowActive)
        {
            // Was this specific beat already hit?
            bool alreadyHit = (beatIndex == 1 && beatsHit >= 1) ||
                              (beatIndex == 2 && beatsHit >= 2);

            Color target = alreadyHit ? heartHitColor : heartActiveColor;
            heartIcon.color = Color.Lerp(heartIcon.color, target, Time.deltaTime * 12f);
        }
        else
        {
            heartIcon.color = Color.Lerp(heartIcon.color, heartInactiveColor, Time.deltaTime * 6f);
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

    // ═══════════════════════════════════════════════════════════════════════
    //  SUCCESS / FAIL FEEDBACK
    // ═══════════════════════════════════════════════════════════════════════

    public void OnCycleSuccess()
    {
        _isFlashing = true;
        _flashTimer = 0f;
        if (circleImage != null)
            circleImage.color = successColor;
    }

    public void OnCycleFailed()
    {
        // Circle smoothly returns to idle via UpdateCircleColor
    }

    void UpdateFlash()
    {
        if (!_isFlashing) return;
        _flashTimer += Time.deltaTime;
        if (_flashTimer >= 0.4f)
            _isFlashing = false;
    }
}

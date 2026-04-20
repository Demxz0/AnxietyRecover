using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Anxiety Meter UI bar.
/// Attach to a Canvas GameObject that contains the bar image and label.
/// </summary>
public class AnxietyHUD : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Bar References")]
    [Tooltip("The Image component used as the fill bar (Image Type = Filled)")]
    [SerializeField] private Image anxietyBarFill;

    [Tooltip("TextMeshPro label showing the current anxiety level in Arabic")]
    [SerializeField] private TextMeshProUGUI levelLabel;

    [Header("Level Colors")]
    [SerializeField] private Color calmColor     = new Color(0.25f, 0.60f, 1.00f); // blue
    [SerializeField] private Color mildColor     = new Color(1.00f, 0.85f, 0.20f); // yellow
    [SerializeField] private Color highColor     = new Color(1.00f, 0.45f, 0.05f); // orange
    [SerializeField] private Color panicColor    = new Color(0.85f, 0.10f, 0.10f); // red

    [Header("Pulse Animation")]
    [Tooltip("The bar pulses when anxiety is in Panic state")]
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseMinAlpha = 0.5f;

    // ─── Private ───────────────────────────────────────────────────────────

    private AnxietyLevel _currentLevel;
    private bool         _isPulsing;

    // ─── Arabic Level Names ──────────────────────────────────────────────

    private static readonly string[] LevelLabels =
    {
        "هادئ",        // Calm
        "قلق خفيف",   // Mild Anxiety
        "قلق شديد",   // High Anxiety
        "نوبة هلع"    // Panic
    };

    // ─── Unity Lifecycle ───────────────────────────────────────────────────

    void Start()
    {
        if (AnxietyManager.Instance == null)
        {
            Debug.LogError("[AnxietyHUD] AnxietyManager not found in scene!");
            return;
        }

        // Subscribe to events
        AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;
        AnxietyManager.Instance.OnLevelChanged   += HandleLevelChanged;

        // Initialize to current state
        HandleAnxietyChanged(AnxietyManager.Instance.AnxietyValue);
        HandleLevelChanged(AnxietyManager.Instance.CurrentLevel);
    }

    void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnAnxietyChanged -= HandleAnxietyChanged;
            AnxietyManager.Instance.OnLevelChanged   -= HandleLevelChanged;
        }
    }

    void Update()
    {
        if (_isPulsing && anxietyBarFill != null)
        {
            // Smoothly pulse the bar alpha during Panic state
            float alpha = Mathf.Lerp(pulseMinAlpha, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);
            Color c = anxietyBarFill.color;
            anxietyBarFill.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    // ─── Event Handlers ────────────────────────────────────────────────────

    void HandleAnxietyChanged(float value)
    {
        if (anxietyBarFill == null) return;

        float normalized = AnxietyManager.Instance.NormalizedAnxiety;
        anxietyBarFill.fillAmount = normalized;
    }

    void HandleLevelChanged(AnxietyLevel level)
    {
        _currentLevel = level;
        _isPulsing    = level == AnxietyLevel.Panic;

        // Update color
        if (anxietyBarFill != null)
        {
            Color targetColor = GetLevelColor(level);
            // Reset alpha to full when switching levels
            anxietyBarFill.color = new Color(targetColor.r, targetColor.g, targetColor.b, 1f);
        }

        // Update Arabic label
        if (levelLabel != null)
            levelLabel.text = LevelLabels[(int)level];
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    Color GetLevelColor(AnxietyLevel level)
    {
        return level switch
        {
            AnxietyLevel.Calm        => calmColor,
            AnxietyLevel.MildAnxiety => mildColor,
            AnxietyLevel.HighAnxiety => highColor,
            AnxietyLevel.Panic       => panicColor,
            _                        => calmColor
        };
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Anxiety Meter UI.
///
/// UI hierarchy expected:
///   ┌─ AnxietyHUD (this script)
///   ├─ BarFill   ← Image (Type = Filled, Fill Method = Vertical, Fill Origin = Bottom).
///   │             Its fillAmount is driven 0→1 by the current anxiety value.
///   │             Assign ONE sprite to it — the bar will grow/shrink on that single image.
///   ├─ BarIcon   ← Image (optional — the static icon beside the bar, never touched by code)
///   └─ LevelLabel← TextMeshProUGUI (optional Arabic level name)
/// </summary>
public class AnxietyHUD : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Bar Fill Image")]
    [Tooltip("Image set to Type=Filled, FillMethod=Vertical, FillOrigin=Bottom. " +
             "Its fillAmount is driven by anxiety (0=empty, 1=full). Assign ONE sprite to it.")]
    [SerializeField] private Image barFill;

    [Tooltip("TextMeshPro label showing the current anxiety level in Arabic (optional).")]
    [SerializeField] private TextMeshProUGUI levelLabel;

    [Header("Pulse Animation (Panic only)")]
    [Tooltip("Speed of the alpha pulse on the bar during Panic state.")]
    [SerializeField] private float pulseSpeed    = 3f;
    [SerializeField] private float pulseMinAlpha = 0.5f;

    // ─── Private ───────────────────────────────────────────────────────────

    private AnxietyLevel _currentLevel;
    private bool         _isPulsing;

    // ─── Arabic Level Names ──────────────────────────────────────────────

    private static readonly string[] LevelLabels =
    {
        "هادئ",          // Calm
        "قلق خفيف",     // Mild Anxiety
        "قلق شديد",     // High Anxiety
        "قلق مفرط",     // Extreme Anxiety
        "نوبة هلع"      // Panic
    };

    // ─── Unity Lifecycle ───────────────────────────────────────────────────

    void Start()
    {
        if (AnxietyManager.Instance == null)
        {
            Debug.LogError("[AnxietyHUD] AnxietyManager not found in scene!");
            return;
        }

        // Subscribe to every anxiety value change for the fill, and level change for label/pulse
        AnxietyManager.Instance.OnAnxietyChanged += HandleAnxietyChanged;
        AnxietyManager.Instance.OnLevelChanged    += HandleLevelChanged;

        // Ensure the fill image is configured correctly
        if (barFill != null)
        {
            barFill.type       = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Vertical;
            barFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        }

        // Initialize to current state
        HandleAnxietyChanged(AnxietyManager.Instance.AnxietyValue);
        HandleLevelChanged(AnxietyManager.Instance.CurrentLevel);
    }

    void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.OnAnxietyChanged -= HandleAnxietyChanged;
            AnxietyManager.Instance.OnLevelChanged    -= HandleLevelChanged;
        }
    }

    void Update()
    {
        if (_isPulsing && barFill != null)
        {
            // Pulse the bar alpha during Panic
            float alpha = Mathf.Lerp(pulseMinAlpha, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);
            Color c = barFill.color;
            barFill.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    // ─── Event Handlers ────────────────────────────────────────────────────

    /// <summary>
    /// Called every time the anxiety value changes. Drives the fill amount.
    /// </summary>
    void HandleAnxietyChanged(float value)
    {
        if (barFill == null || AnxietyManager.Instance == null) return;
        barFill.fillAmount = AnxietyManager.Instance.NormalizedAnxiety;
    }

    /// <summary>
    /// Called when the anxiety level category changes. Updates label and pulse.
    /// </summary>
    void HandleLevelChanged(AnxietyLevel level)
    {
        _currentLevel = level;
        _isPulsing    = level == AnxietyLevel.Panic;

        // Reset alpha when leaving Panic
        if (!_isPulsing && barFill != null)
        {
            Color c = barFill.color;
            barFill.color = new Color(c.r, c.g, c.b, 1f);
        }

        // Update Arabic label
        if (levelLabel != null)
            levelLabel.text = LevelLabels[(int)level];
    }
}

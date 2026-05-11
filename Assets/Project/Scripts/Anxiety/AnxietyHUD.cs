using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Anxiety Meter UI bar.
///
/// UI hierarchy expected:
///   ┌─ AnxietyHUD (this script)
///   ├─ BarFill          ← Image (Type = Filled, Bottom→Top). Assign to "Anxiety Bar Fill".
///   └─ LevelIcon        ← Image (Type = Simple, no fill). Assign to "Level Icon".
///
/// The fill bar controls fill amount only — its sprite never changes.
/// The icon image swaps its sprite per anxiety level — it is never clipped.
/// </summary>
public class AnxietyHUD : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Bar References")]
    [Tooltip("Image (Type = Filled) that shows how full the anxiety bar is. Its sprite is never changed.")]
    [SerializeField] private Image anxietyBarFill;

    [Tooltip("A plain Image (Type = Simple) shown next to/above the bar. Its sprite swaps per level.")]
    [SerializeField] private Image levelIcon;

    [Tooltip("TextMeshPro label showing the current anxiety level in Arabic")]
    [SerializeField] private TextMeshProUGUI levelLabel;

    [Header("Level Sprites")]
    [Tooltip("Sprites in order: Calm, MildAnxiety, HighAnxiety, ExtremeAnxiety, Panic")]
    [SerializeField] private Sprite calmSprite;
    [SerializeField] private Sprite mildSprite;
    [SerializeField] private Sprite highSprite;
    [SerializeField] private Sprite extremeSprite;
    [SerializeField] private Sprite panicSprite;

    [Header("Pulse Animation (Panic only)")]
    [Tooltip("Speed of the alpha pulse on the fill bar during Panic state")]
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
            // Pulse the fill bar alpha during Panic
            float alpha = Mathf.Lerp(pulseMinAlpha, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);
            Color c = anxietyBarFill.color;
            anxietyBarFill.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    // ─── Event Handlers ────────────────────────────────────────────────────

    /// <summary>Called every time the raw anxiety value changes.</summary>
    void HandleAnxietyChanged(float value)
    {
        if (anxietyBarFill == null) return;

        // Only update the fill amount — never touch the sprite on this image
        anxietyBarFill.fillAmount = AnxietyManager.Instance.NormalizedAnxiety;
    }

    /// <summary>Called when the anxiety level category changes.</summary>
    void HandleLevelChanged(AnxietyLevel level)
    {
        _currentLevel = level;
        _isPulsing    = level == AnxietyLevel.Panic;

        // Reset fill bar alpha to full (pulse coroutine takes over for Panic)
        if (anxietyBarFill != null)
        {
            Color c = anxietyBarFill.color;
            anxietyBarFill.color = new Color(c.r, c.g, c.b, 1f);
        }

        // Swap the icon sprite — this image is NOT a fill image, so no clipping
        if (levelIcon != null)
        {
            Sprite s = GetLevelSprite(level);
            if (s != null)
                levelIcon.sprite = s;
        }

        // Update Arabic label
        if (levelLabel != null)
            levelLabel.text = LevelLabels[(int)level];
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    Sprite GetLevelSprite(AnxietyLevel level)
    {
        return level switch
        {
            AnxietyLevel.Calm           => calmSprite,
            AnxietyLevel.MildAnxiety    => mildSprite,
            AnxietyLevel.HighAnxiety    => highSprite,
            AnxietyLevel.ExtremeAnxiety => extremeSprite,
            AnxietyLevel.Panic          => panicSprite,
            _                           => calmSprite
        };
    }
}

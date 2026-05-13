using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Anxiety Meter UI.
///
/// UI hierarchy expected:
///   ┌─ AnxietyHUD (this script)
///   ├─ BarBackground  ← Image (the empty-bar backdrop, no script needed)
///   ├─ BarFill        ← Image (Type = Simple). Sprite swaps per anxiety level.
///   │                    Size/position is fully controlled by the editor — this script
///   │                    never modifies the RectTransform.
///   └─ LevelLabel     ← TextMeshProUGUI (optional Arabic level name)
/// </summary>
public class AnxietyHUD : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Bar References")]
    [Tooltip("Image (Type = Simple, pivot Y = 0, anchored bottom) that acts as the filling bar. " +
             "Its sprite swaps per level; its height is driven by anxiety value.")]
    [SerializeField] private Image barFill;

    [Tooltip("TextMeshPro label showing the current anxiety level in Arabic (optional).")]
    [SerializeField] private TextMeshProUGUI levelLabel;

    [Header("Level Sprites")]
    [Tooltip("Sprites shown in the bar for each level — swap as level changes. " +
             "Each sprite can have any height; the bar height is driven by anxiety, not fill clipping.")]
    [SerializeField] private Sprite calmSprite;
    [SerializeField] private Sprite mildSprite;
    [SerializeField] private Sprite highSprite;
    [SerializeField] private Sprite extremeSprite;
    [SerializeField] private Sprite panicSprite;

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

        AnxietyManager.Instance.OnLevelChanged += HandleLevelChanged;

        // Initialize to current state
        HandleLevelChanged(AnxietyManager.Instance.CurrentLevel);
    }

    void OnDestroy()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.OnLevelChanged -= HandleLevelChanged;
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
    /// Called when the anxiety level category changes. Swaps the sprite.
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

        // Swap the bar sprite — no fill clipping, height is handled separately
        if (barFill != null)
        {
            Sprite s = GetLevelSprite(level);
            if (s != null)
                barFill.sprite = s;
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

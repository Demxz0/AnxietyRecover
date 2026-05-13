using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD showing medication pill count and dependency state.
///
/// Displays pill icons that dim as they're used, and a cooldown indicator.
/// The overall tint shifts from safe (white/blue) to warning (orange/red)
/// as more pills are consumed, visually communicating dependency.
///
/// SETUP:
///   1. Add to a Canvas.
///   2. Create 3 Image children for pill icons + assign them.
///   3. Optionally add a cooldown text label.
/// </summary>
public class MedicationHUD : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Pill Icons (assign 3 Image components — one per pill)")]
    [Tooltip("Pill icon for pill 1 (leftmost).")]
    [SerializeField] private Image pillIcon1;

    [Tooltip("Pill icon for pill 2.")]
    [SerializeField] private Image pillIcon2;

    [Tooltip("Pill icon for pill 3 (rightmost).")]
    [SerializeField] private Image pillIcon3;

    [Header("Colors")]
    [Tooltip("Color when pill is available.")]
    [SerializeField] private Color pillAvailableColor = new Color(0.3f, 0.85f, 0.4f, 1f);

    [Tooltip("Color when pill has been used (dependency indicator).")]
    [SerializeField] private Color pillUsedColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);

    [Tooltip("Warning tint applied to remaining pills as dependency grows.")]
    [SerializeField] private Color dependencyWarningColor = new Color(1f, 0.5f, 0.1f, 1f);

    [Header("Cooldown Display")]
    [Tooltip("Optional text showing cooldown remaining.")]
    [SerializeField] private TextMeshProUGUI cooldownLabel;

    [Header("Key Hint")]
    [Tooltip("Optional text showing the medication key.")]
    [SerializeField] private TextMeshProUGUI keyHintLabel;

    // ═══════════════════════════════════════════════════════════════════════
    //  PRIVATE
    // ═══════════════════════════════════════════════════════════════════════

    private Image[] _pillIcons;

    void Start()
    {
        _pillIcons = new[] { pillIcon1, pillIcon2, pillIcon3 };

        if (keyHintLabel != null)
            keyHintLabel.text = "[1]";

        // Subscribe to events
        if (MedicationSystem.Instance != null)
        {
            MedicationSystem.Instance.OnPillTaken += OnPillTaken;
            MedicationSystem.Instance.OnUnlocked  += OnMedicationUnlocked;
        }

        // Hide the HUD until the pills container is picked up.
        // If already unlocked (e.g. scene reloaded), show immediately.
        bool alreadyUnlocked = MedicationSystem.Instance != null && MedicationSystem.Instance.IsUnlocked;
        gameObject.SetActive(alreadyUnlocked);

        if (alreadyUnlocked)
            UpdateDisplay();
    }

    void OnDestroy()
    {
        if (MedicationSystem.Instance != null)
        {
            MedicationSystem.Instance.OnPillTaken -= OnPillTaken;
            MedicationSystem.Instance.OnUnlocked  -= OnMedicationUnlocked;
        }
    }

    void Update()
    {
        UpdateCooldownLabel();
    }

    void OnPillTaken(int remaining)
    {
        UpdateDisplay();
    }

    void OnMedicationUnlocked()
    {
        // Reveal the HUD the moment the container is picked up
        gameObject.SetActive(true);
        UpdateDisplay();
        Debug.Log("[MedicationHUD] Revealed — pills container picked up.");
    }

    void UpdateDisplay()
    {
        if (MedicationSystem.Instance == null) return;

        int used = MedicationSystem.Instance.PillsUsed;
        int remaining = MedicationSystem.Instance.PillsRemaining;

        for (int i = 0; i < _pillIcons.Length; i++)
        {
            if (_pillIcons[i] == null) continue;

            if (i < MedicationSystem.Instance.TotalPills - remaining)
            {
                // This pill has been used
                _pillIcons[i].color = pillUsedColor;
            }
            else if (i < MedicationSystem.Instance.TotalPills)
            {
                // This pill is available — tint based on dependency level
                Color available = Color.Lerp(pillAvailableColor, dependencyWarningColor,
                                             (float)used / MedicationSystem.Instance.TotalPills);
                _pillIcons[i].color = available;
            }
        }
    }

    void UpdateCooldownLabel()
    {
        if (cooldownLabel == null) return;

        if (MedicationSystem.Instance != null && MedicationSystem.Instance.IsOnCooldown)
        {
            cooldownLabel.gameObject.SetActive(true);
            float remaining = MedicationSystem.Instance.CooldownRemaining;
            cooldownLabel.text = $"{remaining:F0}s";
        }
        else
        {
            cooldownLabel.gameObject.SetActive(false);
        }
    }
}

using UnityEngine;

/// <summary>
/// Triggers an objective hint when the player enters a zone or when called from code.
///
/// TWO MODES:
///   1. Zone Mode  — attach to a Trigger Collider. Hint shows when player enters.
///   2. Script Mode — call Fire() from any room manager or interaction script.
///
/// SETUP:
///   - Assign 'hintText' in the Inspector.
///   - Set 'displayDuration' (0 = stays until replaced/cleared).
///   - Set 'triggerOnce = true' to prevent re-triggering on re-entry.
///   - Set 'clearOnExit = true' if the hint should disappear when the player leaves the zone.
///
/// EXAMPLE (from DependencyRoomManager):
///   [SerializeField] private ObjectiveHintTrigger phonePickupHint;
///   // In OnPhonePickedUp():
///   phonePickupHint?.Fire();
/// </summary>
public class ObjectiveHintTrigger : MonoBehaviour
{
    [TextArea(2, 4)]
    [Tooltip("The hint text to display on screen.")]
    [SerializeField] private string hintText = "...";

    [Tooltip("How long (seconds) the hint stays on screen. 0 = stays until replaced or cleared.")]
    [SerializeField] private float displayDuration = 0f;

    [Tooltip("If true, this trigger can only fire once.")]
    [SerializeField] private bool triggerOnce = true;

    [Tooltip("If true (Zone Mode only), the hint clears when the player leaves the zone.")]
    [SerializeField] private bool clearOnExit = false;

    private bool _hasFired;

    // ─── Zone Mode ────────────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Fire();
    }

    void OnTriggerExit(Collider other)
    {
        if (!clearOnExit) return;
        if (!other.CompareTag("Player")) return;
        ObjectiveHintManager.Instance?.ClearHint();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Fire the hint. Can be called from any script.
    /// Respects the triggerOnce setting.
    /// </summary>
    public void Fire()
    {
        if (triggerOnce && _hasFired) return;
        if (string.IsNullOrEmpty(hintText)) return;
        if (ObjectiveHintManager.Instance == null)
        {
            Debug.LogWarning("[ObjectiveHintTrigger] ObjectiveHintManager not found in scene.");
            return;
        }

        _hasFired = true;
        ObjectiveHintManager.Instance.ShowHint(hintText, displayDuration);
    }

    /// <summary>Allow this trigger to fire again.</summary>
    public void ResetTrigger() => _hasFired = false;

#if UNITY_EDITOR
    [ContextMenu("Test: Fire Now")]
    void TestFire()
    {
        _hasFired = false;
        Fire();
    }
#endif
}

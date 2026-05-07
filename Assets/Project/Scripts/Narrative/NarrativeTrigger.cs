using UnityEngine;

/// <summary>
/// Attach to any GameObject to trigger a narrative text moment.
///
/// TWO MODES:
///   1. Zone Mode  — attach to a Trigger Collider. Text shows when the player enters.
///   2. Script Mode — call TriggerNarrative() from any other script.
///
/// SETUP:
///   - Assign 'entry' in the Inspector.
///   - If using Zone Mode: make sure the Collider has IsTrigger = true,
///     and the Player GameObject has the "Player" tag.
///   - Set 'triggerOnce = true' (recommended) to avoid re-showing on re-entry.
/// </summary>
public class NarrativeTrigger : MonoBehaviour
{
    [Tooltip("The NarrativeEntry ScriptableObject to show when triggered.")]
    [SerializeField] private NarrativeEntry entry;

    [Tooltip("If true, this trigger can only fire once per game session.")]
    [SerializeField] private bool triggerOnce = true;

    private bool _hasFired;

    // ─── Zone Mode ────────────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        TriggerNarrative();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Call from any script to trigger this narrative entry.
    /// Respects the triggerOnce setting.
    /// </summary>
    public void TriggerNarrative()
    {
        if (triggerOnce && _hasFired) return;
        if (entry == null) { Debug.LogWarning($"[NarrativeTrigger] No NarrativeEntry assigned on {gameObject.name}."); return; }
        if (NarrativeManager.Instance == null) { Debug.LogWarning("[NarrativeTrigger] NarrativeManager not found in scene."); return; }

        _hasFired = true;
        NarrativeManager.Instance.Show(entry);
    }

    /// <summary>Reset so the trigger can fire again.</summary>
    public void Reset() => _hasFired = false;

#if UNITY_EDITOR
    [ContextMenu("Test: Fire Now")]
    void TestFire()
    {
        _hasFired = false;
        TriggerNarrative();
    }
#endif
}

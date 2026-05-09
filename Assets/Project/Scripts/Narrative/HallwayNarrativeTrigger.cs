using UnityEngine;

/// <summary>
/// Place this on a Trigger Collider in the hallway.
/// When the player first enters the hallway, it shows an inner-voice floating text
/// at the assigned anchor position.
///
/// SETUP:
///   1. Create an empty GameObject in the hallway at the entry point.
///   2. Add a BoxCollider — set IsTrigger = true.
///   3. Attach this script.
///   4. Assign 'hallwayEntry' (a NarrativeEntry ScriptableObject).
///   5. Create another empty child GO called "TextAnchor", position it where
///      you want the text to float, and assign it to 'textAnchor'.
///   6. Player must have the "Player" tag.
/// </summary>
public class HallwayNarrativeTrigger : MonoBehaviour
{
    [Tooltip("Inner-voice NarrativeEntry shown when the player first enters the hallway.")]
    [SerializeField] private NarrativeEntry hallwayEntry;

    [Tooltip("World position where the text will appear. " +
             "Create an empty child GameObject, place it at the desired spot, and assign it here.")]
    [SerializeField] private Transform textAnchor;

    [Tooltip("If true, this trigger fires only once per session (recommended).")]
    [SerializeField] private bool triggerOnce = true;

    private bool _hasFired;

    void OnTriggerEnter(Collider other)
    {
        if (!other.transform.root.CompareTag("Player")) return;
        if (triggerOnce && _hasFired) return;

        if (hallwayEntry == null)
        {
            Debug.LogWarning("[HallwayNarrativeTrigger] hallwayEntry not assigned.");
            return;
        }

        if (NarrativeManager.Instance == null)
        {
            Debug.LogWarning("[HallwayNarrativeTrigger] NarrativeManager not found in scene.");
            return;
        }

        _hasFired = true;
        // textAnchor null = spawns in front of player; assigned = spawns at that world position
        NarrativeManager.Instance.Show(hallwayEntry, textAnchor);
        Debug.Log("[HallwayNarrativeTrigger] Player entered hallway — showing inner-voice text.");
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Fire Now")]
    void TestFire()
    {
        _hasFired = false;
        if (hallwayEntry != null && NarrativeManager.Instance != null)
            NarrativeManager.Instance.Show(hallwayEntry, textAnchor);
    }
#endif
}

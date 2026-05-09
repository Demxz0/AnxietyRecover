using UnityEngine;

/// <summary>
/// Reusable proximity trigger for specific areas in the Open Room
/// (gym corner, childhood memory corner, etc.).
///
/// SETUP:
///   1. Create an empty GameObject near the area.
///   2. Add a SphereCollider or BoxCollider — IsTrigger = true.
///   3. Attach this script.
///   4. Set 'areaLabel' for easy identification in the Hierarchy.
///   5. Assign 'areaEntry' (a NarrativeEntry ScriptableObject).
///   6. Create an empty child GO called "TextAnchor", position it exactly
///      where the text should float, and assign it to 'textAnchor'.
///   7. Player must have the "Player" tag.
/// </summary>
public class OpenRoomAreaTrigger : MonoBehaviour
{
    [Tooltip("Human-readable name for this area (used in log messages only).")]
    [SerializeField] private string areaLabel = "Open Room Area";

    [Tooltip("Inner-voice NarrativeEntry shown when the player gets near this area.")]
    [SerializeField] private NarrativeEntry areaEntry;

    [Tooltip("World position where the text will appear. " +
             "Create an empty child GameObject, place it at the desired spot, and assign it here.")]
    [SerializeField] private Transform textAnchor;

    [Tooltip("If true, trigger fires only once per session (recommended).")]
    [SerializeField] private bool triggerOnce = true;

    private bool _hasFired;

    void OnTriggerEnter(Collider other)
    {
        if (!other.transform.root.CompareTag("Player")) return;
        if (triggerOnce && _hasFired) return;

        if (areaEntry == null)
        {
            Debug.LogWarning($"[OpenRoomAreaTrigger] ({areaLabel}) areaEntry not assigned.");
            return;
        }

        if (NarrativeManager.Instance == null)
        {
            Debug.LogWarning($"[OpenRoomAreaTrigger] ({areaLabel}) NarrativeManager not found in scene.");
            return;
        }

        _hasFired = true;
        NarrativeManager.Instance.Show(areaEntry, textAnchor);
        Debug.Log($"[OpenRoomAreaTrigger] ({areaLabel}) Player approached — showing inner-voice text.");
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Fire Now")]
    void TestFire()
    {
        _hasFired = false;
        if (areaEntry != null && NarrativeManager.Instance != null)
            NarrativeManager.Instance.Show(areaEntry, textAnchor);
    }
#endif
}

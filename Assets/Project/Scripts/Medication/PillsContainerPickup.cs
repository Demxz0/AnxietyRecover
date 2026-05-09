using UnityEngine;

/// <summary>
/// Attach to the pills container object in the start room.
/// When the player interacts with it (presses E), it:
///   1. Calls MedicationSystem.Unlock() — reveals the HUD and enables the medication key
///   2. Hides or destroys the container object (it's been picked up)
///   3. Fires only once — subsequent interactions do nothing
///
/// HOW TO SET UP IN THE EDITOR:
///   1. Place your pills container prop in the start room.
///   2. Add a Collider (Is Trigger = true) to it, or use its mesh collider.
///   3. Add an InteractableHandIcon component so the hand cursor appears.
///   4. Attach this script.
///   5. In the Inspector, assign 'containerVisual' to the mesh/visual child
///      that should disappear on pickup (leave empty to do nothing visually).
///   6. The paper that explains medication is a separate interactable object
///      next to the container — use a standard PaperInteraction script on it.
/// </summary>
public class PillsContainerPickup : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("The GameObject holding the visible pill container mesh. " +
             "It will be disabled when the player picks it up. " +
             "Leave empty if you handle visibility yourself.")]
    [SerializeField] private GameObject containerVisual;

    [Tooltip("If true, destroy the container GameObject entirely on pickup. " +
             "If false, just disable the visual.")]
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Interaction")]
    [Tooltip("How close the player must be to interact (used only if no trigger collider is set up).")]
    [SerializeField] private float interactRadius = 2f;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _pickedUp;

    // ── Interaction Entry Point ───────────────────────────────────────────────

    /// <summary>
    /// Call this from the Interactable system when the player presses E.
    /// Compatible with the existing InteractableHandIcon / interaction pipeline.
    /// </summary>
    public void OnInteract()
    {
        if (_pickedUp) return;
        _pickedUp = true;

        // Unlock the medication system — reveals HUD and enables the key
        if (MedicationSystem.Instance != null)
            MedicationSystem.Instance.Unlock();
        else
            Debug.LogWarning("[PillsContainerPickup] MedicationSystem not found in scene!");

        // Hide or destroy the container
        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else if (containerVisual != null)
        {
            containerVisual.SetActive(false);
        }

        Debug.Log("[PillsContainerPickup] Pills container picked up — medication system unlocked.");
    }

    // ── Fallback: trigger-based interaction ───────────────────────────────────
    // If you prefer a trigger zone + key press rather than the hand icon system,
    // you can use this instead. Both approaches work.

    private bool _playerInRange;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) _playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) _playerInRange = false;
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Trigger Pickup")]
    void TestPickup()
    {
        _pickedUp = false; // allow re-fire in editor
        OnInteract();
    }
#endif
}

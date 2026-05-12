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
///   3. Set the GameObject's Layer to "Interactable" (important for crosshair).
///   4. Add an InteractableHandIcon component so the hand cursor appears (optional but recommended).
///   5. Attach this script.
///   6. In the Inspector, assign 'containerVisual' to the mesh/visual child
///      that should disappear on pickup (leave empty to do nothing visually).
/// </summary>
public class PillsContainerPickup : MonoBehaviour, IInteractable
{
    [Header("Visual")]
    [Tooltip("The GameObject holding the visible pill container mesh. " +
             "It will be disabled when the player picks it up. " +
             "Leave empty if you handle visibility yourself.")]
    [SerializeField] private GameObject containerVisual;

    [Tooltip("If true, destroy the container GameObject entirely on pickup. " +
             "If false, just disable the visual.")]
    [SerializeField] private bool destroyOnPickup = false;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _pickedUp;

    // ── Interaction Entry Point ───────────────────────────────────────────────

    public void Interact()
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

    public string GetPromptText() => "Take Medication";

#if UNITY_EDITOR
    [ContextMenu("Test: Trigger Pickup")]
    void TestPickup()
    {
        _pickedUp = false; // allow re-fire in editor
        Interact();
    }
#endif
}

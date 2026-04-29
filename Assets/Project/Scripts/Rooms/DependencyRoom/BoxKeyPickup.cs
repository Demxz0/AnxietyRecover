using UnityEngine;

/// <summary>
/// Attach to the 3D key mesh that sits inside the open combo lock box.
/// The key starts INACTIVE (hidden in the locked box). ComboLockInteraction
/// activates it once the correct code is entered and the lid opens.
/// When the player clicks the key, it is collected — it disappears from the box
/// and the key icon appears on the HUD.
///
/// SETUP:
///   1. Place this script on the key 3D mesh inside the box.
///   2. The key GameObject should start with SetActive(false) in the scene
///      (ComboLockInteraction will enable it on solve).
///   3. Make sure the key mesh has a Collider and is on the Interactable layer.
///   4. Assign 'keyObject' in ComboLockInteraction to point to this GameObject.
/// </summary>
public class BoxKeyPickup : MonoBehaviour, IInteractable
{
    private bool _collected;

    void OnEnable()
    {
        // Reset each time the object becomes active, so a Debug force-solve works
        _collected = false;
    }

    public void Interact()
    {
        if (_collected) return;

        _collected = true;

        // Advance room stage and register key in game state (shows HUD icon)
        if (DependencyRoomManager.Instance != null)
            DependencyRoomManager.Instance.OnComboLockSolved();
        else
            GameStateManager.Instance?.CollectHallwayKey();

        // Visually remove the key from the box
        gameObject.SetActive(false);

        Debug.Log("[BoxKeyPickup] Key collected — icon shown on HUD.");
    }

    public string GetPromptText() => "Take Key";
}

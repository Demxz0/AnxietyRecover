using UnityEngine;

/// <summary>
/// The missing light switch piece sitting on the bed.
/// Player presses E → piece is collected instantly and the object disappears.
/// No canvas, no UI — behaves exactly like picking up the hallway key.
///
/// SETUP:
///   1. Attach to the 3D switch piece GameObject on the bed.
///   2. Make sure the GameObject is on the Interactable layer.
///   3. Assign an optional pickup AudioClip in the Inspector.
/// </summary>
public class LightSwitchPieceInteraction : MonoBehaviour, IInteractable
{
    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   pickupSound;

    private bool _pickedUp;

    public void Interact()
    {
        if (_pickedUp) return;
        _pickedUp = true;

        // Play pickup sound
        if (audioSource != null && pickupSound != null)
            audioSource.PlayOneShot(pickupSound);

        // Register in game state
        GameStateManager.Instance?.CollectSwitchPiece();

        Debug.Log("[SwitchPiece] Picked up!");

        // Immediately hide the 3D object
        gameObject.SetActive(false);
    }

    public string GetPromptText() => "Pick Up Switch Piece";
}

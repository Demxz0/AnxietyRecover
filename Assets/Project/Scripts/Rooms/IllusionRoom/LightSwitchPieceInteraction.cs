using UnityEngine;

/// <summary>
/// The missing light switch piece sitting ON THE BED.
/// Player interacts → piece added to inventory.
/// IllusionRoomManager does NOT need to know where the piece is physically.
///
/// SETUP:
///   1. Create a 3D object representing the switch piece (mesh on the bed).
///   2. Attach this script to it.
///   3. Add a CanvasItemInteraction component to show a "Switch Piece" 2D image canvas
///      when the player first examines it (optional but recommended).
///   4. After pickup, the GameObject deactivates itself.
/// </summary>
[RequireComponent(typeof(CanvasItemInteraction))]
public class LightSwitchPieceInteraction : MonoBehaviour
{
    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   pickupSound;

    [Tooltip("Seconds to wait after pickup before hiding the object (let the canvas close first).")]
    [SerializeField] private float hideDelay = 0.5f;

    private CanvasItemInteraction _canvas;
    private bool _pickedUp;

    void Awake()
    {
        _canvas = GetComponent<CanvasItemInteraction>();
        _canvas.OnFirstInteracted += HandlePickup;
    }

    void OnDestroy()
    {
        if (_canvas != null) _canvas.OnFirstInteracted -= HandlePickup;
    }

    void HandlePickup()
    {
        if (_pickedUp) return;
        _pickedUp = true;

        // Record in inventory
        GameStateManager.Instance?.CollectSwitchPiece();

        // Play sound
        if (audioSource != null && pickupSound != null)
            audioSource.PlayOneShot(pickupSound);

        Debug.Log("[SwitchPiece] Picked up from the bed!");

        // Deactivate after a short delay so canvas can still show
        Invoke(nameof(HideSelf), hideDelay);
    }

    void HideSelf() => gameObject.SetActive(false);
}

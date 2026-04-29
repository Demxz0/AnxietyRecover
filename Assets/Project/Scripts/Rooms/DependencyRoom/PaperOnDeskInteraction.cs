using UnityEngine;

/// <summary>
/// Attach to the paper on the computer desk.
/// When the player examines it, shows a canvas with code diagrams
/// and notifies DependencyRoomManager that Digit 1 was found.
///
/// SETUP:
///   1. Attach to the desk paper 3D object.
///   2. Assign the canvas that shows the code diagram image.
///   3. This script works via CanvasItemInteraction — add that component too,
///      OR drive it purely through this script's own Interact().
/// </summary>
[RequireComponent(typeof(CanvasItemInteraction))]
public class PaperOnDeskInteraction : MonoBehaviour
{
    private CanvasItemInteraction _canvas;

    void Awake()
    {
        _canvas = GetComponent<CanvasItemInteraction>();
        // Listen for the first examination
        _canvas.OnFirstInteracted += HandleFirstRead;
    }

    void OnDestroy()
    {
        if (_canvas != null) _canvas.OnFirstInteracted -= HandleFirstRead;
    }

    void HandleFirstRead()
    {
        // Only notify the manager if we're in the right stage
        if (DependencyRoomManager.Instance != null)
            DependencyRoomManager.Instance.OnDeskPaperRead();

        Debug.Log("[DeskPaper] Paper read — Digit 1 (4) found via code smell diagram.");
    }
}

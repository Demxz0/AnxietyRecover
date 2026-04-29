using UnityEngine;

/// <summary>
/// Sticky note above the book — shows a canvas with the hint text:
/// "Time is your friend if you could just calm down..."
/// Just a CanvasItemInteraction wrapper. No special logic needed.
///
/// SETUP:
///   1. Attach to the sticky note 3D object.
///   2. Add CanvasItemInteraction to the same GameObject.
///   3. Assign a canvas showing the note image with the hint text.
/// </summary>
[RequireComponent(typeof(CanvasItemInteraction))]
public class StickyNoteInteraction : MonoBehaviour
{
    // This script is intentionally minimal.
    // The CanvasItemInteraction component handles all display logic.
    // The note just shows the hint — no game state changes needed.
    void Start()
    {
        Debug.Log("[StickyNote] Ready. Canvas shows hint: 'Time is your friend...'");
    }
}

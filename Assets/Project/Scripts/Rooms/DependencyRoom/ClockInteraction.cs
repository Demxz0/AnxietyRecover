using UnityEngine;

/// <summary>
/// Digital clock showing 13:07 — the hint for the piano note arrangement.
/// Shows a canvas image when the player examines it.
/// Just a CanvasItemInteraction wrapper.
///
/// SETUP:
///   1. Attach to the digital clock 3D object.
///   2. Add CanvasItemInteraction to the same GameObject.
///   3. Assign a canvas with a 2D image of the clock showing "13:07".
/// </summary>
[RequireComponent(typeof(CanvasItemInteraction))]
public class ClockInteraction : MonoBehaviour
{
    // No extra logic needed — the clock just shows the time image.
    // The player reads it and uses the digits 1, 3, 0, 7
    // as the note order in the piano puzzle.
    void Start()
    {
        Debug.Log("[Clock] Shows 13:07 — digits 1,3,0,7 are the piano note order.");
    }
}

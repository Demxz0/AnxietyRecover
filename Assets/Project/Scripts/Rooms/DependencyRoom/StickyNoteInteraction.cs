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
    private CanvasItemInteraction _canvas;

    void Awake()
    {
        _canvas = GetComponent<CanvasItemInteraction>();
        if (_canvas != null)
            _canvas.OnFirstInteracted += HandleFirstRead;
    }

    void OnDestroy()
    {
        if (_canvas != null)
            _canvas.OnFirstInteracted -= HandleFirstRead;
    }

    void HandleFirstRead()
    {
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.ReduceOneLevel();
            
        Debug.Log("[StickyNote] Sticky note read — anxiety reduced.");
    }
}

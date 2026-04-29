using UnityEngine;

/// <summary>
/// Attach to the crumpled paper in the trash bin.
/// When the player examines it, shows the password on a canvas
/// and notifies DependencyRoomManager.
///
/// SETUP:
///   1. Attach to the trash paper 3D object (crumpled paper mesh inside the trash bin).
///   2. Add CanvasItemInteraction to the same GameObject.
///   3. Assign a canvas that shows the password image/text "YouGotThis123".
/// </summary>
[RequireComponent(typeof(CanvasItemInteraction))]
public class TrashPaperInteraction : MonoBehaviour
{
    private CanvasItemInteraction _canvas;

    void Awake()
    {
        _canvas = GetComponent<CanvasItemInteraction>();
        _canvas.OnFirstInteracted += HandleFirstRead;
    }

    void OnDestroy()
    {
        if (_canvas != null) _canvas.OnFirstInteracted -= HandleFirstRead;
    }

    void HandleFirstRead()
    {
        if (DependencyRoomManager.Instance != null)
            DependencyRoomManager.Instance.OnTrashPaperRead();

        Debug.Log("[TrashPaper] Password 'YouGotThis123' revealed.");
    }
}

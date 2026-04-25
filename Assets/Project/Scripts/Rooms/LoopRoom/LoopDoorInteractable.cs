using UnityEngine;
using DG.Tweening;

/// <summary>
/// The exit door in the Loop Room (Kitchen).
/// Delegates to LoopRoomManager — if the loop isn't done, teleports player back.
/// If the loop IS done, opens normally.
///
/// SETUP:
///   1. Attach to the kitchen exit door (instead of DoorController).
///   2. Assign doorPivot (same as DoorController).
///   3. Attach this as the door's IInteractable source.
///   4. Make sure LoopRoomManager is in the scene and StartLoop() is called on entry.
/// </summary>
public class LoopDoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Door Pivot")]
    [SerializeField] private Transform doorPivot;

    [Header("Animation")]
    [SerializeField] private float openAngle    = -90f;
    [SerializeField] private float animDuration = 0.6f;
    [SerializeField] private Ease  openEase     = Ease.OutBack;

    private bool      _isOpen;
    private bool      _isAnimating;
    private Quaternion _closedRot;
    private Quaternion _openRot;

    void Start()
    {
        if (doorPivot == null) doorPivot = transform;
        _closedRot = doorPivot.rotation;
        _openRot   = _closedRot * Quaternion.Euler(0f, openAngle, 0f);

        // Auto-start the loop when the scene loads (if LoopRoomManager exists)
        if (LoopRoomManager.Instance != null)
            LoopRoomManager.Instance.StartLoop();
    }

    public void Interact()
    {
        if (_isAnimating) return;

        // If loop is complete, open door normally
        if (LoopRoomManager.Instance == null || LoopRoomManager.Instance.OnPlayerTriesToExit())
        {
            OpenDoor();
            return;
        }

        // Loop not complete — teleport happened inside OnPlayerTriesToExit()
        // Play a brief "blocked" animation (door starts to open then slams shut)
        StartCoroutine(BlockedDoorRoutine());
    }

    public string GetPromptText()
    {
        if (LoopRoomManager.Instance != null && LoopRoomManager.Instance.LoopComplete)
            return "Exit Kitchen";
        return "Open Door";
    }

    void OpenDoor()
    {
        if (_isOpen) return;
        _isAnimating = true;
        _isOpen = true;
        doorPivot.DORotateQuaternion(_openRot, animDuration)
                 .SetEase(openEase)
                 .OnComplete(() => _isAnimating = false);
    }

    System.Collections.IEnumerator BlockedDoorRoutine()
    {
        // Door nudges open slightly then snaps shut — reinforcing the loop feeling
        _isAnimating = true;

        Quaternion slight = _closedRot * Quaternion.Euler(0f, openAngle * 0.15f, 0f);
        doorPivot.DORotateQuaternion(slight, 0.2f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(0.25f);
        doorPivot.DORotateQuaternion(_closedRot, 0.2f).SetEase(Ease.InQuad);
        yield return new WaitForSeconds(0.25f);

        _isAnimating = false;
    }
}

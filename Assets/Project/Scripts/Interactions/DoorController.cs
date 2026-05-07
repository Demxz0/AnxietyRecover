using UnityEngine;
using DG.Tweening;

public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Door Reference")]
    [SerializeField] private Transform doorPivot;

    [Header("Door Type")]
    [SerializeField] private RotationAxis openAxis = RotationAxis.Y;

    [Header("Animation Settings")]
    [SerializeField] private float openAngle = -90f;
    [SerializeField] private float animDuration = 0.9f;
    [SerializeField] private Ease openEase = Ease.OutQuart;
    [SerializeField] private Ease closeEase = Ease.InOutSine;

    public enum RotationAxis { X, Y, Z }

    private bool _isOpen = false;
    private bool _isAnimating = false;
    private bool _isLocked = false;
    private Quaternion _closedRotation;
    private Quaternion _openRotation;

    void Start()
    {
        // Use WORLD rotation to bypass the huge scale values in the hierarchy
        // This prevents stretching caused by non-uniform scale on parent objects
        _closedRotation = doorPivot.rotation;
        _openRotation = _closedRotation * GetAxisRotation(openAngle);
    }

    Quaternion GetAxisRotation(float angle)
    {
        return openAxis switch
        {
            RotationAxis.X => Quaternion.Euler(angle, 0f, 0f),
            RotationAxis.Y => Quaternion.Euler(0f, angle, 0f),
            RotationAxis.Z => Quaternion.Euler(0f, 0f, angle),
            _ => Quaternion.identity
        };
    }

    public void Interact()
    {
        if (_isAnimating) return;
        if (_isLocked) { Debug.Log("[Door] Locked."); return; }
        ToggleDoor();
    }

    public string GetPromptText()
    {
        if (_isLocked) return "Locked";
        return _isOpen ? "Close Door" : "Open Door";
    }

    /// <summary>Closes the door instantly (no animation) and locks it.</summary>
    public void ForceClose()
    {
        if (_isAnimating)
        {
            doorPivot.DOKill();
            _isAnimating = false;
        }
        _isOpen = false;
        doorPivot.DORotateQuaternion(_closedRotation, animDuration)
            .SetEase(closeEase)
            .OnComplete(() => _isAnimating = false);
    }

    /// <summary>Prevents the player from opening this door.</summary>
    public void Lock()   => _isLocked = true;

    /// <summary>Allows the player to open this door again.</summary>
    public void Unlock() => _isLocked = false;

    void ToggleDoor()
    {
        _isAnimating = true;
        _isOpen = !_isOpen;

        // Play door sound via AudioManager
        if (_isOpen)
            AudioManager.Instance?.PlayOneShot(SoundID.DoorOpen);
        else
            AudioManager.Instance?.PlayOneShot(SoundID.DoorClose);

        Quaternion targetRotation = _isOpen ? _openRotation : _closedRotation;
        Ease ease = _isOpen ? openEase : closeEase;

        // DORotateQuaternion (world space) instead of DOLocalRotateQuaternion
        // This is the key fix - avoids the huge parent scale affecting rotation
        doorPivot
            .DORotateQuaternion(targetRotation, animDuration)
            .SetEase(ease)
            .OnComplete(() => _isAnimating = false);
    }
}
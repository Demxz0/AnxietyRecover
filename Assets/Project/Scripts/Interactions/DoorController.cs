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
    [SerializeField] private float animDuration = 0.6f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InOutSine;

    public enum RotationAxis { X, Y, Z }

    private bool _isOpen = false;
    private bool _isAnimating = false;
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
        ToggleDoor();
    }

    public string GetPromptText()
    {
        return _isOpen ? "Close Door" : "Open Door";
    }

    void ToggleDoor()
    {
        _isAnimating = true;
        _isOpen = !_isOpen;

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
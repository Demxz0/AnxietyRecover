using UnityEngine;
using DG.Tweening;

public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Door Reference")]
    [SerializeField] private Transform doorPivot;

    [Header("Animation Settings")]
    [SerializeField] private float openAngle = -90f;
    [SerializeField] private float animDuration = 0.6f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InOutSine;

    private bool _isOpen = false;
    private bool _isAnimating = false;

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

        float targetAngle = _isOpen ? openAngle : 0f;
        Ease ease = _isOpen ? openEase : closeEase;

        doorPivot
            .DOLocalRotate(new Vector3(-90f, targetAngle, 0f), animDuration, RotateMode.Fast)
            .SetEase(ease)
            .OnComplete(() => _isAnimating = false);
    }
}

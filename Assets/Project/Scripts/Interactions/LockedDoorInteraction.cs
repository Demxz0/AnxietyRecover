using UnityEngine;
using DG.Tweening;

/// <summary>
/// A door that requires the hallway key (from GameStateManager) to open.
/// Once unlocked, it behaves like a normal door and the key icon is hidden.
///
/// SETUP:
///   1. Attach to the hallway door GameObject (same object as your door mesh or pivot).
///   2. Assign the doorPivot (same as DoorController).
///   3. Set openAngle and openAxis to match your door layout.
///   4. The door opens automatically when the player interacts AND has the key.
/// </summary>
public class LockedDoorInteraction : MonoBehaviour, IInteractable
{
    [Header("Door Pivot")]
    [SerializeField] private Transform doorPivot;

    [Header("Animation")]
    [SerializeField] private float openAngle    = -90f;
    [SerializeField] private float animDuration = 0.9f;
    [SerializeField] private Ease  openEase     = Ease.OutQuart;

    [Header("Anxiety — Locked Interaction")]
    [Tooltip("Anxiety added each time the player tries to open the locked door.")]
    [SerializeField] private float anxietyOnLockedAttempt = 5f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   lockedSound;
    [SerializeField] private AudioClip   openSound;

    private bool _isUnlocked;
    private bool _isOpen;
    private bool _isAnimating;
    private Quaternion _closedRot;
    private Quaternion _openRot;

    // Tracks how many times the player has clicked the locked door.
    // Anxiety only increases on the FIRST click.
    private int _lockedClickCount;

    void Start()
    {
        if (doorPivot == null) doorPivot = transform;
        _closedRot = doorPivot.rotation;
        _openRot   = _closedRot * Quaternion.Euler(0f, 0f, openAngle);
    }

    public void Interact()
    {
        if (_isAnimating) return;

        if (!_isUnlocked)
        {
            // Player does not have the key yet
            if (GameStateManager.Instance != null && GameStateManager.Instance.HasHallwayKey)
            {
                Unlock();
            }
            else
            {
                Debug.Log("[LockedDoor] Door is locked. Player needs the key.");
                PlaySound(lockedSound);

                // Only add anxiety on the VERY FIRST click — subsequent clicks do nothing
                if (_lockedClickCount == 0 && AnxietyManager.Instance != null)
                    AnxietyManager.Instance.AddAnxiety(anxietyOnLockedAttempt);

                _lockedClickCount++;
            }
            return;
        }

        ToggleDoor();
    }

    public string GetPromptText()
    {
        if (!_isUnlocked) return "Locked";
        return _isOpen ? "Close Door" : "Open Door";
    }

    void Unlock()
    {
        _isUnlocked = true;
        Debug.Log("[LockedDoor] Door unlocked with key!");
        PlaySound(openSound);

        // Hide key icon from HUD (key is consumed)
        if (InventoryUI.Instance != null)
            InventoryUI.Instance.HideKeyIcon();

        ToggleDoor();
    }

    void ToggleDoor()
    {
        if (_isAnimating) return;
        _isAnimating = true;
        _isOpen      = !_isOpen;

        Quaternion target = _isOpen ? _openRot : _closedRot;
        doorPivot
            .DORotateQuaternion(target, animDuration)
            .SetEase(openEase)
            .OnComplete(() =>
            {
                _isAnimating = false;
                if (_isOpen && GameStateManager.Instance != null)
                    GameStateManager.Instance.CompleteDependencyRoom();
            });
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}

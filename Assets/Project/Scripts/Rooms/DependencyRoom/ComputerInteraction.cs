using UnityEngine;

/// <summary>
/// Attach to the computer 3D object.
/// If the player knows the password → shows the desktop canvas (Digit 2).
/// If not → shows a "password required" message canvas.
///
/// SETUP:
///   1. Attach to the computer monitor/screen mesh.
///   2. Assign 'desktopCanvas' — a canvas showing the computer screen with Digit 2.
///   3. Assign 'lockedCanvas'  — a canvas showing "Password Required" (can be same or different canvas).
///   4. Add CanvasItemInteraction as a SEPARATE component only if you want generic examine.
///      This script handles its own Interact() logic.
/// </summary>
public class ComputerInteraction : MonoBehaviour, IInteractable
{
    [Header("Canvases")]
    [Tooltip("Canvas shown when computer is successfully unlocked (shows Digit 2).")]
    [SerializeField] private GameObject desktopCanvas;

    [Tooltip("Canvas shown when player doesn't know the password yet.")]
    [SerializeField] private GameObject lockedCanvas;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   unlockSound;
    [SerializeField] private AudioClip   deniedSound;

    private bool _isOpen;

    void Start()
    {
        SetCanvasActive(desktopCanvas, false);
        SetCanvasActive(lockedCanvas,  false);
    }

    void Update()
    {
        // Close any open canvas with E or Escape
        if (_isOpen && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
            CloseAll();
    }

    public void Interact()
    {
        if (_isOpen) { CloseAll(); return; }

        bool knowsPassword = GameStateManager.Instance != null && GameStateManager.Instance.KnowsPassword;

        if (knowsPassword)
        {
            SetCanvasActive(desktopCanvas, true);
            _isOpen = true;
            PlaySound(unlockSound);

            // Notify manager the first time
            if (GameStateManager.Instance != null && !GameStateManager.Instance.ComputerUnlocked)
            {
                GameStateManager.Instance.SetComputerUnlocked();
                if (DependencyRoomManager.Instance != null)
                    DependencyRoomManager.Instance.OnComputerAccessed();
                Debug.Log("[Computer] Desktop unlocked — Digit 2 (2) revealed.");
            }
        }
        else
        {
            SetCanvasActive(lockedCanvas, true);
            _isOpen = true;
            PlaySound(deniedSound);
            Debug.Log("[Computer] Password required — player hasn't found the trash paper yet.");
        }
    }

    public string GetPromptText()
    {
        bool knows = GameStateManager.Instance != null && GameStateManager.Instance.KnowsPassword;
        return knows ? "Use Computer" : "Computer (Locked)";
    }

    void CloseAll()
    {
        SetCanvasActive(desktopCanvas, false);
        SetCanvasActive(lockedCanvas,  false);
        _isOpen = false;
    }

    void SetCanvasActive(GameObject canvas, bool state)
    {
        if (canvas != null) canvas.SetActive(state);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}

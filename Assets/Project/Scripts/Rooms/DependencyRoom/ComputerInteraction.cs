using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the computer 3D object.
/// The locked canvas shows a password input field.
/// Player types the password from the trash paper and hits Enter or Submit.
/// Correct → desktop canvas opens. Wrong → error message shown.
///
/// SETUP:
///   1. Attach to the computer monitor/screen mesh.
///   2. Assign 'desktopCanvas' — shown when unlocked.
///   3. Assign 'lockedCanvas'  — shown with the InputField to type the password.
///   4. Inside 'lockedCanvas' assign: passwordInput (InputField), submitPasswordButton (Button),
///      feedbackText (Text).
/// </summary>
public class ComputerInteraction : MonoBehaviour, IInteractable
{
    [Header("Canvases")]
    [Tooltip("Canvas shown when computer is successfully unlocked (shows Digit 2).")]
    [SerializeField] private GameObject desktopCanvas;

    [Tooltip("Canvas shown for the password login screen.")]
    [SerializeField] private GameObject lockedCanvas;

    [Header("Password UI (inside lockedCanvas)")]
    [Tooltip("InputField where the player types the password.")]
    [SerializeField] private InputField passwordInput;

    [Tooltip("Button to submit the password.")]
    [SerializeField] private Button submitPasswordButton;

    [Tooltip("Feedback text for wrong password.")]
    [SerializeField] private Text feedbackText;

    [Tooltip("The exact password shown on the trash paper.")]
    [SerializeField] private string correctPassword = "YouGotThis123";

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   unlockSound;
    [SerializeField] private AudioClip   deniedSound;

    private bool _isOpen;
    private float _timeOpened;

    // ─── Setup ───────────────────────────────────────────────────────────────

    void Start()
    {
        SetCanvasActive(desktopCanvas, false);
        SetCanvasActive(lockedCanvas,  false);

        // Wire submit button
        if (submitPasswordButton != null)
            submitPasswordButton.onClick.AddListener(CheckPassword);

        // Wire InputField's native Submit event (fires on Enter key)
        if (passwordInput != null)
            passwordInput.onSubmit.AddListener(_ => CheckPassword());
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    void Update()
    {
        // Escape closes any open canvas — we read raw keyboard because the
        // Player action map is disabled while IsInUI.
        if (_isOpen && Time.time - _timeOpened > 0.1f
            && UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseAll();
        }
    }

    // ─── IInteractable ───────────────────────────────────────────────────────

    public void Interact()
    {
        if (_isOpen) { CloseAll(); return; }

        bool isUnlocked = GameStateManager.Instance != null && GameStateManager.Instance.ComputerUnlocked;

        if (isUnlocked)
        {
            SetCanvasActive(desktopCanvas, true);
            _isOpen = true;
            _timeOpened = Time.time;
            UIInputMode.Enter();
            PlaySound(unlockSound);
        }
        else
        {
            SetCanvasActive(lockedCanvas, true);
            _isOpen = true;
            _timeOpened = Time.time;
            UIInputMode.Enter();

            if (passwordInput != null)
            {
                passwordInput.text = "";
                // Defer ActivateInputField by one frame so the canvas is
                // fully visible before Unity focuses the field.
                StartCoroutine(ActivateInputNextFrame());
            }
            if (feedbackText != null) feedbackText.text = "";
        }
    }

    public string GetPromptText()
    {
        bool isUnlocked = GameStateManager.Instance != null && GameStateManager.Instance.ComputerUnlocked;
        return isUnlocked ? "Use Computer" : "Login to Computer";
    }

    // ─── Password Logic ───────────────────────────────────────────────────────

    void CheckPassword()
    {
        if (passwordInput == null) return;

        if (passwordInput.text.Trim() == correctPassword)
        {
            SetCanvasActive(lockedCanvas, false);
            SetCanvasActive(desktopCanvas, true);
            PlaySound(unlockSound);

            if (GameStateManager.Instance != null && !GameStateManager.Instance.ComputerUnlocked)
            {
                GameStateManager.Instance.SetComputerUnlocked();
                if (DependencyRoomManager.Instance != null)
                    DependencyRoomManager.Instance.OnComputerAccessed();
                Debug.Log("[Computer] Desktop unlocked — password correct!");
            }
        }
        else
        {
            PlaySound(deniedSound);
            if (feedbackText != null) feedbackText.text = "Incorrect Password";
            if (passwordInput != null) passwordInput.ActivateInputField();
            Debug.Log("[Computer] Wrong password entered.");
        }
    }

    // ─── Canvas Control ───────────────────────────────────────────────────────

    void CloseAll()
    {
        SetCanvasActive(desktopCanvas, false);
        SetCanvasActive(lockedCanvas,  false);
        _isOpen = false;
        UIInputMode.Exit();
    }

    System.Collections.IEnumerator ActivateInputNextFrame()
    {
        yield return null;
        if (passwordInput != null) passwordInput.ActivateInputField();
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

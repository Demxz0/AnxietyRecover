using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Piano note arrangement puzzle.
/// The paper above the piano shows 4 notes. The player must arrange them
/// in the order shown by the clock (13:07 → 1,3,0,7).
/// On correct arrangement, Digit 3 is revealed.
///
/// HOW IT WORKS:
///   - The puzzle canvas shows 4 note slots, each with an UP and DOWN button to cycle.
///   - The correct sequence is configurable (default: 1, 3, 0, 7 from clock 13:07).
///   - On Submit, the sequence is checked. Correct → reveals Digit 3 (configurable).
///   - Wrong → anxiety spike + shows feedback.
///
/// SETUP:
///   1. Attach to the paper-above-piano 3D object.
///   2. Create a puzzle Canvas with:
///        - 4 "slots", each with an up-arrow Button and down-arrow Button + a UI Image showing the current sprite.
///        - A Submit Button.
///        - A Result Text showing success/fail feedback.
///   3. Assign all references in the Inspector.
///   4. Assign the Digit 3 reveal canvas (shows the number when puzzle is solved).
///   5. Assign 10 sprites (digits 0-9) to 'digitSprites'. Index 0 = sprite for 0, index 1 = sprite for 1, etc.
/// </summary>
public class PianoNoteInteraction : MonoBehaviour, IInteractable
{
    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Puzzle Canvas")]
    [Tooltip("The canvas that shows the note arrangement puzzle.")]
    [SerializeField] private GameObject puzzleCanvas;

    [Tooltip("10 sprites representing digits 0-9 in order. Index 0 = digit 0, index 9 = digit 9.")]
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];

    [Tooltip("4 UI Image components — one per note slot. Each displays the sprite for the current value.")]
    [SerializeField] private Image[] slotImages = new Image[4];

    [Tooltip("4 UP buttons — each increments the corresponding slot value.")]
    [SerializeField] private Button[] upButtons = new Button[4];

    [Tooltip("4 DOWN buttons — each decrements the corresponding slot value.")]
    [SerializeField] private Button[] downButtons = new Button[4];

    [Tooltip("Submit button — checks if the sequence is correct.")]
    [SerializeField] private Button submitButton;

    [Tooltip("Text showing 'Correct!' or 'Try Again' feedback.")]
    [SerializeField] private Text feedbackText;

    [Header("Correct Sequence")]
    [Tooltip("The correct note values in order (from clock 13:07 → 1,3,0,7).")]
    [SerializeField] private int[] correctSequence = { 1, 3, 0, 7 };

    [Header("Digit 3 Reveal Canvas")]
    [Tooltip("Canvas that appears after solving the puzzle, showing Digit 3.")]
    [SerializeField] private GameObject digit3Canvas;

    [Tooltip("Digit 3 value revealed on this canvas (set to the number shown on the canvas image).")]
    [SerializeField] private int digit3Value = 7;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   correctSound;
    [SerializeField] private AudioClip   wrongSound;
    [SerializeField] private AudioClip   clickSound;

    [Header("Close Button")]
    [Tooltip("Button on the puzzle canvas that closes it. Must be assigned — keyboard shortcuts are disabled.")]
    [SerializeField] private Button closeButton;

    [Header("Anxiety — Wrong Attempt")]
    [SerializeField] private float anxietyOnWrongAttempt = 8f;

    // ─── State ────────────────────────────────────────────────────────────────
    private int[] _currentValues = { 0, 0, 0, 0 };
    private bool  _isSolved;
    private bool  _isOpen;
    private float _timeOpened;

    // ─── Setup ────────────────────────────────────────────────────────────────
    void Start()
    {
        SetCanvasActive(puzzleCanvas, false);
        SetCanvasActive(digit3Canvas,  false);

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            if (upButtons.Length   > i && upButtons[idx]   != null) upButtons[idx].onClick.AddListener(()   => Increment(idx));
            if (downButtons.Length > i && downButtons[idx] != null) downButtons[idx].onClick.AddListener(() => Decrement(idx));
        }

        if (submitButton != null) submitButton.onClick.AddListener(CheckAnswer);
        if (closeButton  != null) closeButton.onClick.AddListener(ClosePuzzle);
        UpdateSlotDisplay();
    }

    // Canvas is closed exclusively via the close button — no keyboard shortcut.

    // ─── IInteractable ───────────────────────────────────────────────────────
    public void Interact()
    {
        if (_isSolved) return;
        if (_isOpen)   return; // already open — only the close button can dismiss it
        OpenPuzzle();
    }

    public string GetPromptText() => _isSolved ? "" : "Solve Piano Puzzle";

    // ─── Puzzle Logic ─────────────────────────────────────────────────────────
    void Increment(int idx)
    {
        _currentValues[idx] = (_currentValues[idx] + 1) % 10;
        UpdateSlotDisplay();
        PlaySound(clickSound);
    }

    void Decrement(int idx)
    {
        _currentValues[idx] = (_currentValues[idx] + 9) % 10; // wrap around
        UpdateSlotDisplay();
        PlaySound(clickSound);
    }

    void UpdateSlotDisplay()
    {
        for (int i = 0; i < 4; i++)
        {
            if (slotImages.Length <= i || slotImages[i] == null) continue;
            int v = _currentValues[i];
            if (digitSprites != null && digitSprites.Length > v && digitSprites[v] != null)
                slotImages[i].sprite = digitSprites[v];
        }
    }

    void CheckAnswer()
    {
        bool correct = true;
        for (int i = 0; i < 4; i++)
            if (correctSequence.Length <= i || _currentValues[i] != correctSequence[i])
                { correct = false; break; }

        if (correct)
        {
            _isSolved = true;
            PlaySound(correctSound);

            if (feedbackText != null) feedbackText.text = "Correct!";

            GameStateManager.Instance?.SetDigit3Found();
            DependencyRoomManager.Instance?.OnPianoSolved();

            // Show Digit 3 reveal canvas
            SetCanvasActive(digit3Canvas, true);

            Debug.Log($"[Piano] Puzzle SOLVED! Digit 3 = {digit3Value}");
        }
        else
        {
            PlaySound(wrongSound);
            if (feedbackText != null) feedbackText.text = "Try Again...";
            if (AnxietyManager.Instance != null)
                AnxietyManager.Instance.AddAnxiety(anxietyOnWrongAttempt);
            Debug.Log("[Piano] Wrong sequence.");
        }
    }

    // ─── Canvas Control ───────────────────────────────────────────────────────
    void OpenPuzzle()
    {
        SetCanvasActive(puzzleCanvas, true);
        _isOpen = true;
        _timeOpened = Time.time;
        if (feedbackText != null) feedbackText.text = "";
        UIInputMode.Enter();
        // Force cursor visible in case UIInputMode was in a stale state
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void ClosePuzzle()
    {
        SetCanvasActive(puzzleCanvas, false);
        _isOpen = false;
        UIInputMode.Exit();
    }

    void SetCanvasActive(GameObject go, bool state)
    {
        if (go != null) go.SetActive(state);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Force Solve Piano")]
    void DebugSolve()
    {
        _isSolved = true;
        GameStateManager.Instance?.SetDigit3Found();
        DependencyRoomManager.Instance?.OnPianoSolved();
        Debug.Log("[Piano] DEBUG: Force solved.");
    }
#endif
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3-digit combination lock box.
/// The player must enter the correct digits (4, 2, Digit3) to get the hallway key.
///
/// HOW IT WORKS:
///   - Each digit slot has UP and DOWN buttons to cycle 0-9.
///   - A Submit button checks the combination.
///   - Correct → lock opens, key collected.
///   - Wrong → anxiety spike + feedback.
///
/// SETUP:
///   1. Attach to the combo lock box 3D object.
///   2. Create a puzzle Canvas with:
///        - 3 digit Text labels + UP/DOWN buttons per digit.
///        - A Submit Button.
///        - A Result Text for feedback.
///   3. Assign all references in the Inspector.
///   4. Default combination: 4, 2, 7 (matches Digit1=4, Digit2=2, Digit3=7).
///      Change via the correctCombination array in the Inspector.
/// </summary>
public class ComboLockInteraction : MonoBehaviour, IInteractable
{
    [Header("Puzzle Canvas")]
    [SerializeField] private GameObject lockCanvas;

    [Tooltip("3 Text labels — one per digit slot.")]
    [SerializeField] private Text[] digitTexts = new Text[3];

    [Tooltip("3 UP buttons — each increments the corresponding digit.")]
    [SerializeField] private Button[] upButtons = new Button[3];

    [Tooltip("3 DOWN buttons — each decrements the corresponding digit.")]
    [SerializeField] private Button[] downButtons = new Button[3];

    [SerializeField] private Button submitButton;
    [SerializeField] private Text   feedbackText;

    [Header("Correct Combination")]
    [Tooltip("Correct 3-digit code. Default: 4, 2, 7")]
    [SerializeField] private int[] correctCombination = { 4, 2, 7 };

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   correctSound;
    [SerializeField] private AudioClip   wrongSound;
    [SerializeField] private AudioClip   clickSound;

    [Header("Anxiety — Wrong Attempt")]
    [SerializeField] private float anxietyOnWrong = 10f;

    [Header("Requirement Hint")]
    [Tooltip("If true, shows a message if not all 3 digits are found yet.")]
    [SerializeField] private bool requireAllDigitsFound = true;

    // ─── State ────────────────────────────────────────────────────────────────
    private int[] _current = { 0, 0, 0 };
    private bool  _isSolved;
    private bool  _isOpen;

    void Start()
    {
        if (lockCanvas != null) lockCanvas.SetActive(false);

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            if (upButtons.Length   > i && upButtons[idx]   != null) upButtons[idx].onClick.AddListener(()   => Increment(idx));
            if (downButtons.Length > i && downButtons[idx] != null) downButtons[idx].onClick.AddListener(() => Decrement(idx));
        }

        if (submitButton != null) submitButton.onClick.AddListener(CheckCombination);
        UpdateDisplay();
    }

    void Update()
    {
        if (_isOpen && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
            CloseLock();
    }

    public void Interact()
    {
        if (_isSolved) return;
        if (_isOpen) { CloseLock(); return; }

        if (requireAllDigitsFound && GameStateManager.Instance != null)
        {
            bool hasAll = GameStateManager.Instance.Digit1Found &&
                          GameStateManager.Instance.Digit2Found &&
                          GameStateManager.Instance.Digit3Found;
            if (!hasAll)
            {
                Debug.Log("[ComboLock] Not all digits found yet!");
                if (feedbackText != null)
                {
                    if (lockCanvas != null) lockCanvas.SetActive(true);
                    feedbackText.text = "You need all 3 digits first...";
                }
                return;
            }
        }

        OpenLock();
    }

    public string GetPromptText() => _isSolved ? "Opened" : "Use Combo Lock";

    void Increment(int idx) { _current[idx] = (_current[idx] + 1) % 10; UpdateDisplay(); PlaySound(clickSound); }
    void Decrement(int idx) { _current[idx] = (_current[idx] + 9) % 10; UpdateDisplay(); PlaySound(clickSound); }

    void UpdateDisplay()
    {
        for (int i = 0; i < 3; i++)
            if (digitTexts.Length > i && digitTexts[i] != null)
                digitTexts[i].text = _current[i].ToString();
    }

    void CheckCombination()
    {
        bool correct = true;
        for (int i = 0; i < 3; i++)
            if (correctCombination.Length <= i || _current[i] != correctCombination[i])
                { correct = false; break; }

        if (correct)
        {
            _isSolved = true;
            PlaySound(correctSound);
            if (feedbackText != null) feedbackText.text = "Unlocked!";
            Debug.Log("[ComboLock] Correct combination! Key obtained.");

            if (DependencyRoomManager.Instance != null)
                DependencyRoomManager.Instance.OnComboLockSolved();
            else
                GameStateManager.Instance?.CollectHallwayKey();
        }
        else
        {
            PlaySound(wrongSound);
            if (feedbackText != null) feedbackText.text = "Incorrect code.";
            if (AnxietyManager.Instance != null)
                AnxietyManager.Instance.AddAnxiety(anxietyOnWrong);
            Debug.Log("[ComboLock] Wrong combination!");
        }
    }

    void OpenLock()
    {
        if (lockCanvas != null) lockCanvas.SetActive(true);
        _isOpen = true;
        if (feedbackText != null) feedbackText.text = "";
    }

    void CloseLock()
    {
        if (lockCanvas != null) lockCanvas.SetActive(false);
        _isOpen = false;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Force Solve Lock")]
    void DebugSolve()
    {
        _isSolved = true;
        DependencyRoomManager.Instance?.OnComboLockSolved();
        Debug.Log("[ComboLock] DEBUG: Force solved.");
    }
#endif
}

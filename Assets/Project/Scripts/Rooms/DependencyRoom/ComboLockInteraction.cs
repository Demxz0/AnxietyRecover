using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3-digit combination lock box.
/// The player must enter the correct digits to get the hallway key.
///
/// HOW IT WORKS:
///   - Each digit slot has UP and DOWN buttons to cycle 0-9.
///   - A Submit button checks the combination.
///   - Correct → lock opens, lid rotates, key disappears (collected).
///   - Wrong → anxiety spike + feedback.
///
/// SETUP:
///   1. Attach to the combo lock box 3D object.
///   2. Create a puzzle Canvas with:
///        - 3 digit Text labels + UP/DOWN buttons per digit.
///        - A Submit Button.
///        - A Result Text for feedback.
///   3. Assign all references in the Inspector.
///   4. Assign 'keyObject' to the 3D key mesh inside the lockbox — it hides on solve.
///   5. Default combination: 4, 2, 7. Change via correctCombination array.
/// </summary>
public class ComboLockInteraction : MonoBehaviour, IInteractable
{
    [Header("Animation")]
    [Tooltip("The lid/cover Transform that rotates open on the X axis.")]
    [SerializeField] private Transform coverTransform;
    [Tooltip("Target X rotation when the lid is fully open.")]
    [SerializeField] private float openAngleX = 90f;
    [Tooltip("Degrees per second the lid rotates.")]
    [SerializeField] private float openSpeed = 100f;

    [Header("Key Object")]
    [Tooltip("The 3D key mesh inside the lockbox. It will be hidden when the player collects the key.")]
    [SerializeField] private GameObject keyObject;

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

    [Header("Close Button")]
    [Tooltip("Button on the lock canvas that closes it. Must be assigned — keyboard shortcuts are disabled.")]
    [SerializeField] private Button closeButton;

    [Header("Anxiety — Wrong Attempt")]
    [SerializeField] private float anxietyOnWrong = 10f;

    [Header("Requirement")]
    [Tooltip("If true, shows a hint if not all 3 digits are found yet.")]
    [SerializeField] private bool requireAllDigitsFound = true;

    // ─── State ────────────────────────────────────────────────────────────────
    private int[] _current = { 0, 0, 0 };
    private bool  _isSolved;
    private bool  _isOpen;
    private float _timeOpened;

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
        if (closeButton  != null) closeButton.onClick.AddListener(CloseLock);
        UpdateDisplay();
    }

    // Canvas is closed exclusively via the close button — no keyboard shortcut.

    void Update()
    {
        // Animate lid opening after solve
        if (_isSolved && coverTransform != null)
        {
            Vector3 rot = coverTransform.localEulerAngles;
            float newX = Mathf.MoveTowardsAngle(rot.x, openAngleX, openSpeed * Time.deltaTime);
            coverTransform.localEulerAngles = new Vector3(newX, rot.y, rot.z);
        }
    }

    public void Interact()
    {
        if (_isSolved) return;
        if (_isOpen)   return; // already open — only the close button can dismiss it

        if (requireAllDigitsFound && GameStateManager.Instance != null)
        {
            bool hasAll = GameStateManager.Instance.Digit1Found &&
                          GameStateManager.Instance.Digit2Found &&
                          GameStateManager.Instance.Digit3Found;
            if (!hasAll)
            {
                OpenLock();
                if (feedbackText != null) feedbackText.text = "You need all 3 digits first...";
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
            Debug.Log("[ComboLock] Correct combination! Box opened — click the key to pick it up.");

            // Make sure the key is visible and interactable (BoxKeyPickup handles actual pickup)
            if (keyObject != null) keyObject.SetActive(true);

            // Close the canvas so the player can interact with the world
            CloseLock();
        }
        else
        {
            PlaySound(wrongSound);
            if (feedbackText != null) feedbackText.text = "Incorrect code.";
            if (AnxietyManager.Instance != null)
                AnxietyManager.Instance.AddAnxiety(anxietyOnWrong);
            Debug.Log("[ComboLock] Wrong combination.");
        }
    }

    void OpenLock()
    {
        if (lockCanvas != null) lockCanvas.SetActive(true);
        _isOpen = true;
        _timeOpened = Time.time;
        if (feedbackText != null) feedbackText.text = "";
        UIInputMode.Enter();
        // Force cursor visible in case UIInputMode was in a stale state
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void CloseLock()
    {
        if (lockCanvas != null) lockCanvas.SetActive(false);
        _isOpen = false;
        UIInputMode.Exit();
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
        // Keep key visible — player must still click it to collect
        if (keyObject != null) keyObject.SetActive(true);
        Debug.Log("[ComboLock] DEBUG: Force solved — click the key to pick it up.");
    }
#endif
}

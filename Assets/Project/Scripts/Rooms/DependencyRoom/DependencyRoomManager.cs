using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the entire Dependency Room (Office) puzzle sequence.
/// All other Dependency Room scripts report back to this manager.
///
/// PUZZLE STAGES:
///   0  Room entered → phone starts ringing, phoneRingCanvas shown
///   1  Player picks up phone → NPC clip plays
///   2  Call cuts off immediately after NPC clip → anxiety spike → PhoneBusy
///   3  Player reads desk paper → Digit 1 revealed (optional but helpful)
///   4  Player reads trash paper → anxiety STOPS increasing
///   5  Player uses computer → Digit 2 revealed
///   6  Player solves piano (clock hint) → Digit 3 revealed
///   7  Player enters correct combo → gets key
///
/// SETUP:
///   Attach to a Manager GameObject in the Dependency Room scene.
///   Assign all audio clips and triggers in the Inspector.
/// </summary>
public class DependencyRoomManager : MonoBehaviour
{
    public static DependencyRoomManager Instance { get; private set; }

    public enum Stage
    {
        WaitingForPhonePickup = 0,
        NpcPlaying            = 1,
        PhoneBusy             = 2,
        WaitingForComputer    = 3,
        WaitingForPiano       = 4,
        WaitingForLock        = 5,
        Complete              = 6
    }

    public Stage CurrentStage { get; private set; } = Stage.WaitingForPhonePickup;

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("NPC Audio Clip")]
    [Tooltip("The single NPC voice clip. After it ends the call cuts off immediately.")]
    [SerializeField] private AudioClip npcClip;

    [Header("Phone Ring Canvas")]
    [Tooltip("World-space canvas shown on/near the phone while it is ringing. " +
             "Assign the canvas GameObject here — it will be shown/hidden automatically.")]
    [SerializeField] private GameObject phoneRingCanvas;

    [Header("Office Phone")]
    [Tooltip("The OfficephoneInteraction component in the room.")]
    [SerializeField] private OfficephoneInteraction officePhone;

    [Header("Anxiety — Call Cut-off")]
    [Tooltip("Anxiety added when the NPC call suddenly cuts off.")]
    [SerializeField] private float anxietyOnCutOff = 15f;

    [Header("Anxiety — Gradual (after call-back attempt)")]
    [Tooltip("Anxiety added per second while the player is stuck after call-back and " +
             "has not yet found the trash paper. Stops once trash paper is read.")]
    [SerializeField] private float anxietyPerSecondAfterCallBack = 1.5f;

    [Header("Room Entry Trigger")]
    [Tooltip("Enable phone ringing when player enters the room.")]
    [SerializeField] private bool ringOnRoomEntry = true;

    // ─── Private State ────────────────────────────────────────────────────────
    private bool _trashPaperRead;
    private bool _helpGoneHintShown;
    private Coroutine _gradualAnxietyRoutine;
    private bool _hasCalledBack;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (ringOnRoomEntry)
            StartPhoneRinging();
    }

    // ─── Stage Progression API ───────────────────────────────────────────────

    /// <summary>Called by PhoneInteraction when player picks up the cellphone.</summary>
    public void OnPhonePickedUp()
    {
        if (CurrentStage >= Stage.PhoneBusy)
        {
            if (_hasCalledBack) return;
            _hasCalledBack = true;

            // Player tries to call back — line is busy
            Debug.Log("[DependencyRoom] Call-back attempt — number unavailable.");
            AudioManager.Instance?.PlayOneShot(SoundID.CallbackUnavailable);

            StartCoroutine(ShowHelpGoneHintWithDelay(2f));

            if (!_trashPaperRead && _gradualAnxietyRoutine == null)
                _gradualAnxietyRoutine = StartCoroutine(GradualAnxietyRoutine());
            return;
        }

        if (CurrentStage != Stage.WaitingForPhonePickup) return;

        StopPhoneRinging();
        CurrentStage = Stage.NpcPlaying;
        Debug.Log("[DependencyRoom] Stage 1 — NPC clip playing.");
        StartCoroutine(PlayNpcThenCutOff());
    }

    /// <summary>Called by PaperOnDeskInteraction when player reads the desk paper.
    /// Gives Digit 1 — does NOT gate the call cut-off.</summary>
    public void OnDeskPaperRead()
    {
        GameStateManager.Instance?.SetSeenDeskPaper();
        GameStateManager.Instance?.SetDigit1Found();
        Debug.Log("[DependencyRoom] Desk paper read — Digit 1 found.");
    }

    /// <summary>Called by TrashPaperInteraction when player reads the trash paper.</summary>
    public void OnTrashPaperRead()
    {
        if (CurrentStage < Stage.PhoneBusy) return;

        _trashPaperRead = true;
        GameStateManager.Instance?.SetSeenTrashPaper();
        Debug.Log("[DependencyRoom] Trash paper read — anxiety drain stopped.");

        if (_gradualAnxietyRoutine != null)
        {
            StopCoroutine(_gradualAnxietyRoutine);
            _gradualAnxietyRoutine = null;
        }

        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.ReduceOneLevel();
    }

    /// <summary>Called by ComputerInteraction when computer is successfully accessed.</summary>
    public void OnComputerAccessed()
    {
        if (GameStateManager.Instance == null) return;

        GameStateManager.Instance.SetComputerUnlocked();
        GameStateManager.Instance.SetDigit2Found();

        if (CurrentStage == Stage.WaitingForComputer)
        {
            CurrentStage = Stage.WaitingForPiano;
            Debug.Log("[DependencyRoom] Stage → waiting for piano puzzle.");
        }
    }

    /// <summary>Called by PianoNoteInteraction when piano puzzle is solved.</summary>
    public void OnPianoSolved()
    {
        if (CurrentStage < Stage.WaitingForPiano) return;

        GameStateManager.Instance?.SetDigit3Found();
        CurrentStage = Stage.WaitingForLock;
        Debug.Log("[DependencyRoom] Stage — all 3 digits found! Waiting for combo lock.");
    }

    /// <summary>Called by BoxKeyPickup when the player clicks the key after opening the lock.</summary>
    public void OnComboLockSolved()
    {
        if (CurrentStage == Stage.Complete) return;
        CurrentStage = Stage.Complete;
        GameStateManager.Instance?.CollectHallwayKey();
        Debug.Log("[DependencyRoom] Key picked up — room complete!");

        if (officePhone != null)
            officePhone.StartRinging();
        else
            Debug.LogWarning("[DependencyRoom] OfficephoneInteraction not assigned.");
    }

    // ─── Phone Helpers ────────────────────────────────────────────────────────

    public void StartPhoneRinging()
    {
        AudioManager.Instance?.PlayLoop(SoundID.CellphoneRing);

        // Show the world-space canvas on the phone
        if (phoneRingCanvas != null)
            phoneRingCanvas.SetActive(true);
    }

    public void StopPhoneRinging()
    {
        AudioManager.Instance?.Stop(SoundID.CellphoneRing);
    }

    // ─── Coroutines ───────────────────────────────────────────────────────────

    /// <summary>
    /// Plays the single NPC clip, then IMMEDIATELY plays the cut-off sound.
    /// No paper read required — the cut-off always fires after the clip ends.
    /// </summary>
    IEnumerator PlayNpcThenCutOff()
    {
        float duration = AudioManager.Instance != null ? AudioManager.Instance.PlayNpcClip(npcClip) : 0f;
        if (duration <= 0f)
        {
            Debug.Log("[DependencyRoom] NPC clip not assigned — skipping audio, proceeding to cut-off.");
            duration = 1f;
        }
        yield return new WaitForSeconds(duration);

        // ── Cut off immediately after the NPC clip ends ──
        AudioManager.Instance?.PlayOneShot(SoundID.CallCutOff);
        Debug.Log("[DependencyRoom] Call cut off! Player is on their own.");

        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.AddAnxiety(anxietyOnCutOff);

        // Player inner voice: "I should call them back"
        yield return new WaitForSeconds(1.5f);
        AudioManager.Instance?.PlayOneShot(SoundID.PlayerVoiceCallBack);

        // Wait a bit for the player voice to finish before allowing interaction
        yield return new WaitForSeconds(2f);
        
        CurrentStage = Stage.PhoneBusy;
        Debug.Log("[DependencyRoom] Player can now interact with the phone to attempt call back.");
        
        yield return new WaitForSeconds(0.5f);
        CurrentStage = Stage.WaitingForComputer;
        Debug.Log("[DependencyRoom] Waiting for player to access computer.");
    }

    IEnumerator GradualAnxietyRoutine()
    {
        Debug.Log("[DependencyRoom] Gradual anxiety started — player can't reach anyone.");
        while (!_trashPaperRead)
        {
            if (AnxietyManager.Instance != null && !AnxietyManager.Instance.IsPanicActive)
                AnxietyManager.Instance.AddAnxiety(anxietyPerSecondAfterCallBack * Time.deltaTime);
            yield return null;
        }
        Debug.Log("[DependencyRoom] Gradual anxiety stopped.");
    }

    IEnumerator ShowHelpGoneHintWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_helpGoneHintShown)
        {
            _helpGoneHintShown = true;
            ObjectiveHintManager.Instance?.ShowHint(
                "المساعدة انقطعت , عليك الإعتماد على نفسك لإيجاد المفتاح", 10f);
        }
    }

    // ─── Editor Helpers ───────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG: Skip to Stage — WaitingForComputer")]
    void DebugSkip()
    {
        StopPhoneRinging();
        GameStateManager.Instance?.SetSeenDeskPaper();
        GameStateManager.Instance?.SetDigit1Found();
        GameStateManager.Instance?.SetSeenTrashPaper();
        _trashPaperRead = true;
        CurrentStage = Stage.WaitingForComputer;
        Debug.Log("[DependencyRoom] DEBUG: Skipped to WaitingForComputer.");
    }
#endif
}

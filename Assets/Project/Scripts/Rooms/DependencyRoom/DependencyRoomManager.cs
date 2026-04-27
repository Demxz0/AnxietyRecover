using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the entire Dependency Room (Office) puzzle sequence.
/// All other Dependency Room scripts report back to this manager.
///
/// PUZZLE STAGES:
///   0  Room entered → phone starts ringing
///   1  Player picks up phone → NPC Part 1 plays (desk paper hint / Digit 1)
///   2  Player reads desk paper → NPC Part 2 plays (trash paper / password hint)
///   3  NPC call cuts off → phone busy → anxiety spike
///   4  Player reads trash paper → password known
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
        NpcPart1Playing       = 1,
        WaitingForDeskPaper   = 2,
        NpcPart2Playing       = 3,
        PhoneBusy             = 4,
        WaitingForComputer    = 5,
        WaitingForPiano       = 6,
        WaitingForLock        = 7,
        Complete              = 8
    }

    public Stage CurrentStage { get; private set; } = Stage.WaitingForPhonePickup;

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Phone Audio")]
    [Tooltip("Looping phone ring (plays until player picks up).")]
    [SerializeField] private AudioSource phoneRingSource;

    [Tooltip("NPC voice — Part 1: explains desk paper and Digit 1.")]
    [SerializeField] private AudioClip npcPart1Clip;

    [Tooltip("NPC voice — Part 2: hints about trash paper / password (before cut-off).")]
    [SerializeField] private AudioClip npcPart2Clip;

    [Tooltip("Static / glitch sound that plays when the call cuts off.")]
    [SerializeField] private AudioClip callCutOffClip;

    [Tooltip("Busy-tone sound played when player tries to call back after cut-off.")]
    [SerializeField] private AudioClip busyToneClip;

    [Tooltip("AudioSource on the phone object used to play NPC audio.")]
    [SerializeField] private AudioSource npcAudioSource;

    [Header("Anxiety — Call Cut-off")]
    [Tooltip("Anxiety added when the NPC call suddenly cuts off.")]
    [SerializeField] private float anxietyOnCutOff = 15f;

    [Header("Room Entry Trigger")]
    [Tooltip("Enable phone ringing when player enters the room. " +
             "Uses a trigger collider on this or a child GameObject.")]
    [SerializeField] private bool ringOnRoomEntry = true;

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

    /// <summary>Called by PhoneInteraction when player picks up the phone.</summary>
    public void OnPhonePickedUp()
    {
        if (CurrentStage >= Stage.PhoneBusy)
        {
            Debug.Log("the number can not be reached");
            PlayBusyTone();
            return;
        }

        if (CurrentStage != Stage.WaitingForPhonePickup) return;

        StopPhoneRinging();
        CurrentStage = Stage.NpcPart1Playing;
        Debug.Log("[DependencyRoom] Stage 1 — NPC Part 1 playing.");
        StartCoroutine(PlayNpcPart1());
    }

    /// <summary>Called by PaperOnDeskInteraction when player reads the desk paper.</summary>
    public void OnDeskPaperRead()
    {
        if (CurrentStage != Stage.WaitingForDeskPaper) return;

        GameStateManager.Instance?.SetSeenDeskPaper();
        GameStateManager.Instance?.SetDigit1Found();

        CurrentStage = Stage.NpcPart2Playing;
        Debug.Log("[DependencyRoom] Stage 3 — NPC Part 2 playing.");
        StartCoroutine(PlayNpcPart2ThenCutOff());
    }

    /// <summary>Called by TrashPaperInteraction when player reads the trash paper.</summary>
    public void OnTrashPaperRead()
    {
        if (CurrentStage < Stage.PhoneBusy) return;

        GameStateManager.Instance?.SetSeenTrashPaper();
        Debug.Log("[DependencyRoom] Trash paper read — password known.");
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
            Debug.Log("[DependencyRoom] Stage 5 → now waiting for piano puzzle.");
        }
    }

    /// <summary>Called by PianoNoteInteraction when piano puzzle is solved.</summary>
    public void OnPianoSolved()
    {
        if (CurrentStage < Stage.WaitingForPiano) return;

        GameStateManager.Instance?.SetDigit3Found();
        CurrentStage = Stage.WaitingForLock;
        Debug.Log("[DependencyRoom] Stage 6 — all 3 digits found! Waiting for combo lock.");
    }

    /// <summary>Called by ComboLockInteraction when the correct code is entered.</summary>
    public void OnComboLockSolved()
    {
        CurrentStage = Stage.Complete;
        GameStateManager.Instance?.CollectHallwayKey();
        Debug.Log("[DependencyRoom] Combo lock solved — key collected!");
    }

    // ─── Phone Helpers ────────────────────────────────────────────────────────
    public void StartPhoneRinging()
    {
        if (phoneRingSource != null && !phoneRingSource.isPlaying)
            phoneRingSource.Play();
    }

    public void StopPhoneRinging()
    {
        if (phoneRingSource != null) phoneRingSource.Stop();
    }

    void PlayBusyTone()
    {
        if (npcAudioSource != null && busyToneClip != null)
            npcAudioSource.PlayOneShot(busyToneClip);
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.AddAnxiety(3f);
        Debug.Log("[DependencyRoom] Phone is busy — line cut off.");
    }

    // ─── Coroutines ───────────────────────────────────────────────────────────
    IEnumerator PlayNpcPart1()
    {
        if (npcAudioSource != null && npcPart1Clip != null)
        {
            npcAudioSource.clip = npcPart1Clip;
            npcAudioSource.Play();
            yield return new WaitForSeconds(npcPart1Clip.length);
        }
        else
        {
            Debug.Log("[DependencyRoom] NPC Part 1 clip not assigned — skipping audio.");
            yield return new WaitForSeconds(1f);
        }

        // After part 1, wait for desk paper
        CurrentStage = Stage.WaitingForDeskPaper;
        Debug.Log("[DependencyRoom] Stage 2 — waiting for player to read desk paper.");
    }

    IEnumerator PlayNpcPart2ThenCutOff()
    {
        if (npcAudioSource != null && npcPart2Clip != null)
        {
            npcAudioSource.clip = npcPart2Clip;
            npcAudioSource.Play();
            yield return new WaitForSeconds(npcPart2Clip.length);
        }
        else
        {
            Debug.Log("[DependencyRoom] NPC Part 2 clip not assigned — skipping to cut-off.");
            yield return new WaitForSeconds(1f);
        }

        // Play static cut-off sound
        if (npcAudioSource != null && callCutOffClip != null)
            npcAudioSource.PlayOneShot(callCutOffClip);

        // Anxiety spike — the call cut off!
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.AddAnxiety(anxietyOnCutOff);

        CurrentStage = Stage.PhoneBusy;
        Debug.Log("[DependencyRoom] Stage 4 — call cut off! Player on their own.");

        // After the cut-off, player needs computer → advance internally
        yield return new WaitForSeconds(1f);
        CurrentStage = Stage.WaitingForComputer;
        Debug.Log("[DependencyRoom] Stage 5 — waiting for player to access computer.");
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
        CurrentStage = Stage.WaitingForComputer;
        Debug.Log("[DependencyRoom] DEBUG: Skipped to WaitingForComputer.");
    }
#endif
}

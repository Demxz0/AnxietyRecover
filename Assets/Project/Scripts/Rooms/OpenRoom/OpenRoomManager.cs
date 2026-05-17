using UnityEngine;

/// <summary>
/// Open Room — permanent safe zone. No panic attacks, no anxiety triggers.
///
/// BEHAVIOUR:
///   - On player entry: BlockPanicAttack() is called on AnxietyManager.
///   - On player exit:  UnblockPanicAttack() is called (normal rooms resume).
///   - Lights start OFF (RoomLightController with startsOff=true).
///   - If LoopRoom was already completed when the player arrives → lights turn on immediately.
///   - If player arrives before completing LoopRoom → lights stay off and turn on when LoopRoom completes.
///   - No AnxietyTriggers should be placed in this room at all.
///
/// SETUP:
///   1. Attach to a trigger zone that covers the entire Open Room (large BoxCollider, Is Trigger = true).
///   2. Assign openRoomLight (the ceiling RoomLightController with startsOff = true).
///   3. Ensure 'Tag' of the Player is "Player".
///   4. NO AnxietyTriggers should exist inside this room.
/// </summary>
public class OpenRoomManager : MonoBehaviour
{
    public static OpenRoomManager Instance { get; private set; }

    [Header("Room Light")]
    [Tooltip("The RoomLightController for this room's ceiling light (must have startsOff = true).")]
    [SerializeField] private RoomLightController openRoomLight;

    [Tooltip("Light reveal intensity when turning on.")]
    [SerializeField] private float revealIntensity = 1.2f;

    [Header("Window Sunlight (optional)")]
    [Tooltip("The WindowSunlightController for the open room window (if present).")]
    [SerializeField] private WindowSunlightController windowLight;

    [Header("Ending Logic")]
    [Tooltip("The blocking collider GameObject that prevents entering or moving in the room until all events are complete.")]
    [SerializeField] private GameObject openRoomBlocker;

    [Header("Narrator — Third Speech (Ending)")]
    [Tooltip("NarrativeEntry that plays when all 3 rooms are completed and the ending triggers.")]
    [SerializeField] private NarrativeEntry narratorThirdEntry;

    [Tooltip("World position where the narrator ending text will appear. " +
             "Create an empty child GO, place it where you want the text, and assign it here.")]
    [SerializeField] private Transform textAnchor;

    // ─── State ────────────────────────────────────────────────────────────────
    private bool _playerInside;
    private bool _lightsOn;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Subscribe to all rooms completion in case player arrives first
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnAllRoomsCompleted += HandleAllRoomsCompleted;

        // If all rooms were already done before arriving here, trigger the ending logic immediately
        if (GameStateManager.Instance != null && GameStateManager.Instance.AllRoomsCompleted)
            TriggerEndingLogic();
    }

    void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnAllRoomsCompleted -= HandleAllRoomsCompleted;
    }

    // ─── Room Entry / Exit ────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = true;

        // Block ALL panic attacks while in the Open Room
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.BlockPanicAttack();

        Debug.Log("[OpenRoom] Player entered — panic attacks BLOCKED.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = false;

        // Unblock panic attacks when leaving
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.UnblockPanicAttack();

        Debug.Log("[OpenRoom] Player exited — panic attacks unblocked.");
    }

    // ─── Lights & Ending Logic ───────────────────────────────────────────────
    void HandleAllRoomsCompleted()
    {
        // Wait until the previous narrator entry (e.g. Illusion Room completion) finishes speaking
        StartCoroutine(HandleAllRoomsCompletedRoutine());
    }

    private System.Collections.IEnumerator HandleAllRoomsCompletedRoutine()
    {
        if (NarrativeManager.Instance != null)
        {
            while (NarrativeManager.Instance.IsShowing)
                yield return null;
        }

        TriggerEndingLogic();
    }

    void TriggerEndingLogic()
    {
        if (_lightsOn) return;
        _lightsOn = true;

        // 1. Turn on lights
        if (openRoomLight != null)
        {
            openRoomLight.TurnOn(revealIntensity);
            Debug.Log("[OpenRoom] Ceiling light turned ON (all rooms completed).");
        }

        if (windowLight != null)
        {
            windowLight.enabled = true;
            Debug.Log("[OpenRoom] Window sunlight activated.");
        }

        // 2. Disable blocking collider
        if (openRoomBlocker != null)
        {
            openRoomBlocker.SetActive(false);
            Debug.Log("[OpenRoom] Blocking collider disabled.");
        }

        // 3. Reset anxiety
        if (AnxietyManager.Instance != null)
        {
            AnxietyManager.Instance.SetAnxiety(0f);
            Debug.Log("[OpenRoom] Anxiety reset to 0 (game ending triggered).");
        }

        // 4. Narrator's third speech — the final inner voice moment
        if (narratorThirdEntry != null && NarrativeManager.Instance != null)
            NarrativeManager.Instance.Show(narratorThirdEntry, textAnchor);
        else if (narratorThirdEntry == null)
            Debug.LogWarning("[OpenRoom] narratorThirdEntry not assigned — Narrator 3rd speech won't play.");

        // 5. Start the Open Room final music
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicState(AudioManager.MusicState.Open);
            Debug.Log("[OpenRoom] Final Open Room music started.");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Force Ending Logic")]
    void DebugTriggerEnding() => TriggerEndingLogic();

    [ContextMenu("DEBUG: Simulate Player Enter")]
    void DebugEnter()
    {
        _playerInside = true;
        AnxietyManager.Instance?.BlockPanicAttack();
        Debug.Log("[OpenRoom] DEBUG: Player simulated entering.");
    }
#endif
}

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
        // Subscribe to loop room completion in case player arrives first
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnLoopRoomCompleted += HandleLoopRoomCompleted;

        // If loop was already done before arriving here, turn on lights immediately
        if (GameStateManager.Instance != null && GameStateManager.Instance.LoopRoomCompleted)
            TurnOnLights();
    }

    void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnLoopRoomCompleted -= HandleLoopRoomCompleted;
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

    // ─── Lights ───────────────────────────────────────────────────────────────
    void HandleLoopRoomCompleted()
    {
        // Fires the moment LoopRoom is finished — turn on immediately
        TurnOnLights();
    }

    void TurnOnLights()
    {
        if (_lightsOn) return;
        _lightsOn = true;

        if (openRoomLight != null)
        {
            openRoomLight.TurnOn(revealIntensity);
            Debug.Log("[OpenRoom] Ceiling light turned ON (loop room completed).");
        }

        if (windowLight != null)
        {
            windowLight.enabled = true;
            Debug.Log("[OpenRoom] Window sunlight activated.");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Force Lights On")]
    void DebugLightsOn() => TurnOnLights();

    [ContextMenu("DEBUG: Simulate Player Enter")]
    void DebugEnter()
    {
        _playerInside = true;
        AnxietyManager.Instance?.BlockPanicAttack();
        Debug.Log("[OpenRoom] DEBUG: Player simulated entering.");
    }
#endif
}

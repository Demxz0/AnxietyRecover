using System;
using UnityEngine;

/// <summary>
/// Global singleton that tracks all persistent game flags and inventory.
/// Subscribe to events to react to state changes without tight coupling.
///
/// SETUP: Place on a single persistent GameObject in your scene.
/// It uses DontDestroyOnLoad so it survives any scene transitions.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    // ─── Inventory ──────────────────────────────────────────────────────────
    public bool HasHallwayKey   { get; private set; }
    public bool HasSwitchPiece  { get; private set; }

    // ─── Dependency Room Progress ────────────────────────────────────────────
    public bool HasSeenDeskPaper    { get; private set; }
    public bool HasSeenTrashPaper   { get; private set; }
    public bool KnowsPassword       { get; private set; }
    public bool ComputerUnlocked    { get; private set; }
    public bool Digit1Found         { get; private set; }
    public bool Digit2Found         { get; private set; }
    public bool Digit3Found         { get; private set; }

    // ─── Room Completion ─────────────────────────────────────────────────────
    public bool DependencyRoomCompleted { get; private set; }
    public bool IllusionRoomCompleted   { get; private set; }
    public bool LoopRoomCompleted       { get; private set; }

    // ─── Illusion Room — Switch Piece ─────────────────────────────────────────
    /// <summary>
    /// True once the light-switch piece has been placed in the Illusion Room.
    /// BathroomManager uses this to decide if the bathroom effect should activate.
    /// </summary>
    public bool IsSwitchPiecePlaced { get; private set; }

    // ─── Events ──────────────────────────────────────────────────────────────
    public event Action OnHallwayKeyPickup;
    public event Action OnSwitchPiecePickup;
    public event Action OnDependencyRoomCompleted;
    public event Action OnIllusionRoomCompleted;
    public event Action OnLoopRoomCompleted;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    // ─── Inventory ───────────────────────────────────────────────────────────
    public void CollectHallwayKey()
    {
        if (HasHallwayKey) return;
        HasHallwayKey = true;
        Debug.Log("[GameState] Key collected.");
        OnHallwayKeyPickup?.Invoke();
    }

    public void CollectSwitchPiece()
    {
        if (HasSwitchPiece) return;
        HasSwitchPiece = true;
        Debug.Log("[GameState] Switch piece collected.");
        OnSwitchPiecePickup?.Invoke();
    }

    // ─── Dependency Room ─────────────────────────────────────────────────────
    public void SetSeenDeskPaper()
    {
        if (HasSeenDeskPaper) return;
        HasSeenDeskPaper = true;
        Debug.Log("[GameState] Desk paper read.");
    }

    public void SetSeenTrashPaper()
    {
        if (HasSeenTrashPaper) return;
        HasSeenTrashPaper = true;
        KnowsPassword = true;
        Debug.Log("[GameState] Trash paper read — password known.");
    }

    public void SetComputerUnlocked()
    {
        if (ComputerUnlocked) return;
        ComputerUnlocked = true;
        Debug.Log("[GameState] Computer unlocked.");
    }

    public void SetDigit1Found() { if (!Digit1Found) { Digit1Found = true; Debug.Log("[GameState] Digit 1 found!"); } }
    public void SetDigit2Found() { if (!Digit2Found) { Digit2Found = true; Debug.Log("[GameState] Digit 2 found!"); } }
    public void SetDigit3Found() { if (!Digit3Found) { Digit3Found = true; Debug.Log("[GameState] Digit 3 found!"); } }

    // ─── Room Completion ─────────────────────────────────────────────────────
    public void CompleteDependencyRoom()
    {
        if (DependencyRoomCompleted) return;
        DependencyRoomCompleted = true;
        Debug.Log("[GameState] Dependency Room COMPLETE.");
        OnDependencyRoomCompleted?.Invoke();
    }

    public void CompleteIllusionRoom()
    {
        if (IllusionRoomCompleted) return;
        IllusionRoomCompleted = true;
        // The switch piece being placed is what completes the Illusion Room.
        SetSwitchPiecePlaced();
        Debug.Log("[GameState] Illusion Room COMPLETE.");
        OnIllusionRoomCompleted?.Invoke();
    }

    /// <summary>
    /// Call when the light switch piece is placed in the Illusion Room socket.
    /// Prevents the bathroom effect from triggering again.
    /// </summary>
    public void SetSwitchPiecePlaced()
    {
        if (IsSwitchPiecePlaced) return;
        IsSwitchPiecePlaced = true;
        Debug.Log("[GameState] Switch piece placed — bathroom effect locked out.");
    }

    public void CompleteLoopRoom()
    {
        if (LoopRoomCompleted) return;
        LoopRoomCompleted = true;
        Debug.Log("[GameState] Loop Room COMPLETE.");
        OnLoopRoomCompleted?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Give All Items")]
    void DebugGiveAll()
    {
        CollectHallwayKey();
        CollectSwitchPiece();
        SetDigit1Found();
        SetDigit2Found();
        SetDigit3Found();
    }
#endif
}

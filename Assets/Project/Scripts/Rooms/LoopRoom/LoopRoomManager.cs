using System.Collections;
using UnityEngine;

/// <summary>
/// Loop Room (Kitchen) — Agoraphobia simulation.
///
/// HOW IT WORKS:
///   - The player is trapped in the kitchen for a set duration (default: 60s).
///   - Every time the player tries to open the exit door, they are teleported back
///     to the room's respawn point — the loop effect.
///   - A slow, continuous anxiety increase runs during the wait to encourage breathing.
///   - After the wait time, the exit unlocks and the room is marked complete.
///   - On completion, an event fires that the Open Room listens to (to turn on its lights).
///
/// SETUP:
///   1. Attach to a Manager GameObject in the Kitchen.
///   2. Set the exit door reference — the script intercepts the door's normal Interact().
///      Instead of using DoorController on the exit door, put LoopDoorInteractable on it.
///   3. Assign the player respawn point Transform (a position inside the kitchen).
///   4. Assign the player Transform (or tag it "Player").
///   5. Optional: Assign an AudioSource for a looping ambient clock/tension sound.
/// </summary>
public class LoopRoomManager : MonoBehaviour
{
    public static LoopRoomManager Instance { get; private set; }

    [Header("Loop Settings")]
    [Tooltip("How many seconds the player must wait before the loop ends.")]
    [SerializeField] private float loopDuration = 60f;

    [Tooltip("Spawn point inside the kitchen the player is returned to.")]
    [SerializeField] private Transform respawnPoint;

    [Tooltip("The player's CharacterController or Transform. Tag the player 'Player'.")]
    [SerializeField] private Transform playerTransform;

    [Header("Anxiety")]
    [Tooltip("Anxiety added per second while trapped. Keep low to encourage breathing, not panic.")]
    [SerializeField] private float anxietyPerSecond = 2f;

    [Tooltip("Anxiety added each time the player tries to exit and gets looped back.")]
    [SerializeField] private float anxietyOnLoopBack = 5f;

    [Header("Audio (optional)")]
    [Tooltip("Looping ambient sound while trapped (e.g. kitchen hum, clock ticking).")]
    [SerializeField] private AudioSource ambientSource;

    [Tooltip("Sound played each time the player is teleported back.")]
    [SerializeField] private AudioSource loopBackSource;
    [SerializeField] private AudioClip   loopBackSound;

    // ─── State ────────────────────────────────────────────────────────────────
    public bool LoopComplete { get; private set; }
    private float _elapsedTime;
    private bool  _started;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── Entry ────────────────────────────────────────────────────────────────

    /// <summary>Call this when the player first enters the kitchen.</summary>
    public void StartLoop()
    {
        if (_started || LoopComplete) return;
        _started = true;

        if (ambientSource != null && !ambientSource.isPlaying)
            ambientSource.Play();

        StartCoroutine(LoopTimerRoutine());
        Debug.Log("[LoopRoom] Loop started — player must wait 60 seconds.");
    }

    // ─── Called by LoopDoorInteractable ──────────────────────────────────────

    /// <summary>
    /// Called whenever the player interacts with the exit door.
    /// Returns true if the loop is over (door should open normally).
    /// Returns false if the player should be teleported back.
    /// </summary>
    public bool OnPlayerTriesToExit()
    {
        if (LoopComplete)
        {
            Debug.Log("[LoopRoom] Loop over — player may exit.");
            return true;
        }

        TeleportPlayerBack();
        return false;
    }

    // ─── Private ─────────────────────────────────────────────────────────────
    IEnumerator LoopTimerRoutine()
    {
        while (_elapsedTime < loopDuration)
        {
            _elapsedTime += Time.deltaTime;

            // Gentle continuous anxiety
            if (AnxietyManager.Instance != null && !AnxietyManager.Instance.IsPanicActive)
                AnxietyManager.Instance.AddAnxiety(anxietyPerSecond * Time.deltaTime);

            yield return null;
        }

        EndLoop();
    }

    void TeleportPlayerBack()
    {
        if (playerTransform == null)
        {
            // Try to find by tag
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null && respawnPoint != null)
        {
            // Disable CharacterController temporarily for teleport if present
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.position = respawnPoint.position;

            if (cc != null) cc.enabled = true;
        }

        // Anxiety spike
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.AddAnxiety(anxietyOnLoopBack);

        // Play loop-back sound
        if (loopBackSource != null && loopBackSound != null)
            loopBackSource.PlayOneShot(loopBackSound);

        float remaining = loopDuration - _elapsedTime;
        Debug.Log($"[LoopRoom] Player looped back. Time remaining: {remaining:F0}s.");
    }

    void EndLoop()
    {
        LoopComplete = true;

        if (ambientSource != null) ambientSource.Stop();

        GameStateManager.Instance?.CompleteLoopRoom();
        Debug.Log("[LoopRoom] Loop COMPLETE — exit door unlocked!");
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Skip Loop (Complete Immediately)")]
    void DebugSkip()
    {
        _elapsedTime = loopDuration;
        EndLoop();
    }
#endif
}

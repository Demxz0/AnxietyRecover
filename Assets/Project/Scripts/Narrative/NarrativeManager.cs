using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central controller for all narrative text moments in the game.
/// Uses an object pool of WorldFloatTextRenderer prefabs — no runtime Instantiate.
///
/// USAGE:
///   // Text spawns at the anchor's world position:
///   NarrativeManager.Instance.Show(myEntry, myAnchorTransform);
///
///   // Text spawns in front of the player (no anchor):
///   NarrativeManager.Instance.Show(myEntry);
///
/// Each individual caller passes its OWN anchor Transform so every text
/// has its own fixed world position. The global spawnAnchor has been removed.
///
/// SETUP:
///   1. Create a GameObject "NarrativeManager" and attach this script.
///   2. Assign 'floatTextPrefab' (a prefab with WorldFloatTextRenderer).
///   3. Assign 'playerTransform' (the player root or camera).
///   4. Assign 'mainCamera'.
///   5. Set 'poolSize' (default 5 is enough for WebGL).
/// </summary>
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }

    [Header("Pool")]
    [Tooltip("Prefab with WorldFloatTextRenderer. Pre-instantiated at start — no runtime allocs.")]
    [SerializeField] private WorldFloatTextRenderer floatTextPrefab;
    [SerializeField] private int poolSize = 5;

    [Header("Scene References")]
    [Tooltip("The player's root Transform (or camera Transform) — fallback when no anchor is given.")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera mainCamera;

    // ─── Queue stores both entry AND its anchor so position is preserved ──────
    private struct QueuedEntry
    {
        public NarrativeEntry entry;
        public Transform      anchor; // null = spawn in front of player
    }

    // ─── Object Pool ──────────────────────────────────────────────────────────
    private List<WorldFloatTextRenderer> _pool = new List<WorldFloatTextRenderer>();

    // ─── Queue ────────────────────────────────────────────────────────────────
    private Queue<QueuedEntry> _queue = new Queue<QueuedEntry>();
    private bool _isShowing;

    /// <summary>True if a narrative entry is currently being displayed or spoken.</summary>
    public bool IsShowing => _isShowing;

    /// <summary>Fired when the current entry and all queued entries have finished displaying.</summary>
    public event System.Action OnNarrativeFinished;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (mainCamera == null) mainCamera = Camera.main;

        // Player fallback: prefer explicit assignment → tag search → camera itself
        if (playerTransform == null)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
                playerTransform = playerGO.transform;
            else if (mainCamera != null)
                playerTransform = mainCamera.transform;
        }

        Debug.Log($"[NarrativeManager] playerTransform = {(playerTransform != null ? playerTransform.name + " @ " + playerTransform.position : "NULL")}  |  " +
                  $"camera = {(mainCamera != null ? mainCamera.name : "NULL")}");

        BuildPool();
    }

    void BuildPool()
    {
        if (floatTextPrefab == null)
        {
            Debug.LogWarning("[NarrativeManager] floatTextPrefab not assigned! Text display will not work.");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            var instance = Instantiate(floatTextPrefab);
            instance.gameObject.SetActive(false);
            _pool.Add(instance);
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Show a NarrativeEntry at the given anchor's world position.
    /// If anchor is null, text spawns in front of the player camera instead.
    /// If something is already showing, the entry is queued.
    /// </summary>
    public void Show(NarrativeEntry entry, Transform anchor = null)
    {
        if (entry == null) return;

        if (_isShowing)
        {
            _queue.Enqueue(new QueuedEntry { entry = entry, anchor = anchor });
            return;
        }

        StartCoroutine(ShowRoutine(entry, anchor));
    }

    /// <summary>
    /// Immediately interrupt whatever is showing and display this entry.
    /// Use sparingly (e.g., panic attack starts — clear any floating text).
    /// </summary>
    public void ShowImmediate(NarrativeEntry entry, Transform anchor = null)
    {
        foreach (var r in _pool)
            if (r.InUse) r.ForceHide();

        _queue.Clear();
        _isShowing = false;

        if (entry != null) StartCoroutine(ShowRoutine(entry, anchor));
    }

    /// <summary>Clear all queued and active text immediately.</summary>
    public void ClearAll()
    {
        foreach (var r in _pool)
            if (r.InUse) r.ForceHide();
        _queue.Clear();
        _isShowing = false;
        
        // Unblock panic attacks if they were blocked
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.UnblockPanicAttack();
        
        StopAllCoroutines();
        OnNarrativeFinished?.Invoke();
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    IEnumerator ShowRoutine(NarrativeEntry entry, Transform anchor)
    {
        _isShowing = true;

        // Block panic attacks while narrator is speaking
        if (entry.narratorClip != null && AnxietyManager.Instance != null)
            AnxietyManager.Instance.BlockPanicAttack();

        WorldFloatTextRenderer renderer = GetAvailableRenderer();
        if (renderer == null)
        {
            Debug.LogWarning("[NarrativeManager] Pool exhausted — skipping this entry.");
            _isShowing = false;
            if (entry.narratorClip != null && AnxietyManager.Instance != null)
                AnxietyManager.Instance.UnblockPanicAttack();
            ProcessQueue();
            yield break;
        }

        // Play narrator voice if assigned
        float narratorDuration = 0f;
        if (entry.narratorClip != null && AudioManager.Instance != null)
        {
            narratorDuration = AudioManager.Instance.PlayNarratorClip(entry.narratorClip);
            Debug.Log($"[NarrativeManager] Narrator clip duration: {narratorDuration}s for entry: {entry.name}");
        }

        // anchor != null → use that exact world position and rotation; null → spawn in front of player
        Vector3? overridePos = anchor != null ? anchor.position : (Vector3?)null;
        Quaternion? overrideRot = anchor != null ? anchor.rotation : (Quaternion?)null;
        renderer.Show(entry, playerTransform, mainCamera, overridePos, overrideRot);

        // Calculate wait time based on whether we're waiting for narrator or using display duration
        float waitTime;
        if (entry.narratorClip != null && narratorDuration > 0f)
        {
            // If narrator audio is present, always wait for its full duration + fade out
            waitTime = narratorDuration + entry.fadeOutTime;
            Debug.Log($"[NarrativeManager] Waiting for narrator: {narratorDuration}s + fade {entry.fadeOutTime}s = {waitTime}s total");
        }
        else if (entry.displayDuration > 0f)
        {
            // Use explicit display duration if set
            waitTime = entry.fadeInTime + entry.displayDuration + entry.fadeOutTime;
            Debug.Log($"[NarrativeManager] Using display duration: {waitTime}s");
        }
        else
        {
            // Default to 3 seconds
            waitTime = entry.fadeInTime + 3f + entry.fadeOutTime;
            Debug.Log($"[NarrativeManager] Using default duration: {waitTime}s");
        }

        yield return new WaitForSeconds(waitTime);

        // Unblock panic attacks after narrator finishes
        if (entry.narratorClip != null && AnxietyManager.Instance != null)
            AnxietyManager.Instance.UnblockPanicAttack();

        _isShowing = false;
        ProcessQueue();
    }

    void ProcessQueue()
    {
        if (_queue.Count > 0)
        {
            QueuedEntry next = _queue.Dequeue();
            StartCoroutine(ShowRoutine(next.entry, next.anchor));
        }
        else
        {
            // The queue is empty, and _isShowing is false. We are completely done.
            OnNarrativeFinished?.Invoke();
        }
    }

    WorldFloatTextRenderer GetAvailableRenderer()
    {
        foreach (var r in _pool)
            if (!r.InUse) return r;
        return null;
    }

    // ─── Editor Helpers ───────────────────────────────────────────────────────

#if UNITY_EDITOR
    [Header("Editor Test")]
    [SerializeField] private NarrativeEntry testEntry;
    [Tooltip("Optional anchor for the test entry — leave None to spawn in front of player.")]
    [SerializeField] private Transform testAnchor;

    [ContextMenu("Test: Show Entry")]
    void TestShow()
    {
        if (testEntry != null) Show(testEntry, testAnchor);
        else Debug.Log("[NarrativeManager] Assign testEntry in Inspector first.");
    }

    [ContextMenu("Test: Clear All")]
    void TestClear() => ClearAll();
#endif
}

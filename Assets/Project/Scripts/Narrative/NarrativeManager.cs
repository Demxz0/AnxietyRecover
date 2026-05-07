using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central controller for all narrative text moments in the game.
/// Uses an object pool of WorldFloatTextRenderer prefabs — no runtime Instantiate.
///
/// USAGE (from any script):
///   NarrativeManager.Instance.Show(myNarrativeEntry);
///
/// The manager handles:
///   • Spawning text in world-space in front of the player
///   • Preventing overlap (queues the next entry if one is already showing)
///   • Playing narrator voice through AudioManager
///   • Respecting waitForNarrator flag
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
    [Tooltip("The player's root Transform (or camera Transform) — used to position text in front of player.")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera mainCamera;

    // ─── Object Pool ──────────────────────────────────────────────────────────
    private List<WorldFloatTextRenderer> _pool = new List<WorldFloatTextRenderer>();

    // ─── Queue ────────────────────────────────────────────────────────────────
    private Queue<NarrativeEntry> _queue = new Queue<NarrativeEntry>();
    private bool _isShowing;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (mainCamera == null) mainCamera = Camera.main;
        if (playerTransform == null && Camera.main != null) playerTransform = Camera.main.transform;

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
            var instance = Instantiate(floatTextPrefab, transform);
            instance.gameObject.SetActive(false);
            _pool.Add(instance);
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Show a NarrativeEntry. If something is already showing, it is queued.
    /// </summary>
    public void Show(NarrativeEntry entry)
    {
        if (entry == null) return;

        if (_isShowing)
        {
            _queue.Enqueue(entry);
            return;
        }

        StartCoroutine(ShowRoutine(entry));
    }

    /// <summary>
    /// Immediately interrupt whatever is showing and display this entry.
    /// Use sparingly (e.g., panic attack starts — clear any floating text).
    /// </summary>
    public void ShowImmediate(NarrativeEntry entry)
    {
        // Force-hide all active renderers
        foreach (var r in _pool)
            if (r.InUse) r.ForceHide();

        _queue.Clear();
        _isShowing = false;

        if (entry != null) StartCoroutine(ShowRoutine(entry));
    }

    /// <summary>Clear all queued and active text immediately.</summary>
    public void ClearAll()
    {
        foreach (var r in _pool)
            if (r.InUse) r.ForceHide();
        _queue.Clear();
        _isShowing = false;
        StopAllCoroutines();
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    IEnumerator ShowRoutine(NarrativeEntry entry)
    {
        _isShowing = true;

        // Get a pooled renderer
        WorldFloatTextRenderer renderer = GetAvailableRenderer();
        if (renderer == null)
        {
            Debug.LogWarning("[NarrativeManager] Pool exhausted — skipping this entry.");
            _isShowing = false;
            ProcessQueue();
            yield break;
        }

        // Play narrator voice if assigned
        float narratorDuration = 0f;
        if (entry.narratorClip != null && AudioManager.Instance != null)
            narratorDuration = AudioManager.Instance.PlayNarratorClip(entry.narratorClip);

        // Show the text
        renderer.Show(entry, playerTransform, mainCamera);

        // Wait: for narrator finish (if flagged) OR for the renderer to finish
        float waitTime = entry.waitForNarrator && narratorDuration > 0f
            ? narratorDuration + entry.fadeOutTime
            : entry.fadeInTime + (entry.displayDuration > 0f ? entry.displayDuration : Mathf.Max(narratorDuration, 3f)) + entry.fadeOutTime;

        yield return new WaitForSeconds(waitTime);

        _isShowing = false;
        ProcessQueue();
    }

    void ProcessQueue()
    {
        if (_queue.Count > 0)
            StartCoroutine(ShowRoutine(_queue.Dequeue()));
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

    [ContextMenu("Test: Show Entry")]
    void TestShow()
    {
        if (testEntry != null) Show(testEntry);
        else Debug.Log("[NarrativeManager] Assign testEntry in Inspector first.");
    }

    [ContextMenu("Test: Clear All")]
    void TestClear() => ClearAll();
#endif
}

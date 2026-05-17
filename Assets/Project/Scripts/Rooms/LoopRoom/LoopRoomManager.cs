using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Expanding Room (Kitchen) — Agoraphobia simulation.
///
/// HOW IT WORKS:
///   The illusion of a stretching room is created PURELY through camera/post-processing
///   effects — the geometry is never touched, so the shared house walls stay intact.
///
///   Effects that ramp up as the player stays in the kitchen:
///     • FOV narrows  (makes the far end look farther away — the Vertigo/dolly-zoom feel)
///     • Lens distortion increases (warps the perceived space)
///     • Chromatic aberration intensifies (color fringing = "reality breaking")
///     • Vignette darkens (tunnel-vision pressure)
///     • Player movement speed drastically slows (wet-cement feel)
///
///   The ONLY way to reverse it:
///     Stand completely still → complete breathing cycles (F inhale / G exhale).
///     Each completed cycle rolls the effect back incrementally.
///     After enough cycles the effect is fully gone and the room is marked complete.
///
/// SETUP:
///   1. Attach to a Manager GameObject in the Kitchen.
///   2. Assign mainCamera (or let it auto-find Camera.main).
///   3. Create a Global Volume in the scene with a Volume Profile that contains:
///        • Lens Distortion
///        • Chromatic Aberration
///        • Vignette
///      Assign that Volume to the "Post Process Volume" field.
///   4. Assign playerMovement (or tag the player "Player").
///   5. Add a BoxCollider (IsTrigger) + LoopRoomEntryTrigger on the kitchen doorway.
/// </summary>
public class LoopRoomManager : MonoBehaviour
{
    public static LoopRoomManager Instance { get; private set; }

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Camera")]
    [Tooltip("Main camera. Auto-finds Camera.main if not set.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Normal camera FOV (should match your camera's default).")]
    [SerializeField] private float normalFOV = 60f;

    [Tooltip("FOV at maximum effect. Lower = more claustrophobic tunnel-vision.")]
    [SerializeField] private float distortedFOV = 44f;

    [Header("Post-Processing Volume")]
    [Tooltip("A Global Volume in the scene whose Profile has LensDistortion, " +
             "ChromaticAberration, and Vignette components. " +
             "Their weights will be driven at runtime.")]
    [SerializeField] private Volume postProcessVolume;

    [Tooltip("Maximum lens distortion intensity at full effect. Negative = barrel distortion.")]
    [SerializeField] [Range(-1f, 0f)] private float maxLensDistortion = -0.4f;

    [Tooltip("Maximum chromatic aberration intensity at full effect (0–1).")]
    [SerializeField] [Range(0f, 1f)] private float maxChromaticAberration = 0.6f;

    [Tooltip("Maximum vignette intensity at full effect (0–1).")]
    [SerializeField] [Range(0f, 0.8f)] private float maxVignette = 0.55f;

    [Header("Player Movement Slowdown")]
    [Tooltip("PlayerMovement component. Auto-found via 'Player' tag if not set.")]
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("Speed multiplier applied to the player at maximum effect (0.1 = 10% of normal speed).")]
    [SerializeField] private float minSpeedMultiplier = 0.15f;

    [Tooltip("How fast the player must move (CharacterController velocity) to NOT be " +
             "considered standing still.")]
    [SerializeField] private float standingStillThreshold = 0.05f;

    [Header("Effect Ramp")]
    [Tooltip("How fast the effect ramps up per second (0–1 progress scale).")]
    [SerializeField] private float rampUpSpeed = 0.08f;

    [Tooltip("How fast the effect reverses per second while the player is breathing correctly.")]
    [SerializeField] private float reverseSpeed = 0.12f;

    [Tooltip("Each completed breathing cycle instantly rolls back this much progress (0–1).")]
    [SerializeField] private float recoveryPerCycle = 0.25f;

    [Header("Anxiety")]
    [Tooltip("Anxiety added per second while the effect is active and the player is NOT " +
             "breathing to reverse it.")]
    [SerializeField] private float anxietyPerSecond = 1.5f;

    [Header("Kitchen Door")]
    [Tooltip("The DoorController on the kitchen door. It will auto-close and lock when the " +
             "player enters, and unlock once the room loop is complete.")]
    [SerializeField] private DoorController kitchenDoor;

    [Tooltip("Seconds after the player enters before the door closes (lets them walk in first).")]
    [SerializeField] private float doorCloseDelay = 1.5f;

    [Header("Audio (optional)")]
    [Tooltip("Looping ambient sound that plays while the effect is active.")]
    [SerializeField] private AudioSource ambientSource;

    // ─── State ────────────────────────────────────────────────────────────────

    /// <summary>True once the player has entered the kitchen.</summary>
    public bool IsActive     { get; private set; }

    /// <summary>True once the effect has been fully reversed through breathing.</summary>
    public bool RoomComplete { get; private set; }

    /// <summary>Effect progress: 0 = normal, 1 = maximum distortion.</summary>
    public float EffectProgress { get; private set; }

    // Post-processing overrides
    private LensDistortion      _lensDistortion;
    private ChromaticAberration _chromaticAberration;
    private Vignette            _vignette;

    // Breathing tracking
    private int  _lastCycleCount;
    private bool _isReversing;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
            
        // Automatically grab the camera's starting FOV so we don't override the user's settings!
        if (mainCamera != null)
            normalFOV = mainCamera.fieldOfView;

        if (playerMovement == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerMovement = p.GetComponent<PlayerMovement>();
        }

        // Grab post-processing overrides from the Volume profile
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out _lensDistortion);
            postProcessVolume.profile.TryGet(out _chromaticAberration);
            postProcessVolume.profile.TryGet(out _vignette);
        }

        if (BreathingSystem.Instance != null)
            _lastCycleCount = BreathingSystem.Instance.ConsecutiveSuccessfulCycles;

        // Make sure effects are zeroed out at start
        ApplyEffects(0f);
    }

    void Update()
    {
        if (!IsActive || RoomComplete) return;

        // ── Detect completed breathing cycles ─────────────────────────────
        if (BreathingSystem.Instance != null)
        {
            int current = BreathingSystem.Instance.ConsecutiveSuccessfulCycles;
            if (current > _lastCycleCount)
            {
                int newCycles = current - _lastCycleCount;
                _lastCycleCount = current;
                OnBreathingCycleCompleted(newCycles);
            }
            else if (current < _lastCycleCount)
            {
                // Streak reset — stop reversing
                _lastCycleCount = current;
                _isReversing = false;
            }
        }

        // ── Progress ──────────────────────────────────────────────────────
        bool playerIsStill = IsPlayerStandingStill();

        if (_isReversing && playerIsStill)
        {
            EffectProgress -= reverseSpeed * Time.deltaTime;
            if (EffectProgress <= 0f)
            {
                EffectProgress = 0f;
                _isReversing = false;
                CheckCompletion();
            }
        }
        else
        {
            // Not reversing — effect keeps ramping up
            EffectProgress += rampUpSpeed * Time.deltaTime;
            EffectProgress = Mathf.Clamp01(EffectProgress);

            // Anxiety increases while the effect is growing and player is not breathing
            if (AnxietyManager.Instance != null && !AnxietyManager.Instance.IsPanicActive)
                AnxietyManager.Instance.AddAnxiety(anxietyPerSecond * Time.deltaTime);
        }

        // ── Apply everything ──────────────────────────────────────────────
        ApplyEffects(EffectProgress);
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>Call when the player first enters the kitchen.</summary>
    public void StartExpandingEffect()
    {
        if (IsActive || RoomComplete) return;
        IsActive = true;

        if (BreathingSystem.Instance != null)
            _lastCycleCount = BreathingSystem.Instance.ConsecutiveSuccessfulCycles;

        if (ambientSource != null && !ambientSource.isPlaying)
            ambientSource.Play();

        Debug.Log("[ExpandingRoom] Effect started — stand still and breathe to reverse it.");
    }

    /// <summary>
    /// Closes and locks the kitchen door behind the player (called once on entry).
    /// The door unlocks automatically when the room is complete.
    /// </summary>
    public void CloseAndLockKitchenDoor()
    {
        if (kitchenDoor == null) return;
        StartCoroutine(DelayedDoorClose());
    }

    System.Collections.IEnumerator DelayedDoorClose()
    {
        yield return new WaitForSeconds(doorCloseDelay);
        kitchenDoor.ForceClose();
        kitchenDoor.Lock();
        Debug.Log("[ExpandingRoom] Kitchen door closed and locked.");
    }

    // ─── Private ─────────────────────────────────────────────────────────────

    void OnBreathingCycleCompleted(int cycles)
    {
        if (!IsPlayerStandingStill())
        {
            Debug.Log("[ExpandingRoom] Cycle completed but player is moving — no reversal.");
            return;
        }

        float reduction = recoveryPerCycle * cycles;
        EffectProgress = Mathf.Max(0f, EffectProgress - reduction);
        _isReversing = true;

        Debug.Log($"[ExpandingRoom] Breath cycle! Effect rolled back by {reduction:F2}. " +
                  $"Progress: {EffectProgress:F2}");
    }

    bool IsPlayerStandingStill()
    {
        if (playerMovement == null) return true;
        CharacterController cc = playerMovement.GetComponent<CharacterController>();
        if (cc == null) return true;
        float speed = new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;
        return speed < standingStillThreshold;
    }

    void ApplyEffects(float t)
    {
        // ── FOV ───────────────────────────────────────────────────────────
        if (mainCamera != null)
            mainCamera.fieldOfView = Mathf.Lerp(normalFOV, distortedFOV, t);

        // ── Player speed ──────────────────────────────────────────────────
        if (playerMovement != null)
            playerMovement.SpeedMultiplier = Mathf.Lerp(1f, minSpeedMultiplier, t);

        // ── Post-processing ───────────────────────────────────────────────
        if (_lensDistortion != null)
        {
            _lensDistortion.active = t > 0f;
            _lensDistortion.intensity.Override(Mathf.Lerp(0f, maxLensDistortion, t));
        }

        if (_chromaticAberration != null)
        {
            _chromaticAberration.active = t > 0f;
            _chromaticAberration.intensity.Override(Mathf.Lerp(0f, maxChromaticAberration, t));
        }

        if (_vignette != null)
        {
            _vignette.active = t > 0f;
            _vignette.intensity.Override(Mathf.Lerp(0f, maxVignette, t));
        }
    }

    void CheckCompletion()
    {
        if (EffectProgress > 0f) return;

        RoomComplete = true;

        // Restore everything to normal
        ApplyEffects(0f);

        if (ambientSource != null)
            ambientSource.Stop();

        // Unlock the kitchen door so the player can leave ONLY after the narrator finishes
        StartCoroutine(UnlockDoorWhenNarratorFinishes());

        GameStateManager.Instance?.CompleteLoopRoom();

        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.StartGradualReduction(0f, 2f);

        Debug.Log("[ExpandingRoom] Effect fully reversed — room COMPLETE!");
    }

    private System.Collections.IEnumerator UnlockDoorWhenNarratorFinishes()
    {
        if (NarrativeManager.Instance != null)
        {
            while (NarrativeManager.Instance.IsShowing)
            {
                yield return null;
            }
        }

        if (kitchenDoor != null)
        {
            kitchenDoor.Unlock();
            Debug.Log("[ExpandingRoom] Kitchen door unlocked after narrator finished speaking.");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Skip (Complete Immediately)")]
    void DebugSkip()
    {
        IsActive = true;
        EffectProgress = 0f;
        ApplyEffects(0f);
        CheckCompletion();
    }

    [ContextMenu("DEBUG: Force Max Effect")]
    void DebugMaxEffect()
    {
        IsActive = true;
        EffectProgress = 1f;
        ApplyEffects(1f);
    }
#endif
}

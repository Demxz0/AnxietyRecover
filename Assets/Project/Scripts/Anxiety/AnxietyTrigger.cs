using UnityEngine;

/// <summary>
/// How this trigger delivers its anxiety.
/// </summary>
public enum AnxietyTriggerMode
{
    /// <summary>Adds anxiety every second while the player stays inside the collider zone.</summary>
    Continuous,

    /// <summary>One-time spike the first time the player enters the collider zone.</summary>
    OnEnterOnce,

    /// <summary>Spike every time the player enters the collider zone.</summary>
    OnEnterEach,

    /// <summary>
    /// Does nothing automatically — only fires when Fire() is called from another script.
    /// Use this for any non-spatial event: opening a drawer, hearing a sound,
    /// failing a puzzle, pressing a button, interacting with an object, etc.
    /// </summary>
    Manual
}

/// <summary>
/// Universal anxiety trigger. Works for ALL types of anxiety-inducing events.
///
/// USAGE:
///
///   — Zone-based (Continuous / OnEnterOnce / OnEnterEach):
///       Attach to a GameObject with a Trigger Collider.
///       Fires automatically when the Player (tag: "Player") enters/stays.
///
///   — Event-based (Manual):
///       Attach to any GameObject. Call Fire() or FireAmount() from any other script.
///       Example: drawerScript.GetComponent‹AnxietyTrigger›().Fire();
///       Or:       anxietyTrigger.FireAmount(15f);
///
///   — Direct (no component needed):
///       For quick one-off events, just call:
///       AnxietyManager.Instance.AddAnxiety(amount);
///
/// Use the Label field to name each trigger for easy debugging in the console.
/// </summary>
public class AnxietyTrigger : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Descriptive name for this trigger — shown in debug logs. " +
             "Example: 'Dark Hallway', 'Scary Drawer', 'Failed Puzzle', 'Clock Ticking'")]
    [SerializeField] private string triggerLabel = "Unnamed Trigger";

    [Header("Mode")]
    [SerializeField] private AnxietyTriggerMode mode = AnxietyTriggerMode.Continuous;

    [Header("Anxiety Values")]
    [Tooltip("Used by Continuous mode: anxiety added per second.")]
    [SerializeField] private float anxietyPerSecond = 5f;

    [Tooltip("Used by OnEnterOnce, OnEnterEach, and Manual modes: flat anxiety spike.")]
    [SerializeField] private float anxietyAmount = 10f;

    [Header("Continuous Mode — Grace Period")]
    [Tooltip("Seconds the player must remain inside before Continuous mode starts adding anxiety. " +
             "Set to 0 for immediate effect.")]
    [SerializeField] private float gracePeriod = 0f;

    [Header("State")]
    [Tooltip("Uncheck to disable without removing from scene. Also controllable at runtime.")]
    [SerializeField] private bool isActive = true;

    // ─── Private State ─────────────────────────────────────────────────────

    private bool  _playerInside;
    private bool  _hasFiredOnce; // tracks OnEnterOnce
    private float _insideTimer;  // how long player has been inside

    // ─── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Fire the anxiety event using the configured anxietyAmount.
    /// Works with any mode — designed for Manual mode but can supplement any mode.
    /// Call this from any script when an event happens.
    /// </summary>
    public void Fire()
    {
        if (!isActive || AnxietyManager.Instance == null) return;
        Debug.Log($"[AnxietyTrigger] '{triggerLabel}' fired → +{anxietyAmount}");
        AnxietyManager.Instance.AddAnxiety(anxietyAmount);
    }

    /// <summary>
    /// Fire with a specific amount, overriding the Inspector value.
    /// Useful when the same trigger has variable impact depending on context.
    /// </summary>
    public void FireAmount(float amount)
    {
        if (!isActive || AnxietyManager.Instance == null) return;
        Debug.Log($"[AnxietyTrigger] '{triggerLabel}' fired → +{amount}");
        AnxietyManager.Instance.AddAnxiety(amount);
    }

    /// <summary>
    /// Enable or disable this trigger at runtime.
    /// Example: SetActive(false) when the player resolves the situation causing this trigger.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
        if (!active)
        {
            _playerInside = false;
            _insideTimer  = 0f;
        }
        Debug.Log($"[AnxietyTrigger] '{triggerLabel}' → active: {active}");
    }

    /// <summary>Lets this trigger fire again if mode is OnEnterOnce (e.g., after a room resets).</summary>
    public void ResetOnceFlag() => _hasFiredOnce = false;

    public string Label    => triggerLabel;
    public bool   IsActive => isActive;

    // ─── Zone Detection (Continuous / OnEnterOnce / OnEnterEach) ──────────

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || !isActive) return;

        _playerInside = true;
        _insideTimer  = 0f;

        if (mode == AnxietyTriggerMode.OnEnterEach)
        {
            Fire();
        }
        else if (mode == AnxietyTriggerMode.OnEnterOnce && !_hasFiredOnce)
        {
            _hasFiredOnce = true;
            Fire();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInside = false;
        _insideTimer  = 0f;
    }

    void Update()
    {
        if (!_playerInside || !isActive || mode != AnxietyTriggerMode.Continuous) return;
        if (AnxietyManager.Instance == null) return;

        _insideTimer += Time.deltaTime;
        if (_insideTimer < gracePeriod) return;

        AnxietyManager.Instance.AddAnxiety(anxietyPerSecond * Time.deltaTime);
    }

    // ─── Editor Visualization ──────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Color by mode
        Color gizmoColor = mode switch
        {
            AnxietyTriggerMode.Continuous  => new Color(1f, 0.5f, 0f),  // orange
            AnxietyTriggerMode.OnEnterOnce => new Color(1f, 0f,   0f),  // red
            AnxietyTriggerMode.OnEnterEach => new Color(0.8f, 0f, 0.8f),// purple
            AnxietyTriggerMode.Manual      => new Color(0f,  0.8f, 1f), // cyan
            _                               => Color.white
        };

        gizmoColor.a = isActive ? 0.2f : 0.05f;
        Gizmos.color = gizmoColor;

        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            gizmoColor.a = isActive ? 0.7f : 0.15f;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius);
        }
    }

    // Show label in Scene view
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.3f,
            $"[{mode}] {triggerLabel}\n±{(mode == AnxietyTriggerMode.Continuous ? anxietyPerSecond + "/s" : anxietyAmount.ToString())}"
        );
    }
#endif
}

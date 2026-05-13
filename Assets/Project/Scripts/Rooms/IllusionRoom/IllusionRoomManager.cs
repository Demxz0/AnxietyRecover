using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the entire Illusion Room (Bedroom) experience.
///
/// FLOW:
///   1. Player enters bedroom → voices play, silhouettes appear, anxiety rises continuously
///   2a. BAD PATH — Player hides in bathroom:
///        - After 30s: door shaking + louder voices
///        - Panic attack triggers
///        - After panic: door shakes/unlocks → player exits bathroom
///        - Sounds shift back to bedroom
///   2b. GOOD PATH — Player finds switch piece on bed → brings it to the broken switch
///        - Switch is placed → light turns on
///        - Silhouettes disappear, voices stop, anxiety drops
///
/// SETUP:
///   Attach to a Manager GameObject in the Bedroom/Illusion Room.
///   Assign all references in the Inspector.
/// </summary>
public class IllusionRoomManager : MonoBehaviour
{
    public static IllusionRoomManager Instance { get; private set; }

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Voices / Ambient Audio")]
    [Tooltip("AudioSource playing the judging voices loop (bedroom ambient voices).")]
    [SerializeField] private AudioSource voicesSource;

    [Tooltip("Normal volume of voices in the bedroom.")]
    [SerializeField] private float voicesNormalVolume = 0.5f;

    [Tooltip("Louder volume when voices 'shift' to the bathroom hallway (after bad choice).")]
    [SerializeField] private float voicesLoudVolume = 0.85f;

    [Header("Silhouettes")]
    [Tooltip("Parent GameObject holding all the silhouette character meshes. " +
             "Enable on entry, disable on light-on.")]
    [SerializeField] private GameObject silhouettesParent;

    [Header("Room Light")]
    [Tooltip("The RoomLightController on the bedroom ceiling light (startsOff = true).")]
    [SerializeField] private RoomLightController bedroomLight;

    [Tooltip("Brightness for the dramatic light reveal.")]
    [SerializeField] private float revealIntensity = 1.6f;

    [Header("Anxiety")]
    [Tooltip("Anxiety added per second while in the dark bedroom (voices active).")]
    [SerializeField] private float anxietyPerSecond = 4f;

    [Tooltip("Anxiety reduced when the light turns on successfully.")]
    [SerializeField] private float anxietyReductionOnLightOn = 40f;

    [Header("Entry Trigger")]
    [Tooltip("Box collider on the bedroom doorway — detects player entry.")]
    [SerializeField] private bool startOnAwake = false;

    // ─── State ────────────────────────────────────────────────────────────────
    public bool IsIllusionActive { get; private set; }
    public bool LightIsOn        { get; private set; }

    private bool      _badPathTriggered;
    private bool      _anxiausPaused;
    private Coroutine _anxietyCoroutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        SetSilhouettes(false);
        SetVoicesVolume(0f);

        if (startOnAwake) OnPlayerEntersRoom();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Call this when the player enters the bedroom doorway.</summary>
    public void OnPlayerEntersRoom()
    {
        if (IsIllusionActive || LightIsOn) return;

        IsIllusionActive = true;
        SetSilhouettes(true);
        StartVoices(voicesNormalVolume);

        // Immediately spike anxiety to 50 % of max, then the continuous routine
        // raises it slowly from there.
        if (AnxietyManager.Instance != null)
        {
            float spikeTarget = AnxietyManager.Instance.MaxAnxiety * 0.5f;
            if (AnxietyManager.Instance.AnxietyValue < spikeTarget)
                AnxietyManager.Instance.SetAnxiety(spikeTarget);
        }

        _anxietyCoroutine = StartCoroutine(ContinuousAnxietyRoutine());
        Debug.Log("[IllusionRoom] Player entered — illusion active. Anxiety spiked to 50 %.");
    }

    // ─── Bathroom API ─────────────────────────────────────────────────────────

    /// <summary>Phase 0: Player just entered bathroom — pause anxiety, silence voices.</summary>
    public void OnPlayerEntersBathroom()
    {
        if (!IsIllusionActive) return;
        _badPathTriggered = true;
        _anxiausPaused = true;

        // Silence voices — the bathroom is a momentary refuge
        SetVoicesVolume(0f);
        Debug.Log("[IllusionRoom] BAD PATH: Player in bathroom — voices silenced, anxiety paused.");
    }

    /// <summary>Phase 1: 30s elapsed — knocking starts, voices louder, anxiety resumes fast.</summary>
    public void OnBathroomKnockingStarts()
    {
        if (!IsIllusionActive) return;
        _anxiausPaused = false;

        // Voices snap back louder (muffled through door effect)
        SetVoicesVolume(voicesLoudVolume);
        Debug.Log("[IllusionRoom] Knocking phase — voices louder, anxiety resuming fast.");
    }

    /// <summary>Called by BathroomManager when player exits bathroom after panic.</summary>
    public void OnPlayerExitsBathroom()
    {
        _anxiausPaused = false;
        // Voices return to normal bedroom level
        SetVoicesVolume(voicesNormalVolume);
        Debug.Log("[IllusionRoom] Player exited bathroom — voices back to normal.");
    }

    // Keep old name as alias for backwards compat
    public void OnPlayerHidesInBathroom() => OnPlayerEntersBathroom();

    /// <summary>Called by LightSwitchInteraction when the switch is activated.</summary>
    public void OnLightTurnedOn()
    {
        if (LightIsOn) return;
        LightIsOn        = true;
        IsIllusionActive = false;

        // Stop anxiety drain
        if (_anxietyCoroutine != null)
        {
            StopCoroutine(_anxietyCoroutine);
            _anxietyCoroutine = null;
        }

        // Stop voices, hide silhouettes
        StopVoices();
        SetSilhouettes(false);

        // Turn on the ceiling light
        if (bedroomLight != null)
            bedroomLight.TurnOn(revealIntensity);

        // Reduce anxiety — this was all in their head
        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.ReduceAnxiety(anxietyReductionOnLightOn);

        // Silence room-specific inner voices
        InnerVoiceManager.Instance?.SetRoom(InnerVoiceManager.RoomZone.None);

        GameStateManager.Instance?.CompleteIllusionRoom();
        Debug.Log("[IllusionRoom] Light ON — illusion ended! Anxiety reduced.");
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────
    IEnumerator ContinuousAnxietyRoutine()
    {
        while (IsIllusionActive)
        {
            // Respect the bathroom safe-window pause
            if (!_anxiausPaused && AnxietyManager.Instance != null)
                AnxietyManager.Instance.AddAnxiety(anxietyPerSecond * Time.deltaTime);
            yield return null;
        }
    }

    void SetSilhouettes(bool state)
    {
        if (silhouettesParent != null) silhouettesParent.SetActive(state);
    }

    void StartVoices(float volume)
    {
        if (voicesSource == null) return;
        voicesSource.volume = volume;
        if (!voicesSource.isPlaying) voicesSource.Play();
    }

    void StopVoices()
    {
        if (voicesSource != null) voicesSource.Stop();
    }

    void SetVoicesVolume(float volume)
    {
        if (voicesSource != null) voicesSource.volume = volume;
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Trigger Room Entry")]
    void DebugEntry() => OnPlayerEntersRoom();

    [ContextMenu("DEBUG: Force Light On")]
    void DebugLightOn() => OnLightTurnedOn();
#endif
}

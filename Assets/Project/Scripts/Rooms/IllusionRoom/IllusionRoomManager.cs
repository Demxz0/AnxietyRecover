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

    [Tooltip("Anxiety added per second after panic attack in bathroom (much slower recovery).")]
    [SerializeField] private float anxietyPerSecondPostPanic = 0.2f;

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
    private bool      _postPanicMode;
    private Coroutine _anxietyCoroutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Fallback: if voicesSource is not assigned in the Inspector, find or create one dynamically
        if (voicesSource == null)
        {
            voicesSource = GetComponent<AudioSource>();
            if (voicesSource == null)
            {
                GameObject voiceGO = new GameObject("JudgingVoicesAudioSource");
                voiceGO.transform.SetParent(transform);
                voicesSource = voiceGO.AddComponent<AudioSource>();
            }
        }
    }

    void Start()
    {
        SetSilhouettes(false);
        SetVoicesVolume(0f);

        // Dynamically assign the PeopleJudging clip and copy SFX mixer routing
        if (voicesSource != null)
        {
            if (AudioManager.Instance != null)
            {
                if (voicesSource.clip == null)
                    voicesSource.clip = AudioManager.Instance.PeopleJudgingClip;
                
                if (AudioManager.Instance.SfxSource != null)
                    voicesSource.outputAudioMixerGroup = AudioManager.Instance.SfxSource.outputAudioMixerGroup;
            }
            voicesSource.loop = true;
            voicesSource.playOnAwake = false;
        }

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

        _anxietyCoroutine = StartCoroutine(ContinuousAnxietyRoutine());

        // Player inner voice: "I have to hide in the bathroom"
        // Plays 2.5 seconds after entering — the voices have started and the player
        // is disoriented, so this line misdirects them toward the bathroom.
        StartCoroutine(PlayBathroomMisdirectionVoice());

        // Notify InnerVoiceManager of the new zone (clips only play at ExtremeAnxiety+)
        InnerVoiceManager.Instance?.SetRoom(InnerVoiceManager.RoomZone.IllusionRoom);

        Debug.Log("[IllusionRoom] Player entered — illusion active. Anxiety spiked to 50 %.");
    }

    IEnumerator PlayBathroomMisdirectionVoice()
    {
        yield return new WaitForSeconds(2.5f);
        if (IsIllusionActive) // still in the room after the delay
            AudioManager.Instance?.PlayOneShot(SoundID.PlayerVoiceIllusionBathroom);
    }

    // ─── Bathroom API ─────────────────────────────────────────────────────────

    /// <summary>Phase 0: Player just entered bathroom — pause anxiety, silence voices.</summary>
    public void OnPlayerEntersBathroom()
    {
        if (!IsIllusionActive) return;
        _badPathTriggered = true;
        _anxiausPaused = true;

        if (AnxietyManager.Instance != null)
            AnxietyManager.Instance.StartGradualReduction(AnxietyManager.Instance.MildThreshold, 2f);

        // Silence voices — the bathroom is a momentary refuge
        SetVoicesVolume(0f);
        Debug.Log("[IllusionRoom] BAD PATH: Player in bathroom — voices silenced, anxiety dropping to Mild.");
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
    public void OnPlayerExitsBathroom(bool postPanic = false)
    {
        _anxiausPaused = false;
        // Voices return to normal bedroom level
        SetVoicesVolume(voicesNormalVolume);

        if (postPanic)
        {
            // Slow down the anxiety rate permanently after a panic attack in the bathroom
            _postPanicMode = true;
            Debug.Log("[IllusionRoom] Post-panic mode: anxiety increase rate set to " + anxietyPerSecondPostPanic + "/s (was " + anxietyPerSecond + "/s).");
        }
        else
        {
            Debug.Log("[IllusionRoom] Player exited bathroom — voices back to normal.");
        }
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
            AnxietyManager.Instance.StartGradualReduction(0f, 2f);

        // Silence room-specific inner voices
        InnerVoiceManager.Instance?.SetRoom(InnerVoiceManager.RoomZone.None);

        GameStateManager.Instance?.CompleteIllusionRoom();
        Debug.Log("[IllusionRoom] Light ON — illusion ended! Anxiety reduced.");
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────
    
    /// <summary>Public accessor for the voices AudioSource (used by AudioManager for master fade).</summary>
    public AudioSource GetVoicesSource() => voicesSource;
    
    IEnumerator ContinuousAnxietyRoutine()
    {
        while (IsIllusionActive)
        {
            // Respect the bathroom safe-window pause
            if (!_anxiausPaused && AnxietyManager.Instance != null)
            {
                // Use post-panic rate if in post-panic mode, otherwise use normal rate
                float currentRate = _postPanicMode ? anxietyPerSecondPostPanic : anxietyPerSecond;
                AnxietyManager.Instance.AddAnxiety(currentRate * Time.deltaTime);
            }
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

    public void StopVoices()
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

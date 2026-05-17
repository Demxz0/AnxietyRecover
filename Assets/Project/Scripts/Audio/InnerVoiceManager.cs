using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Plays random inner-voice (intrusive thought) audio clips depending on which
/// room the player is currently in.
///
/// KEY RULES:
///   • Clips only play when anxiety is at EXTREME level or above.
///   • Two AudioSources allow clips to OVERLAP (for the chaotic feel at extreme anxiety).
///   • Both sources should be assigned to the "InnerVoice" Audio Mixer Group
///     to get the in-head DSP effect (EQ + Echo set up in the mixer).
///   • The two special player voice lines (call-back, bathroom) are NOT managed
///     here — they live in AudioManager and are triggered by room scripts.
///
/// AUDIO MIXER SETUP (do this once in the editor):
///   Window → Audio → Audio Mixer → Create "GameAudioMixer"
///   Add groups: Master → Music, SFX, Voice, InnerVoice
///   On InnerVoice group add effects:
///     • Parametric Equalizer: boost 200-800Hz, cut above 3kHz
///     • Echo: Delay 15ms, Decay 0.2, Wet 0.25
///   Assign voiceSource and overlapSource Output → InnerVoice mixer group.
///
/// HOW TO SET UP IN THE EDITOR:
///   1. Create an empty GameObject anywhere in the scene, name it "InnerVoiceManager".
///   2. Add TWO AudioSource components (no clip needed).
///   3. Attach this script and assign both AudioSources in the Inspector.
///   4. Set both AudioSource Output fields to the "InnerVoice" mixer group.
///   5. Fill in the AudioClip arrays for each room zone.
///   6. The room managers and entry triggers will call SetRoom() automatically.
/// </summary>
public class InnerVoiceManager : MonoBehaviour
{
    public static InnerVoiceManager Instance { get; private set; }

    // ─── Room Zones enum ─────────────────────────────────────────────────────
    public enum RoomZone
    {
        None,
        StartRoom,
        IllusionRoom,
        LoopRoom,
        DependencyRoom
    }

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Audio Sources")]
    [Tooltip("Primary AudioSource for inner voice clips. Output → InnerVoice mixer group.")]
    [SerializeField] private AudioSource voiceSource;

    [Tooltip("Secondary AudioSource so clips can overlap at ExtremeAnxiety. " +
             "Output → InnerVoice mixer group (same settings as voiceSource).")]
    [SerializeField] private AudioSource overlapSource;

    [Header("Start Room Voices")]
    [Tooltip("Random inner-voice clips played while in the start room (ExtremeAnxiety+).")]
    [SerializeField] private AudioClip[] startRoomVoices;
    [SerializeField] [Range(0f, 1f)] private float startRoomVolume = 0.5f;

    [Header("Illusion Room Voices")]
    [Tooltip("Random inner-voice clips played in the Bedroom / Illusion Room (ExtremeAnxiety+).")]
    [SerializeField] private AudioClip[] illusionRoomVoices;
    [SerializeField] [Range(0f, 1f)] private float illusionRoomVolume = 0.7f;

    [Header("Loop Room Voices (Kitchen)")]
    [Tooltip("Random inner-voice clips played in the Kitchen / Loop Room (ExtremeAnxiety+).")]
    [SerializeField] private AudioClip[] loopRoomVoices;
    [SerializeField] [Range(0f, 1f)] private float loopRoomVolume = 0.6f;

    [Header("Dependency Room Voices")]
    [Tooltip("Random inner-voice clips played in the Office / Dependency Room (ExtremeAnxiety+).")]
    [SerializeField] private AudioClip[] dependencyRoomVoices;
    [SerializeField] [Range(0f, 1f)] private float dependencyRoomVolume = 0.6f;

    [Header("Timing")]
    [Tooltip("Minimum seconds between voice clips within a room (at ExtremeAnxiety threshold).")]
    [SerializeField] private float minGapBetweenClips = 20f;

    [Tooltip("Maximum seconds between voice clips within a room (at ExtremeAnxiety threshold).")]
    [SerializeField] private float maxGapBetweenClips = 60f;

    [Tooltip("At max anxiety (100%) the gap shrinks to this minimum value.")]
    [SerializeField] private float minGapAtMaxAnxiety = 5f;

    [Tooltip("Seconds to fade in/out volume when switching rooms.")]
    [SerializeField] private float crossfadeDuration = 1f;

    [Tooltip("When true, a second clip can overlap with the first at ExtremeAnxiety.")]
    [SerializeField] private bool allowOverlapAtExtreme = true;

    // ─── State ────────────────────────────────────────────────────────────────

    private RoomZone  _currentZone  = RoomZone.None;
    private Coroutine _voiceRoutine;
    private Coroutine _fadeRoutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();

        if (voiceSource != null)
        {
            voiceSource.playOnAwake = false;
            voiceSource.loop        = false;
            voiceSource.volume      = 0f;
        }

        if (overlapSource != null)
        {
            overlapSource.playOnAwake = false;
            overlapSource.loop        = false;
            overlapSource.volume      = 0f;
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Switch the active room zone. Voices change immediately.
    /// Pass RoomZone.None to stop all voices.
    /// </summary>
    public void SetRoom(RoomZone zone)
    {
        if (_currentZone == zone) return;
        _currentZone = zone;

        if (_voiceRoutine != null) { StopCoroutine(_voiceRoutine); _voiceRoutine = null; }

        if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
        if (overlapSource != null && overlapSource.isPlaying) overlapSource.Stop();

        if (zone == RoomZone.None)
        {
            FadeTo(0f);
            Debug.Log("[InnerVoiceManager] Zone = None — voices silent.");
            return;
        }

        AudioClip[] clips  = GetClipsForZone(zone);
        float       volume = GetVolumeForZone(zone);

        if (clips == null || clips.Length == 0)
        {
            Debug.Log($"[InnerVoiceManager] Zone {zone} has no clips — staying silent.");
            return;
        }

        FadeTo(volume);
        _voiceRoutine = StartCoroutine(VoiceLoop(clips));
        Debug.Log($"[InnerVoiceManager] Zone: {zone} ({clips.Length} clip(s)).");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    AudioClip[] GetClipsForZone(RoomZone zone) => zone switch
    {
        RoomZone.StartRoom      => startRoomVoices,
        RoomZone.IllusionRoom   => illusionRoomVoices,
        RoomZone.LoopRoom       => loopRoomVoices,
        RoomZone.DependencyRoom => dependencyRoomVoices,
        _                       => null
    };

    float GetVolumeForZone(RoomZone zone) => zone switch
    {
        RoomZone.StartRoom      => startRoomVolume,
        RoomZone.IllusionRoom   => illusionRoomVolume,
        RoomZone.LoopRoom       => loopRoomVolume,
        RoomZone.DependencyRoom => dependencyRoomVolume,
        _                       => 0f
    };

    // ─── Voice Loop ───────────────────────────────────────────────────────────

    IEnumerator VoiceLoop(AudioClip[] clips)
    {
        while (true)
        {
            // ── Anxiety Gate: only play at ExtremeAnxiety or above ────────────
            // Wait until the player reaches ExtremeAnxiety before the first clip
            while (AnxietyManager.Instance == null ||
                   AnxietyManager.Instance.CurrentLevel < AnxietyLevel.ExtremeAnxiety)
            {
                yield return new WaitForSeconds(2f); // poll every 2s
            }

            // Calculate gap based on current anxiety (shorter gap = more frequent at high anxiety)
            float normalized = AnxietyManager.Instance != null
                ? AnxietyManager.Instance.NormalizedAnxiety
                : 0f;
            float t   = Mathf.Clamp01((normalized - 0.65f) / 0.35f); // 0 at extreme threshold, 1 at 100%
            float gap = Mathf.Lerp(minGapBetweenClips, minGapAtMaxAnxiety, t);
            gap += Random.Range(-gap * 0.2f, gap * 0.2f); // small randomness

            yield return new WaitForSeconds(Mathf.Max(2f, gap));

            // Re-check anxiety after the gap
            if (AnxietyManager.Instance == null ||
                AnxietyManager.Instance.CurrentLevel < AnxietyLevel.ExtremeAnxiety)
                continue;

            // Don't play during panic
            if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsPanicActive)
                continue;

            // Pick a random clip
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) continue;

            // Play on primary source
            if (voiceSource != null)
            {
                voiceSource.clip = clip;
                voiceSource.Play();
                Debug.Log($"[InnerVoiceManager] Inner voice: {clip.name}");
            }

            // At extreme anxiety, optionally overlap a second clip slightly after
            if (allowOverlapAtExtreme && overlapSource != null &&
                AnxietyManager.Instance != null &&
                AnxietyManager.Instance.CurrentLevel >= AnxietyLevel.ExtremeAnxiety)
            {
                StartCoroutine(PlayOverlapClip(clips, clip.length * 0.5f));
            }

            // Wait for the clip to finish before the next gap starts
            if (clip != null)
                yield return new WaitForSeconds(clip.length);
        }
    }

    /// <summary>Plays a different clip on the overlap source after a delay.</summary>
    IEnumerator PlayOverlapClip(AudioClip[] clips, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (AnxietyManager.Instance == null ||
            AnxietyManager.Instance.CurrentLevel < AnxietyLevel.ExtremeAnxiety) yield break;

        AudioClip overlapClip = clips[Random.Range(0, clips.Length)];
        if (overlapClip == null) yield break;

        overlapSource.clip = overlapClip;
        overlapSource.Play();
        Debug.Log($"[InnerVoiceManager] Overlap inner voice: {overlapClip.name}");
    }

    // ─── Crossfade ────────────────────────────────────────────────────────────

    void FadeTo(float targetVolume)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(targetVolume));
    }

    IEnumerator FadeRoutine(float targetVolume)
    {
        if (voiceSource == null) yield break;

        float start   = voiceSource.volume;
        float elapsed = 0f;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float v = Mathf.Lerp(start, targetVolume, elapsed / crossfadeDuration);
            if (voiceSource  != null) voiceSource.volume  = v;
            if (overlapSource != null) overlapSource.volume = v;
            yield return null;
        }

        if (voiceSource  != null) voiceSource.volume  = targetVolume;
        if (overlapSource != null) overlapSource.volume = targetVolume;
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Set Zone — Start Room")]
    void TestStart()      => SetRoom(RoomZone.StartRoom);

    [ContextMenu("Test: Set Zone — Illusion Room")]
    void TestIllusion()   => SetRoom(RoomZone.IllusionRoom);

    [ContextMenu("Test: Set Zone — Loop Room")]
    void TestLoop()       => SetRoom(RoomZone.LoopRoom);

    [ContextMenu("Test: Set Zone — Dependency Room")]
    void TestDependency() => SetRoom(RoomZone.DependencyRoom);

    [ContextMenu("Test: Set Zone — None (Silent)")]
    void TestNone()       => SetRoom(RoomZone.None);
#endif
}

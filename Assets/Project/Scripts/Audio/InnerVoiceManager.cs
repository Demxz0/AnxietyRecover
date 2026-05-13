using System.Collections;
using UnityEngine;

/// <summary>
/// Plays random inner-voice (intrusive thought) audio clips depending on which
/// room the player is currently in. Each room has its own set of clips and its
/// own volume, so the tone can shift between rooms.
///
/// HOW TO SET UP IN THE EDITOR:
///   1. Create an empty GameObject anywhere in the scene, name it "InnerVoiceManager".
///   2. Add an AudioSource component to it (no clip needed — this script controls it).
///   3. Attach this script and assign the AudioSource in the Inspector.
///   4. Fill in the AudioClip arrays for each room zone once you have recordings ready.
///      Leave arrays empty for now — the system will simply stay silent for that zone.
///   5. The room managers and entry triggers will call SetRoom() automatically (wired below).
///
/// BEHAVIOUR:
///   • When SetRoom() is called, the active clip set switches immediately.
///   • A random clip from the current set plays, then a random gap elapses, then another
///     clip plays — repeating until SetRoom(None) is called or the game ends.
///   • If the player is in a panic attack, voice clips are suppressed.
///   • Volume fades between rooms (crossfade).
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

    [Header("Audio Source")]
    [Tooltip("An AudioSource on this GameObject used for all inner voice clips.")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Start Room Voices")]
    [Tooltip("Random inner-voice clips that play while the player is in the start room.")]
    [SerializeField] private AudioClip[] startRoomVoices;
    [SerializeField] [Range(0f, 1f)] private float startRoomVolume = 0.5f;

    [Header("Illusion Room Voices")]
    [Tooltip("Random inner-voice clips that play in the Bedroom (Illusion Room).")]
    [SerializeField] private AudioClip[] illusionRoomVoices;
    [SerializeField] [Range(0f, 1f)] private float illusionRoomVolume = 0.7f;

    [Header("Loop Room Voices (Kitchen)")]
    [Tooltip("Random inner-voice clips that play in the Kitchen (Agoraphobia / Loop Room).")]
    [SerializeField] private AudioClip[] loopRoomVoices;
    [SerializeField] [Range(0f, 1f)] private float loopRoomVolume = 0.6f;

    [Header("Dependency Room Voices")]
    [Tooltip("Random inner-voice clips that play in the Office (Dependency Room).")]
    [SerializeField] private AudioClip[] dependencyRoomVoices;
    [SerializeField] [Range(0f, 1f)] private float dependencyRoomVolume = 0.6f;

    [Header("Timing")]
    [Tooltip("Minimum seconds between voice clips within a room.")]
    [SerializeField] private float minGapBetweenClips = 20f;

    [Tooltip("Maximum seconds between voice clips within a room.")]
    [SerializeField] private float maxGapBetweenClips = 60f;

    [Tooltip("Seconds to fade in/out when switching rooms (crossfade).")]
    [SerializeField] private float crossfadeDuration = 1f;

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

        if (voiceSource == null)
            Debug.LogError("[InnerVoiceManager] No AudioSource found! Add one to this GameObject.");

        if (voiceSource != null)
        {
            voiceSource.playOnAwake = false;
            voiceSource.loop        = false;
            voiceSource.volume      = 0f;
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Switch the active room zone. Voice clips will change immediately.
    /// Pass RoomZone.None to stop all voices (e.g., between rooms or on room completion).
    /// </summary>
    public void SetRoom(RoomZone zone)
    {
        if (_currentZone == zone) return;
        _currentZone = zone;

        // Stop the running voice loop
        if (_voiceRoutine != null) { StopCoroutine(_voiceRoutine); _voiceRoutine = null; }

        // Stop any currently playing clip
        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.Stop();

        if (zone == RoomZone.None)
        {
            FadeTo(0f);
            Debug.Log("[InnerVoiceManager] Zone set to None — voices silent.");
            return;
        }

        AudioClip[] clips  = GetClipsForZone(zone);
        float       volume = GetVolumeForZone(zone);

        if (clips == null || clips.Length == 0)
        {
            Debug.Log($"[InnerVoiceManager] Zone {zone} has no clips assigned — staying silent.");
            return;
        }

        FadeTo(volume);
        _voiceRoutine = StartCoroutine(VoiceLoop(clips));
        Debug.Log($"[InnerVoiceManager] Switched to zone: {zone} ({clips.Length} clip(s) available).");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    AudioClip[] GetClipsForZone(RoomZone zone)
    {
        return zone switch
        {
            RoomZone.StartRoom      => startRoomVoices,
            RoomZone.IllusionRoom   => illusionRoomVoices,
            RoomZone.LoopRoom       => loopRoomVoices,
            RoomZone.DependencyRoom => dependencyRoomVoices,
            _                       => null
        };
    }

    float GetVolumeForZone(RoomZone zone)
    {
        return zone switch
        {
            RoomZone.StartRoom      => startRoomVolume,
            RoomZone.IllusionRoom   => illusionRoomVolume,
            RoomZone.LoopRoom       => loopRoomVolume,
            RoomZone.DependencyRoom => dependencyRoomVolume,
            _                       => 0f
        };
    }

    // ─── Voice Loop ───────────────────────────────────────────────────────────

    IEnumerator VoiceLoop(AudioClip[] clips)
    {
        while (true)
        {
            // Random gap before next clip
            float gap = Random.Range(minGapBetweenClips, maxGapBetweenClips);
            yield return new WaitForSeconds(gap);

            // Don't play during panic
            if (AnxietyManager.Instance != null && AnxietyManager.Instance.IsPanicActive)
                continue;

            // Pick and play a random clip
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) continue;

            if (voiceSource != null)
            {
                voiceSource.clip = clip;
                voiceSource.Play();
                Debug.Log($"[InnerVoiceManager] Playing inner voice: {clip.name}");

                // Wait for the clip to finish before the next gap starts
                yield return new WaitForSeconds(clip.length);
            }
        }
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
            voiceSource.volume = Mathf.Lerp(start, targetVolume, elapsed / crossfadeDuration);
            yield return null;
        }
        voiceSource.volume = targetVolume;
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Set Zone — Start Room")]
    void TestStart()       => SetRoom(RoomZone.StartRoom);

    [ContextMenu("Test: Set Zone — Illusion Room")]
    void TestIllusion()    => SetRoom(RoomZone.IllusionRoom);

    [ContextMenu("Test: Set Zone — Loop Room")]
    void TestLoop()        => SetRoom(RoomZone.LoopRoom);

    [ContextMenu("Test: Set Zone — Dependency Room")]
    void TestDependency()  => SetRoom(RoomZone.DependencyRoom);

    [ContextMenu("Test: Set Zone — None (Silent)")]
    void TestNone()        => SetRoom(RoomZone.None);
#endif
}

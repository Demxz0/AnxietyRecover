using UnityEngine;

/// <summary>
/// ScriptableObject defining a single narrative moment.
///
/// CREATE: Right-click in Project → Create → Narrative → Narrative Entry
///
/// STYLE GUIDE:
///   Narrator   — The external narrator voice. Has an AudioClip. Pinned at an anchor.
///                Large warm text. No typewriter. Stays for the full audio length.
///   Discovery  — Inner-voice thought on discovery. White, large, drifts upward. Typewriter on.
///   Calming    — Gentle inner-voice. Soft blue, smaller, drifts slowly. Typewriter on.
///
/// FIELDS:
///   text            — The words shown floating in the world.
///   style           — Controls visual appearance and behaviour (Narrator / Discovery / Calming).
///   narratorClip    — AudioClip to play (only used for Narrator style entries).
///   displayDuration — How long text stays fully visible. 0 = auto (clip length or 3s).
///   fadeInTime      — Seconds to fade in.
///   fadeOutTime     — Seconds to fade out.
///   typewriterReveal— If true, text appears letter by letter (auto-off for Narrator style).
///   waitForNarrator — If true, NarrativeManager will not show the next entry until this clip ends.
/// </summary>
[CreateAssetMenu(menuName = "Narrative/Narrative Entry", fileName = "NewNarrativeEntry")]
public class NarrativeEntry : ScriptableObject
{
    [TextArea(3, 8)]
    [Tooltip("The text shown floating in the world.")]
    public string text;

    [Tooltip("Visual style for this entry.\n" +
             "• Narrator  — External voice. Warm, large, pinned. Use with an AudioClip.\n" +
             "• Discovery — Inner-voice on discovery. White, drifts up.\n" +
             "• Calming   — Gentle inner-voice. Soft blue, slow drift.")]
    public NarrativeStyle style = NarrativeStyle.Discovery;

    [Tooltip("AudioClip to play alongside this entry (Narrator style only — leave empty for inner-voice).")]
    public AudioClip narratorClip;

    [Tooltip("How many seconds the text stays fully visible. 0 = use narrator clip length, or 3s default.")]
    public float displayDuration = 0f;

    [Tooltip("Seconds for the text to fade in.")]
    public float fadeInTime = 0.5f;

    [Tooltip("Seconds for the text to fade out.")]
    public float fadeOutTime = 1f;

    [Tooltip("If true, text appears one character at a time (typewriter effect). Auto-disabled for Narrator style.")]
    public bool typewriterReveal = true;

    [Tooltip("If true, NarrativeManager will not queue the next entry until this narrator clip finishes.")]
    public bool waitForNarrator = false;
}

/// <summary>
/// Visual style variants for NarrativeEntry.
/// Controls font size, color, drift behaviour, and whether the text is pinned.
/// </summary>
public enum NarrativeStyle
{
    /// <summary>
    /// External narrator voice.
    /// Warm off-white, large text. PINNED at the assigned anchor (no drift/sway).
    /// Always faces camera. Designed to sync with an AudioClip.
    /// Typewriter is disabled so text appears instantly with the voice.
    /// </summary>
    Narrator,

    /// <summary>
    /// Inner-voice moment on a discovery.
    /// White, large text. Drifts upward. Typewriter on.
    /// </summary>
    Discovery,

    /// <summary>
    /// Gentle inner-voice thought.
    /// Soft blue-white, smaller text. Slow drift. Typewriter on.
    /// </summary>
    Calming,
}

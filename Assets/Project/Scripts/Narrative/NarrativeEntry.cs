using UnityEngine;

/// <summary>
/// ScriptableObject defining a single narrative moment.
///
/// CREATE: Right-click in Project → Create → Narrative → Narrative Entry
///
/// FIELDS:
///   text            — The words that will float in front of the player.
///   style           — Controls visual appearance (font, color, animation speed).
///   narratorClip    — Optional narrator voice clip to play alongside the text.
///   displayDuration — How long the text stays fully visible (seconds). 0 = auto (voice length).
///   fadeInTime      — Seconds to fade in.
///   fadeOutTime     — Seconds to fade out.
///   typewriterReveal— If true, text appears letter by letter.
///   waitForNarrator — If true, NarrativeManager will not show another entry until this voice finishes.
/// </summary>
[CreateAssetMenu(menuName = "Narrative/Narrative Entry", fileName = "NewNarrativeEntry")]
public class NarrativeEntry : ScriptableObject
{
    [TextArea(3, 8)]
    [Tooltip("The text that will float in front of the player.")]
    public string text;

    [Tooltip("Visual style for this entry.")]
    public NarrativeStyle style = NarrativeStyle.Discovery;

    [Tooltip("Optional narrator voice clip to play with this entry.")]
    public AudioClip narratorClip;

    [Tooltip("How many seconds the text stays fully visible. 0 = use narrator clip length, or 3s default.")]
    public float displayDuration = 0f;

    [Tooltip("Seconds for the text to fade in.")]
    public float fadeInTime = 0.5f;

    [Tooltip("Seconds for the text to fade out.")]
    public float fadeOutTime = 1f;

    [Tooltip("If true, text appears one character at a time (typewriter effect).")]
    public bool typewriterReveal = false;

    [Tooltip("If true, NarrativeManager will not queue the next entry until this narrator clip finishes.")]
    public bool waitForNarrator = false;
}

/// <summary>
/// Visual style variants for NarrativeEntry.
/// Controls font size, color, and float behavior in WorldFloatTextRenderer.
/// </summary>
public enum NarrativeStyle
{
    /// <summary>White, large text — used for discoveries, paper content, room entries.</summary>
    Discovery,

    /// <summary>Soft blue/white, slow drift — used for calming moments and narrator reflections.</summary>
    Calming,
}

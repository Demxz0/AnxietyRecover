/// <summary>
/// All sound identifiers in the game.
/// AudioManager maps each ID to its assigned AudioClip.
/// </summary>
public enum SoundID
{
    None = 0,

    // ── Doors ──────────────────────────────────────────────
    DoorOpen,
    DoorClose,

    // ── UI / Puzzle ────────────────────────────────────────
    ButtonClick,
    RightAnswer,
    WrongAnswer,

    // ── Phones ─────────────────────────────────────────────
    CellphoneRing,
    OfficephoneRing,
    CallCutOff,
    CallbackUnavailable,

    // ── Environment ────────────────────────────────────────
    PaperSound,
    DoorKnock,

    // ── Breathing / Panic ──────────────────────────────────
    BreathingHeavy,
    Inhale,
    Exhale,
    Heartbeat,
    PeopleJudging,

    // ── Voice ──────────────────────────────────────────────
    // NpcVoice and NarratorVoice are handled via
    // AudioManager.PlayNpcClip() / AudioManager.PlayNarratorClip()
    // and do NOT have a SoundID because they take arbitrary clips.
}

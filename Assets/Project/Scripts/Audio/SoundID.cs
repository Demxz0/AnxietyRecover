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

    // ── Player Inner Voice ─────────────────────────────────
    // These are NOT part of InnerVoiceManager — they are
    // special one-shot narrative lines triggered by specific events.

    /// <summary>Player's inner voice after the NPC call cuts off: "I should call them back..."</summary>
    PlayerVoiceCallBack,

    /// <summary>
    /// Player's inner voice when entering the Illusion Room and hearing the people sounds:
    /// "I have to hide in the bathroom" — a deliberate misdirection for the player.
    /// Triggered by IllusionRoomManager on room entry.
    /// </summary>
    PlayerVoiceIllusionBathroom,
}

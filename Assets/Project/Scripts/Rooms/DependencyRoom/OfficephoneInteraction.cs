using System.Collections;
using UnityEngine;

/// <summary>
/// The NEW office phone — separate from the cellphone.
///
/// This phone rings AFTER the player picks up the key from the combo lock.
/// DependencyRoomManager calls StartRinging() to activate it.
/// When the player interacts: ring stops, and the Narrator's first speech plays
/// through NarrativeManager.
///
/// SETUP:
///   1. Attach to the office phone 3D object.
///   2. Assign 'narratorFirstEntry' (a NarrativeEntry with the narrator's first clip).
///   3. DependencyRoomManager references this component — assign it in that Inspector.
///   4. Ensure AudioManager exists in the scene.
/// </summary>
public class OfficephoneInteraction : MonoBehaviour, IInteractable
{
    [Header("Narrator Entry")]
    [Tooltip("NarrativeEntry that plays when the player answers the office phone. " +
             "This is the Narrator's very first speech. Assign a NarrativeEntry with " +
             "the narrator AudioClip and the spoken text set to empty (or a short reflection).")]
    [SerializeField] private NarrativeEntry narratorFirstEntry;

    [Header("Ring Delay")]
    [Tooltip("Seconds after key pickup before the office phone starts ringing.")]
    [SerializeField] private float ringStartDelay = 1.5f;

    // ─── State ────────────────────────────────────────────────────────────────
    private bool _isRinging;
    private bool _hasBeenAnswered;

    // ─── Ring Start ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DependencyRoomManager when the key is picked up.
    /// Starts the office phone ringing after a short delay.
    /// </summary>
    public void StartRinging()
    {
        StartCoroutine(RingAfterDelay());
    }

    IEnumerator RingAfterDelay()
    {
        yield return new WaitForSeconds(ringStartDelay);
        _isRinging = true;
        AudioManager.Instance?.PlayLoop(SoundID.OfficephoneRing);
        Debug.Log("[OfficephoneInteraction] Office phone is ringing — Narrator is calling.");
    }

    // ─── IInteractable ────────────────────────────────────────────────────────

    public void Interact()
    {
        if (!_isRinging || _hasBeenAnswered) return;
        _hasBeenAnswered = true;
        _isRinging = false;

        // Stop ring
        AudioManager.Instance?.Stop(SoundID.OfficephoneRing);

        // Trigger Narrator's first speech
        if (narratorFirstEntry != null && NarrativeManager.Instance != null)
        {
            NarrativeManager.Instance.Show(narratorFirstEntry);
            Debug.Log("[OfficephoneInteraction] Player answered — Narrator begins first speech.");
        }
        else
        {
            Debug.LogWarning("[OfficephoneInteraction] narratorFirstEntry or NarrativeManager not assigned.");
        }
    }

    public string GetPromptText()
    {
        if (_hasBeenAnswered || !_isRinging) return "";
        return "Answer";
    }
}

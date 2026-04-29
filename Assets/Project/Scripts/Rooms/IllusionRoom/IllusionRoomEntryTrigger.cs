using UnityEngine;

/// <summary>
/// Bedroom Entry Trigger — detects when the player first walks through the bedroom door
/// and starts the Illusion Room sequence.
///
/// SETUP:
///   1. Create an empty GameObject at the bedroom doorway.
///   2. Add a BoxCollider (Is Trigger = true), sized to span the doorway.
///   3. Attach this script.
///   4. IllusionRoomManager must be in the scene.
/// </summary>
public class IllusionRoomEntryTrigger : MonoBehaviour
{
    private bool _triggered;

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;

        if (IllusionRoomManager.Instance != null)
            IllusionRoomManager.Instance.OnPlayerEntersRoom();
        else
            Debug.LogWarning("[IllusionEntryTrigger] IllusionRoomManager not found!");
    }
}

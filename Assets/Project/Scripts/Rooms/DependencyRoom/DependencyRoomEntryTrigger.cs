using UnityEngine;

/// <summary>
/// Dependency Room Entry Trigger — starts the phone ringing when the player walks in.
/// Also switches the InnerVoiceManager to the Dependency Room voice set.
///
/// SETUP:
///   1. Create an empty GameObject at the office doorway.
///   2. Add a BoxCollider (Is Trigger = true), sized to span the doorway.
///   3. Attach this script.
///   4. DependencyRoomManager must be in the scene.
///      Alternatively, set DependencyRoomManager.ringOnRoomEntry = true (it auto-starts in Start()).
/// </summary>
public class DependencyRoomEntryTrigger : MonoBehaviour
{
    private bool _triggered;

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;

        if (DependencyRoomManager.Instance != null)
            DependencyRoomManager.Instance.StartPhoneRinging();
        else
            Debug.LogWarning("[DependencyEntryTrigger] DependencyRoomManager not found!");

        // Switch inner voices to Dependency Room set
        InnerVoiceManager.Instance?.SetRoom(InnerVoiceManager.RoomZone.DependencyRoom);
    }
}

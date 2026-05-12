using UnityEngine;

/// <summary>
/// Place this on an empty GameObject with a BoxCollider (IsTrigger = true)
/// at the entrance of the Expanding Room (Kitchen).
/// When the player walks in, the agoraphobia stretch effect begins.
/// Also fires a one-time breathing hint and sets the InnerVoiceManager to the Loop zone.
/// </summary>
public class LoopRoomEntryTrigger : MonoBehaviour
{
    private bool _hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            _hasTriggered = true;

            if (LoopRoomManager.Instance != null)
            {
                LoopRoomManager.Instance.StartExpandingEffect();
                LoopRoomManager.Instance.CloseAndLockKitchenDoor();
                Debug.Log("[ExpandingRoom] Player entered the kitchen. Expanding effect started, door closing.");
            }

            // Guide hint — breathing is the only way out
            ObjectiveHintManager.Instance?.ShowHint(
                "أنت تعاني من رهاب الميادين , قف بسكون وتنفس جيدا", 10f);

            // Switch inner voices to Loop Room set
            InnerVoiceManager.Instance?.SetRoom(InnerVoiceManager.RoomZone.LoopRoom);
        }
    }
}

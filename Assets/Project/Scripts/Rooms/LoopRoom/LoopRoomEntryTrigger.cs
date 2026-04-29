using UnityEngine;

/// <summary>
/// Place this on an empty GameObject with a BoxCollider (IsTrigger = true)
/// at the entrance of the Expanding Room (Kitchen).
/// When the player walks in, the agoraphobia stretch effect begins.
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
        }
    }
}

using UnityEngine;

/// <summary>
/// Place this on an empty GameObject with a BoxCollider (IsTrigger = true) at the entrance of the Loop Room.
/// When the player walks into it, the loop will start.
/// </summary>
public class LoopRoomEntryTrigger : MonoBehaviour
{
    private bool _hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;

        // Check if it's the player (assuming your player has the "Player" tag)
        if (other.CompareTag("Player"))
        {
            _hasTriggered = true;

            if (LoopRoomManager.Instance != null)
            {
                LoopRoomManager.Instance.StartLoop();
                Debug.Log("[LoopRoom] Player entered the kitchen. Loop started!");
            }
        }
    }
}

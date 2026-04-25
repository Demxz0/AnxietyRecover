using UnityEngine;

/// <summary>
/// Attach to the phone 3D object. Handles player interaction with the phone.
/// Reports to DependencyRoomManager.
///
/// SETUP:
///   1. Attach to the phone mesh/GameObject.
///   2. Assign this phone's AudioSource if you want it to ring on the object itself.
///   3. DependencyRoomManager handles the actual NPC audio source.
/// </summary>
public class PhoneInteraction : MonoBehaviour, IInteractable
{
    [Header("Audio (optional)")]
    [Tooltip("Used for the phone ring sound on the phone object itself.")]
    [SerializeField] private AudioSource localAudioSource;

    public void Interact()
    {
        if (DependencyRoomManager.Instance != null)
            DependencyRoomManager.Instance.OnPhonePickedUp();
        else
            Debug.LogWarning("[PhoneInteraction] DependencyRoomManager not found in scene!");
    }

    public string GetPromptText()
    {
        if (DependencyRoomManager.Instance == null) return "Pick Up Phone";

        return DependencyRoomManager.Instance.CurrentStage == DependencyRoomManager.Stage.PhoneBusy
            ? "Call Back"
            : "Pick Up Phone";
    }
}

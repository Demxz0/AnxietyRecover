using UnityEngine;

/// <summary>
/// Attach to the cellphone 3D object. Handles player interaction with the cellphone.
/// Reports to DependencyRoomManager. Audio handled by AudioManager.
/// </summary>
public class PhoneInteraction : MonoBehaviour, IInteractable
{
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

        return DependencyRoomManager.Instance.CurrentStage >= DependencyRoomManager.Stage.PhoneBusy
            ? "Call Back"
            : "Pick Up Phone";
    }
}

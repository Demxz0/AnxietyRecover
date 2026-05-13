using UnityEngine;

/// <summary>
/// The broken light switch on the wall.
/// If the player has the switch piece → they can place it → switch becomes usable → lights on.
/// Uses two prefab states (broken vs. complete) swapped on placement.
///
/// SETUP:
///   1. Create two 3D objects at the same position:
///        - "BrokenSwitchMesh"  — the broken/empty switch housing.
///        - "FullSwitchMesh"    — the complete switch with the piece installed.
///   2. Start with BrokenSwitchMesh active, FullSwitchMesh inactive.
///   3. Assign both to this script.
///   4. Assign the bedroom RoomLightController.
///   5. Attach this script to the switch's parent (or any of the meshes with a collider).
///   6. Assign 'narratorSecondEntry' — plays after the light turns on (Narrator's 2nd speech).
/// </summary>
public class LightSwitchInteraction : MonoBehaviour, IInteractable
{
    [Header("Switch State Meshes")]
    [Tooltip("The broken/empty switch mesh — visible before piece is placed.")]
    [SerializeField] private GameObject brokenSwitchMesh;

    [Tooltip("The complete switch mesh — visible after piece is placed.")]
    [SerializeField] private GameObject fullSwitchMesh;

    [Header("Room Light")]
    [Tooltip("The RoomLightController for the bedroom ceiling light.")]
    [SerializeField] private RoomLightController bedroomLightController;

    [Tooltip("Intensity of the light reveal flash.")]
    [SerializeField] private float revealIntensity = 1.6f;

    [Header("Narrator — Second Speech")]
    [Tooltip("NarrativeEntry that plays when the player turns on the light (Narrator's 2nd speech).")]
    [SerializeField] private NarrativeEntry narratorSecondEntry;

    [Tooltip("World position where the narrator text will appear. " +
             "Create an empty child GO, place it where you want the text, and assign it here.")]
    [SerializeField] private Transform textAnchor;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   placePieceSound;
    [SerializeField] private AudioClip   switchClickSound;

    // ─── State ────────────────────────────────────────────────────────────────
    private bool _pieceInstalled;
    private bool _lightOn;

    void Start()
    {
        SetMesh(pieceInstalled: false);
    }

    public void Interact()
    {
        if (_lightOn) return;

        if (!_pieceInstalled)
        {
            // Does the player have the switch piece?
            if (GameStateManager.Instance != null && GameStateManager.Instance.HasSwitchPiece)
            {
                InstallPiece();
                ActivateSwitch(); // Immediately activate after placing
            }
            else
            {
                Debug.Log("[LightSwitch] Switch piece not found. The player needs to search the room.");
            }
            return;
        }

        // Piece is installed — player hits the switch
        ActivateSwitch();
    }

    public string GetPromptText()
    {
        if (_lightOn)          return "";
        if (!_pieceInstalled)  return GameStateManager.Instance != null && GameStateManager.Instance.HasSwitchPiece
                                        ? "Install Switch Piece"
                                        : "Broken Switch";
        return "Turn On Light";
    }

    void InstallPiece()
    {
        _pieceInstalled = true;
        SetMesh(pieceInstalled: true);

        // Remove piece icon from inventory
        if (InventoryUI.Instance != null) InventoryUI.Instance.HideSwitchPieceIcon();

        PlaySound(placePieceSound);
        Debug.Log("[LightSwitch] Switch piece installed.");
    }

    void ActivateSwitch()
    {
        _lightOn = true;
        PlaySound(switchClickSound);

        // Turn on room light via RoomLightController
        if (bedroomLightController != null)
            bedroomLightController.TurnOn(revealIntensity);

        // Tell IllusionRoomManager the illusion is over
        IllusionRoomManager.Instance?.OnLightTurnedOn();

        // Narrator's second speech — plays at the assigned anchor position
        if (narratorSecondEntry != null && NarrativeManager.Instance != null)
            NarrativeManager.Instance.Show(narratorSecondEntry, textAnchor);
        else if (narratorSecondEntry == null)
            Debug.LogWarning("[LightSwitch] narratorSecondEntry not assigned — Narrator 2nd speech won't play.");

        Debug.Log("[LightSwitch] LIGHT ON — illusion dispelled!");
    }

    void SetMesh(bool pieceInstalled)
    {
        if (brokenSwitchMesh != null) brokenSwitchMesh.SetActive(!pieceInstalled);
        if (fullSwitchMesh   != null) fullSwitchMesh.SetActive(pieceInstalled);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}

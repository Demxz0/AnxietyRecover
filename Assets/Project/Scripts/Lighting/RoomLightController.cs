using UnityEngine;

/// <summary>
/// STUB — Realtime lighting logic removed for WebGL baked lighting compatibility.
///
/// All lights in the scene should be BAKED (Mixed or Baked mode) via:
///   Window → Rendering → Lighting → Generate Lighting
///
/// The TurnOn() method is kept because IllusionRoom's LightSwitchInteraction
/// calls it to enable the initially-off illusion room light.
/// For the anxiety color-shift effect, use AnxietyColorGrading.cs on a URP Volume instead.
/// </summary>
[RequireComponent(typeof(Light))]
public class RoomLightController : MonoBehaviour
{
    [Header("Room Identity")]
    [SerializeField] private string roomName = "Room";

    [Header("Illusion Room — Light Switch")]
    [Tooltip("If true, the light starts completely OFF. Call TurnOn() from LightSwitchInteraction.")]
    [SerializeField] private bool startsOff = false;

    private Light _light;
    private bool  _isOn;

    void Awake()
    {
        _light = GetComponent<Light>();

        if (startsOff)
        {
            _isOn            = false;
            _light.intensity = 0f;
            _light.enabled   = false;
        }
        else
        {
            _isOn = true;
        }
    }

    // ─── Illusion Room — Public API ───────────────────────────────────────────

    /// <summary>
    /// Call this from LightSwitchInteraction when the player connects the missing piece.
    /// Enables the light (which must be baked as Mixed so it contributes to the scene).
    /// </summary>
    public void TurnOn(float revealIntensity = 1.6f)
    {
        if (_isOn) return;

        _isOn            = true;
        _light.enabled   = true;
        _light.intensity = revealIntensity;

        Debug.Log($"[RoomLightController] '{roomName}' switched ON.");
    }
}

// Keep the enum so other scripts that reference it still compile
public enum RoomPreset
{
    None,
    OpenRoom,
    LivingRoom,
    Bedroom,
    IllusionRoom,
    LoopRoom,
    Bathroom,
    Hallway,
    Kitchen
}

using UnityEngine;

/// <summary>
/// Applies smooth Perlin-noise camera shake on top of the existing camera rotation.
/// Attach to the same GameObject as <see cref="MouseLook"/>.
///
/// The <see cref="PanicAttackController"/> drives <see cref="Intensity"/> from 0 → 1
/// during a panic attack and back to 0 on recovery.
/// </summary>
public class PanicCameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [Tooltip("Maximum angular displacement in degrees at full intensity.")]
    [SerializeField] private float maxShakeAngle = 3f;

    [Tooltip("How fast the noise pattern changes. Higher = more frantic.")]
    [SerializeField] private float noiseSpeed = 8f;

    // ─── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Set by PanicAttackController. 0 = no shake, 1 = full shake.
    /// </summary>
    public float Intensity { get; set; }

    // ─── Private ───────────────────────────────────────────────────────────

    private float _seedX;
    private float _seedY;
    private float _seedZ;

    void Awake()
    {
        // Random seeds so X, Y, Z shake independently
        _seedX = Random.Range(0f, 100f);
        _seedY = Random.Range(100f, 200f);
        _seedZ = Random.Range(200f, 300f);
    }

    void LateUpdate()
    {
        if (Intensity <= 0.001f) return;

        float time = Time.time * noiseSpeed;
        float angle = maxShakeAngle * Intensity;

        // Perlin noise returns 0–1; remap to -1 to +1
        float offsetX = (Mathf.PerlinNoise(_seedX, time) - 0.5f) * 2f * angle;
        float offsetY = (Mathf.PerlinNoise(_seedY, time) - 0.5f) * 2f * angle;
        float offsetZ = (Mathf.PerlinNoise(_seedZ, time) - 0.5f) * 2f * angle * 0.5f; // less roll

        // Apply as additive rotation (runs after MouseLook in Update)
        transform.localRotation *= Quaternion.Euler(offsetX, offsetY, offsetZ);
    }
}

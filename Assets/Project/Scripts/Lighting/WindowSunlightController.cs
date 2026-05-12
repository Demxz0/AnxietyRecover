using UnityEngine;

/// <summary>
/// STUB — Realtime sunlight logic removed for WebGL baked lighting compatibility.
/// The Light component values are set once at startup and never changed at runtime.
///
/// If you want the window light, set its Color and Intensity directly on the Light
/// component in the Inspector and bake. For runtime variation use AnxietyColorGrading.cs.
/// </summary>
[RequireComponent(typeof(Light))]
public class WindowSunlightController : MonoBehaviour
{
    [Header("Sunlight Colours")]
    [Tooltip("Static dawn colour applied at startup. Never changed at runtime.")]
    [SerializeField] private Color dawnColor = new Color(1.00f, 0.88f, 0.62f);

    [Header("Intensity")]
    [SerializeField] private float baseIntensity = 1.8f;

    [Header("Dust Particles (Optional)")]
    [SerializeField] private ParticleSystem dustParticles;

    void Awake()
    {
        Light light  = GetComponent<Light>();
        light.color     = dawnColor;
        light.intensity = baseIntensity;
    }

    void OnEnable()
    {
        if (dustParticles != null) dustParticles.Play();
    }

    void OnDisable()
    {
        if (dustParticles != null) dustParticles.Stop();
    }
}

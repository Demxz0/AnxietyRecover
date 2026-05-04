using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Exit Door Ending Sequence:
///   Phase 1 — Scene gradually blurs and washes out to white (via Post Processing).
///   Phase 2 — A cinematic radial light burst (lens flare effect) explodes from the center,
///              built from layered UI rays + a bloom pulse, consuming the screen entirely.
///
/// SETUP:
/// ──────────────────────────────────────────────────────────────────────
/// POST PROCESSING:
///   1. Add a Global Volume to your scene (GameObject > Volume > Global Volume).
///   2. Create a new Volume Profile and add:
///        - "Vignette"
///        - "Bloom"
///        - "Color Adjustments" (or "Color Grading" in older URP)
///        - "Depth Of Field" (Gaussian or Bokeh mode)
///   3. Set ALL their intensities/weights to 0 at start — this script drives them.
///   4. Assign the Volume to "postProcessVolume" below.
///
/// CANVAS (Sort Order 100+):
///   5. "whiteFadeImage"  — full-screen white Image, alpha 0.
///   6. "lensFlareRoot"   — an empty RectTransform anchored to center.
///        Under it, add 6–8 white Images (thin rectangles rotated at even angles,
///        e.g. 0°, 30°, 60°, 90°, 120°, 150° — like sun ray spokes).
///        Each ray: Width ~8, Height ~1200, pivot at bottom center (so they radiate outward).
///        Set all alphas to 0.
///   7. "centerGlowImage" — a white circle Image anchored to center (~200×200px), alpha 0.
///        Use a soft radial gradient sprite for best results.
/// ──────────────────────────────────────────────────────────────────────
/// </summary>
public class ExitDoorInteraction : MonoBehaviour, IInteractable
{
    [Header("Post Processing")]
    [Tooltip("Global Volume with Bloom, DepthOfField, and ColorAdjustments overrides.")]
    [SerializeField] private Volume postProcessVolume;

    [Header("Phase 1 – Blur & Wash Out")]
    [Tooltip("Full-screen white UI Image. Alpha starts at 0.")]
    [SerializeField] private Image whiteFadeImage;

    [Tooltip("How long the blur + washout phase lasts (seconds).")]
    [SerializeField] private float blurWashDuration = 3.5f;

    [Tooltip("Max Gaussian blur radius at peak of phase 1.")]
    [SerializeField] private float maxBlurAmount = 25f;

    [Tooltip("Max Bloom intensity at peak of phase 1.")]
    [SerializeField] private float maxBloomIntensity = 8f;

    [Header("Phase 2 – Lens Flare Burst")]
    [Tooltip("Empty RectTransform at screen center. Parent of all ray Images.")]
    [SerializeField] private RectTransform lensFlareRoot;

    [Tooltip("The individual ray Images under lensFlareRoot.")]
    [SerializeField] private Image[] rays;

    [Tooltip("Soft glowing circle at the center of the burst.")]
    [SerializeField] private Image centerGlowImage;

    [Tooltip("How long the lens flare burst + final whiteout takes (seconds).")]
    [SerializeField] private float burstDuration = 2.5f;

    [Tooltip("Max scale the rays grow to (higher = longer rays).")]
    [SerializeField] private float rayMaxScale = 3.5f;

    [Tooltip("Pause between phase 1 and phase 2 (seconds).")]
    [SerializeField] private float pauseBetweenPhases = 0.3f;

    // Post processing overrides
    private Bloom _bloom;
    private DepthOfField _dof;
    private ColorAdjustments _colorAdjust;

    private bool _hasTriggered = false;

    void Start()
    {
        // Grab PP overrides
        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out _bloom);
            postProcessVolume.profile.TryGet(out _dof);
            postProcessVolume.profile.TryGet(out _colorAdjust);
        }

        // Hide all UI elements at start
        SetImageAlpha(whiteFadeImage, 0f);
        if (whiteFadeImage) whiteFadeImage.gameObject.SetActive(false);

        if (lensFlareRoot) lensFlareRoot.gameObject.SetActive(false);

        if (centerGlowImage)
        {
            SetImageAlpha(centerGlowImage, 0f);
            centerGlowImage.rectTransform.localScale = Vector3.zero;
        }

        if (rays != null)
        {
            foreach (var ray in rays)
            {
                if (ray == null) continue;
                SetImageAlpha(ray, 0f);
                ray.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            }
        }
    }

    public void Interact()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        Debug.Log("[ExitDoor] Triggering cinematic ending...");

        PlayerMovement.CanMove = false;
        MouseLook.CanLook = false;

        StartCoroutine(EndingSequence());
    }

    public string GetPromptText() => _hasTriggered ? "" : "Leave";

    // ─────────────────────────────────────────────────────────────────────────
    //  MAIN SEQUENCE
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator EndingSequence()
    {
        yield return StartCoroutine(Phase1_BlurAndWashout());
        yield return new WaitForSeconds(pauseBetweenPhases);
        yield return StartCoroutine(Phase2_LensFlareBurst());

        Debug.Log("[ExitDoor] Ending complete.");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("CreditsScene");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PHASE 1 — Progressive blur + white washout
    //  The world loses focus and bleaches out, as if overwhelmed by incoming light.
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator Phase1_BlurAndWashout()
    {
        if (whiteFadeImage) whiteFadeImage.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < blurWashDuration)
        {
            elapsed += Time.deltaTime;
            // Use an eased curve: slow start, accelerates toward the end
            float t = Mathf.Clamp01(elapsed / blurWashDuration);
            float eased = Mathf.Pow(t, 1.6f); // gentle ease-in

            // ── Depth of Field (blur) ──────────────────────────────
            if (_dof != null)
            {
                // Gaussian DOF: increase blur size progressively
                if (_dof.mode.value == DepthOfFieldMode.Gaussian)
                {
                    _dof.gaussianStart.Override(Mathf.Lerp(0f, 0.01f, eased));
                    _dof.gaussianEnd.Override(Mathf.Lerp(100f, 0.1f, eased));
                    _dof.gaussianMaxRadius.Override(Mathf.Lerp(0f, maxBlurAmount, eased));
                }
                // Bokeh DOF fallback
                else
                {
                    _dof.focusDistance.Override(Mathf.Lerp(10f, 0.1f, eased));
                    _dof.aperture.Override(Mathf.Lerp(1f, 32f, eased));
                }
            }

            // ── Bloom (light bleed) ────────────────────────────────
            if (_bloom != null)
            {
                _bloom.intensity.Override(Mathf.Lerp(0f, maxBloomIntensity, eased));
                _bloom.threshold.Override(Mathf.Lerp(1f, 0.1f, eased)); // lower = more blooms
                _bloom.scatter.Override(Mathf.Lerp(0.7f, 1f, eased));   // spread the bloom wider
            }

            // ── Color shift toward overexposed white ───────────────
            if (_colorAdjust != null)
            {
                // Lift exposure: scene gets blown out like looking into a light
                _colorAdjust.postExposure.Override(Mathf.Lerp(0f, 4f, eased));
                // Desaturate: colors bleed away
                _colorAdjust.saturation.Override(Mathf.Lerp(0f, -100f, eased));
            }

            // ── White overlay: subtle wash, the PP does most of the work ──
            SetImageAlpha(whiteFadeImage, Mathf.Lerp(0f, 0.35f, eased));

            yield return null;
        }

        // Snap to max blur state
        SetImageAlpha(whiteFadeImage, 0.35f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PHASE 2 — Cinematic radial lens flare bursts from center
    //  Rays shoot outward in a staggered starburst, center glow pulses,
    //  then everything floods to pure white.
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator Phase2_LensFlareBurst()
    {
        if (lensFlareRoot) lensFlareRoot.gameObject.SetActive(true);

        float elapsed = 0f;

        // Beat 1 (0–40%): center glow pulses in fast
        // Beat 2 (20–80%): rays shoot outward with stagger
        // Beat 3 (60–100%): everything washes to pure white

        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / burstDuration);

            // ── CENTER GLOW ────────────────────────────────────────
            // Grows quickly from center, punchy ease-out
            float glowT = Mathf.Clamp01(t / 0.4f);
            float glowEased = 1f - Mathf.Pow(1f - glowT, 3f); // ease-out cubic
            if (centerGlowImage)
            {
                centerGlowImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0f, 2.5f, glowEased);
                SetImageAlpha(centerGlowImage, Mathf.Lerp(0f, 1f, glowEased));
            }

            // ── RAYS ───────────────────────────────────────────────
            // Rays stagger outward: each ray has its own delay offset
            if (rays != null)
            {
                int rayCount = rays.Length;
                for (int i = 0; i < rayCount; i++)
                {
                    if (rays[i] == null) continue;

                    // Stagger: each ray starts slightly after the previous
                    float delay = (float)i / rayCount * 0.25f; // spread over first 25% of phase
                    float rayT = Mathf.Clamp01((t - 0.15f - delay) / 0.6f);
                    float rayEased = 1f - Mathf.Pow(1f - rayT, 2.5f); // ease-out

                    // Rays grow outward (Y scale = length)
                    rays[i].rectTransform.localScale = new Vector3(
                        Mathf.Lerp(1f, 1f + (float)i * 0.04f, rayEased),   // slight width variation
                        Mathf.Lerp(0f, rayMaxScale, rayEased),              // length shoots out
                        1f
                    );

                    // Rays fade in quickly then hold
                    float rayAlpha = Mathf.Clamp01(rayT * 3f);
                    SetImageAlpha(rays[i], rayAlpha);
                }
            }

            // ── BLOOM PULSE ────────────────────────────────────────
            // Spike bloom during burst for cinematic punch
            if (_bloom != null)
            {
                float burstBloom = Mathf.Clamp01(t / 0.5f);
                _bloom.intensity.Override(Mathf.Lerp(maxBloomIntensity, maxBloomIntensity * 2.5f, burstBloom));
            }

            // ── FINAL WHITEOUT (last 40%) ──────────────────────────
            float whiteT = Mathf.Clamp01((t - 0.6f) / 0.4f);
            float whiteEased = Mathf.SmoothStep(0f, 1f, whiteT);
            SetImageAlpha(whiteFadeImage, Mathf.Lerp(0.35f, 1f, whiteEased));

            yield return null;
        }

        // Snap fully white
        SetImageAlpha(whiteFadeImage, 1f);
        if (centerGlowImage) SetImageAlpha(centerGlowImage, 1f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}

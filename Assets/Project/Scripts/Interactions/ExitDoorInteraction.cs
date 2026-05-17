using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Exit Door Ending Sequence:
///
///   Phase 1 — Scene gradually blurs and washes out to pure white (Post Processing).
///   Phase 2 — Two sequential Arabic texts appear with slide+fade animation on the white screen.
///             The OpenRoomMusic keeps playing throughout (AudioManager persists via DontDestroyOnLoad).
///             After the second text fades out, the MainMenu scene is loaded.
///
/// SETUP:
/// ──────────────────────────────────────────────────────────────────────
/// POST PROCESSING:
///   1. Add a Global Volume to your scene with a Volume Profile containing:
///        - Bloom
///        - Depth Of Field (Gaussian mode recommended)
///        - Color Adjustments
///   2. Set all intensities to 0 at start — this script drives them.
///   3. Assign the Volume to "postProcessVolume".
///
/// CANVAS (Sort Order 100+):
///   4. "whiteFadeImage"  — full-screen white Image, alpha 0.
///   5. "endText1"        — TextMeshProUGUI, Arabic text 1 ("أنت لست وحدك , اطلب المساعدة"), alpha 0.
///   6. "endText2"        — TextMeshProUGUI, Arabic text 2 ("نشكركم للعب"), alpha 0.
///   Both texts should be centered on screen, font size 60+, white color.
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

    [Header("Phase 2 – Ending Texts")]
    [Tooltip("First Arabic text: 'أنت لست وحدك , اطلب المساعدة'")]
    [SerializeField] private TextMeshProUGUI endText1;

    [Tooltip("Second Arabic text: 'نشكركم للعب'")]
    [SerializeField] private TextMeshProUGUI endText2;

    [Tooltip("How long each text fades IN (seconds).")]
    [SerializeField] private float textFadeInDuration = 1.5f;

    [Tooltip("How long each text stays fully visible (seconds).")]
    [SerializeField] private float textHoldDuration = 3f;

    [Tooltip("How long each text fades OUT (seconds).")]
    [SerializeField] private float textFadeOutDuration = 1.5f;

    [Tooltip("How many pixels the text slides vertically during fade in/out.")]
    [SerializeField] private float textSlideDistance = 40f;

    [Tooltip("Short pause between Text 1 disappearing and Text 2 appearing (seconds).")]
    [SerializeField] private float gapBetweenTexts = 0.5f;

    [Header("Scene")]
    [Tooltip("Exact scene name to load after the ending. Must match Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // Post processing overrides
    private Bloom            _bloom;
    private DepthOfField     _dof;
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
        if (whiteFadeImage != null) whiteFadeImage.gameObject.SetActive(false);

        SetTMPAlpha(endText1, 0f);
        SetTMPAlpha(endText2, 0f);
    }

    public void Interact()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        Debug.Log("[ExitDoor] Triggering ending sequence...");

        PlayerMovement.CanMove = false;
        MouseLook.CanLook      = false;

        StartCoroutine(EndingSequence());
    }

    public string GetPromptText() => _hasTriggered ? "" : "Leave";

    // ─────────────────────────────────────────────────────────────────────────
    //  MAIN SEQUENCE
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator EndingSequence()
    {
        yield return StartCoroutine(Phase1_BlurAndWashout());
        yield return StartCoroutine(Phase2_TextSequence());

        Debug.Log("[ExitDoor] Ending complete — loading MainMenu.");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PHASE 1 — Progressive blur + white washout
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator Phase1_BlurAndWashout()
    {
        if (whiteFadeImage != null) whiteFadeImage.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < blurWashDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / blurWashDuration);
            float eased = Mathf.Pow(t, 1.6f); // gentle ease-in

            // Depth of Field blur
            if (_dof != null)
            {
                if (_dof.mode.value == DepthOfFieldMode.Gaussian)
                {
                    _dof.gaussianStart.Override(Mathf.Lerp(0f, 0.01f, eased));
                    _dof.gaussianEnd.Override(Mathf.Lerp(100f, 0.1f, eased));
                    _dof.gaussianMaxRadius.Override(Mathf.Lerp(0f, maxBlurAmount, eased));
                }
                else
                {
                    _dof.focusDistance.Override(Mathf.Lerp(10f, 0.1f, eased));
                    _dof.aperture.Override(Mathf.Lerp(1f, 32f, eased));
                }
            }

            // Bloom
            if (_bloom != null)
            {
                _bloom.intensity.Override(Mathf.Lerp(0f, maxBloomIntensity, eased));
                _bloom.threshold.Override(Mathf.Lerp(1f, 0.1f, eased));
                _bloom.scatter.Override(Mathf.Lerp(0.7f, 1f, eased));
            }

            // Color shift to white
            if (_colorAdjust != null)
            {
                _colorAdjust.postExposure.Override(Mathf.Lerp(0f, 4f, eased));
                _colorAdjust.saturation.Override(Mathf.Lerp(0f, -100f, eased));
            }

            // White overlay — ramps to full at the end
            SetImageAlpha(whiteFadeImage, Mathf.Lerp(0f, 1f, eased));

            yield return null;
        }

        // Snap to pure white
        SetImageAlpha(whiteFadeImage, 1f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PHASE 2 — Two Arabic texts with slide + fade animation
    //  OpenRoomMusic keeps playing (AudioManager is DontDestroyOnLoad).
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator Phase2_TextSequence()
    {
        // Show Text 1
        yield return StartCoroutine(AnimateText(endText1, fadeIn: true));
        yield return new WaitForSeconds(textHoldDuration);
        yield return StartCoroutine(AnimateText(endText1, fadeIn: false));

        yield return new WaitForSeconds(gapBetweenTexts);

        // Show Text 2
        yield return StartCoroutine(AnimateText(endText2, fadeIn: true));
        yield return new WaitForSeconds(textHoldDuration);
        yield return StartCoroutine(AnimateText(endText2, fadeIn: false));
    }

    /// <summary>
    /// Fades a TMP text in (alpha 0→1, slides up) or out (alpha 1→0, slides down).
    /// </summary>
    private IEnumerator AnimateText(TextMeshProUGUI text, bool fadeIn)
    {
        if (text == null) yield break;

        text.gameObject.SetActive(true);

        float duration  = fadeIn ? textFadeInDuration : textFadeOutDuration;
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha   = fadeIn ? 1f : 0f;

        // Slide: fade in = slide up (start low, end at origin), fade out = slide down
        RectTransform rt = text.rectTransform;
        Vector2 originalPos = rt.anchoredPosition;
        Vector2 startPos = fadeIn
            ? originalPos + Vector2.down * textSlideDistance
            : originalPos;
        Vector2 endPos = fadeIn
            ? originalPos
            : originalPos + Vector2.down * textSlideDistance;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            SetTMPAlpha(text, Mathf.Lerp(startAlpha, endAlpha, t));
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            yield return null;
        }

        SetTMPAlpha(text, endAlpha);
        rt.anchoredPosition = endPos;

        if (!fadeIn)
            text.gameObject.SetActive(false);
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

    private void SetTMPAlpha(TextMeshProUGUI tmp, float alpha)
    {
        if (tmp == null) return;
        Color c = tmp.color;
        c.a = alpha;
        tmp.color = c;
    }
}

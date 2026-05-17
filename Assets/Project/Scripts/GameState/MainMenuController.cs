using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Main Menu controller for the MainMenu scene.
///
/// SCENE SETUP:
///   1. File → New Scene (Empty) → save as "MainMenu" in Assets/Project/Scenes/.
///   2. File → Build Settings → Add Open Scenes:
///        Index 0 = MainMenu
///        Index 1 = MainGame
///   3. Position the camera to face the Open Room window (warm sunlight view).
///   4. Add a Particle System for dust motes (see Inspector tooltip hints).
///   5. Add a Canvas → TextMeshProUGUI for the press-any-key text.
///   6. Add an AudioSource to the scene with the MainMusic clip (Loop=true, PlayOnAwake=true).
///   7. Attach this script to any GameObject. Wire all fields in Inspector.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("TextMeshPro text that pulses — content should be 'اضغط أي زر للبدء'.")]
    [SerializeField] private TextMeshProUGUI pressAnyKeyText;

    [Tooltip("How fast the text pulses (higher = faster breathing glow).")]
    [SerializeField] private float pulseSpeed = 1.2f;

    [Tooltip("Minimum alpha value during the pulse (0 = fully invisible at trough).")]
    [SerializeField] [Range(0f, 1f)] private float pulseMinAlpha = 0.3f;

    [Header("Audio")]
    [Tooltip("AudioSource in this scene playing the main menu music. Loop=true, PlayOnAwake=true.")]
    [SerializeField] private AudioSource mainMenuMusicSource;

    [Header("Scene")]
    [Tooltip("Exact name of the main game scene to load. Must match Build Settings.")]
    [SerializeField] private string mainGameSceneName = "MainGame";

    [Tooltip("Fade-out duration before loading the main game scene.")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Fade Canvas")]
    [Tooltip("Full-screen black Image CanvasGroup used to fade out before loading. " +
             "Set its CanvasGroup alpha to 0 at start.")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    // ── State ────────────────────────────────────────────────────────────────
    private bool _loading = false;
    private PlayerInputActions _input;

    void Awake()
    {
        _input = new PlayerInputActions();
    }

    void OnEnable()  => _input.Enable();
    void OnDisable() => _input.Disable();

    void Start()
    {
        // Ensure fade canvas starts invisible
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        // Ensure music is playing
        if (mainMenuMusicSource != null && !mainMenuMusicSource.isPlaying)
            mainMenuMusicSource.Play();
    }

    void Update()
    {
        if (_loading) return;

        // Pulse the press-any-key text
        PulseText();

        // Any key starts the game — Keyboard.current is valid after _input.Enable()
        if (!_loading && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            StartCoroutine(LoadMainGame());
    }

    void PulseText()
    {
        if (pressAnyKeyText == null) return;

        // Sine wave oscillates between pulseMinAlpha and 1.0
        float alpha = Mathf.Lerp(pulseMinAlpha, 1f,
                                 (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) + 1f) * 0.5f);

        Color c = pressAnyKeyText.color;
        c.a = alpha;
        pressAnyKeyText.color = c;
    }

    IEnumerator LoadMainGame()
    {
        _loading = true;

        // Fade out to black before loading
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeOutDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        Debug.Log($"[MainMenu] Loading scene: {mainGameSceneName}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainGameSceneName);
    }
}

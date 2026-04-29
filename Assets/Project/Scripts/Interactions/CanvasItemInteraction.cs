using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic interactable — shows a Canvas with a 2D image when the player interacts.
/// Player closes it ONLY by clicking the close button on the canvas.
///
/// SETUP:
///   1. Attach to any 3D object that the player can examine.
///   2. Create a Canvas (World Space or Screen Space) with a 2D Image on it.
///   3. Assign that Canvas GameObject to 'itemCanvas'.
///   4. Assign the close Button inside the canvas to 'closeButton'.
///   5. Hook up 'OnFirstInteracted' in the Inspector to trigger one-time events.
/// </summary>
public class CanvasItemInteraction : MonoBehaviour, IInteractable
{
    [Header("Canvas Reference")]
    [SerializeField] private GameObject itemCanvas;

    [Header("Interaction Settings")]
    [SerializeField] private string promptText = "Examine";
    [SerializeField] private bool canInteractMultipleTimes = true;

    [Header("Optional: Anxiety on Interact")]
    [Tooltip("Adds this amount of anxiety once when the player first examines this object. 0 = no effect.")]
    [SerializeField] private float anxietyOnFirstInteract = 0f;

    [Header("Optional: Close Button")]
    [SerializeField] private Button closeButton;

    // ─── Events ──────────────────────────────────────────────────────────────
    /// <summary>Fires every time the player examines this object.</summary>
    public event Action OnInteracted;
    /// <summary>Fires the FIRST time only.</summary>
    public event Action OnFirstInteracted;

    // ─── State ────────────────────────────────────────────────────────────────
    private bool _hasInteractedOnce;
    private bool _isOpen;
    private float _timeOpened;

    public bool HasInteractedOnce => _hasInteractedOnce;
    public bool IsOpen            => _isOpen;

    void Start()
    {
        if (itemCanvas != null)  itemCanvas.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(CloseCanvas);
    }

    // Canvas is closed exclusively via the close button — no keyboard shortcut.

    // ─── IInteractable ───────────────────────────────────────────────────────
    public void Interact()
    {
        if (!canInteractMultipleTimes && _hasInteractedOnce) return;

        OpenCanvas();

        bool firstTime = !_hasInteractedOnce;
        if (firstTime)
        {
            _hasInteractedOnce = true;
            if (anxietyOnFirstInteract > 0f && AnxietyManager.Instance != null)
                AnxietyManager.Instance.AddAnxiety(anxietyOnFirstInteract);
            OnFirstInteracted?.Invoke();
        }

        OnInteracted?.Invoke();
        Debug.Log($"[CanvasItemInteraction] '{gameObject.name}' examined.");
    }

    public string GetPromptText() => _isOpen ? "Close" : promptText;

    // ─── Canvas Control ──────────────────────────────────────────────────────
    public void OpenCanvas()
    {
        if (itemCanvas == null)
        {
            Debug.LogWarning($"[CanvasItemInteraction] '{gameObject.name}' has no canvas assigned!");
            return;
        }
        itemCanvas.SetActive(true);
        _isOpen = true;
        _timeOpened = Time.time;
        UIInputMode.Enter();
    }

    public void CloseCanvas()
    {
        if (itemCanvas != null) itemCanvas.SetActive(false);
        _isOpen = false;
        UIInputMode.Exit();
    }

    /// <summary>Force-open the canvas from another script.</summary>
    public void ForceOpen() => OpenCanvas();

    /// <summary>Force-close the canvas from another script.</summary>
    public void ForceClose() => CloseCanvas();
}

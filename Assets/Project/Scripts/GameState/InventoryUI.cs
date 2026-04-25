using UnityEngine;

/// <summary>
/// Shows collected item icons in the HUD when picked up.
///
/// SETUP:
///   1. Create a Canvas (Screen Space – Overlay), name it "InventoryCanvas".
///   2. Add a Panel with a HorizontalLayoutGroup inside the canvas.
///   3. Add Image children to the panel — one per item.
///   4. Assign each Image's GameObject to the slots below.
///   5. Attach this script to the InventoryCanvas or any persistent GameObject.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [Header("Item Icon GameObjects (Image components)")]
    [Tooltip("GameObject containing the key Image. Hidden until key is picked up.")]
    [SerializeField] private GameObject keyIconObject;

    [Tooltip("GameObject containing the switch piece Image. Hidden until piece is picked up.")]
    [SerializeField] private GameObject switchPieceIconObject;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        SetActive(keyIconObject,         false);
        SetActive(switchPieceIconObject, false);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnHallwayKeyPickup  += ShowKeyIcon;
            GameStateManager.Instance.OnSwitchPiecePickup += ShowSwitchPieceIcon;
        }
        else
        {
            Debug.LogWarning("[InventoryUI] GameStateManager not found!");
        }
    }

    void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnHallwayKeyPickup  -= ShowKeyIcon;
            GameStateManager.Instance.OnSwitchPiecePickup -= ShowSwitchPieceIcon;
        }
    }

    // ─── Public ──────────────────────────────────────────────────────────────
    public void ShowKeyIcon()         { SetActive(keyIconObject,         true);  Debug.Log("[InventoryUI] Key icon shown."); }
    public void ShowSwitchPieceIcon() { SetActive(switchPieceIconObject, true);  Debug.Log("[InventoryUI] Switch piece icon shown."); }
    public void HideKeyIcon()         { SetActive(keyIconObject,         false); }
    public void HideSwitchPieceIcon() { SetActive(switchPieceIconObject, false); }

    void SetActive(GameObject obj, bool state)
    {
        if (obj != null) obj.SetActive(state);
    }
}

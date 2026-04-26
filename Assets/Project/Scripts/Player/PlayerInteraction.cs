using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("How far the player can reach to interact with an object.")]
    [SerializeField] private float interactDistance = 3f; 
    [Tooltip("How thick the ray is. Higher value makes it much easier to hit small objects on the floor.")]
    [SerializeField] private float interactThickness = 0.3f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("Camera Reference")]
    [SerializeField] private Camera _cam;

    [Header("UI")]
    [SerializeField] private GameObject interactPromptUI;
    [SerializeField] private TextMeshProUGUI promptText;

    private IInteractable _currentTarget;
    private PlayerInputActions _inputActions;

    void Awake()
    {
        _inputActions = new PlayerInputActions();

        if (_cam == null) _cam = Camera.main;
    }

    void OnEnable()
    {
        _inputActions.Player.Enable();
        _inputActions.Player.Interact.performed += OnInteractPerformed;
    }

    void OnDisable()
    {
        _inputActions.Player.Interact.performed -= OnInteractPerformed;
        _inputActions.Player.Disable();
    }

    void Update()
    {
        DetectInteractable();
    }

    void DetectInteractable()
    {
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        // Using SphereCast instead of Raycast creates a "thick" cylinder, making it way easier to hit objects
        if (Physics.SphereCast(ray, interactThickness, out hit, interactDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                if (_currentTarget != interactable)
                {
                    _currentTarget = interactable;
                    ShowPrompt(_currentTarget.GetPromptText());
                    Debug.Log($"<color=green>[Interaction] Success! Pointing at: {hit.collider.gameObject.name}</color>");
                }
                return;
            }
            else
            {
                // THIS IS LIKELY THE BUG: It hit an object on the Interactable layer, but there is no script attached!
                Debug.Log($"<color=orange>[Interaction] WARNING: Hit '{hit.collider.gameObject.name}' but it has NO interactable script attached! It might be blocking your ray.</color>");
            }
        }

        if (_currentTarget != null)
        {
            _currentTarget = null;
            HidePrompt();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (_currentTarget != null)
        {
            _currentTarget.Interact();
            ShowPrompt(_currentTarget.GetPromptText());
        }
    }

    void ShowPrompt(string text)
    {
        if (interactPromptUI) interactPromptUI.SetActive(true);
        if (promptText) promptText.text = $"[E] {text}";
    }

    void HidePrompt()
    {
        if (interactPromptUI) interactPromptUI.SetActive(false);
    }
    
    void OnDrawGizmosSelected()
    {
        if (_cam != null)
        {
            Gizmos.color = Color.green;
            Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Gizmos.DrawRay(ray.origin, ray.direction * interactDistance);
        }
    }
}

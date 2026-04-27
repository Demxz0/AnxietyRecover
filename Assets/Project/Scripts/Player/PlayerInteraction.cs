using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
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
        if (UIInputMode.IsInUI)
        {
            if (_currentTarget != null)
            {
                _currentTarget = null;
                HidePrompt();
            }
            return;
        }

        DetectInteractable();
    }

    void DetectInteractable()
    {
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        if (Physics.SphereCast(ray, interactThickness, out hit, interactDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                if (_currentTarget != interactable)
                {
                    _currentTarget = interactable;
                    ShowPrompt(_currentTarget.GetPromptText());
                }
                return;
            }
            else
            {
                Debug.Log($"<color=orange>[Interaction] WARNING: Hit '{hit.collider.gameObject.name}' on Interactable layer but has NO interactable script!</color>");
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
        if (UIInputMode.IsInUI) return;
        if (_currentTarget == null) return;

        // Cache before Interact() — opening a canvas calls UIInputMode.Enter()
        // which clears _currentTarget, so ShowPrompt below would crash.
        IInteractable target = _currentTarget;
        target.Interact();

        if (!UIInputMode.IsInUI && _currentTarget != null)
            ShowPrompt(_currentTarget.GetPromptText());
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

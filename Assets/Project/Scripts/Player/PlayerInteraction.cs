using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Handles player raycasting for interaction.
///
/// CROSSHAIR COLOR:
///   The crosshair dot turns red when the raycast hits an IInteractable.
///   Assign the crosshair Image to 'crosshairImage' in the Inspector.
///
/// CURSOR VISIBILITY:
///   Cursor is LOCKED and HIDDEN at all times during gameplay.
///   It only appears when a canvas item is open (UIInputMode.Enter) or Esc is pressed.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float interactThickness = 0.3f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("Camera Reference")]
    [SerializeField] private Camera _cam;

    [Header("Crosshair")]
    [Tooltip("The crosshair dot Image in the center of the screen. " +
             "Turns red when aiming at an interactable object.")]
    [SerializeField] private Image crosshairImage;
    [SerializeField] private Color crosshairDefaultColor = Color.white;
    [SerializeField] private Color crosshairInteractableColor = Color.red;

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
                SetCrosshairColor(false);
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
                    _currentTarget = interactable;

                SetCrosshairColor(true);
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
        }

        SetCrosshairColor(false);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (UIInputMode.IsInUI) return;
        if (_currentTarget == null) return;

        // Cache before Interact() — opening a canvas calls UIInputMode.Enter()
        // which clears _currentTarget, so ShowPrompt below would crash.
        IInteractable target = _currentTarget;
        target.Interact();
    }

    void SetCrosshairColor(bool isInteractable)
    {
        if (crosshairImage == null) return;
        crosshairImage.color = isInteractable ? crosshairInteractableColor : crosshairDefaultColor;
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

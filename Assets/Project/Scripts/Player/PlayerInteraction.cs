using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactRadius = 2f;
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

        Debug.DrawRay(transform.position, Vector3.up * interactRadius, Color.yellow);
    }

    void DetectInteractable()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius, interactableLayer);

        if (hits.Length > 0)
        {
            Collider closest = GetClosest(hits);
            IInteractable interactable = closest.GetComponent<IInteractable>();

            if (interactable != null)
            {
                if (_currentTarget != interactable)
                {
                    _currentTarget = interactable;
                    ShowPrompt(_currentTarget.GetPromptText());
                    Debug.Log("Found interactable: " + closest.gameObject.name);
                }
                return;
            }
        }

        if (_currentTarget != null)
        {
            _currentTarget = null;
            HidePrompt();
        }
    }

    Collider GetClosest(Collider[] colliders)
    {
        Collider closest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider col in colliders)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = col;
            }
        }

        return closest;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        Debug.Log("E pressed — target: " + (_currentTarget != null ? _currentTarget.ToString() : "NULL"));

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
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
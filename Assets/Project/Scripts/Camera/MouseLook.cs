using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    public float sensitivityX;
    public float sensitivityY;

    public Transform orientation;

    private PlayerInputActions input;
    private Vector2 lookInput;

    float xRotation = 0f; 
    float yRotation = 0f; 

    void Awake()
    {
        input = new PlayerInputActions();

        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += ctx => lookInput = Vector2.zero;
    }

    void OnEnable()
    {
        input.Enable();
    }

    void OnDisable() => input.Disable();

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static bool CanLook = true;

    void Update()
    {
        if (!CanLook) return;

        float mouseX = lookInput.x * sensitivityX * Time.deltaTime;
        float mouseY = lookInput.y * sensitivityY * Time.deltaTime;

        yRotation += mouseX; 
        xRotation -= mouseY; 

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
        orientation.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    /// <summary>
    /// Resets the stored pitch (xRotation) to 0 so the camera looks straight ahead
    /// when controls are re-enabled. Preserves the current horizontal yaw direction.
    /// Call this just before setting CanLook = true in the wake-up sequence.
    /// </summary>
    public void ResetPitchToForward()
    {
        xRotation = 0f;
        // Sync yRotation from the current world Y angle so horizontal aim is preserved
        yRotation = transform.eulerAngles.y;
        // Apply immediately so there is no single-frame snap
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        if (orientation != null)
            orientation.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }
}

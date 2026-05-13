using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public Transform orientation;

    public CharacterController controller;
    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    public float gravity = -9.8f;

    private PlayerInputActions input;
    private Vector2 moveInput;
    private float currentSpeed;
    private Vector3 velocity;

    /// <summary>
    /// Multiplier applied to movement speed. Set by other systems (e.g., medication drowsiness).
    /// 1.0 = normal speed, 0.5 = half speed, etc.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    public static bool CanMove = true;

    void Awake()
    {
        input = new PlayerInputActions();
        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();

    void Update()
    {
        Debug.Log($"FPS: {1f / Time.deltaTime:F1} | DeltaTime: {Time.deltaTime * 1000:F2}ms");
    
        if (!CanMove) return;

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        currentSpeed = input.Player.Run.IsPressed() ? runSpeed : walkSpeed;
        currentSpeed *= SpeedMultiplier;

        Vector3 move = orientation.right * moveInput.x + orientation.forward * moveInput.y;
        controller.Move(move * currentSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}

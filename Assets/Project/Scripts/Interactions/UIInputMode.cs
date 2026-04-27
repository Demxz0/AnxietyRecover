using UnityEngine;

public static class UIInputMode
{
    public static bool IsInUI { get; private set; }

    public static void Enter()
    {
        if (IsInUI) return;
        IsInUI = true;

        MouseLook.CanLook = false;
        PlayerMovement.CanMove = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void Exit()
    {
        if (!IsInUI) return;
        IsInUI = false;

        MouseLook.CanLook = true;
        PlayerMovement.CanMove = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}

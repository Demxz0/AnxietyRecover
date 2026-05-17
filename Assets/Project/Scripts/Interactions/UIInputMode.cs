using UnityEngine;

public static class UIInputMode
{
    public static bool IsInUI => _openCount > 0;
    private static int _openCount = 0;

    public static void Enter()
    {
        _openCount++;
        if (_openCount == 1)
        {
            MouseLook.CanLook = false;
            PlayerMovement.CanMove = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public static void Exit()
    {
        if (_openCount > 0)
        {
            _openCount--;
        }

        if (_openCount == 0)
        {
            MouseLook.CanLook = true;
            PlayerMovement.CanMove = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

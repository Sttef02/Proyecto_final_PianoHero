#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#else
using UnityEngine;
#endif

public static class KeyboardInput
{
#if ENABLE_INPUT_SYSTEM
    static readonly Key[] LaneKeys = { Key.D, Key.F, Key.J, Key.K };

    public static bool LaneDown(int lane) => Keyboard.current != null && Keyboard.current[LaneKeys[lane]].wasPressedThisFrame;

    public static bool LaneUp(int lane) => Keyboard.current != null && Keyboard.current[LaneKeys[lane]].wasReleasedThisFrame;

    public static bool EscapeDown() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    public static int NavigationDirection()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return 0;
        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) return -1;
        if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) return 1;
        return 0;
    }
#else
    static readonly KeyCode[] LaneKeys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };

    public static bool LaneDown(int lane) => Input.GetKeyDown(LaneKeys[lane]);

    public static bool LaneUp(int lane) => Input.GetKeyUp(LaneKeys[lane]);

    public static bool EscapeDown() => Input.GetKeyDown(KeyCode.Escape);

    public static int NavigationDirection()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)) return -1;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)) return 1;
        return 0;
    }
#endif
}

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public struct PointerState
{
    public Vector2 Position;
    public float Scroll;
    public bool Clicked;
}

public static class PointerInput
{
    public static PointerState Read()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null) return new PointerState();
        return new PointerState
        {
            Position = mouse.position.ReadValue(),
            Scroll = mouse.scroll.ReadValue().y,
            Clicked = mouse.leftButton.wasPressedThisFrame
        };
#else
        return new PointerState
        {
            Position = Input.mousePosition,
            Scroll = Input.mouseScrollDelta.y,
            Clicked = Input.GetMouseButtonDown(0)
        };
#endif
    }
}

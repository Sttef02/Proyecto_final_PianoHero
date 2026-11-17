using UnityEngine;

public struct Layout
{
    public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    public static readonly Vector2 TopRight = new Vector2(1f, 1f);
    public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    public Vector2 AnchorMin;
    public Vector2 AnchorMax;
    public Vector2 Pivot;
    public Vector2 Position;
    public Vector2 Size;

    public Layout(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        AnchorMin = anchorMin;
        AnchorMax = anchorMax;
        Pivot = pivot;
        Position = position;
        Size = size;
    }

    public static Layout At(Vector2 anchor, Vector2 position, Vector2 size) => new Layout(anchor, anchor, anchor, position, size);

    public static Layout Centered(Vector2 position, Vector2 size) => At(Center, position, size);

    // Ocupa todo el ancho del padre, pegado arriba (pivot) con la altura dada.
    public static Layout TopRow(Vector2 pivot, Vector2 position, float widthInset, float height) =>
        new Layout(TopLeft, TopRight, pivot, position, new Vector2(widthInset, height));

    public static Layout BottomRow(Vector2 position, float widthInset, float height) =>
        new Layout(BottomLeft, BottomRight, BottomCenter, position, new Vector2(widthInset, height));

    public void ApplyTo(RectTransform rect)
    {
        rect.anchorMin = AnchorMin;
        rect.anchorMax = AnchorMax;
        rect.pivot = Pivot;
        rect.anchoredPosition = Position;
        rect.sizeDelta = Size;
    }
}

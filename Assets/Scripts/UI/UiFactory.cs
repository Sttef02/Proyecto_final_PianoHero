using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UiFactory
{
    const int SortingOrder = 50;
    const float MatchWidthOrHeight = 0.5f;
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    const int CornerTextureSize = 64;
    const int CornerRadius = 22;
    const float SpritePixelsPerUnit = 100f;
    const float SlicedPixelsPerUnitMultiplier = 1.6f;
    const float BorderThickness = 3f;

    readonly TMP_FontAsset font;
    readonly Sprite roundedSprite;

    public UiFactory(TMP_FontAsset font)
    {
        this.font = font != null ? font : TMP_Settings.defaultFontAsset;
        roundedSprite = CreateRoundedSprite();
    }

    public RectTransform CreateCanvas(Transform parent, string name)
    {
        RectTransform rect = CreateRect(name, parent);
        var canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = rect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = MatchWidthOrHeight;
        return rect;
    }

    public RectTransform Group(Transform parent, string name)
    {
        RectTransform group = CreateRect(name, parent);
        Stretch(group, 0f);
        return group;
    }

    // Panel con borde: un fondo redondeado encima de otro un poco mas grande.
    public RectTransform Panel(Transform parent, string name, Layout layout, Color background, Color border)
    {
        RectTransform panel = CreateRect(name, parent);
        layout.ApplyTo(panel);
        Stretch(Box(panel, "Borde", border).rectTransform, -BorderThickness);
        Stretch(Box(panel, "Fondo", background).rectTransform, 0f);
        return panel;
    }

    public Image Box(Transform parent, string name, Color color, bool rounded = true)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (rounded)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = SlicedPixelsPerUnitMultiplier;
        }
        return image;
    }

    public TextMeshProUGUI Label(Transform parent, string name, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(rect, 0f);
        return label;
    }

    public TextMeshProUGUI Label(Transform parent, string name, float fontSize, Color color, TextAlignmentOptions alignment, Layout layout)
    {
        TextMeshProUGUI label = Label(parent, name, fontSize, color, alignment);
        layout.ApplyTo(label.rectTransform);
        return label;
    }

    public static void Outline(TMP_Text label, float width)
    {
        label.outlineWidth = width;
        label.outlineColor = new Color32(0, 0, 0, 255);
    }

    public static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Layout.Center;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static void Stretch(RectTransform rect, float horizontalInset, float verticalInset)
    {
        Stretch(rect, 0f);
        rect.offsetMin = new Vector2(horizontalInset, verticalInset);
        rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return (RectTransform)gameObject.transform;
    }

    static Sprite CreateRoundedSprite()
    {
        var texture = new Texture2D(CornerTextureSize, CornerTextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[CornerTextureSize * CornerTextureSize];
        for (int y = 0; y < CornerTextureSize; y++)
            for (int x = 0; x < CornerTextureSize; x++)
                pixels[y * CornerTextureSize + x] = new Color32(255, 255, 255, CornerAlpha(x, y));
        texture.SetPixels32(pixels);
        texture.Apply();

        var border = new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius);
        return Sprite.Create(texture, new Rect(0, 0, CornerTextureSize, CornerTextureSize), Layout.Center,
                             SpritePixelsPerUnit, 0, SpriteMeshType.FullRect, border);
    }

    static byte CornerAlpha(int x, int y)
    {
        var pixel = new Vector2(x + 0.5f, y + 0.5f);
        var nearestInside = new Vector2(Mathf.Clamp(pixel.x, CornerRadius, CornerTextureSize - CornerRadius),
                                        Mathf.Clamp(pixel.y, CornerRadius, CornerTextureSize - CornerRadius));
        float coverage = Mathf.Clamp01(CornerRadius - Vector2.Distance(pixel, nearestInside) + 0.5f);
        return (byte)(coverage * 255);
    }
}

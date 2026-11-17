using UnityEngine;

public class HighwayBuilder
{
    const string MaterialResourceName = "PianoHeroMaterial";
    static readonly string[] FallbackShaders =
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Unlit",
        "Standard",
        "Sprites/Default"
    };

    const float TrackExtraLength = 6f;
    const float TrackOffsetBehind = 3f;
    const float TrackSideMargin = 0.4f;
    const float TrackThickness = 0.1f;
    const float TrackHeight = -0.08f;
    const float LaneLineWidth = 0.04f;
    const float LaneLineHeight = -0.02f;
    const float LaneLineThickness = 0.02f;
    const float HitLineDepth = 0.08f;
    const float HitLineHeight = -0.01f;
    public const float ButtonThickness = 0.03f;
    public const float ButtonSizeFactor = 0.8f;
    public const float IdleButtonBrightness = 0.35f;
    const float ButtonHeight = 0.02f;
    const float NoteHeadSizeFactor = 0.75f;
    const float NoteHeadThickness = 0.08f;
    const float TailBrightness = 0.8f;

    static readonly Color TrackColor = new Color(0.06f, 0.06f, 0.09f);
    static readonly Color LaneLineColor = new Color(0.5f, 0.5f, 0.6f);
    static readonly Color BackgroundColor = new Color(0.02f, 0.02f, 0.04f);
    static readonly Vector3 CameraPosition = new Vector3(0f, 5.5f, -6.5f);
    static readonly Vector3 CameraRotation = new Vector3(28f, 0f, 0f);

    readonly Transform root;
    readonly float laneWidth;
    readonly Material baseMaterial;
    readonly Shader fallbackShader;

    public HighwayBuilder(Transform root, float laneWidth)
    {
        this.root = root;
        this.laneWidth = laneWidth;
        // En el build Unity quita los shaders que ningun material usa; el material de Resources siempre se incluye.
        baseMaterial = Resources.Load<Material>(MaterialResourceName);
        if (baseMaterial != null) return;
        fallbackShader = FindFallbackShader();
        if (fallbackShader == null) Debug.LogError("No se encontro el material " + MaterialResourceName + " ni un shader de respaldo.");
    }

    public float LaneX(int lane) => (lane - (ArduinoInput.LaneCount - 1) / 2f) * laneWidth;

    Material CreateMaterial(Color color)
    {
        var material = baseMaterial != null ? new Material(baseMaterial) : new Material(fallbackShader);
        material.color = color;
        return material;
    }

    public void BuildTrack(float length)
    {
        float width = laneWidth * ArduinoInput.LaneCount;
        float centerZ = length / 2f - TrackOffsetBehind;
        float fullLength = length + TrackExtraLength;

        CreatePrimitive(PrimitiveType.Cube, new Vector3(0f, TrackHeight, centerZ),
                        new Vector3(width + TrackSideMargin, TrackThickness, fullLength), CreateMaterial(TrackColor));

        Material lineMaterial = CreateMaterial(LaneLineColor);
        for (int line = 0; line <= ArduinoInput.LaneCount; line++)
        {
            float lineX = (line - ArduinoInput.LaneCount / 2f) * laneWidth;
            CreatePrimitive(PrimitiveType.Cube, new Vector3(lineX, LaneLineHeight, centerZ),
                            new Vector3(LaneLineWidth, LaneLineThickness, fullLength), lineMaterial);
        }

        CreatePrimitive(PrimitiveType.Cube, new Vector3(0f, HitLineHeight, 0f),
                        new Vector3(width, LaneLineThickness, HitLineDepth), CreateMaterial(Color.white));
    }

    public Renderer[] BuildLaneButtons(Color[] laneColors)
    {
        var buttons = new Renderer[ArduinoInput.LaneCount];
        float size = laneWidth * ButtonSizeFactor;
        for (int lane = 0; lane < buttons.Length; lane++)
        {
            GameObject button = CreatePrimitive(PrimitiveType.Cylinder, new Vector3(LaneX(lane), ButtonHeight, 0f),
                                                new Vector3(size, ButtonThickness, size),
                                                CreateMaterial(laneColors[lane] * IdleButtonBrightness));
            buttons[lane] = button.GetComponent<Renderer>();
        }
        return buttons;
    }

    public GameObject CreateNoteHead(Color color)
    {
        float size = laneWidth * NoteHeadSizeFactor;
        return CreatePrimitive(PrimitiveType.Cylinder, Vector3.zero, new Vector3(size, NoteHeadThickness, size), CreateMaterial(color));
    }

    public GameObject CreateNoteTail(Color color) =>
        CreatePrimitive(PrimitiveType.Cube, Vector3.zero, Vector3.one, CreateMaterial(color * TailBrightness));

    public static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        camera.transform.position = CameraPosition;
        camera.transform.rotation = Quaternion.Euler(CameraRotation);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
    }

    GameObject CreatePrimitive(PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        Object.Destroy(primitive.GetComponent<Collider>());
        primitive.transform.SetParent(root, false);
        primitive.transform.localPosition = position;
        primitive.transform.localScale = scale;
        primitive.GetComponent<Renderer>().sharedMaterial = material;
        return primitive;
    }

    static Shader FindFallbackShader()
    {
        foreach (string shaderName in FallbackShaders)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null) return shader;
        }
        return null;
    }
}

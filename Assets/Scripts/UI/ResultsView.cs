using TMPro;
using UnityEngine;

public struct ResultsData
{
    public string Title;
    public Difficulty Difficulty;
    public float Score;
    public int Perfects;
    public int Goods;
    public int Misses;
    public int BestStreak;
    public bool IsNewRecord;
}

public class ResultsView
{
    const float GradeS = 95f;
    const float GradeA = 85f;
    const float GradeB = 70f;
    const float GradeC = 50f;
    const float PercentScale = 100f;
    const float ScoreCountSeconds = 1.2f;
    const float GradeAppearAt = 1.0f;
    const float GradePopSeconds = 0.25f;
    const float GradePopScale = 1.6f;
    const float RecordAppearAt = 1.2f;
    const float RecordBlinkRate = 3f;
    const float RecordBlinkOn = 1.4f;
    const float FooterAppearAt = 1.4f;
    const string GradeLetterS = "S";
    const string GradeLetterA = "A";
    const string GradeLetterB = "B";
    const string GradeLetterC = "C";
    const string GradeLetterD = "D";
    const string NewRecordText = "NUEVO RECORD!";
    const string FooterText = "B1  O  CLIC  PARA VOLVER AL MENU";

    const float TitleFontSize = 54f;
    const float DifficultyFontSize = 30f;
    const float GradeFontSize = 280f;
    const float ScoreFontSize = 96f;
    const float RecordFontSize = 36f;
    const float StatsFontSize = 32f;
    const float FooterFontSize = 28f;
    const float GradeOutline = 0.2f;
    const float StatsLineSpacing = 18f;

    static readonly Vector2 PanelSize = new Vector2(1160f, 860f);
    static readonly Layout TitleLayout = Layout.TopRow(Layout.TopCenter, new Vector2(0f, -40f), -80f, 70f);
    static readonly Layout DifficultyLayout = Layout.TopRow(Layout.TopCenter, new Vector2(0f, -112f), -80f, 40f);
    static readonly Layout GradeLayout = new Layout(Layout.TopLeft, Layout.TopLeft, Layout.Center, new Vector2(250f, -380f), new Vector2(360f, 340f));
    static readonly Layout ScoreLayout = Layout.TopRow(Layout.TopLeft, new Vector2(470f, -190f), -510f, 110f);
    static readonly Layout RecordLayout = Layout.TopRow(Layout.TopLeft, new Vector2(474f, -300f), -510f, 46f);
    static readonly Layout StatsLayout = Layout.TopRow(Layout.TopLeft, new Vector2(474f, -370f), -510f, 330f);
    static readonly Layout FooterLayout = Layout.BottomRow(new Vector2(0f, 36f), -80f, 40f);

    readonly RectTransform root;
    TextMeshProUGUI titleText, difficultyText, gradeText, scoreText, statsText, recordText, footerText;
    float shownAt;
    float targetScore;
    bool isNewRecord;
    bool visible;

    public ResultsView(UiFactory ui, Transform canvas)
    {
        root = ui.Group(canvas, "Results");
        UiFactory.Stretch(ui.Box(root, "Overlay", Palette.Overlay, false).rectTransform, 0f);
        RectTransform panel = ui.Panel(root, "Panel", Layout.Centered(Vector2.zero, PanelSize), Palette.PanelBackground, Palette.Accent);
        BuildHeader(ui, panel);
        BuildBody(ui, panel);
    }

    public void SetVisible(bool show)
    {
        visible = show;
        root.gameObject.SetActive(show);
    }

    public void Show(ResultsData data, float now)
    {
        float accuracy = Accuracy(data);
        string grade = Grade(accuracy);

        titleText.text = TextFormat.ForPixelFont(data.Title);
        UiFactory.ShowDifficulty(difficultyText, data.Difficulty);
        gradeText.text = grade;
        gradeText.color = GradeColor(grade);
        statsText.text =
            $"PERFECTO      {data.Perfects}\n" +
            $"BIEN          {data.Goods}\n" +
            $"FALLO         {data.Misses}\n" +
            $"RACHA MAXIMA  {data.BestStreak}\n" +
            $"PRECISION     {accuracy:0.0}%";

        targetScore = data.Score;
        isNewRecord = data.IsNewRecord;
        shownAt = now;
        SetVisible(true);
    }

    public void Animate(float now)
    {
        if (!visible) return;
        float elapsed = now - shownAt;

        float progress = Mathf.Clamp01(elapsed / ScoreCountSeconds);
        float eased = 1f - (1f - progress) * (1f - progress);
        scoreText.text = TextFormat.Points(targetScore * eased);

        float gradeProgress = Mathf.Clamp01((elapsed - GradeAppearAt) / GradePopSeconds);
        float gradeScale = gradeProgress <= 0f ? 0f : Mathf.Lerp(GradePopScale, 1f, gradeProgress);
        gradeText.rectTransform.localScale = Vector3.one * gradeScale;

        bool blinkOn = UiFactory.IsBlinkOn(now, RecordBlinkRate, RecordBlinkOn);
        recordText.gameObject.SetActive(isNewRecord && elapsed > RecordAppearAt && blinkOn);
        footerText.alpha = elapsed > FooterAppearAt ? 1f : 0f;
    }

    static float Accuracy(ResultsData data)
    {
        int total = data.Perfects + data.Goods + data.Misses;
        return total > 0 ? (data.Perfects + data.Goods) * PercentScale / total : 0f;
    }

    static string Grade(float accuracy)
    {
        if (accuracy >= GradeS) return GradeLetterS;
        if (accuracy >= GradeA) return GradeLetterA;
        if (accuracy >= GradeB) return GradeLetterB;
        if (accuracy >= GradeC) return GradeLetterC;
        return GradeLetterD;
    }

    static Color GradeColor(string grade)
    {
        switch (grade)
        {
            case GradeLetterS: return Palette.Medium;
            case GradeLetterA: return Palette.Easy;
            case GradeLetterB: return Palette.Accent;
            case GradeLetterC: return Palette.GradeC;
            default: return Palette.Expert;
        }
    }

    void BuildHeader(UiFactory ui, RectTransform panel)
    {
        titleText = ui.Label(panel, "Title", TitleFontSize, Color.white, TextAlignmentOptions.Center, TitleLayout);
        difficultyText = ui.Label(panel, "Difficulty", DifficultyFontSize, Palette.Medium, TextAlignmentOptions.Center, DifficultyLayout);
        gradeText = ui.Label(panel, "Grade", GradeFontSize, Palette.Accent, TextAlignmentOptions.Center, GradeLayout);
        UiFactory.Outline(gradeText, GradeOutline);
    }

    void BuildBody(UiFactory ui, RectTransform panel)
    {
        scoreText = ui.Label(panel, "Score", ScoreFontSize, Color.white, TextAlignmentOptions.Left, ScoreLayout);
        recordText = ui.Label(panel, "Record", RecordFontSize, Palette.Medium, TextAlignmentOptions.Left, RecordLayout);
        recordText.text = NewRecordText;
        statsText = ui.Label(panel, "Stats", StatsFontSize, Color.white, TextAlignmentOptions.TopLeft, StatsLayout);
        statsText.lineSpacing = StatsLineSpacing;
        footerText = ui.Label(panel, "Footer", FooterFontSize, Palette.SecondaryText, TextAlignmentOptions.Center, FooterLayout);
        footerText.text = FooterText;
    }
}

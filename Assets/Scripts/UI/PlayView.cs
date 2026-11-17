using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct HudState
{
    public float Score;
    public int Streak;
    public int Multiplier;
    public int LitLeds;
    public bool StarMode;
    public string Title;
    public Difficulty Difficulty;
    public float Progress;
    public double SongTime;
}

struct PopAnimation
{
    public float VisibleSeconds;
    public float HoldSeconds;
    public float PopSeconds;
    public float PopScale;
}

public class PlayView
{
    static readonly PopAnimation JudgementPop = new PopAnimation { VisibleSeconds = 0.5f, HoldSeconds = 0.3f, PopSeconds = 0.12f, PopScale = 1.45f };
    static readonly PopAnimation NoticePop = new PopAnimation { VisibleSeconds = 1.2f, HoldSeconds = 0.9f, PopSeconds = 0.15f, PopScale = 1.3f };

    const float CountdownPulse = 0.35f;
    const float CountdownMinAlpha = 0.2f;
    const float GoSeconds = 0.6f;
    const float GoGrowth = 0.4f;
    const float StarBlinkRate = 8f;
    const float StarBlinkOnTime = 1f;
    const float NeverShown = -10f;
    const string GoText = "YA!";
    const string ScoreCaption = "PUNTOS";
    const string StreakLabel = "RACHA ";
    const string MultiplierLabel = "X";

    const float CaptionFontSize = 22f;
    const float ScoreFontSize = 64f;
    const float StreakFontSize = 28f;
    const float MultiplierFontSize = 50f;
    const float SongTitleFontSize = 32f;
    const float DifficultyFontSize = 26f;
    const float JudgementFontSize = 92f;
    const float NoticeFontSize = 48f;
    const float CountdownFontSize = 230f;
    const float TextOutline = 0.25f;
    const float CountdownOutline = 0.3f;
    const float ProgressBarHeight = 10f;

    static readonly Layout ScoreCardLayout = Layout.At(Layout.TopLeft, new Vector2(28f, -28f), new Vector2(460f, 196f));
    static readonly Layout ScoreCaptionLayout = Layout.TopRow(Layout.TopLeft, new Vector2(26f, -16f), -40f, 30f);
    static readonly Layout ScoreLayout = Layout.TopRow(Layout.TopLeft, new Vector2(24f, -44f), -40f, 72f);
    static readonly Layout StreakLayout = Layout.At(Layout.TopLeft, new Vector2(26f, -122f), new Vector2(260f, 36f));
    static readonly Vector2 FirstLedPosition = new Vector2(26f, -162f);
    static readonly Vector2 LedSpacing = new Vector2(62f, 0f);
    static readonly Vector2 LedSize = new Vector2(54f, 16f);
    static readonly Layout MultiplierLayout = Layout.At(Layout.BottomRight, new Vector2(-20f, 20f), new Vector2(120f, 84f));
    static readonly Layout SongCardLayout = Layout.At(Layout.TopRight, new Vector2(-28f, -28f), new Vector2(620f, 112f));
    static readonly Layout SongTitleLayout = Layout.TopRow(Layout.TopRight, new Vector2(-24f, -14f), -48f, 44f);
    static readonly Layout DifficultyLayout = Layout.TopRow(Layout.TopRight, new Vector2(-24f, -62f), -48f, 34f);
    static readonly Layout JudgementLayout = Layout.Centered(new Vector2(0f, 230f), new Vector2(1000f, 120f));
    static readonly Layout NoticeLayout = Layout.Centered(new Vector2(0f, 140f), new Vector2(1100f, 70f));
    static readonly Layout CountdownLayout = Layout.Centered(new Vector2(0f, 80f), new Vector2(800f, 300f));

    readonly RectTransform root;
    readonly Color[] laneColors;
    readonly List<Image> leds = new List<Image>();
    TextMeshProUGUI scoreText, streakText, multiplierText, titleText, difficultyText;
    TextMeshProUGUI judgementText, noticeText, countdownText;
    Image multiplierBox;
    RectTransform progressFill;
    float judgementShownAt = NeverShown;
    float noticeShownAt = NeverShown;

    public PlayView(UiFactory ui, Transform canvas, Color[] laneColors)
    {
        this.laneColors = laneColors;
        root = ui.Group(canvas, "PlayHud");
        BuildScoreCard(ui);
        BuildSongCard(ui);
        BuildProgressBar(ui);
        BuildCenterTexts(ui);
    }

    public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

    public void ResetAnimations() => judgementShownAt = noticeShownAt = NeverShown;

    public void Refresh(HudState state)
    {
        RefreshScore(state);
        titleText.text = TextFormat.ForPixelFont(state.Title);
        UiFactory.ShowDifficulty(difficultyText, state.Difficulty);
        progressFill.anchorMax = new Vector2(Mathf.Clamp01(state.Progress), 1f);
        RefreshCountdown(state.SongTime);
    }

    public void ShowJudgement(HitResult result)
    {
        judgementText.text = TextFormat.ResultLabel(result);
        judgementText.color = Palette.ForResult(result);
        judgementShownAt = Time.unscaledTime;
    }

    public void ShowNotice(string text, Color color)
    {
        noticeText.text = TextFormat.ForPixelFont(text);
        noticeText.color = color;
        noticeShownAt = Time.unscaledTime;
    }

    public void Animate(float now)
    {
        AnimatePop(judgementText, now - judgementShownAt, JudgementPop);
        AnimatePop(noticeText, now - noticeShownAt, NoticePop);
    }

    void RefreshScore(HudState state)
    {
        scoreText.text = TextFormat.Points(state.Score);
        streakText.text = StreakLabel + state.Streak;
        multiplierText.text = MultiplierLabel + state.Multiplier;
        multiplierBox.color = Palette.ForMultiplier(state.Multiplier);

        bool blinkOn = UiFactory.IsBlinkOn(Time.unscaledTime, StarBlinkRate, StarBlinkOnTime);
        for (int i = 0; i < leds.Count; i++)
        {
            bool lit = state.StarMode ? blinkOn : i < state.LitLeds;
            leds[i].color = lit ? laneColors[i % laneColors.Length] : Palette.LedOff;
        }
    }

    void RefreshCountdown(double songTime)
    {
        if (songTime < 0.0)
        {
            int secondsLeft = Mathf.CeilToInt((float)-songTime);
            float fraction = (float)-songTime - (secondsLeft - 1);
            countdownText.text = secondsLeft.ToString();
            countdownText.color = Color.white;
            countdownText.rectTransform.localScale = Vector3.one * (1f + CountdownPulse * fraction);
            countdownText.alpha = Mathf.Clamp01(CountdownMinAlpha + fraction);
        }
        else if (songTime < GoSeconds)
        {
            countdownText.text = GoText;
            countdownText.color = Palette.Accent;
            countdownText.rectTransform.localScale = Vector3.one * (1f + (float)songTime * GoGrowth);
            countdownText.alpha = 1f - (float)songTime / GoSeconds;
        }
        else
        {
            countdownText.text = string.Empty;
        }
    }

    // Aparece grande, vuelve a su tamano y se desvanece.
    static void AnimatePop(TMP_Text label, float elapsed, PopAnimation animation)
    {
        if (elapsed >= animation.VisibleSeconds)
        {
            label.alpha = 0f;
            return;
        }
        float scale = elapsed < animation.PopSeconds ? Mathf.Lerp(animation.PopScale, 1f, elapsed / animation.PopSeconds) : 1f;
        label.rectTransform.localScale = Vector3.one * scale;
        float fadeSeconds = animation.VisibleSeconds - animation.HoldSeconds;
        label.alpha = elapsed < animation.HoldSeconds ? 1f : 1f - (elapsed - animation.HoldSeconds) / fadeSeconds;
    }

    void BuildScoreCard(UiFactory ui)
    {
        RectTransform card = ui.Panel(root, "ScoreCard", ScoreCardLayout, Palette.PanelBackground, Palette.PanelBorder);
        ui.Label(card, "ScoreTitle", CaptionFontSize, Palette.SecondaryText, TextAlignmentOptions.TopLeft, ScoreCaptionLayout).text = ScoreCaption;
        scoreText = ui.Label(card, "Score", ScoreFontSize, Color.white, TextAlignmentOptions.TopLeft, ScoreLayout);
        streakText = ui.Label(card, "Streak", StreakFontSize, Color.white, TextAlignmentOptions.TopLeft, StreakLayout);

        for (int i = 0; i < ArduinoInput.LedCount; i++)
        {
            Image led = ui.Box(card, "Led" + i, Palette.LedOff);
            Layout.At(Layout.TopLeft, FirstLedPosition + LedSpacing * i, LedSize).ApplyTo(led.rectTransform);
            leds.Add(led);
        }

        multiplierBox = ui.Box(card, "Multiplier", Palette.Accent);
        MultiplierLayout.ApplyTo(multiplierBox.rectTransform);
        multiplierText = ui.Label(multiplierBox.rectTransform, "Value", MultiplierFontSize, Color.black, TextAlignmentOptions.Center);
    }

    void BuildSongCard(UiFactory ui)
    {
        RectTransform card = ui.Panel(root, "SongCard", SongCardLayout, Palette.PanelBackground, Palette.PanelBorder);
        titleText = ui.Label(card, "Title", SongTitleFontSize, Color.white, TextAlignmentOptions.Right, SongTitleLayout);
        difficultyText = ui.Label(card, "Difficulty", DifficultyFontSize, Palette.Medium, TextAlignmentOptions.Right, DifficultyLayout);
    }

    void BuildProgressBar(UiFactory ui)
    {
        Image track = ui.Box(root, "Progress", Palette.ProgressTrack, false);
        Layout.BottomRow(Vector2.zero, 0f, ProgressBarHeight).ApplyTo(track.rectTransform);

        progressFill = ui.Box(track.rectTransform, "Fill", Palette.Accent, false).rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
    }

    void BuildCenterTexts(UiFactory ui)
    {
        judgementText = ui.Label(root, "Judgement", JudgementFontSize, Color.white, TextAlignmentOptions.Center, JudgementLayout);
        UiFactory.Outline(judgementText, TextOutline);
        noticeText = ui.Label(root, "Notice", NoticeFontSize, Palette.Accent, TextAlignmentOptions.Center, NoticeLayout);
        UiFactory.Outline(noticeText, TextOutline);
        countdownText = ui.Label(root, "Countdown", CountdownFontSize, Color.white, TextAlignmentOptions.Center, CountdownLayout);
        UiFactory.Outline(countdownText, CountdownOutline);
    }
}

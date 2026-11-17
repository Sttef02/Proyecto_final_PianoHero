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

public class PlayView
{
    const float JudgementVisibleSeconds = 0.5f;
    const float JudgementHoldSeconds = 0.3f;
    const float JudgementPopSeconds = 0.12f;
    const float JudgementPopScale = 1.45f;
    const float NoticeVisibleSeconds = 1.2f;
    const float NoticeHoldSeconds = 0.9f;
    const float NoticePopSeconds = 0.15f;
    const float NoticePopScale = 1.3f;
    const float CountdownPulse = 0.35f;
    const float CountdownMinAlpha = 0.2f;
    const float GoSeconds = 0.6f;
    const float GoGrowth = 0.4f;
    const float StarBlinkRate = 8f;
    const float NeverShown = -10f;
    const string GoText = "YA!";

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
        root = ui.Group(canvas, "Juego");
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
        difficultyText.text = TextFormat.DifficultyLabel(state.Difficulty);
        difficultyText.color = Palette.ForDifficulty(state.Difficulty);
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
        AnimatePop(judgementText, now - judgementShownAt, JudgementVisibleSeconds, JudgementHoldSeconds, JudgementPopSeconds, JudgementPopScale);
        AnimatePop(noticeText, now - noticeShownAt, NoticeVisibleSeconds, NoticeHoldSeconds, NoticePopSeconds, NoticePopScale);
    }

    void RefreshScore(HudState state)
    {
        scoreText.text = TextFormat.Points(state.Score);
        streakText.text = "RACHA " + state.Streak;
        multiplierText.text = "X" + state.Multiplier;
        multiplierBox.color = Palette.ForMultiplier(state.Multiplier);

        bool blinkOn = Time.unscaledTime * StarBlinkRate % 2f < 1f;
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
            countdownText.text = "";
        }
    }

    // Aparece grande, vuelve a su tamano y se desvanece.
    static void AnimatePop(TMP_Text label, float elapsed, float visible, float hold, float pop, float popScale)
    {
        if (elapsed >= visible)
        {
            label.alpha = 0f;
            return;
        }
        label.rectTransform.localScale = Vector3.one * (elapsed < pop ? Mathf.Lerp(popScale, 1f, elapsed / pop) : 1f);
        label.alpha = elapsed < hold ? 1f : 1f - (elapsed - hold) / (visible - hold);
    }

    void BuildScoreCard(UiFactory ui)
    {
        RectTransform card = ui.Panel(root, "Puntaje", Layout.At(Layout.TopLeft, new Vector2(28f, -28f), new Vector2(460f, 196f)),
                                      Palette.PanelBackground, Palette.PanelBorder);
        ui.Label(card, "Titulo", 22f, Palette.SecondaryText, TextAlignmentOptions.TopLeft,
                 Layout.TopRow(Layout.TopLeft, new Vector2(26f, -16f), -40f, 30f)).text = "PUNTOS";
        scoreText = ui.Label(card, "Puntos", 64f, Color.white, TextAlignmentOptions.TopLeft,
                             Layout.TopRow(Layout.TopLeft, new Vector2(24f, -44f), -40f, 72f));
        streakText = ui.Label(card, "Racha", 28f, Color.white, TextAlignmentOptions.TopLeft,
                              Layout.At(Layout.TopLeft, new Vector2(26f, -122f), new Vector2(260f, 36f)));

        for (int i = 0; i < ArduinoInput.LaneCount; i++)
        {
            Image led = ui.Box(card, "Led" + i, Palette.LedOff);
            Layout.At(Layout.TopLeft, new Vector2(26f + i * 62f, -162f), new Vector2(54f, 16f)).ApplyTo(led.rectTransform);
            leds.Add(led);
        }

        multiplierBox = ui.Box(card, "Multiplicador", Palette.Accent);
        Layout.At(Layout.BottomRight, new Vector2(-20f, 20f), new Vector2(120f, 84f)).ApplyTo(multiplierBox.rectTransform);
        multiplierText = ui.Label(multiplierBox.rectTransform, "Valor", 50f, Color.black, TextAlignmentOptions.Center);
    }

    void BuildSongCard(UiFactory ui)
    {
        RectTransform card = ui.Panel(root, "Cancion", Layout.At(Layout.TopRight, new Vector2(-28f, -28f), new Vector2(620f, 112f)),
                                      Palette.PanelBackground, Palette.PanelBorder);
        titleText = ui.Label(card, "Titulo", 32f, Color.white, TextAlignmentOptions.Right,
                             Layout.TopRow(Layout.TopRight, new Vector2(-24f, -14f), -48f, 44f));
        difficultyText = ui.Label(card, "Dificultad", 26f, Palette.Medium, TextAlignmentOptions.Right,
                                  Layout.TopRow(Layout.TopRight, new Vector2(-24f, -62f), -48f, 34f));
    }

    void BuildProgressBar(UiFactory ui)
    {
        Image track = ui.Box(root, "Progreso", Palette.ProgressTrack, false);
        Layout.BottomRow(Vector2.zero, 0f, 10f).ApplyTo(track.rectTransform);

        progressFill = ui.Box(track.rectTransform, "Relleno", Palette.Accent, false).rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
    }

    void BuildCenterTexts(UiFactory ui)
    {
        judgementText = ui.Label(root, "Juicio", 92f, Color.white, TextAlignmentOptions.Center,
                                 Layout.Centered(new Vector2(0f, 230f), new Vector2(1000f, 120f)));
        UiFactory.Outline(judgementText, 0.25f);
        noticeText = ui.Label(root, "Aviso", 48f, Palette.Accent, TextAlignmentOptions.Center,
                              Layout.Centered(new Vector2(0f, 140f), new Vector2(1100f, 70f)));
        UiFactory.Outline(noticeText, 0.25f);
        countdownText = ui.Label(root, "Cuenta", 230f, Color.white, TextAlignmentOptions.Center,
                                 Layout.Centered(new Vector2(0f, 80f), new Vector2(800f, 300f)));
        UiFactory.Outline(countdownText, 0.3f);
    }
}

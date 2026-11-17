using TMPro;
using UnityEngine;

public class MessageView
{
    const string RecordingHelp = "B1 + B4 (2 S) O ESC PARA TERMINAR Y GUARDAR";

    readonly RectTransform messagePanel;
    readonly TextMeshProUGUI messageText;
    readonly RectTransform recordingBanner;
    readonly TextMeshProUGUI recordingText;
    float messageHiddenAt;

    public MessageView(UiFactory ui, Transform canvas)
    {
        messagePanel = ui.Panel(canvas, "Mensaje", Layout.At(Layout.BottomCenter, new Vector2(0f, 170f), new Vector2(1240f, 110f)),
                                Palette.MessageBackground, Palette.Accent);
        messageText = ui.Label(messagePanel, "Texto", 28f, Color.white, TextAlignmentOptions.Center);
        messageText.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Stretch(messageText.rectTransform, 24f, 8f);
        messagePanel.gameObject.SetActive(false);

        var banner = ui.Box(canvas, "Grabando", Palette.RecordingBanner, false);
        recordingBanner = banner.rectTransform;
        Layout.TopRow(Layout.TopCenter, Vector2.zero, 0f, 120f).ApplyTo(recordingBanner);
        recordingText = ui.Label(recordingBanner, "Texto", 34f, Color.white, TextAlignmentOptions.Center);
        UiFactory.Stretch(recordingText.rectTransform, 20f, 0f);
        recordingBanner.gameObject.SetActive(false);
    }

    public void ShowMessage(string text, float seconds, float now)
    {
        messageText.text = TextFormat.ForPixelFont(text);
        messageHiddenAt = now + seconds;
        messagePanel.gameObject.SetActive(true);
    }

    public void ShowRecording(int noteCount, double songTime)
    {
        recordingBanner.gameObject.SetActive(true);
        recordingText.text = songTime < 0.0
            ? $"GRABANDO CHART EN {Mathf.CeilToInt((float)-songTime)}..."
            : $"GRABANDO CHART  -  {noteCount} NOTAS   |   {RecordingHelp}";
    }

    public void HideRecording() => recordingBanner.gameObject.SetActive(false);

    public void Animate(float now)
    {
        if (messagePanel.gameObject.activeSelf && now > messageHiddenAt) messagePanel.gameObject.SetActive(false);
    }
}

using TMPro;
using UnityEngine;

public class MessageView
{
    const string RecordingHelp = "B1 + B4 (2 S) O ESC PARA TERMINAR Y GUARDAR";
    const string CountdownFormat = "GRABANDO CHART EN {0}...";
    const string RecordingFormat = "GRABANDO CHART  -  {0} NOTAS   |   {1}";
    const float MessageFontSize = 28f;
    const float RecordingFontSize = 34f;
    const float MessagePaddingX = 24f;
    const float MessagePaddingY = 8f;
    const float RecordingPaddingX = 20f;
    const float RecordingBannerHeight = 120f;

    static readonly Layout MessageLayout = Layout.At(Layout.BottomCenter, new Vector2(0f, 170f), new Vector2(1240f, 110f));

    readonly RectTransform messagePanel;
    readonly TextMeshProUGUI messageText;
    readonly RectTransform recordingBanner;
    readonly TextMeshProUGUI recordingText;
    float messageHiddenAt;

    public MessageView(UiFactory ui, Transform canvas)
    {
        messagePanel = ui.Panel(canvas, "Message", MessageLayout, Palette.MessageBackground, Palette.Accent);
        messageText = ui.Label(messagePanel, "Text", MessageFontSize, Color.white, TextAlignmentOptions.Center);
        messageText.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Stretch(messageText.rectTransform, MessagePaddingX, MessagePaddingY);
        messagePanel.gameObject.SetActive(false);

        recordingBanner = ui.Box(canvas, "RecordingBanner", Palette.RecordingBanner, false).rectTransform;
        Layout.TopRow(Layout.TopCenter, Vector2.zero, 0f, RecordingBannerHeight).ApplyTo(recordingBanner);
        recordingText = ui.Label(recordingBanner, "Text", RecordingFontSize, Color.white, TextAlignmentOptions.Center);
        UiFactory.Stretch(recordingText.rectTransform, RecordingPaddingX, 0f);
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
            ? string.Format(CountdownFormat, Mathf.CeilToInt((float)-songTime))
            : string.Format(RecordingFormat, noteCount, RecordingHelp);
    }

    public void HideRecording() => recordingBanner.gameObject.SetActive(false);

    public void Animate(float now)
    {
        if (messagePanel.gameObject.activeSelf && now > messageHiddenAt) messagePanel.gameObject.SetActive(false);
    }
}

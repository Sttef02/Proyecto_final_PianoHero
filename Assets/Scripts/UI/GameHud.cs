using TMPro;
using UnityEngine;

public class GameHud : MonoBehaviour
{
    const string CanvasName = "PianoHeroHUD";

    public PlayView Play { get; private set; }

    ResultsView results;
    MessageView messages;

    public void Build(TMP_FontAsset font, Color[] laneColors)
    {
        var factory = new UiFactory(font);
        RectTransform canvas = factory.CreateCanvas(transform, CanvasName);
        Play = new PlayView(factory, canvas, laneColors);
        results = new ResultsView(factory, canvas);
        messages = new MessageView(factory, canvas);
        HideAll();
    }

    public void ShowPlay()
    {
        results.SetVisible(false);
        messages.HideRecording();
        Play.ResetAnimations();
        Play.SetVisible(true);
    }

    public void ShowResults(ResultsData data)
    {
        Play.SetVisible(false);
        results.Show(data, Time.unscaledTime);
    }

    public void ShowRecording(int noteCount, double songTime) => messages.ShowRecording(noteCount, songTime);

    public void ShowMessage(string text, float seconds) => messages.ShowMessage(text, seconds, Time.unscaledTime);

    public void HideAll()
    {
        Play.SetVisible(false);
        results.SetVisible(false);
        messages.HideRecording();
    }

    void Update()
    {
        if (Play == null) return;
        float now = Time.unscaledTime;
        Play.Animate(now);
        results.Animate(now);
        messages.Animate(now);
    }
}

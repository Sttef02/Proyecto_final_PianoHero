using UnityEngine;

public static class Palette
{
    public static readonly Color Accent = new Color(1f, 0.6f, 0.1f);
    public static readonly Color PanelBackground = new Color(0.04f, 0.04f, 0.08f, 0.88f);
    public static readonly Color PanelBorder = new Color(0.22f, 0.22f, 0.32f, 1f);
    public static readonly Color SecondaryText = new Color(0.66f, 0.66f, 0.78f, 1f);
    public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.72f);
    public static readonly Color MessageBackground = new Color(0.05f, 0.05f, 0.09f, 0.95f);
    public static readonly Color RecordingBanner = new Color(0.55f, 0.05f, 0.08f, 0.88f);
    public static readonly Color ProgressTrack = new Color(1f, 1f, 1f, 0.12f);
    public static readonly Color LedOff = new Color(1f, 1f, 1f, 0.15f);
    public static readonly Color KeyIconIdle = new Color(0.55f, 0.55f, 0.55f, 1f);

    public static readonly Color Easy = new Color(0.24f, 0.86f, 0.52f);
    public static readonly Color Medium = new Color(1f, 0.76f, 0.2f);
    public static readonly Color Expert = new Color(1f, 0.3f, 0.3f);
    public static readonly Color NeutralMultiplier = new Color(0.8f, 0.8f, 0.85f);
    public static readonly Color GradeC = new Color(0.6f, 0.7f, 1f);

    public static readonly Color Perfect = new Color(1f, 0.9f, 0.2f);
    public static readonly Color Good = new Color(0.4f, 0.9f, 1f);
    public static readonly Color Miss = new Color(1f, 0.3f, 0.3f);

    public static Color ForDifficulty(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.Easy: return Easy;
            case Difficulty.Expert: return Expert;
            default: return Medium;
        }
    }

    public static Color ForMultiplier(int multiplier)
    {
        if (multiplier >= ScoreTracker.MaxMultiplier) return Expert;
        if (multiplier == 3) return Medium;
        if (multiplier == 2) return Easy;
        return NeutralMultiplier;
    }

    public static Color ForResult(HitResult result)
    {
        switch (result)
        {
            case HitResult.Perfect: return Perfect;
            case HitResult.Good: return Good;
            default: return Miss;
        }
    }
}

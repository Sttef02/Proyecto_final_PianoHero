using System.Globalization;
using System.Text;

public static class TextFormat
{
    const char InvertedExclamation = '\u00A1';
    const char InvertedQuestion = '\u00BF';

    // La fuente pixel (BoldPixels) no trae tildes ni signos de apertura.
    public static string ForPixelFont(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c == InvertedExclamation || c == InvertedQuestion) continue;
            builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    public static string DifficultyLabel(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.Easy: return "FACIL";
            case Difficulty.Expert: return "EXPERTO";
            default: return "MEDIA";
        }
    }

    public static string ResultLabel(HitResult result)
    {
        switch (result)
        {
            case HitResult.Perfect: return "PERFECTO!";
            case HitResult.Good: return "BIEN";
            default: return "FALLO";
        }
    }

    public static string Points(float points) => ((int)System.Math.Round(points)).ToString("N0");
}

using System;
using System.Globalization;
using System.Text;

public static class TextFormat
{
    const char InvertedExclamation = '\u00A1';
    const char InvertedQuestion = '\u00BF';
    const string ThousandsFormat = "N0";

    // La fuente pixel (BoldPixels) no trae tildes ni signos de apertura.
    public static string ForPixelFont(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var builder = new StringBuilder(text.Length);
        foreach (char character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (character == InvertedExclamation || character == InvertedQuestion) continue;
            builder.Append(character);
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

    public static string Points(float points) => ((int)Math.Round(points)).ToString(ThousandsFormat);
}

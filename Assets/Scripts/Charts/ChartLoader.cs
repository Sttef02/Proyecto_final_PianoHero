using System;
using System.IO;
using UnityEngine;

public static class ChartLoader
{
    const char ByteOrderMark = '\uFEFF';
    const string JsonStart = "{";
    const string ChartExtension = ".json";
    const string ProjectFolder = "PianoHero";
    const string ChartsFolder = "Charts";
    const char InvalidCharReplacement = '_';

    public static ChartData Load(TextAsset file, string chartDifficulty)
    {
        if (file == null || string.IsNullOrWhiteSpace(file.text)) return null;

        string text = file.text.TrimStart(ByteOrderMark, ' ', '\r', '\n', '\t');
        try
        {
            ChartData chart = text.StartsWith(JsonStart)
                ? JsonUtility.FromJson<ChartData>(text)
                : MoonscraperParser.Parse(text, chartDifficulty);
            if (chart?.notes != null) chart.SortNotes();
            return chart;
        }
        catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
        {
            Debug.LogError($"El chart {file.name} no se pudo leer: {e.Message}");
            return null;
        }
    }

    // En el editor se guarda dentro del proyecto para poder asignarlo a una cancion. Devuelve null si falla.
    public static string Save(ChartData chart, string fileName)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(invalid, InvalidCharReplacement);

        try
        {
            string folder = SaveFolder();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName + ChartExtension);
            File.WriteAllText(path, JsonUtility.ToJson(chart, true));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            return path;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError("No se pudo guardar el chart: " + e.Message);
            return null;
        }
    }

    static string SaveFolder()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, ProjectFolder, ChartsFolder);
#else
        return Path.Combine(Application.persistentDataPath, ChartsFolder);
#endif
    }
}

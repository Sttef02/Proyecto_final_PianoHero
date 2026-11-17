using System.IO;
using UnityEngine;

public static class ChartLoader
{
    const char ByteOrderMark = '\uFEFF';
    const string JsonStart = "{";
    const string ChartExtension = ".json";
    const string ProjectFolder = "PianoHero";
    const string ChartsFolder = "Charts";

    public static ChartData Load(TextAsset file, string chartDifficulty)
    {
        if (file == null) return null;

        string text = file.text.TrimStart(ByteOrderMark, ' ', '\r', '\n', '\t');
        ChartData chart = text.StartsWith(JsonStart)
            ? JsonUtility.FromJson<ChartData>(text)
            : MoonscraperParser.Parse(text, chartDifficulty);
        if (chart?.notes != null) chart.SortNotes();
        return chart;
    }

    // En el editor se guarda dentro del proyecto para poder asignarlo a una cancion.
    public static string Save(ChartData chart, string fileName)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(invalid, '_');

        string folder = SaveFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName + ChartExtension);
        File.WriteAllText(path, JsonUtility.ToJson(chart, true));
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
        return path;
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

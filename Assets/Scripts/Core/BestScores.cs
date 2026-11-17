using UnityEngine;

public static class BestScores
{
    const string KeyPrefix = "PianoHero.Record.";

    public static bool TrySave(SongData song, float score)
    {
        string key = KeyPrefix + song.name;
        if (score <= 0f || score <= PlayerPrefs.GetFloat(key, 0f)) return false;

        PlayerPrefs.SetFloat(key, score);
        PlayerPrefs.Save();
        return true;
    }
}

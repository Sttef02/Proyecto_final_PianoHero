using System;
using System.Collections.Generic;

[Serializable]
public class ChartNote
{
    public float time;
    public int lane;
    public float duration;
}

[Serializable]
public class ChartData
{
    public const float DefaultBpm = 120f;

    public string title;
    public string artist;
    public float bpm = DefaultBpm;
    public float offset;
    public List<ChartNote> notes = new List<ChartNote>();

    public void SortNotes() => notes.Sort((a, b) => a.time.CompareTo(b.time));
}

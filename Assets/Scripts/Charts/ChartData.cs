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
    public const double SecondsPerMinute = 60.0;

    public string title;
    public string artist;
    public float bpm = DefaultBpm;
    public float offset;
    public List<ChartNote> notes = new List<ChartNote>();

    public void SortNotes() => notes.Sort((first, second) => first.time.CompareTo(second.time));
}

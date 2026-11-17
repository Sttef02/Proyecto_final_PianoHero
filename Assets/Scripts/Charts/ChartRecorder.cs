using System.Collections.Generic;

public class ChartRecorder
{
    const float SustainThresholdSeconds = 0.25f;
    const double NotPressed = -1.0;

    readonly List<ChartNote> notes = new List<ChartNote>();
    readonly double[] pressStart = new double[ArduinoInput.LaneCount];

    public int NoteCount => notes.Count;

    public ChartRecorder(IEnumerable<ChartNote> existingNotes)
    {
        for (int lane = 0; lane < pressStart.Length; lane++) pressStart[lane] = NotPressed;
        if (existingNotes == null) return;
        foreach (ChartNote note in existingNotes)
            notes.Add(new ChartNote { time = note.time, lane = note.lane, duration = note.duration });
    }

    public void Press(int lane, double songTime)
    {
        if (ArduinoInput.IsValidLane(lane) && songTime >= 0.0) pressStart[lane] = songTime;
    }

    public void Release(int lane, double songTime)
    {
        if (!ArduinoInput.IsValidLane(lane) || pressStart[lane] == NotPressed) return;

        double held = songTime - pressStart[lane];
        notes.Add(new ChartNote
        {
            time = (float)pressStart[lane],
            lane = lane,
            duration = held >= SustainThresholdSeconds ? (float)held : 0f
        });
        pressStart[lane] = NotPressed;
    }

    public ChartData Finish(SongData song, double songTime)
    {
        for (int lane = 0; lane < pressStart.Length; lane++) Release(lane, songTime);

        var chart = new ChartData
        {
            title = song.title,
            artist = song.artist,
            bpm = song.bpm,
            notes = new List<ChartNote>(notes)
        };
        ChartQuantizer.Quantize(chart, song.bpm, song.firstBeatTime, song.subdivision);
        return chart;
    }
}

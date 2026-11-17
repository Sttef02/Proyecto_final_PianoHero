using System;
using System.Collections.Generic;

public static class ChartQuantizer
{
    const double SecondsPerMinute = 60.0;

    // Ajusta cada nota a la grilla del BPM para corregir pequenos errores al grabar.
    public static void Quantize(ChartData chart, float bpm, float firstBeatTime, int subdivision)
    {
        if (bpm <= 0f || subdivision <= 0) return;

        double step = SecondsPerMinute / bpm / subdivision;
        var occupied = new HashSet<(long, int)>();
        var quantized = new List<ChartNote>();
        foreach (ChartNote note in chart.notes)
        {
            long slot = (long)Math.Round((note.time - firstBeatTime) / step);
            if (!occupied.Add((slot, note.lane))) continue;

            quantized.Add(new ChartNote
            {
                time = (float)(firstBeatTime + slot * step),
                lane = note.lane,
                duration = note.duration > 0f ? (float)(Math.Max(1, Math.Round(note.duration / step)) * step) : 0f
            });
        }
        chart.notes = quantized;
        chart.SortNotes();
    }
}

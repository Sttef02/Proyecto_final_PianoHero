using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

// Lee charts de Moonscraper / Clone Hero (.chart) y los pasa a 4 carriles.
public static class MoonscraperParser
{
    const int DefaultResolution = 192;
    const double TempoScale = 1000.0;
    const double SecondsPerMinute = 60.0;
    const float MinSustainSeconds = 0.15f;
    const int MaxPlayableFret = 4;
    const string SongSection = "Song";
    const string TempoSection = "SyncTrack";
    const string TrackSuffix = "Single";
    const string ResolutionKey = "Resolution";
    const string NameKey = "Name";
    const string ArtistKey = "Artist";
    static readonly string[] DifficultyFallbacks = { "Expert", "Hard", "Medium", "Easy" };
    static readonly Regex TempoLine = new Regex(@"^\s*(\d+)\s*=\s*B\s+(\d+)");
    static readonly Regex NoteLine = new Regex(@"^\s*(\d+)\s*=\s*N\s+(\d+)\s+(\d+)");

    struct TempoChange
    {
        public long Tick;
        public double Bpm;
    }

    public static ChartData Parse(string text, string difficulty)
    {
        Dictionary<string, List<string>> sections = ReadSections(text);
        var chart = new ChartData();
        int resolution = ReadSongInfo(sections, chart);
        List<TempoChange> tempo = ReadTempoMap(sections);
        chart.bpm = (float)tempo[0].Bpm;

        List<string> track = FindTrack(sections, difficulty);
        if (track == null)
        {
            Debug.LogWarning("El .chart no tiene ninguna pista de guitarra (ExpertSingle, HardSingle...).");
            return chart;
        }
        ReadNotes(track, tempo, resolution, chart);
        return chart;
    }

    static Dictionary<string, List<string>> ReadSections(string text)
    {
        var sections = new Dictionary<string, List<string>>();
        List<string> current = null;
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                current = new List<string>();
                sections[line.Substring(1, line.Length - 2)] = current;
            }
            else if (current != null && line.Length > 0 && line != "{" && line != "}")
            {
                current.Add(line);
            }
        }
        return sections;
    }

    static int ReadSongInfo(Dictionary<string, List<string>> sections, ChartData chart)
    {
        int resolution = DefaultResolution;
        if (!sections.TryGetValue(SongSection, out List<string> lines)) return resolution;

        foreach (string line in lines)
        {
            string[] pair = line.Split(new[] { '=' }, 2);
            if (pair.Length < 2) continue;
            string key = pair[0].Trim();
            string value = pair[1].Trim().Trim('"');
            if (key == ResolutionKey && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0)
                resolution = parsed;
            else if (key == NameKey) chart.title = value;
            else if (key == ArtistKey) chart.artist = value;
        }
        return resolution;
    }

    static List<TempoChange> ReadTempoMap(Dictionary<string, List<string>> sections)
    {
        var tempo = new List<TempoChange>();
        if (sections.TryGetValue(TempoSection, out List<string> lines))
        {
            foreach (string line in lines)
            {
                Match match = TempoLine.Match(line);
                if (!match.Success) continue;
                tempo.Add(new TempoChange
                {
                    Tick = ParseLong(match.Groups[1].Value),
                    Bpm = ParseLong(match.Groups[2].Value) / TempoScale
                });
            }
        }
        tempo.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        if (tempo.Count == 0 || tempo[0].Tick != 0) tempo.Insert(0, new TempoChange { Tick = 0, Bpm = ChartData.DefaultBpm });
        return tempo;
    }

    static List<string> FindTrack(Dictionary<string, List<string>> sections, string difficulty)
    {
        if (!string.IsNullOrEmpty(difficulty) && sections.TryGetValue(difficulty + TrackSuffix, out List<string> requested))
            return requested;
        foreach (string fallback in DifficultyFallbacks)
            if (sections.TryGetValue(fallback + TrackSuffix, out List<string> track)) return track;
        return null;
    }

    static void ReadNotes(List<string> track, List<TempoChange> tempo, int resolution, ChartData chart)
    {
        var seen = new HashSet<(long, int)>();
        foreach (string line in track)
        {
            Match match = NoteLine.Match(line);
            if (!match.Success) continue;

            long tick = ParseLong(match.Groups[1].Value);
            int fret = (int)ParseLong(match.Groups[2].Value);
            long sustainTicks = ParseLong(match.Groups[3].Value);
            // 5 = forzada, 6 = tap, 7 = abierta: no son notas jugables.
            if (fret > MaxPlayableFret) continue;

            int lane = Mathf.Min(fret, ArduinoInput.LaneCount - 1);
            if (!seen.Add((tick, lane))) continue;

            float time = (float)TicksToSeconds(tick, tempo, resolution);
            float duration = sustainTicks > 0 ? (float)(TicksToSeconds(tick + sustainTicks, tempo, resolution) - time) : 0f;
            chart.notes.Add(new ChartNote { time = time, lane = lane, duration = duration < MinSustainSeconds ? 0f : duration });
        }
    }

    static double TicksToSeconds(long tick, List<TempoChange> tempo, int resolution)
    {
        double seconds = 0.0;
        for (int i = 0; i < tempo.Count && tick > tempo[i].Tick; i++)
        {
            long segmentEnd = i + 1 < tempo.Count ? Math.Min(tick, tempo[i + 1].Tick) : tick;
            seconds += (segmentEnd - tempo[i].Tick) / (double)resolution * SecondsPerMinute / tempo[i].Bpm;
        }
        return seconds;
    }

    static long ParseLong(string value) => long.Parse(value, CultureInfo.InvariantCulture);
}

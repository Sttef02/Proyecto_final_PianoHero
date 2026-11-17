using System.Diagnostics;

public static class GameClock
{
    static readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public static double Now => stopwatch.Elapsed.TotalSeconds;
}

using System;
using UnityEngine;

[Serializable]
public class ScoreTracker
{
    public const int MaxMultiplier = 4;
    const int PerfectPoints = 100;
    const int GoodPoints = 50;
    const float SustainPointsPerSecond = 50f;

    [Tooltip("Notas seguidas para encender cada LED y subir el multiplicador.")]
    [Min(1)]
    public int notesPerLed = 10;
    [Tooltip("Racha a partir de la cual los 4 LEDs parpadean.")]
    [Min(1)]
    public int starStreak = 50;

    public event Action<int> MultiplierIncreased;
    public event Action<int> StarStreakReached;

    public float Score { get; private set; }
    public int Streak { get; private set; }
    public int BestStreak { get; private set; }
    public int Perfects { get; private set; }
    public int Goods { get; private set; }
    public int Misses { get; private set; }

    public int Multiplier => 1 + Mathf.Min(MaxMultiplier - 1, Streak / NotesPerStep);
    public int LitLeds => Mathf.Min(ArduinoInput.LedCount, Streak / NotesPerStep);
    public bool IsStarMode => Streak >= starStreak;
    public int LedCommand => IsStarMode ? ArduinoInput.StarModeLedCommand : LitLeds;

    int NotesPerStep => Mathf.Max(1, notesPerLed);

    int lastMultiplier = 1;

    public void Reset()
    {
        Score = 0f;
        Streak = BestStreak = Perfects = Goods = Misses = 0;
        lastMultiplier = 1;
    }

    public void Register(HitResult result)
    {
        switch (result)
        {
            case HitResult.Perfect:
                Perfects++;
                AddHit(PerfectPoints);
                break;
            case HitResult.Good:
                Goods++;
                AddHit(GoodPoints);
                break;
            case HitResult.Miss:
                Misses++;
                BreakStreak();
                break;
        }
    }

    public void AddSustain(float seconds) => Score += seconds * SustainPointsPerSecond * Multiplier;

    public void BreakStreak()
    {
        Streak = 0;
        lastMultiplier = 1;
    }

    void AddHit(int points)
    {
        Streak++;
        BestStreak = Mathf.Max(BestStreak, Streak);
        Score += points * Multiplier;

        if (Multiplier > lastMultiplier) MultiplierIncreased?.Invoke(Multiplier);
        lastMultiplier = Multiplier;
        if (Streak == starStreak) StarStreakReached?.Invoke(Streak);
    }
}

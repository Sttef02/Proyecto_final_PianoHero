using UnityEngine;

public class HighwayNote
{
    public ChartNote Data;
    public Transform Head;
    public Transform Tail;
    public Renderer HeadRenderer;
    public Renderer TailRenderer;
    public bool WasHit;
    public bool WasMissed;
    public bool IsHeld;

    public int Lane => Data.lane;
    public float EndTime => Data.time + Data.duration;
    public bool IsSustain => Data.duration > 0f;
}

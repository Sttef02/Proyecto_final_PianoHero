using System;
using System.Collections.Generic;
using UnityEngine;

public class Highway : MonoBehaviour
{
    const double EarlyReleaseTolerance = 0.1;
    const float HitFlashSeconds = 0.12f;
    const float HoldFlashSeconds = 0.05f;
    const float MissedNoteDespawnZ = -4f;
    const float NoteHeadHeight = 0.08f;
    const float NoteTailHeight = 0.04f;
    const float NoteTailThickness = 0.04f;
    const float NoteTailWidthFactor = 0.22f;
    const float MinTailLength = 0.01f;
    const float ButtonFlashScale = 0.95f;
    const float ButtonPressedBrightness = 0.9f;
    const float ButtonFlashBrightness = 1.4f;
    const string TrackObjectName = "Track";
    static readonly Color MissedNoteColor = new Color(0.35f, 0.35f, 0.38f);

    [Header("Forma")]
    public float laneWidth = 1.3f;
    public float length = 45f;
    [Tooltip("Unidades por segundo a las que bajan las notas.")]
    public float noteSpeed = 14f;
    public bool adjustCamera = true;

    [Header("Ventanas de acierto (segundos, hacia cada lado)")]
    public float perfectWindow = 0.05f;
    public float goodWindow = 0.10f;

    [Header("Colores de los carriles")]
    public Color[] laneColors =
    {
        new Color(0.15f, 0.85f, 0.25f),
        new Color(0.95f, 0.2f, 0.2f),
        new Color(1f, 0.85f, 0.1f),
        new Color(0.2f, 0.45f, 1f)
    };

    public event Action<HitResult, int> NoteJudged;
    public event Action<float> SustainHeld;

    public float LastNoteTime { get; private set; }
    public bool IsFinished => nextNote >= notes.Count && activeNotes.Count == 0;
    public bool IsHoldingSustain => activeNotes.Exists(note => note.IsHeld);

    readonly List<HighwayNote> notes = new List<HighwayNote>();
    readonly List<HighwayNote> activeNotes = new List<HighwayNote>();
    readonly float[] flashTimers = new float[ArduinoInput.LaneCount];
    HighwayBuilder builder;
    Renderer[] laneButtons;
    int nextNote;
    double lastSongTime;

    void Awake()
    {
        var root = new GameObject(TrackObjectName).transform;
        root.SetParent(transform, false);
        builder = new HighwayBuilder(root, laneWidth);
        builder.BuildTrack(length);
        laneButtons = builder.BuildLaneButtons(laneColors);
        if (adjustCamera) HighwayBuilder.SetupCamera();
    }

    public void SetLaneButtonsVisible(bool visible)
    {
        foreach (Renderer button in laneButtons)
            if (button != null && button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
    }

    public void Load(ChartData chart, float extraOffset)
    {
        Clear();
        if (chart?.notes == null) return;

        foreach (ChartNote source in chart.notes)
        {
            var data = new ChartNote
            {
                time = source.time + chart.offset + extraOffset,
                lane = Mathf.Clamp(source.lane, 0, ArduinoInput.LaneCount - 1),
                duration = source.duration
            };
            notes.Add(new HighwayNote { Data = data });
            LastNoteTime = Mathf.Max(LastNoteTime, data.time + data.duration);
        }
        notes.Sort((first, second) => first.Data.time.CompareTo(second.Data.time));
    }

    public void Clear()
    {
        foreach (HighwayNote note in activeNotes) DestroyNote(note);
        activeNotes.Clear();
        notes.Clear();
        nextNote = 0;
        LastNoteTime = 0f;
    }

    public void Tick(double songTime, bool[] pressed)
    {
        SpawnUpcomingNotes(songTime);
        float elapsed = (float)Math.Max(0.0, songTime - lastSongTime);
        lastSongTime = songTime;

        for (int i = activeNotes.Count - 1; i >= 0; i--)
            if (!UpdateNote(activeNotes[i], songTime, elapsed)) activeNotes.RemoveAt(i);

        UpdateLaneButtons(pressed);
    }

    public bool TryHit(int lane, double songTime)
    {
        HighwayNote target = FindClosestNote(lane, songTime, out double distance);
        if (target == null) return false;

        target.WasHit = true;
        flashTimers[lane] = HitFlashSeconds;
        NoteJudged?.Invoke(distance <= perfectWindow ? HitResult.Perfect : HitResult.Good, lane);

        if (target.IsSustain)
        {
            target.IsHeld = true;
            return true;
        }
        DestroyNote(target);
        activeNotes.Remove(target);
        return true;
    }

    public void Release(int lane, double songTime)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            HighwayNote note = activeNotes[i];
            if (note.Lane != lane || !note.IsHeld) continue;

            note.IsHeld = false;
            if (songTime < note.EndTime - EarlyReleaseTolerance)
            {
                // Soltar antes de tiempo deja de sumar puntos, pero no rompe la racha.
                note.WasMissed = true;
                Tint(note.TailRenderer, MissedNoteColor);
                DestroyHead(note);
                continue;
            }
            DestroyNote(note);
            activeNotes.RemoveAt(i);
        }
    }

    void SpawnUpcomingNotes(double songTime)
    {
        float leadTime = length / noteSpeed;
        while (nextNote < notes.Count && notes[nextNote].Data.time - songTime < leadTime)
        {
            HighwayNote note = notes[nextNote++];
            CreateNoteObjects(note);
            activeNotes.Add(note);
        }
    }

    void CreateNoteObjects(HighwayNote note)
    {
        Color color = laneColors[note.Lane];
        GameObject head = builder.CreateNoteHead(color);
        note.Head = head.transform;
        note.HeadRenderer = head.GetComponent<Renderer>();
        if (!note.IsSustain) return;

        GameObject tail = builder.CreateNoteTail(color);
        note.Tail = tail.transform;
        note.TailRenderer = tail.GetComponent<Renderer>();
    }

    // Devuelve false cuando la nota ya termino y debe salir de la lista.
    bool UpdateNote(HighwayNote note, double songTime, float elapsed)
    {
        if (!note.WasHit && !note.WasMissed && songTime - note.Data.time > goodWindow) MarkMissed(note);

        if (note.IsHeld)
        {
            if (songTime >= note.EndTime)
            {
                note.IsHeld = false;
                DestroyNote(note);
                return false;
            }
            SustainHeld?.Invoke(elapsed);
            flashTimers[note.Lane] = HoldFlashSeconds;
        }

        float headZ = note.IsHeld ? 0f : (float)(note.Data.time - songTime) * noteSpeed;
        float endZ = (float)(note.EndTime - songTime) * noteSpeed;
        PlaceNote(note, headZ, endZ);

        if (!note.WasMissed || endZ >= MissedNoteDespawnZ) return true;
        DestroyNote(note);
        return false;
    }

    void MarkMissed(HighwayNote note)
    {
        note.WasMissed = true;
        Tint(note.HeadRenderer, MissedNoteColor);
        Tint(note.TailRenderer, MissedNoteColor);
        NoteJudged?.Invoke(HitResult.Miss, note.Lane);
    }

    void PlaceNote(HighwayNote note, float headZ, float endZ)
    {
        float laneX = builder.LaneX(note.Lane);
        if (note.Head != null) note.Head.localPosition = new Vector3(laneX, NoteHeadHeight, headZ);
        if (note.Tail == null) return;

        float tailLength = Mathf.Max(MinTailLength, endZ - headZ);
        note.Tail.localPosition = new Vector3(laneX, NoteTailHeight, headZ + tailLength / 2f);
        note.Tail.localScale = new Vector3(laneWidth * NoteTailWidthFactor, NoteTailThickness, tailLength);
    }

    HighwayNote FindClosestNote(int lane, double songTime, out double bestDistance)
    {
        HighwayNote best = null;
        bestDistance = double.MaxValue;
        foreach (HighwayNote note in activeNotes)
        {
            if (note.Lane != lane || note.WasHit || note.WasMissed) continue;
            double distance = Math.Abs(note.Data.time - songTime);
            if (distance > goodWindow || distance >= bestDistance) continue;
            best = note;
            bestDistance = distance;
        }
        return best;
    }

    void UpdateLaneButtons(bool[] pressed)
    {
        for (int lane = 0; lane < laneButtons.Length; lane++)
        {
            flashTimers[lane] = Mathf.Max(0f, flashTimers[lane] - Time.deltaTime);
            bool flashing = flashTimers[lane] > 0f;
            bool isPressed = pressed != null && pressed[lane];

            float brightness = ButtonBrightness(flashing, isPressed);
            laneButtons[lane].material.color = laneColors[lane] * brightness;

            float size = laneWidth * (flashing ? ButtonFlashScale : HighwayBuilder.ButtonSizeFactor);
            laneButtons[lane].transform.localScale = new Vector3(size, HighwayBuilder.ButtonThickness, size);
        }
    }

    static float ButtonBrightness(bool flashing, bool isPressed)
    {
        if (flashing) return ButtonFlashBrightness;
        return isPressed ? ButtonPressedBrightness : HighwayBuilder.IdleButtonBrightness;
    }

    static void Tint(Renderer renderer, Color color)
    {
        if (renderer != null) renderer.material.color = color;
    }

    static void DestroyHead(HighwayNote note)
    {
        if (note.Head != null) Destroy(note.Head.gameObject);
        note.Head = null;
    }

    static void DestroyNote(HighwayNote note)
    {
        DestroyHead(note);
        if (note.Tail != null) Destroy(note.Tail.gameObject);
        note.Tail = null;
    }
}

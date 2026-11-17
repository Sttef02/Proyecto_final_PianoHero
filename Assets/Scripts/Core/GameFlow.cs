using TMPro;
using UnityEngine;

// Controles: menu (perilla/flechas/mouse elige, B1 juega, B4 graba chart, B2/B3 latencia),
// juego (B1-B4 carriles; mantener B1 + B4 o Esc vuelve al menu), resultados (B1 o clic).
public class GameFlow : MonoBehaviour
{
    const float BaseNoteSpeed = 14f;
    const float MinSpeedMultiplier = 0.3f;
    const float SongEndMargin = 0.5f;
    const float AfterLastNoteMargin = 3f;
    const float ExitHoldSeconds = 2f;
    const float LatencyStepMs = 10f;
    const float MessageSeconds = 4f;
    const float SavedChartMessageSeconds = 10f;
    const int PlayLane = 0;
    const int LatencyDownLane = 1;
    const int LatencyUpLane = 2;
    const int RecordLane = 3;
    const string ChartFileSuffix = "_chart";
    const string HighwayObjectName = "Pista";

    [Header("Canciones")]
    public SongData[] songs;

    [Header("Juego")]
    [Tooltip("Si es true, presionar sin nota rompe la racha. Recomendado false con botones de protoboard.")]
    public bool penalizeEmptyHits;
    [Tooltip("Segundos de cuenta regresiva antes de que empiece la cancion.")]
    public float startDelay = 2.5f;
    public ScoreTracker score = new ScoreTracker();

    [Header("Grabacion de charts")]
    [Tooltip("true = conserva las notas del chart actual y agrega las nuevas (grabar por pasadas).")]
    public bool recordOverExisting = true;

    [Header("Referencias (se crean solas si estan vacias)")]
    public ArduinoInput controller;
    public Conductor conductor;
    public Highway highway;
    public GameHud hud;
    [Tooltip("Fuente del puntaje y resultados. Vacio = la misma de los textos del menu.")]
    public TMP_FontAsset hudFont;

    [Header("Menu")]
    public SongMenu menu = new SongMenu();

    public GameState State { get; private set; } = GameState.Menu;

    SongData currentSong;
    ChartRecorder recorder;
    float exitHoldTime;

    void Start()
    {
        CreateMissingComponents();
        menu.Initialize(songs);
        hud.Build(hudFont != null ? hudFont : menu.FindFont(), highway.laneColors);
        SubscribeToEvents();
        GoToMenu();
    }

    void OnDestroy()
    {
        if (controller == null) return;
        controller.ButtonPressed -= OnButtonPressed;
        controller.ButtonReleased -= OnButtonReleased;
    }

    void Update()
    {
        switch (State)
        {
            case GameState.Menu: UpdateMenu(); break;
            case GameState.Playing: UpdatePlaying(); break;
            case GameState.Recording: UpdateRecording(); break;
            case GameState.Results: UpdateResults(); break;
        }
        menu.SetVisible(State == GameState.Menu);
    }

    void CreateMissingComponents()
    {
        if (controller == null) controller = FindFirstObjectByType<ArduinoInput>();
        if (controller == null) controller = gameObject.AddComponent<ArduinoInput>();
        if (conductor == null) conductor = FindFirstObjectByType<Conductor>();
        if (conductor == null) conductor = gameObject.AddComponent<Conductor>();
        if (highway == null) highway = FindFirstObjectByType<Highway>();
        if (highway == null) highway = new GameObject(HighwayObjectName).AddComponent<Highway>();
        if (hud == null) hud = gameObject.AddComponent<GameHud>();
    }

    void SubscribeToEvents()
    {
        controller.ButtonPressed += OnButtonPressed;
        controller.ButtonReleased += OnButtonReleased;
        highway.NoteJudged += OnNoteJudged;
        highway.SustainHeld += OnSustainHeld;
        score.MultiplierIncreased += ShowMultiplierNotice;
        score.StarStreakReached += ShowStreakNotice;
        menu.SelectionChanged += PlayPreview;
    }

    // ---------------------------------------------------------------- Estados

    void UpdateMenu()
    {
        if (menu.HandleInput(controller, PointerInput.Read()) == MenuCommand.Play)
        {
            StartSong();
            return;
        }
        menu.Render(controller, conductor.latencyMs);
    }

    void UpdatePlaying()
    {
        double songTime = conductor.SongTime;
        highway.Tick(songTime, controller.Pressed);
        hud.Play.Refresh(CurrentHudState(songTime));

        if (SongHasEnded(songTime)) FinishSong();
        else if (WantsToExit()) GoToMenu();
    }

    void UpdateRecording()
    {
        double songTime = conductor.SongTime;
        highway.Tick(songTime, controller.Pressed);
        hud.ShowRecording(recorder.NoteCount, songTime);
        if (songTime > conductor.Duration + SongEndMargin || WantsToExit()) FinishRecording();
    }

    void UpdateResults()
    {
        if (PointerInput.Read().Clicked) GoToMenu();
    }

    void GoToMenu()
    {
        State = GameState.Menu;
        highway.SetLaneButtonsVisible(false);
        highway.Clear();
        hud.HideAll();
        controller.SetLeds(0);
        menu.ResetKnob();
        PlayPreview();
    }

    void PlayPreview()
    {
        SongData song = menu.SelectedSong;
        if (song != null) conductor.PlayPreview(song.audio, song.previewStart);
    }

    void StartSong()
    {
        if (!TryLoadSelectedChart(out ChartData chart)) return;

        highway.noteSpeed = BaseNoteSpeed * Mathf.Max(MinSpeedMultiplier, currentSong.speedMultiplier);
        highway.Load(chart, currentSong.extraOffset);
        score.Reset();
        BeginSong(GameState.Playing);
        hud.ShowPlay();
    }

    void StartRecording()
    {
        currentSong = menu.SelectedSong;
        if (!HasAudio(currentSong)) return;

        ChartData existing = recordOverExisting ? ChartLoader.Load(currentSong.chart, currentSong.chartDifficulty) : null;
        recorder = new ChartRecorder(existing?.notes);
        highway.Clear();
        if (existing != null) highway.Load(existing, 0f);
        hud.HideAll();
        BeginSong(GameState.Recording);
    }

    void BeginSong(GameState newState)
    {
        controller.SetLeds(0);
        conductor.Play(currentSong.audio, startDelay);
        highway.SetLaneButtonsVisible(true);
        State = newState;
    }

    bool TryLoadSelectedChart(out ChartData chart)
    {
        chart = null;
        currentSong = menu.SelectedSong;
        if (!HasAudio(currentSong)) return false;

        chart = ChartLoader.Load(currentSong.chart, currentSong.chartDifficulty);
        if (chart != null && chart.notes.Count > 0) return true;
        hud.ShowMessage("Esta cancion no tiene notas. Presiona B4 (K) para grabar el chart.", MessageSeconds);
        return false;
    }

    bool HasAudio(SongData song)
    {
        if (song != null && song.audio != null) return true;
        hud.ShowMessage("Esta cancion no tiene audio asignado.", MessageSeconds);
        return false;
    }

    void FinishSong()
    {
        conductor.Stop();
        controller.SetLeds(0);
        State = GameState.Results;
        hud.ShowResults(new ResultsData
        {
            Title = currentSong.title,
            Difficulty = currentSong.difficulty,
            Score = score.Score,
            Perfects = score.Perfects,
            Goods = score.Goods,
            Misses = score.Misses,
            BestStreak = score.BestStreak,
            IsNewRecord = BestScores.TrySave(currentSong, score.Score)
        });
    }

    void FinishRecording()
    {
        ChartData chart = recorder.Finish(currentSong, conductor.SongTime);
        conductor.Stop();
        string path = ChartLoader.Save(chart, currentSong.title + ChartFileSuffix);
        GoToMenu();
        hud.ShowMessage($"Chart guardado ({chart.notes.Count} notas): {path}\nArrastralo al campo Chart de la cancion.", SavedChartMessageSeconds);
    }

    bool SongHasEnded(double songTime) =>
        songTime > conductor.Duration + SongEndMargin ||
        (highway.IsFinished && songTime > highway.LastNoteTime + AfterLastNoteMargin);

    // Mantener B1 + B4 para salir. No cuenta mientras se sostiene una nota (hay sostenidas de B1 + B4 a la vez).
    bool WantsToExit()
    {
        if (controller.EscapePressed) return true;

        bool holding = controller.Pressed[PlayLane] && controller.Pressed[RecordLane] && !highway.IsHoldingSustain;
        exitHoldTime = holding ? exitHoldTime + Time.deltaTime : 0f;
        if (exitHoldTime <= ExitHoldSeconds) return false;

        exitHoldTime = 0f;
        return true;
    }

    // ---------------------------------------------------------------- Eventos

    void OnButtonPressed(int lane, double clockTime)
    {
        switch (State)
        {
            case GameState.Menu:
                HandleMenuButton(lane);
                break;
            case GameState.Playing:
                if (!highway.TryHit(lane, conductor.TimeAt(clockTime)) && penalizeEmptyHits) BreakStreak();
                break;
            case GameState.Recording:
                recorder.Press(lane, conductor.TimeAt(clockTime));
                break;
            case GameState.Results:
                if (lane == PlayLane) GoToMenu();
                break;
        }
    }

    void OnButtonReleased(int lane, double clockTime)
    {
        if (State == GameState.Playing) highway.Release(lane, conductor.TimeAt(clockTime));
        else if (State == GameState.Recording) recorder.Release(lane, conductor.TimeAt(clockTime));
    }

    void HandleMenuButton(int lane)
    {
        if (!menu.HasSongs) return;
        switch (lane)
        {
            case PlayLane: StartSong(); break;
            case RecordLane: StartRecording(); break;
            case LatencyDownLane: conductor.latencyMs -= LatencyStepMs; break;
            case LatencyUpLane: conductor.latencyMs += LatencyStepMs; break;
        }
    }

    void OnNoteJudged(HitResult result, int lane)
    {
        if (State != GameState.Playing) return;
        score.Register(result);
        hud.Play.ShowJudgement(result);
        controller.SetLeds(score.LedCommand);
    }

    void OnSustainHeld(float seconds)
    {
        if (State == GameState.Playing) score.AddSustain(seconds);
    }

    void BreakStreak()
    {
        score.BreakStreak();
        controller.SetLeds(score.LedCommand);
    }

    void ShowMultiplierNotice(int multiplier)
    {
        string text = multiplier >= ScoreTracker.MaxMultiplier
            ? $"MULTIPLICADOR X{multiplier} - AL MAXIMO!"
            : $"MULTIPLICADOR X{multiplier}!";
        hud.Play.ShowNotice(text, Palette.ForMultiplier(multiplier));
    }

    void ShowStreakNotice(int streak) => hud.Play.ShowNotice($"RACHA DE {streak}!", Palette.Medium);

    HudState CurrentHudState(double songTime) => new HudState
    {
        Score = score.Score,
        Streak = score.Streak,
        Multiplier = score.Multiplier,
        LitLeds = score.LitLeds,
        StarMode = score.IsStarMode,
        Title = currentSong.title,
        Difficulty = currentSong.difficulty,
        Progress = conductor.Duration > 0f ? (float)(songTime / conductor.Duration) : 0f,
        SongTime = songTime
    };
}

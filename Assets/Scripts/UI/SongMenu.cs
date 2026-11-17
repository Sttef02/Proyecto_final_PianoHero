using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum MenuCommand
{
    None,
    Play
}

// Menu de la escena (textos del Canvas del equipo): seleccion con perilla, flechas y mouse.
[Serializable]
public class SongMenu
{
    const string SongListObjectName = "ListaCanciones";
    const float MinAutoFontSize = 18f;
    const float KnobDeadZone = 0.01f;
    const float KnobHysteresis = 0.4f;
    const float KnobEdgeMargin = 0.001f;
    const float ScrollThreshold = 0.01f;
    const float PointerMoveThreshold = 1f;
    const float PercentScale = 100f;
    const string SelectedPrefix = "> ";
    const string SelectedSuffix = " <";

    [Tooltip("Panel que contiene todo el menu. Se oculta al jugar.")]
    public GameObject menuPanel;
    public TMP_Text latencyText;
    public TMP_Text connectionText;
    public TMP_Text knobText;
    [Tooltip("Un texto por cancion, en el mismo orden que la lista de canciones.")]
    public TMP_Text[] songTexts;
    [Tooltip("Boton_1 a Boton_4: se iluminan mientras su boton esta presionado.")]
    public Graphic[] keyIcons;
    public Color selectedColor = Palette.Accent;
    public Color normalColor = Color.white;
    [Tooltip("Elegir cancion con solo pasar el mouse por encima.")]
    public bool selectOnHover = true;
    [Tooltip("Clic sobre la cancion ya elegida = jugar.")]
    public bool clickToPlay = true;

    public event Action SelectionChanged;

    public int SelectedIndex { get; private set; }
    public bool HasSongs => songs.Length > 0;
    public SongData SelectedSong => HasSongs ? songs[SelectedIndex] : null;

    SongData[] songs = new SongData[0];
    float lastKnob = -1f;
    Vector2 lastPointer = new Vector2(-1f, -1f);

    public void Initialize(SongData[] songList)
    {
        songs = songList ?? new SongData[0];
        RepairSongTexts();
        FitSongTextsToOneLine();
    }

    public TMP_FontAsset FindFont()
    {
        foreach (TMP_Text text in songTexts)
            if (text != null && text.font != null) return text.font;
        foreach (TMP_Text text in new[] { latencyText, connectionText, knobText })
            if (text != null && text.font != null) return text.font;
        return null;
    }

    public void ResetKnob() => lastKnob = -1f;

    public void SetVisible(bool visible)
    {
        if (menuPanel != null && menuPanel.activeSelf != visible) menuPanel.SetActive(visible);
    }

    public MenuCommand HandleInput(ArduinoInput controller, PointerState pointer)
    {
        if (!HasSongs) return MenuCommand.None;

        int previous = SelectedIndex;
        ApplyKnob(controller);
        Step(controller.Navigation);
        if (Mathf.Abs(pointer.Scroll) > ScrollThreshold) Step(pointer.Scroll > 0f ? -1 : 1);
        MenuCommand command = ApplyPointer(pointer);

        if (SelectedIndex != previous) SelectionChanged?.Invoke();
        return command;
    }

    public void Render(ArduinoInput controller, float latencyMs)
    {
        RenderSongList();
        if (latencyText != null) latencyText.text = $"Latencia: {latencyMs:0} ms  (B2 -10 / B3 +10)";
        if (connectionText != null) connectionText.text = controller.ConnectionStatus;
        if (knobText != null)
            knobText.text = controller.HasKnobValue ? $"Perilla: {controller.Knob01 * PercentScale:0}%" : "Perilla: sin datos";
        RenderKeyIcons(controller);
    }

    void ApplyKnob(ArduinoInput controller)
    {
        if (!controller.HasKnobValue || Mathf.Abs(controller.Knob01 - lastKnob) <= KnobDeadZone) return;
        lastKnob = controller.Knob01;

        // Histeresis: solo cambia si la perilla esta bien dentro de la zona de la cancion.
        float position = Mathf.Clamp(controller.Knob01 * songs.Length, 0f, songs.Length - KnobEdgeMargin);
        int candidate = Mathf.FloorToInt(position);
        if (candidate != SelectedIndex && Mathf.Abs(position - (candidate + 0.5f)) < KnobHysteresis) SelectedIndex = candidate;
    }

    void Step(int direction)
    {
        if (direction != 0) SelectedIndex = (SelectedIndex + direction + songs.Length) % songs.Length;
    }

    MenuCommand ApplyPointer(PointerState pointer)
    {
        int hovered = SongUnderPointer(pointer.Position);
        bool moved = (pointer.Position - lastPointer).sqrMagnitude > PointerMoveThreshold;
        lastPointer = pointer.Position;

        // Solo selecciona al pasar si el mouse se movio, para no pelear con la perilla.
        if (selectOnHover && moved && hovered >= 0) SelectedIndex = hovered;
        if (!pointer.Clicked || hovered < 0) return MenuCommand.None;
        if (hovered == SelectedIndex && clickToPlay) return MenuCommand.Play;

        SelectedIndex = hovered;
        return MenuCommand.None;
    }

    int SongUnderPointer(Vector2 position)
    {
        for (int i = 0; i < songTexts.Length && i < songs.Length; i++)
        {
            TMP_Text text = songTexts[i];
            if (text == null || !text.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(text.rectTransform, position, CanvasCamera(text))) return i;
        }
        return -1;
    }

    static Camera CanvasCamera(TMP_Text text)
    {
        Canvas canvas = text.canvas != null ? text.canvas.rootCanvas : null;
        return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    void RenderSongList()
    {
        for (int i = 0; i < songTexts.Length; i++)
        {
            TMP_Text text = songTexts[i];
            if (text == null) continue;
            bool hasSong = i < songs.Length && songs[i] != null;
            text.gameObject.SetActive(hasSong);
            if (!hasSong) continue;

            bool selected = i == SelectedIndex;
            string line = $"{songs[i].title}  <size=60%>{songs[i].artist}</size>";
            text.text = selected ? SelectedPrefix + line + SelectedSuffix : line;
            text.color = selected ? selectedColor : normalColor;
        }
    }

    void RenderKeyIcons(ArduinoInput controller)
    {
        for (int i = 0; i < keyIcons.Length && i < ArduinoInput.LaneCount; i++)
            if (keyIcons[i] != null) keyIcons[i].color = controller.Pressed[i] ? Color.white : Palette.KeyIconIdle;
    }

    // En la escena puede faltar algun texto de la lista; se toman de ListaCanciones (Cancion1, Cancion2...).
    void RepairSongTexts()
    {
        if (songTexts == null) songTexts = new TMP_Text[0];
        if (keyIcons == null) keyIcons = new Graphic[0];
        bool missing = songTexts.Length == 0 || Array.Exists(songTexts, text => text == null);
        if (!missing) return;

        Transform list = FindSongList();
        if (list == null) return;

        var texts = new List<TMP_Text>();
        foreach (Transform child in list)
        {
            var text = child.GetComponent<TMP_Text>();
            if (text != null) texts.Add(text);
        }
        texts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        songTexts = texts.ToArray();
    }

    Transform FindSongList()
    {
        foreach (TMP_Text text in songTexts)
            if (text != null) return text.transform.parent;
        if (menuPanel == null) return null;
        foreach (Transform child in menuPanel.GetComponentsInChildren<Transform>(true))
            if (child.name == SongListObjectName) return child;
        return null;
    }

    // Una sola linea por cancion: la letra se achica si no cabe, asi no se montan los textos.
    void FitSongTextsToOneLine()
    {
        foreach (TMP_Text text in songTexts)
        {
            if (text == null) continue;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Min(MinAutoFontSize, text.fontSize);
            text.enableAutoSizing = true;
        }
    }
}

using UnityEngine;

[CreateAssetMenu(menuName = "PianoHero/Cancion", fileName = "NuevaCancion")]
public class SongData : ScriptableObject
{
    [Header("Informacion")]
    public string title = "Sin titulo";
    public string artist = "";
    public Difficulty difficulty = Difficulty.Medium;

    [Header("Archivos")]
    [Tooltip("Audio .ogg o .wav. En el importador usa Load Type = Decompress On Load.")]
    public AudioClip audio;
    [Tooltip("Chart en JSON, o de Moonscraper renombrado a .txt (notes.chart -> notes.txt).")]
    public TextAsset chart;
    [Tooltip("Solo para charts de Moonscraper: Expert, Hard, Medium o Easy.")]
    public string chartDifficulty = "Medium";

    [Header("Ritmo (para grabar y cuantizar)")]
    [Tooltip("Pulsaciones por minuto. 0 = no cuantizar al grabar.")]
    public float bpm = ChartData.DefaultBpm;
    [Tooltip("Segundo del audio donde cae el primer tiempo fuerte.")]
    public float firstBeatTime;
    [Tooltip("Al grabar: 2 = corcheas, 4 = semicorcheas.")]
    public int subdivision = 4;

    [Header("Ajustes de juego")]
    [Tooltip("Corrimiento de todas las notas en segundos (+ = llegan mas tarde).")]
    public float extraOffset;
    [Tooltip("Multiplicador de la velocidad de la autopista para esta cancion.")]
    public float speedMultiplier = 1f;
    [Tooltip("Segundo donde empieza la vista previa en el menu.")]
    public float previewStart = 30f;
}

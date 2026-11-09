using UnityEngine;

/// <summary>
/// Una cancion del juego. Crear con: clic derecho en Project > Create > PianoHero > Cancion
/// </summary>
[CreateAssetMenu(menuName = "PianoHero/Cancion", fileName = "NuevaCancion")]
public class SongData : ScriptableObject
{
    [Header("Informacion")]
    public string titulo = "Sin titulo";
    public string artista = "";

    [Header("Archivos")]
    [Tooltip("Audio .ogg o .wav. En el importador usa Load Type = Decompress On Load.")]
    public AudioClip audio;
    [Tooltip("Chart en JSON (.json) o de Moonscraper renombrado a .txt (notes.chart -> notes.txt).")]
    public TextAsset chart;
    [Tooltip("Solo para charts de Moonscraper: Expert, Hard, Medium o Easy.")]
    public string dificultad = "Medium";

    [Header("Ritmo (para grabar y cuantizar)")]
    [Tooltip("Pulsaciones por minuto de la cancion. 0 = no cuantizar al grabar.")]
    public float bpm = 120f;
    [Tooltip("Segundo exacto del audio donde cae el primer tiempo fuerte (miralo en Audacity).")]
    public float primerTiempo = 0f;
    [Tooltip("Al grabar: 2 = corcheas, 4 = semicorcheas.")]
    public int subdivision = 4;

    [Header("Ajustes de juego")]
    [Tooltip("Corrimiento extra de todas las notas en segundos (+ = las notas llegan mas tarde).")]
    public float offsetExtra = 0f;
    [Tooltip("Multiplicador de velocidad de la autopista para esta cancion.")]
    public float velocidad = 1f;
    [Tooltip("Segundo donde empieza la vista previa en el menu.")]
    public float inicioPreview = 30f;
}

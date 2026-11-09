using System.Collections.Generic;

/// <summary>
/// Graba un chart mientras suena la cancion: cada pulsacion es una nota y
/// si la mantienes mas de "umbralSostenida" segundos queda como nota sostenida.
/// </summary>
public class GrabadorChart
{
    public float umbralSostenida = 0.25f;

    readonly List<NotaChart> notas = new List<NotaChart>();
    readonly double[] inicio = { -1, -1, -1, -1 };
    public int Cantidad => notas.Count;
    public int Previas { get; }

    /// <param name="previas">Notas existentes para grabar encima (otra pasada), o null.</param>
    public GrabadorChart(IEnumerable<NotaChart> previas)
    {
        if (previas == null) return;
        foreach (var n in previas) notas.Add(new NotaChart { t = n.t, carril = n.carril, dur = n.dur });
        Previas = notas.Count;
    }

    public void Presionar(int carril, double t)
    {
        if (t < 0) return;
        inicio[carril] = t;
    }

    public void Soltar(int carril, double t)
    {
        if (inicio[carril] < 0) return;
        double d = t - inicio[carril];
        notas.Add(new NotaChart
        {
            t = (float)inicio[carril],
            carril = carril,
            dur = d >= umbralSostenida ? (float)d : 0f
        });
        inicio[carril] = -1;
    }

    public ChartJson Finalizar(SongData s, double tFinal)
    {
        for (int i = 0; i < 4; i++) if (inicio[i] >= 0) Soltar(i, tFinal);

        var c = new ChartJson
        {
            titulo = s.titulo,
            artista = s.artista,
            bpm = s.bpm,
            offset = 0f,
            notas = new List<NotaChart>(notas)
        };
        ChartUtil.Cuantizar(c, s.bpm, s.primerTiempo, s.subdivision);
        return c;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class NotaChart
{
    public float t;       // segundo en que se debe presionar
    public int carril;    // 0 a 3
    public float dur;     // duracion de la nota sostenida (0 = golpe simple)
}

[Serializable]
public class ChartJson
{
    public string titulo;
    public string artista;
    public float bpm = 120f;
    public float offset = 0f;   // se suma a todas las notas
    public List<NotaChart> notas = new List<NotaChart>();
}

/// <summary>Carga, convierte, cuantiza y guarda charts.</summary>
public static class ChartUtil
{
    const float SOSTENIDA_MINIMA = 0.15f;

    /// <summary>Acepta JSON propio o formato .chart de Moonscraper.</summary>
    public static ChartJson Cargar(TextAsset archivo, string dificultad)
    {
        if (archivo == null) return null;
        string texto = archivo.text.TrimStart('﻿', ' ', '\r', '\n', '\t');
        ChartJson c = texto.StartsWith("{")
            ? JsonUtility.FromJson<ChartJson>(texto)
            : DesdeMoonscraper(texto, dificultad);
        if (c?.notas != null) c.notas.Sort((a, b) => a.t.CompareTo(b.t));
        return c;
    }

    // ---------------------- Moonscraper (.chart) ----------------------

    public static ChartJson DesdeMoonscraper(string texto, string dificultad)
    {
        var secciones = LeerSecciones(texto);
        int resolucion = 192;
        var c = new ChartJson();

        if (secciones.TryGetValue("Song", out var song))
        {
            foreach (var l in song)
            {
                var kv = l.Split(new[] { '=' }, 2);
                if (kv.Length < 2) continue;
                string k = kv[0].Trim(), v = kv[1].Trim().Trim('"');
                if (k == "Resolution") int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out resolucion);
                else if (k == "Name") c.titulo = v;
                else if (k == "Artist") c.artista = v;
            }
        }

        if (resolucion <= 0) resolucion = 192;

        // Mapa de tempo: (tick, bpm)
        var tempos = new List<(long tick, double bpm)>();
        if (secciones.TryGetValue("SyncTrack", out var sync))
        {
            foreach (var l in sync)
            {
                var m = Regex.Match(l, @"^\s*(\d+)\s*=\s*B\s+(\d+)");
                if (m.Success)
                    tempos.Add((long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                                long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 1000.0));
            }
        }
        if (tempos.Count == 0 || tempos[0].tick != 0) tempos.Insert(0, (0, 120));
        tempos.Sort((a, b) => a.tick.CompareTo(b.tick));
        c.bpm = (float)tempos[0].bpm;

        double Segundos(long tick)
        {
            double s = 0;
            for (int i = 0; i < tempos.Count; i++)
            {
                long ini = tempos[i].tick;
                if (tick <= ini) break;
                long fin = (i + 1 < tempos.Count) ? Math.Min(tick, tempos[i + 1].tick) : tick;
                s += (fin - ini) / (double)resolucion * 60.0 / tempos[i].bpm;
            }
            return s;
        }

        // Buscar la dificultad pedida, si no existe la mas cercana
        string[] orden = { dificultad, "Expert", "Hard", "Medium", "Easy" };
        List<string> pista = null;
        foreach (var d in orden)
        {
            if (string.IsNullOrEmpty(d)) continue;
            if (secciones.TryGetValue(d + "Single", out pista)) break;
        }
        if (pista == null)
        {
            Debug.LogWarning("El .chart no tiene ninguna pista de guitarra (ExpertSingle, HardSingle...).");
            return c;
        }

        var vistos = new HashSet<(long, int)>();
        foreach (var l in pista)
        {
            var m = Regex.Match(l, @"^\s*(\d+)\s*=\s*N\s+(\d+)\s+(\d+)");
            if (!m.Success) continue;
            long tick = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int traste = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            long largo = long.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (traste > 4) continue;               // 5 = forzada, 6 = tap, 7 = abierta: se ignoran
            int carril = Mathf.Min(traste, 3);      // naranja (4) se juega en el carril azul
            if (!vistos.Add((tick, carril))) continue;

            float t = (float)Segundos(tick);
            float dur = largo > 0 ? (float)(Segundos(tick + largo) - t) : 0f;
            if (dur < SOSTENIDA_MINIMA) dur = 0f;
            c.notas.Add(new NotaChart { t = t, carril = carril, dur = dur });
        }
        return c;
    }

    static Dictionary<string, List<string>> LeerSecciones(string texto)
    {
        var res = new Dictionary<string, List<string>>();
        List<string> actual = null;
        foreach (var raw in texto.Split('\n'))
        {
            string l = raw.Trim();
            if (l.StartsWith("[") && l.EndsWith("]"))
            {
                actual = new List<string>();
                res[l.Substring(1, l.Length - 2)] = actual;
            }
            else if (actual != null && l != "{" && l != "}" && l.Length > 0)
            {
                actual.Add(l);
            }
        }
        return res;
    }

    // ---------------------- Cuantizar ----------------------

    /// <summary>Ajusta cada nota a la grilla del BPM (corrige pequenos errores al grabar).</summary>
    public static void Cuantizar(ChartJson c, float bpm, float primerTiempo, int subdivision)
    {
        if (bpm <= 0 || subdivision <= 0) return;
        double grilla = 60.0 / bpm / subdivision;
        var vistos = new HashSet<(long, int)>();
        var resultado = new List<NotaChart>();
        foreach (var n in c.notas)
        {
            long paso = (long)Math.Round((n.t - primerTiempo) / grilla);
            if (!vistos.Add((paso, n.carril))) continue;
            float t = (float)(primerTiempo + paso * grilla);
            float dur = 0f;
            if (n.dur > 0)
            {
                long pasosDur = Math.Max(1, (long)Math.Round(n.dur / grilla));
                dur = (float)(pasosDur * grilla);
            }
            resultado.Add(new NotaChart { t = t, carril = n.carril, dur = dur });
        }
        resultado.Sort((a, b) => a.t.CompareTo(b.t));
        c.notas = resultado;
    }

    // ---------------------- Guardar ----------------------

    /// <summary>
    /// En el editor guarda en Assets/PianoHero/Charts (listo para arrastrarlo al SongData).
    /// En un build guarda en la carpeta de datos persistentes.
    /// </summary>
    public static string Guardar(ChartJson c, string nombre)
    {
        foreach (char ch in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(ch, '_');
#if UNITY_EDITOR
        string dir = Path.Combine(Application.dataPath, "PianoHero", "Charts");
#else
        string dir = Path.Combine(Application.persistentDataPath, "Charts");
#endif
        Directory.CreateDirectory(dir);
        string ruta = Path.Combine(dir, nombre + ".json");
        File.WriteAllText(ruta, JsonUtility.ToJson(c, true));
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
        return ruta;
    }
}

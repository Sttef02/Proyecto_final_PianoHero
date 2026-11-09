using System;
using System.Collections.Generic;
using UnityEngine;

public enum Juicio { Perfecto, Bien, Fallo }

/// <summary>
/// Autopista 3D: construye la pista, mueve las notas hacia la camara y juzga los golpes.
/// Si no asignas prefabs, se arma sola con primitivas de Unity.
/// </summary>
public class Pista : MonoBehaviour
{
    [Header("Forma")]
    public float anchoCarril = 1.3f;
    public float largo = 45f;
    [Tooltip("Unidades por segundo a las que bajan las notas.")]
    public float velocidad = 14f;
    public bool ajustarCamara = true;

    [Header("Ventanas de acierto (segundos, hacia cada lado)")]
    public float ventanaPerfecto = 0.05f;
    public float ventanaBien = 0.10f;

    [Header("Colores de los carriles")]
    public Color[] colores =
    {
        new Color(0.15f, 0.85f, 0.25f),
        new Color(0.95f, 0.2f, 0.2f),
        new Color(1f, 0.85f, 0.1f),
        new Color(0.2f, 0.45f, 1f)
    };

    [Header("Prefabs opcionales (dejar vacio = primitivas)")]
    public GameObject prefabNota;

    public event Action<Juicio, int> OnJuicio;
    /// <summary>Segundos sostenidos durante este frame (para sumar puntos).</summary>
    public event Action<float> OnSostenido;

    public int TotalNotas => notas.Count;
    public float UltimoTiempo { get; private set; }
    public bool Terminada => siguiente >= notas.Count && activas.Count == 0;

    class NotaRT
    {
        public NotaChart d;
        public Transform cabeza, cola;
        public Renderer rCabeza, rCola;
        public bool golpeada, fallada, sosteniendo;
        public float Fin => d.t + d.dur;
    }

    readonly List<NotaRT> notas = new List<NotaRT>();
    readonly List<NotaRT> activas = new List<NotaRT>();
    int siguiente;
    double ultimoT;

    Transform raiz;
    Transform[] botones = new Transform[4];
    Renderer[] rBotones = new Renderer[4];
    float[] flash = new float[4];
    Shader shader;
    Material matGris;

    void Awake() => Construir();

    float X(int carril) => (carril - 1.5f) * anchoCarril;

    Material Mat(Color c)
    {
        var m = new Material(shader);
        m.color = c;
        return m;
    }

    GameObject Primitiva(PrimitiveType tipo, Transform padre, Vector3 pos, Vector3 escala, Material m)
    {
        var go = GameObject.CreatePrimitive(tipo);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    void Construir()
    {
        shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        matGris = Mat(new Color(0.35f, 0.35f, 0.38f));

        raiz = new GameObject("Autopista").transform;
        raiz.SetParent(transform, false);

        float ancho = anchoCarril * 4f;
        Primitiva(PrimitiveType.Cube, raiz, new Vector3(0, -0.08f, largo / 2f - 3f),
                  new Vector3(ancho + 0.4f, 0.1f, largo + 6f), Mat(new Color(0.06f, 0.06f, 0.09f)));

        var matLinea = Mat(new Color(0.5f, 0.5f, 0.6f));
        for (int i = 0; i <= 4; i++)
            Primitiva(PrimitiveType.Cube, raiz, new Vector3((i - 2f) * anchoCarril, -0.02f, largo / 2f - 3f),
                      new Vector3(0.04f, 0.02f, largo + 6f), matLinea);

        Primitiva(PrimitiveType.Cube, raiz, new Vector3(0, -0.01f, 0),
                  new Vector3(ancho, 0.02f, 0.08f), Mat(Color.white));

        for (int i = 0; i < 4; i++)
        {
            var b = Primitiva(PrimitiveType.Cylinder, raiz, new Vector3(X(i), 0.02f, 0),
                              new Vector3(anchoCarril * 0.8f, 0.03f, anchoCarril * 0.8f), Mat(colores[i] * 0.35f));
            botones[i] = b.transform;
            rBotones[i] = b.GetComponent<Renderer>();
        }

        if (ajustarCamara && Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(0, 5.5f, -6.5f);
            Camera.main.transform.rotation = Quaternion.Euler(28f, 0, 0);
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
        }
    }

    public void Cargar(ChartJson chart, float offsetExtra)
    {
        Limpiar();
        if (chart?.notas == null) return;
        foreach (var n in chart.notas)
        {
            var copia = new NotaChart { t = n.t + chart.offset + offsetExtra, carril = Mathf.Clamp(n.carril, 0, 3), dur = n.dur };
            notas.Add(new NotaRT { d = copia });
            UltimoTiempo = Mathf.Max(UltimoTiempo, copia.t + copia.dur);
        }
        notas.Sort((a, b) => a.d.t.CompareTo(b.d.t));
    }

    public void Limpiar()
    {
        foreach (var n in activas) Destruir(n);
        activas.Clear();
        notas.Clear();
        siguiente = 0;
        UltimoTiempo = 0;
    }

    void Crear(NotaRT n)
    {
        int c = n.d.carril;
        GameObject cabeza = prefabNota != null
            ? Instantiate(prefabNota, raiz)
            : Primitiva(PrimitiveType.Cylinder, raiz, Vector3.zero,
                        new Vector3(anchoCarril * 0.75f, 0.08f, anchoCarril * 0.75f), Mat(colores[c]));
        n.cabeza = cabeza.transform;
        n.rCabeza = cabeza.GetComponentInChildren<Renderer>();

        if (n.d.dur > 0)
        {
            var cola = Primitiva(PrimitiveType.Cube, raiz, Vector3.zero, Vector3.one, Mat(colores[c] * 0.8f));
            n.cola = cola.transform;
            n.rCola = cola.GetComponent<Renderer>();
        }
    }

    void Destruir(NotaRT n)
    {
        if (n.cabeza != null) Destroy(n.cabeza.gameObject);
        if (n.cola != null) Destroy(n.cola.gameObject);
        n.cabeza = n.cola = null;
    }

    /// <summary>Llamar cada frame con el tiempo de la cancion.</summary>
    public void Actualizar(double t, bool[] presionado)
    {
        float adelanto = largo / velocidad;
        while (siguiente < notas.Count && notas[siguiente].d.t - t < adelanto)
        {
            Crear(notas[siguiente]);
            activas.Add(notas[siguiente]);
            siguiente++;
        }

        float dt = (float)Math.Max(0, t - ultimoT);
        ultimoT = t;

        for (int i = activas.Count - 1; i >= 0; i--)
        {
            var n = activas[i];

            // Fallo por dejarla pasar
            if (!n.golpeada && !n.fallada && t - n.d.t > ventanaBien)
            {
                n.fallada = true;
                if (n.rCabeza) n.rCabeza.material.color = matGris.color;
                if (n.rCola) n.rCola.material.color = matGris.color;
                OnJuicio?.Invoke(Juicio.Fallo, n.d.carril);
            }

            // Sostenida en curso
            if (n.sosteniendo)
            {
                if (t >= n.Fin) { n.sosteniendo = false; Destruir(n); activas.RemoveAt(i); continue; }
                OnSostenido?.Invoke(dt);
                flash[n.d.carril] = 0.05f;
            }

            float zCabeza = n.sosteniendo ? 0f : (float)(n.d.t - t) * velocidad;
            float zFin = (float)(n.Fin - t) * velocidad;

            if (n.cabeza) n.cabeza.localPosition = new Vector3(X(n.d.carril), 0.08f, zCabeza);
            if (n.cola)
            {
                float zIni = zCabeza;
                float l = Mathf.Max(0.01f, zFin - zIni);
                n.cola.localPosition = new Vector3(X(n.d.carril), 0.04f, zIni + l / 2f);
                n.cola.localScale = new Vector3(anchoCarril * 0.22f, 0.04f, l);
            }

            if (n.fallada && zFin < -4f) { Destruir(n); activas.RemoveAt(i); }
        }

        for (int i = 0; i < 4; i++)
        {
            flash[i] = Mathf.Max(0, flash[i] - Time.deltaTime);
            bool on = presionado != null && presionado[i];
            float brillo = flash[i] > 0 ? 1.4f : (on ? 0.9f : 0.35f);
            rBotones[i].material.color = colores[i] * brillo;
            float s = anchoCarril * (flash[i] > 0 ? 0.95f : 0.8f);
            botones[i].localScale = new Vector3(s, 0.03f, s);
        }
    }

    /// <summary>Devuelve true si el golpe acerto una nota.</summary>
    public bool Golpear(int carril, double t)
    {
        NotaRT mejor = null;
        double mejorDist = double.MaxValue;
        foreach (var n in activas)
        {
            if (n.d.carril != carril || n.golpeada || n.fallada) continue;
            double dist = Math.Abs(n.d.t - t);
            if (dist <= ventanaBien && dist < mejorDist) { mejor = n; mejorDist = dist; }
        }
        if (mejor == null) return false;

        mejor.golpeada = true;
        flash[carril] = 0.12f;
        OnJuicio?.Invoke(mejorDist <= ventanaPerfecto ? Juicio.Perfecto : Juicio.Bien, carril);

        if (mejor.d.dur > 0) mejor.sosteniendo = true;
        else { Destruir(mejor); activas.Remove(mejor); }
        return true;
    }

    public void Soltar(int carril, double t)
    {
        for (int i = activas.Count - 1; i >= 0; i--)
        {
            var n = activas[i];
            if (n.d.carril != carril || !n.sosteniendo) continue;
            n.sosteniendo = false;
            if (t < n.Fin - 0.1)
            {
                // Solto antes de tiempo: deja de sumar puntos (no rompe la racha)
                n.fallada = true;
                if (n.rCola) n.rCola.material.color = matGris.color;
                if (n.cabeza) { Destroy(n.cabeza.gameObject); n.cabeza = null; }
            }
            else { Destruir(n); activas.RemoveAt(i); }
        }
    }
}

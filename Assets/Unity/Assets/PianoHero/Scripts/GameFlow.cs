using UnityEngine;

/// <summary>
/// Controla todo el juego: menu (potenciometro), partida, resultados y grabacion de charts.
/// Solo pon este script en un GameObject vacio y arrastra tus canciones a la lista.
///
/// Controles (Arduino / teclado):
///   Menu:       perilla o flechas = elegir cancion | B1 (D) = jugar | B4 (K) = grabar chart
///               B2 (F) / B3 (J) = latencia -10 / +10 ms
///   Jugando:    B1..B4 = carriles | mantener B1 + B4 por 1 s (o Esc) = volver al menu
///   Grabando:   toca al ritmo; al terminar la cancion se guarda el chart
///   Resultados: B1 = volver al menu
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum Estado { Menu, Jugando, Grabando, Resultados }

    [Header("Canciones")]
    public SongData[] canciones;

    [Header("Juego")]
    [Tooltip("Si es true, presionar sin nota rompe la racha. Recomendado false con botones de protoboard.")]
    public bool penalizarGolpesAlAire = false;
    public int notasPorLed = 10;
    [Tooltip("Racha a partir de la cual los 4 LEDs parpadean.")]
    public int rachaParpadeo = 50;
    public float esperaInicial = 2.5f;

    [Header("Grabacion")]
    [Tooltip("true = conserva las notas del chart actual y agrega las nuevas (grabar por pasadas).")]
    public bool grabarEncima = true;

    [Header("Referencias (se crean solas si estan vacias)")]
    public ArduinoInput entrada;
    public Conductor conductor;
    public Pista pista;

    public Estado EstadoActual { get; private set; } = Estado.Menu;

    int sel;
    float ultimoPot = -1f;
    float tiempoCombo;
    string mensaje = "";
    float mensajeHasta;

    // Partida
    SongData cancion;
    float puntos;
    int racha, rachaMax, perfectos, buenos, fallos;
    string popup = "";
    Color popupColor;
    float popupHasta;

    GrabadorChart grabador;

    int Multiplicador => 1 + Mathf.Min(3, racha / notasPorLed);

    void Start()
    {
        if (entrada == null) entrada = FindFirstObjectByType<ArduinoInput>();
        if (entrada == null) entrada = gameObject.AddComponent<ArduinoInput>();
        if (conductor == null) conductor = FindFirstObjectByType<Conductor>();
        if (conductor == null) conductor = gameObject.AddComponent<Conductor>();
        if (pista == null) pista = FindFirstObjectByType<Pista>();
        if (pista == null) pista = new GameObject("Pista").AddComponent<Pista>();

        entrada.OnPress += AlPresionar;
        entrada.OnRelease += AlSoltar;
        pista.OnJuicio += AlJuicio;
        pista.OnSostenido += dt => { if (EstadoActual == Estado.Jugando) puntos += dt * 50f * Multiplicador; };

        IrAlMenu();
    }

    void OnDestroy()
    {
        if (entrada != null) { entrada.OnPress -= AlPresionar; entrada.OnRelease -= AlSoltar; }
    }

    // ------------------------------------------------------------------ Flujo

    void IrAlMenu()
    {
        EstadoActual = Estado.Menu;
        pista.Limpiar();
        entrada.SetLeds(0);
        ultimoPot = -1f;
        ReproducirPreview();
    }

    void ReproducirPreview()
    {
        if (canciones == null || canciones.Length == 0) return;
        var s = canciones[sel];
        if (s != null) conductor.Preview(s.audio, s.inicioPreview);
    }

    void Jugar()
    {
        cancion = canciones[sel];
        if (cancion == null || cancion.audio == null) { Avisar("Esta cancion no tiene audio asignado."); return; }

        var chart = ChartUtil.Cargar(cancion.chart, cancion.dificultad);
        if (chart == null || chart.notas.Count == 0)
        {
            Avisar("Esta cancion no tiene notas. Presiona B4 (K) para grabar el chart.");
            return;
        }

        pista.velocidad = 14f * Mathf.Max(0.3f, cancion.velocidad);
        pista.Cargar(chart, cancion.offsetExtra);
        puntos = 0; racha = rachaMax = perfectos = buenos = fallos = 0;
        entrada.SetLeds(0);
        conductor.Iniciar(cancion.audio, esperaInicial);
        EstadoActual = Estado.Jugando;
    }

    void Grabar()
    {
        cancion = canciones[sel];
        if (cancion == null || cancion.audio == null) { Avisar("Esta cancion no tiene audio asignado."); return; }

        ChartJson previo = grabarEncima ? ChartUtil.Cargar(cancion.chart, cancion.dificultad) : null;
        grabador = new GrabadorChart(previo?.notas);
        pista.Limpiar();
        if (previo != null) pista.Cargar(previo, 0f);   // ves las notas anteriores mientras grabas
        entrada.SetLeds(0);
        conductor.Iniciar(cancion.audio, esperaInicial);
        EstadoActual = Estado.Grabando;
    }

    void TerminarGrabacion()
    {
        var chart = grabador.Finalizar(cancion, conductor.Tiempo);
        conductor.Detener();
        string ruta = ChartUtil.Guardar(chart, cancion.titulo + "_chart");
        Avisar($"Chart guardado ({chart.notas.Count} notas): {ruta}\nArrastralo al campo Chart de la cancion.", 10f);
        IrAlMenu();
    }

    void TerminarPartida()
    {
        conductor.Detener();
        entrada.SetLeds(0);
        EstadoActual = Estado.Resultados;
    }

    // ------------------------------------------------------------------ Entrada

    void AlPresionar(int carril, double reloj)
    {
        switch (EstadoActual)
        {
            case Estado.Menu:
                if (canciones == null || canciones.Length == 0) return;
                if (carril == 0) Jugar();
                else if (carril == 3) Grabar();
                else if (carril == 1) conductor.latenciaMs -= 10f;
                else if (carril == 2) conductor.latenciaMs += 10f;
                break;

            case Estado.Jugando:
                if (!pista.Golpear(carril, conductor.TiempoEn(reloj)) && penalizarGolpesAlAire)
                    RomperRacha();
                break;

            case Estado.Grabando:
                grabador.Presionar(carril, conductor.TiempoEn(reloj));
                break;

            case Estado.Resultados:
                if (carril == 0) IrAlMenu();
                break;
        }
    }

    void AlSoltar(int carril, double reloj)
    {
        if (EstadoActual == Estado.Jugando) pista.Soltar(carril, conductor.TiempoEn(reloj));
        else if (EstadoActual == Estado.Grabando) grabador.Soltar(carril, conductor.TiempoEn(reloj));
    }

    void AlJuicio(Juicio j, int carril)
    {
        if (EstadoActual != Estado.Jugando) return;
        switch (j)
        {
            case Juicio.Perfecto:
                racha++; perfectos++; puntos += 100 * Multiplicador;
                Popup("¡PERFECTO!", new Color(1f, 0.9f, 0.2f));
                break;
            case Juicio.Bien:
                racha++; buenos++; puntos += 50 * Multiplicador;
                Popup("BIEN", new Color(0.4f, 0.9f, 1f));
                break;
            case Juicio.Fallo:
                fallos++;
                RomperRacha();
                Popup("FALLO", new Color(1f, 0.3f, 0.3f));
                break;
        }
        rachaMax = Mathf.Max(rachaMax, racha);
        ActualizarLeds();
    }

    void RomperRacha()
    {
        racha = 0;
        ActualizarLeds();
    }

    void ActualizarLeds()
    {
        int n = racha >= rachaParpadeo ? 5 : Mathf.Min(4, racha / notasPorLed);
        entrada.SetLeds(n);
    }

    // ------------------------------------------------------------------ Update

    void Update()
    {
        switch (EstadoActual)
        {
            case Estado.Menu: ActualizarMenu(); break;

            case Estado.Jugando:
            {
                double t = conductor.Tiempo;
                pista.Actualizar(t, entrada.Presionado);
                bool fin = t > conductor.Duracion + 0.5f || (pista.Terminada && t > pista.UltimoTiempo + 3f);
                if (fin) TerminarPartida();
                else if (QuiereSalir()) IrAlMenu();
                break;
            }

            case Estado.Grabando:
            {
                double t = conductor.Tiempo;
                pista.Actualizar(t, entrada.Presionado);
                if (t > conductor.Duracion + 0.5f || QuiereSalir()) TerminarGrabacion();
                break;
            }
        }
    }

    void ActualizarMenu()
    {
        if (canciones == null || canciones.Length == 0) return;
        int n = canciones.Length;
        int antes = sel;

        if (entrada.PotRecibido && Mathf.Abs(entrada.Pot01 - ultimoPot) > 0.01f)
        {
            ultimoPot = entrada.Pot01;
            float pos = Mathf.Clamp(entrada.Pot01 * n, 0f, n - 0.001f);
            int cand = Mathf.FloorToInt(pos);
            // Histeresis: solo cambia si la perilla esta bien dentro de la zona
            if (cand != sel && Mathf.Abs(pos - (cand + 0.5f)) < 0.4f) sel = cand;
        }
        if (entrada.Navegacion != 0) sel = (sel + entrada.Navegacion + n) % n;

        if (sel != antes) ReproducirPreview();
    }

    bool QuiereSalir()
    {
        if (entrada.EscapeEsteFrame) return true;
        if (entrada.Presionado[0] && entrada.Presionado[3]) tiempoCombo += Time.deltaTime;
        else tiempoCombo = 0f;
        if (tiempoCombo > 1f) { tiempoCombo = 0f; return true; }
        return false;
    }

    void Avisar(string m, float segundos = 4f)
    {
        mensaje = m;
        mensajeHasta = Time.time + segundos;
    }

    void Popup(string texto, Color c)
    {
        popup = texto;
        popupColor = c;
        popupHasta = Time.time + 0.4f;
    }

    // ------------------------------------------------------------------ HUD (OnGUI)
    // HUD provisional para que el juego funcione ya. Luego puedes cambiarlo por un Canvas.

    GUIStyle sTitulo, sTexto, sGrande, sChico;

    void Estilos()
    {
        float k = Screen.height / 1080f;
        sTitulo = new GUIStyle(GUI.skin.label) { fontSize = (int)(64 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sTexto = new GUIStyle(GUI.skin.label) { fontSize = (int)(34 * k), alignment = TextAnchor.MiddleCenter, richText = true };
        sGrande = new GUIStyle(GUI.skin.label) { fontSize = (int)(80 * k), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sChico = new GUIStyle(GUI.skin.label) { fontSize = (int)(24 * k), alignment = TextAnchor.LowerLeft, wordWrap = true };
    }

    void Caja(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    void OnGUI()
    {
        Estilos();
        float W = Screen.width, H = Screen.height, k = H / 1080f;

        switch (EstadoActual)
        {
            case Estado.Menu:
            {
                Caja(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.75f));
                GUI.Label(new Rect(0, 60 * k, W, 90 * k), "PIANO HERO", sTitulo);
                if (canciones == null || canciones.Length == 0)
                {
                    GUI.Label(new Rect(0, H / 2 - 40 * k, W, 80 * k), "Agrega canciones en el componente GameFlow", sTexto);
                    break;
                }
                float y = 220 * k;
                for (int i = 0; i < canciones.Length; i++)
                {
                    var s = canciones[i];
                    string nombre = s == null ? "(vacio)" : $"{s.titulo}  <size={(int)(24 * k)}>{s.artista}</size>";
                    bool activa = i == sel;
                    if (activa) Caja(new Rect(W / 2 - 450 * k, y - 8 * k, 900 * k, 70 * k), new Color(1f, 0.6f, 0.1f, 0.35f));
                    GUI.Label(new Rect(0, y, W, 56 * k), (activa ? "> " : "") + nombre + (activa ? " <" : ""), sTexto);
                    y += 80 * k;
                }
                GUI.Label(new Rect(0, H - 250 * k, W, 50 * k),
                    "Gira la perilla para elegir  |  <b>B1</b> jugar  |  <b>B4</b> grabar chart", sTexto);
                GUI.Label(new Rect(0, H - 200 * k, W, 50 * k),
                    $"Latencia: {conductor.latenciaMs:0} ms  (B2 -10 / B3 +10)", sTexto);
                GUI.Label(new Rect(20 * k, H - 70 * k, W - 40 * k, 50 * k), entrada.EstadoConexion, sChico);

                // Prueba de botones: cada cuadro se ilumina mientras su boton esta presionado
                Color[] cb = pista.colores;
                for (int i = 0; i < 4; i++)
                {
                    var r = new Rect(W - (4 - i) * 70 * k - 20 * k, H - 150 * k, 60 * k, 60 * k);
                    Caja(r, entrada.Presionado[i] ? cb[i] : new Color(cb[i].r, cb[i].g, cb[i].b, 0.2f));
                    GUI.Label(r, "B" + (i + 1), sTexto);
                }
                GUI.Label(new Rect(W - 300 * k, H - 85 * k, 280 * k, 40 * k),
                    entrada.PotRecibido ? $"Perilla: {entrada.Pot01 * 100f:0}%" : "Perilla: sin datos",
                    new GUIStyle(sChico) { alignment = TextAnchor.MiddleRight });
                break;
            }

            case Estado.Jugando:
            {
                GUI.Label(new Rect(30 * k, 20 * k, 600 * k, 60 * k), $"{puntos:N0}", new GUIStyle(sTexto) { alignment = TextAnchor.MiddleLeft, fontSize = (int)(48 * k) });
                GUI.Label(new Rect(30 * k, 80 * k, 600 * k, 50 * k), $"Racha {racha}   x{Multiplicador}", new GUIStyle(sTexto) { alignment = TextAnchor.MiddleLeft });

                // Medidor de racha (espejo de los LEDs)
                for (int i = 0; i < 4; i++)
                {
                    bool on = racha >= rachaParpadeo ? (Time.time * 8f % 2f) < 1f : i < racha / notasPorLed;
                    Caja(new Rect(30 * k + i * 50 * k, 140 * k, 40 * k, 20 * k), on ? new Color(1f, 0.8f, 0.2f) : new Color(1, 1, 1, 0.15f));
                }

                float prog = conductor.Duracion > 0 ? Mathf.Clamp01((float)conductor.Tiempo / conductor.Duracion) : 0;
                Caja(new Rect(0, H - 8 * k, W, 8 * k), new Color(1, 1, 1, 0.15f));
                Caja(new Rect(0, H - 8 * k, W * prog, 8 * k), new Color(1f, 0.6f, 0.1f));

                if (conductor.Tiempo < 0)
                    GUI.Label(new Rect(0, H * 0.3f, W, 100 * k), Mathf.CeilToInt((float)-conductor.Tiempo).ToString(), sGrande);

                if (Time.time < popupHasta)
                {
                    var st = new GUIStyle(sTitulo);
                    st.normal.textColor = popupColor;
                    GUI.Label(new Rect(0, H * 0.35f, W, 90 * k), popup, st);
                }
                GUI.Label(new Rect(W - 520 * k, 20 * k, 500 * k, 40 * k), cancion.titulo, new GUIStyle(sTexto) { alignment = TextAnchor.MiddleRight, fontSize = (int)(26 * k) });
                break;
            }

            case Estado.Grabando:
            {
                Caja(new Rect(0, 0, W, 130 * k), new Color(0.6f, 0, 0, 0.6f));
                GUI.Label(new Rect(0, 10 * k, W, 60 * k), "● GRABANDO CHART", sTitulo);
                GUI.Label(new Rect(0, 70 * k, W, 50 * k),
                    $"Notas: {grabador.Cantidad}   |   B1 + B4 (1 s) o Esc para terminar y guardar", sTexto);
                if (conductor.Tiempo < 0)
                    GUI.Label(new Rect(0, H * 0.3f, W, 100 * k), Mathf.CeilToInt((float)-conductor.Tiempo).ToString(), sGrande);
                break;
            }

            case Estado.Resultados:
            {
                Caja(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.8f));
                int total = perfectos + buenos + fallos;
                float precision = total > 0 ? (perfectos + buenos) * 100f / total : 0;
                GUI.Label(new Rect(0, 120 * k, W, 90 * k), cancion.titulo, sTitulo);
                GUI.Label(new Rect(0, 260 * k, W, 100 * k), $"{puntos:N0}", sGrande);
                GUI.Label(new Rect(0, 400 * k, W, 50 * k), $"Precision: {precision:0.0}%", sTexto);
                GUI.Label(new Rect(0, 460 * k, W, 50 * k), $"Perfectos: {perfectos}   Bien: {buenos}   Fallos: {fallos}", sTexto);
                GUI.Label(new Rect(0, 520 * k, W, 50 * k), $"Racha maxima: {rachaMax}", sTexto);
                GUI.Label(new Rect(0, H - 160 * k, W, 50 * k), "B1 para volver al menu", sTexto);
                break;
            }
        }

        if (Time.time < mensajeHasta)
        {
            Caja(new Rect(W / 2 - 600 * k, H / 2 + 120 * k, 1200 * k, 130 * k), new Color(0, 0, 0, 0.85f));
            GUI.Label(new Rect(W / 2 - 580 * k, H / 2 + 120 * k, 1160 * k, 130 * k), mensaje,
                      new GUIStyle(sTexto) { fontSize = (int)(26 * k), wordWrap = true });
        }
    }
}

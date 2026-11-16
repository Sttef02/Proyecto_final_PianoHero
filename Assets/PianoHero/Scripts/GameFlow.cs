using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla todo el juego: menu (potenciometro), partida, resultados y grabacion de charts.
/// Solo pon este script en un GameObject vacio y arrastra tus canciones a la lista.
///
/// Controles (Arduino / teclado):
///   Menu:       perilla, flechas o mouse = elegir cancion | B1 (D) = jugar | B4 (K) = grabar chart
///               Mouse: rueda o pasar por encima = elegir | clic en la cancion elegida = jugar
///               B2 (F) / B3 (J) = latencia -10 / +10 ms
///   Jugando:    B1..B4 = carriles | mantener B1 + B4 por 2 s (o Esc) = volver al menu
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
    [Tooltip("Puntaje del juego y resultados (Canvas). Se crea solo si esta vacio.")]
    public PianoHeroUI ui;
    [Tooltip("Fuente del puntaje y resultados. Vacio = la misma de los textos del menu (BoldPixels).")]
    public TMP_FontAsset fuenteHud;

    [Header("UI Menu (Canvas)")]
    [Tooltip("Panel que contiene todo el menu. Se oculta al jugar.")]
    public GameObject panelMenu;
    public TMP_Text textoLatencia;
    public TMP_Text textoConexion;      // opcional: puede quedar vacio
    public TMP_Text textoPerilla;
    [Tooltip("Un texto por cancion, en el mismo orden que la lista de canciones.")]
    public TMP_Text[] textosCanciones;
    [Tooltip("Boton_1 a Boton_4 (Image o Raw Image).")]
    public Graphic[] botonesPrueba;
    public Color colorActiva = new Color(1f, 0.6f, 0.1f);
    public Color colorNormal = Color.white;

    [Header("Mouse")]
    [Tooltip("Elegir cancion con solo pasar el mouse por encima.")]
    public bool seleccionarAlPasar = true;
    [Tooltip("Clic sobre la cancion ya elegida = jugar.")]
    public bool clicParaJugar = true;

    public Estado EstadoActual { get; private set; } = Estado.Menu;

    int sel;
    float ultimoPot = -1f;
    float tiempoCombo;
    string mensaje = "";
    float mensajeHasta;

    // Partida
    SongData cancion;
    float puntos;
    int racha, rachaMax, perfectos, buenos, fallos, multAnterior = 1;

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
        if (ui == null) ui = gameObject.AddComponent<PianoHeroUI>();
        ui.Construir(BuscarFuente(), pista.colores);
        RepararListaCanciones();

        entrada.OnPress += AlPresionar;
        entrada.OnRelease += AlSoltar;
        pista.OnJuicio += AlJuicio;
        pista.OnSostenido += dt => { if (EstadoActual == Estado.Jugando) puntos += dt * 50f * Multiplicador; };

        IrAlMenu();
    }

    /// <summary>
    /// Si en la escena falta algun texto de la lista (espacio vacio en textosCanciones), los toma
    /// de los hijos de ListaCanciones (Cancion1, Cancion2...). Ademas deja cada cancion en una sola
    /// linea que se achica si no cabe, para que no se monten entre ellas ni sobre las instrucciones.
    /// </summary>
    void RepararListaCanciones()
    {
        if (textosCanciones == null) textosCanciones = new TMP_Text[0];
        bool faltan = textosCanciones.Length == 0 || System.Array.Exists(textosCanciones, t => t == null);
        if (faltan)
        {
            Transform lista = null;
            foreach (var t in textosCanciones) if (t != null) { lista = t.transform.parent; break; }
            if (lista == null && panelMenu != null)
                foreach (var tr in panelMenu.GetComponentsInChildren<Transform>(true))
                    if (tr.name == "ListaCanciones") { lista = tr; break; }
            if (lista != null)
            {
                var hijos = new List<TMP_Text>();
                foreach (Transform h in lista)
                {
                    var t = h.GetComponent<TMP_Text>();
                    if (t != null) hijos.Add(t);
                }
                hijos.Sort((a, b) => string.CompareOrdinal(a.name, b.name));   // Cancion1, Cancion2, ...
                textosCanciones = hijos.ToArray();
            }
        }
        foreach (var t in textosCanciones)
        {
            if (t == null) continue;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = Mathf.Min(18f, t.fontSize);
            t.enableAutoSizing = true;
        }
    }

    TMP_FontAsset BuscarFuente()
    {
        if (fuenteHud != null) return fuenteHud;
        if (textosCanciones != null)
            foreach (var t in textosCanciones) if (t != null && t.font != null) return t.font;
        foreach (var t in new[] { textoLatencia, textoConexion, textoPerilla })
            if (t != null && t.font != null) return t.font;
        return null;
    }

    /// <summary>"FACIL", "MEDIA" o "EXPERTO" segun el nombre del archivo de la cancion (ej. InstantCrush_Media).</summary>
    static string NombreDificultad(SongData s)
    {
        string n = PianoHeroUI.Limpiar(s != null ? s.name : "");
        if (n.EndsWith("_FACIL")) return "FACIL";
        if (n.EndsWith("_EXPERTO")) return "EXPERTO";
        if (n.EndsWith("_MEDIA")) return "MEDIA";
        return "";
    }

    static Color ColorDificultad(string d) =>
        d == "FACIL" ? PianoHeroUI.ColorFacil : d == "EXPERTO" ? PianoHeroUI.ColorExperto : PianoHeroUI.ColorMedia;

    void OnDestroy()
    {
        if (entrada != null) { entrada.OnPress -= AlPresionar; entrada.OnRelease -= AlSoltar; }
    }

    // ------------------------------------------------------------------ Flujo

    void IrAlMenu()
    {
        EstadoActual = Estado.Menu;
        pista.MostrarBotones(false);   // los circulos de la pista no deben verse detras del menu
        ui.Ocultar();
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
        puntos = 0; racha = rachaMax = perfectos = buenos = fallos = 0; multAnterior = 1;
        entrada.SetLeds(0);
        conductor.Iniciar(cancion.audio, esperaInicial);
        EstadoActual = Estado.Jugando;
        pista.MostrarBotones(true);
        ui.MostrarJuego();
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
        pista.MostrarBotones(true);
        ui.Ocultar();
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

        // Record por cancion y dificultad (se guarda en el PC)
        string clave = "PianoHero.Record." + cancion.name;
        bool record = puntos > 0 && puntos > PlayerPrefs.GetFloat(clave, 0f);
        if (record) { PlayerPrefs.SetFloat(clave, puntos); PlayerPrefs.Save(); }

        string dif = NombreDificultad(cancion);
        ui.Resultados(cancion.titulo, dif, ColorDificultad(dif), puntos, perfectos, buenos, fallos, rachaMax, record);
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

        int m = Multiplicador;
        if (j != Juicio.Fallo && m > multAnterior)
            ui.Aviso(m >= 4 ? "MULTIPLICADOR X4 - AL MAXIMO!" : "MULTIPLICADOR X" + m + "!",
                     m >= 4 ? PianoHeroUI.ColorExperto : m == 3 ? PianoHeroUI.ColorMedia : PianoHeroUI.ColorFacil);
        multAnterior = m;
        if (racha == rachaParpadeo) ui.Aviso("RACHA DE " + racha + "!", PianoHeroUI.ColorMedia);
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
                ActualizarHud(t);
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

            case Estado.Resultados:
            {
                LeerMouse(out _, out _, out bool clic);
                if (clic) IrAlMenu();
                break;
            }
        }

        ActualizarUIMenu();
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

        if (ActualizarMouse(n)) return;   // el clic arranco la cancion

        if (sel != antes) ReproducirPreview();
    }

    // ------------------------------------------------------------------ Mouse en el menu

    Vector2 ultimoMouse = new Vector2(-1, -1);

    /// <summary>Rueda, pasar por encima y clic. Devuelve true si empezo una partida.</summary>
    bool ActualizarMouse(int n)
    {
        LeerMouse(out Vector2 posMouse, out float rueda, out bool clic);

        // Rueda: arriba = cancion anterior, abajo = siguiente
        if (rueda > 0.01f) sel = (sel - 1 + n) % n;
        else if (rueda < -0.01f) sel = (sel + 1) % n;

        int bajoMouse = CancionBajoMouse(posMouse);
        bool seMovio = (posMouse - ultimoMouse).sqrMagnitude > 1f;
        ultimoMouse = posMouse;

        // Solo selecciona al pasar si el mouse se movio (asi no pelea con la perilla)
        if (seleccionarAlPasar && seMovio && bajoMouse >= 0) sel = bajoMouse;

        if (clic && bajoMouse >= 0)
        {
            if (bajoMouse == sel && clicParaJugar)
            {
                Jugar();
                return EstadoActual == Estado.Jugando;
            }
            sel = bajoMouse;
        }
        return false;
    }

    int CancionBajoMouse(Vector2 posMouse)
    {
        if (textosCanciones == null) return -1;
        for (int i = 0; i < textosCanciones.Length && i < canciones.Length; i++)
        {
            var t = textosCanciones[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            Canvas c = t.canvas != null ? t.canvas.rootCanvas : null;
            Camera cam = (c == null || c.renderMode == RenderMode.ScreenSpaceOverlay) ? null : c.worldCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(t.rectTransform, posMouse, cam)) return i;
        }
        return -1;
    }

    static void LeerMouse(out Vector2 pos, out float rueda, out bool clic)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) { pos = Vector2.zero; rueda = 0; clic = false; return; }
        pos = m.position.ReadValue();
        rueda = m.scroll.ReadValue().y;
        clic = m.leftButton.wasPressedThisFrame;
#else
        pos = Input.mousePosition;
        rueda = Input.mouseScrollDelta.y;
        clic = Input.GetMouseButtonDown(0);
#endif
    }

    // ------------------------------------------------------------------ UI del menu (Canvas)

    void ActualizarUIMenu()
    {
        bool enMenu = EstadoActual == Estado.Menu;
        if (panelMenu != null && panelMenu.activeSelf != enMenu) panelMenu.SetActive(enMenu);
        if (!enMenu) return;

        // Lista de canciones
        if (textosCanciones != null)
        {
            for (int i = 0; i < textosCanciones.Length; i++)
            {
                if (textosCanciones[i] == null) continue;
                bool hay = canciones != null && i < canciones.Length && canciones[i] != null;
                textosCanciones[i].gameObject.SetActive(hay);
                if (!hay) continue;

                var s = canciones[i];
                bool activa = i == sel;
                textosCanciones[i].text = (activa ? "> " : "") + $"{s.titulo}  <size=60%>{s.artista}</size>" + (activa ? " <" : "");
                textosCanciones[i].color = activa ? colorActiva : colorNormal;
            }
        }

        // Textos informativos
        if (textoLatencia != null)
            textoLatencia.text = $"Latencia: {conductor.latenciaMs:0} ms  (B2 -10 / B3 +10)";
        if (textoConexion != null)
            textoConexion.text = entrada.EstadoConexion;
        if (textoPerilla != null)
            textoPerilla.text = entrada.PotRecibido ? $"Perilla: {entrada.Pot01 * 100f:0}%" : "Perilla: sin datos";

        // Prueba de botones: cada cuadro se ilumina mientras su boton esta presionado
        if (botonesPrueba != null)
        {
            for (int i = 0; i < botonesPrueba.Length && i < 4; i++)
            {
                if (botonesPrueba[i] == null) continue;
                botonesPrueba[i].color = entrada.Presionado[i]
                    ? Color.white                        // presionado: brillo completo
                    : new Color(0.55f, 0.55f, 0.55f, 1f); // suelto: visible pero apagado
            }
        }
    }

    bool QuiereSalir()
    {
        if (entrada.EscapeEsteFrame) return true;
        // Mantener B1 + B4 para salir, pero NO mientras se sostiene una nota
        // (algunas canciones tienen sostenidas de B1 + B4 a la vez).
        if (entrada.Presionado[0] && entrada.Presionado[3] && !pista.HaySostenida) tiempoCombo += Time.deltaTime;
        else tiempoCombo = 0f;
        if (tiempoCombo > 2f) { tiempoCombo = 0f; return true; }
        return false;
    }

    void Avisar(string m, float segundos = 4f)
    {
        mensaje = m;
        mensajeHasta = Time.time + segundos;
    }

    void Popup(string texto, Color c) => ui.Popup(texto, c);

    void ActualizarHud(double t)
    {
        int leds = Mathf.Min(4, racha / notasPorLed);
        float prog = conductor.Duracion > 0 ? (float)(t / conductor.Duracion) : 0f;
        string dif = NombreDificultad(cancion);
        ui.Hud(puntos, racha, Multiplicador, leds, racha >= rachaParpadeo, cancion.titulo, dif, ColorDificultad(dif), prog, t);
    }

    // ------------------------------------------------------------------ OnGUI
    // El menu, el puntaje del juego y los resultados viven en el Canvas (PianoHeroUI).
    // Aqui solo quedan el aviso de grabacion de charts (herramienta del equipo) y los mensajes.

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
                // Dibujado por el Canvas (ver ActualizarUIMenu).
                break;
            }

            case Estado.Grabando:
            {
                Caja(new Rect(0, 0, W, 130 * k), new Color(0.6f, 0, 0, 0.6f));
                GUI.Label(new Rect(0, 10 * k, W, 60 * k), "● GRABANDO CHART", sTitulo);
                GUI.Label(new Rect(0, 70 * k, W, 50 * k),
                    $"Notas: {grabador.Cantidad}   |   B1 + B4 (2 s) o Esc para terminar y guardar", sTexto);
                if (conductor.Tiempo < 0)
                    GUI.Label(new Rect(0, H * 0.3f, W, 100 * k), Mathf.CeilToInt((float)-conductor.Tiempo).ToString(), sGrande);
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
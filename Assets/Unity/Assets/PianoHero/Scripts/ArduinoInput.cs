using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Lee el Arduino por puerto serial en un hilo aparte y expone eventos de botones,
/// el valor del potenciometro y el control de LEDs. Tambien funciona con teclado
/// (D F J K = carriles, flechas = navegar menu, Esc = salir).
///
/// REQUISITO: Edit > Project Settings > Player > Other Settings >
///            Api Compatibility Level = .NET Framework
/// </summary>
public class ArduinoInput : MonoBehaviour
{
    public static ArduinoInput Instance { get; private set; }

    [Header("Puerto serial")]
    [Tooltip("Vacio = detectar automaticamente. Ejemplo: COM3 (mira el puerto en el IDE de Arduino).")]
    public string puerto = "";
    public int baudios = 115200;

    [Header("Teclado de respaldo")]
    public bool tecladoActivo = true;

    /// <summary>carril 0-3, instante del evento (reloj interno, segundos)</summary>
    public event Action<int, double> OnPress;
    public event Action<int, double> OnRelease;

    public float Pot01 { get; private set; }
    public bool PotRecibido { get; private set; }
    public bool Conectado { get; private set; }
    volatile string estadoConexion = "Buscando Arduino...";
    public string EstadoConexion { get => estadoConexion; private set => estadoConexion = value; }
    public readonly bool[] Presionado = new bool[4];

    /// <summary>-1 / 0 / +1 en el frame en que se presiona una flecha.</summary>
    public int Navegacion { get; private set; }
    public bool EscapeEsteFrame { get; private set; }

    static readonly Stopwatch reloj = Stopwatch.StartNew();
    /// <summary>Reloj de alta precision compartido por todo el juego.</summary>
    public static double Ahora => reloj.Elapsed.TotalSeconds;

    struct Evento { public char tipo; public int valor; public int dato; public double t; }

    readonly ConcurrentQueue<Evento> cola = new ConcurrentQueue<Evento>();
    volatile SerialPort sp;
    Thread hilo;
    volatile bool corriendo;
    int ledsEnviados = -1;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => Conectar();

    public void Conectar()
    {
        Desconectar();
        corriendo = true;
        hilo = new Thread(HiloConexion) { IsBackground = true, Name = "ArduinoSerial" };
        hilo.Start();
    }

    // ---------- Todo lo del puerto ocurre en este hilo (no traba el juego) ----------

    void HiloConexion()
    {
        while (corriendo)
        {
            if (sp == null && !BuscarArduino())
            {
                // Nada encontrado: reintenta cada 3 s (por si lo conectas despues)
                for (int i = 0; i < 30 && corriendo; i++) Thread.Sleep(100);
                continue;
            }
            if (!LeerHastaDesconexion()) Cerrar();
        }
    }

    bool BuscarArduino()
    {
        string[] puertos;
        try { puertos = SerialPort.GetPortNames(); }
        catch (Exception e) { Estado("Error listando puertos: " + e.Message); return false; }

        var candidatos = new List<string>();
        if (!string.IsNullOrWhiteSpace(puerto)) candidatos.Add(puerto.Trim().ToUpper());
        else
        {
            // Los mas altos primero (el Uno suele ser el ultimo en aparecer); COM1 al final
            var orden = new List<string>(puertos);
            orden.Sort((x, y) => NumeroCom(y).CompareTo(NumeroCom(x)));
            foreach (var p in orden) if (p != "COM1") candidatos.Add(p);
            if (orden.Contains("COM1")) candidatos.Add("COM1");
        }

        string lista = puertos.Length == 0 ? "ninguno" : string.Join(", ", puertos);
        if (candidatos.Count == 0)
        {
            Estado($"No hay puertos COM. Revisa el cable USB. Puertos vistos: {lista}. (Teclado: D F J K)");
            return false;
        }

        string errores = "";
        foreach (var nombre in candidatos)
        {
            if (!corriendo) return false;
            Estado($"Probando {nombre}...  (puertos vistos: {lista})");
            string error = Probar(nombre);
            if (error == null)
            {
                Estado($"Arduino conectado en {nombre}");
                return true;
            }
            errores += $"{nombre}: {error}  ";
        }
        Estado($"No se encontro el Arduino. {errores}| Puertos vistos: {lista}. Reintentando... (Teclado: D F J K)");
        return false;
    }

    static int NumeroCom(string p)
    {
        int n = 0;
        foreach (char c in p) if (char.IsDigit(c)) n = n * 10 + (c - '0');
        return n;
    }

    /// <summary>Abre el puerto y espera hasta 7 s un mensaje valido. null = exito.</summary>
    string Probar(string nombre)
    {
        SerialPort p = null;
        try
        {
            // Windows necesita este prefijo para COM10 en adelante
            string ruta = (NumeroCom(nombre) >= 10 && nombre.StartsWith("COM")) ? @"\\.\" + nombre : nombre;
            p = new SerialPort(ruta, baudios)
            {
                ReadTimeout = 50,
                WriteTimeout = 100,
                NewLine = "\n",
                DtrEnable = true,   // reinicia el Uno: manda READY a los ~2 s
                RtsEnable = true
            };
            p.Open();
        }
        catch (UnauthorizedAccessException)
        {
            try { p?.Dispose(); } catch { }
            return "ocupado (¿Monitor Serie del IDE abierto?)";
        }
        catch (Exception e)
        {
            try { p?.Dispose(); } catch { }
            return "no abre (" + e.Message + ")";
        }

        sp = p;
        lineaParcial.Clear();
        recibido.Clear();
        // El Uno se reinicia al abrir el puerto (~2 s). No se le envia nada mientras arranca.
        var limite = Ahora + 7.0;
        bool pedido = false;
        while (corriendo && Ahora < limite)
        {
            // Solo si no ha llegado nada despues de 4 s (placas que no se reinician)
            if (!pedido && Ahora > limite - 3.0) { try { p.Write("?\n"); } catch { } pedido = true; }
            try
            {
                foreach (var linea in LeerLineas())
                {
                    if (linea == "READY" || linea == "H" || linea.StartsWith("P:") || (linea.Length >= 4 && linea[0] == 'B'))
                    {
                        Procesar(linea, Ahora);
                        cola.Enqueue(new Evento { tipo = 'R' });
                        return null;
                    }
                }
            }
            catch (Exception e) { Cerrar(); return "error leyendo (" + e.Message + ")"; }
        }
        Cerrar();
        if (recibido.Length == 0) return "no responde (no llego ningun dato; ¿esta subido el sketch de PianoHero?)";
        string r = recibido.ToString();
        bool basura = false;
        foreach (char c in r) if (c < 9 || c > 126) { basura = true; break; }
        return basura
            ? "llegan datos ilegibles: revisa que el sketch use Serial.begin(115200)"
            : "no responde (llego: \"" + (r.Length > 40 ? r.Substring(0, 40) : r) + "\")";
    }

    readonly byte[] bufLectura = new byte[256];
    readonly System.Text.StringBuilder lineaParcial = new System.Text.StringBuilder();
    readonly List<string> lineas = new List<string>();

    readonly System.Text.StringBuilder recibido = new System.Text.StringBuilder();

    /// <summary>Lee lo que haya llegado sin bloquear (BytesToRead es lo mas confiable en Windows).</summary>
    List<string> LeerLineas()
    {
        lineas.Clear();
        int disponibles = sp.BytesToRead;
        if (disponibles <= 0) { Thread.Sleep(1); return lineas; }
        int n = sp.Read(bufLectura, 0, Math.Min(disponibles, bufLectura.Length));
        for (int i = 0; i < n; i++)
        {
            char c = (char)bufLectura[i];
            if (recibido.Length < 80) recibido.Append(c == '\n' || c == '\r' ? ' ' : c);
            if (c == '\n' || c == '\r')
            {
                if (lineaParcial.Length > 0) { lineas.Add(lineaParcial.ToString().Trim()); lineaParcial.Clear(); }
            }
            else if (lineaParcial.Length < 64) lineaParcial.Append(c);
        }
        return lineas;
    }

    bool LeerHastaDesconexion()
    {
        while (corriendo)
        {
            try
            {
                var ls = LeerLineas();
                double t = Ahora;   // marca de tiempo lo mas cerca posible de la llegada
                foreach (var l in ls) Procesar(l, t);
            }
            catch (Exception)
            {
                if (corriendo) cola.Enqueue(new Evento { tipo = 'X' });
                return false;
            }
        }
        return true;
    }

    void Estado(string s)
    {
        EstadoConexion = s;
        UnityEngine.Debug.Log("[Arduino] " + s);
    }

    void Procesar(string l, double t)
    {
        if (l.Length >= 4 && l[0] == 'B' && l[2] == ':')
        {
            int c = l[1] - '1';
            if (c >= 0 && c < 4) cola.Enqueue(new Evento { tipo = 'B', valor = c, dato = l[3] == '1' ? 1 : 0, t = t });
        }
        else if (l.StartsWith("P:") && int.TryParse(l.Substring(2), out int v))
        {
            cola.Enqueue(new Evento { tipo = 'P', valor = v });
        }
        else if (l == "READY")
        {
            cola.Enqueue(new Evento { tipo = 'R' });
        }
    }

    void Update()
    {
        Navegacion = 0;
        EscapeEsteFrame = false;

        while (cola.TryDequeue(out var e))
        {
            switch (e.tipo)
            {
                case 'B': Cambiar(e.valor, e.dato == 1, e.t); break;
                case 'P': Pot01 = Mathf.Clamp01(e.valor / 1023f); PotRecibido = true; break;
                case 'R':
                    Conectado = true;
                    ledsEnviados = -1;
                    Enviar("?");
                    break;
                case 'X':
                    Conectado = false;
                    Estado("Se perdio la conexion con el Arduino. Reintentando... (Teclado: D F J K)");
                    break;
            }
        }

        if (tecladoActivo) LeerTeclado();
    }

    void LeerTeclado()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        Key[] teclas = { Key.D, Key.F, Key.J, Key.K };
        for (int i = 0; i < 4; i++)
        {
            if (kb[teclas[i]].wasPressedThisFrame) Cambiar(i, true, Ahora);
            if (kb[teclas[i]].wasReleasedThisFrame) Cambiar(i, false, Ahora);
        }
        if (kb.leftArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Navegacion = -1;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Navegacion = 1;
        if (kb.escapeKey.wasPressedThisFrame) EscapeEsteFrame = true;
#else
        KeyCode[] teclas = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        for (int i = 0; i < 4; i++)
        {
            if (Input.GetKeyDown(teclas[i])) Cambiar(i, true, Ahora);
            if (Input.GetKeyUp(teclas[i])) Cambiar(i, false, Ahora);
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)) Navegacion = -1;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)) Navegacion = 1;
        if (Input.GetKeyDown(KeyCode.Escape)) EscapeEsteFrame = true;
#endif
    }

    void Cambiar(int carril, bool abajo, double t)
    {
        if (Presionado[carril] == abajo) return;
        Presionado[carril] = abajo;
        if (abajo) OnPress?.Invoke(carril, t);
        else OnRelease?.Invoke(carril, t);
    }

    /// <summary>0-4 LEDs encendidos, 5 = los cuatro parpadeando.</summary>
    public void SetLeds(int n)
    {
        n = Mathf.Clamp(n, 0, 5);
        if (n == ledsEnviados) return;
        if (Enviar("L" + n)) ledsEnviados = n;
    }

    bool Enviar(string msg)
    {
        var p = sp;
        if (p == null || !Conectado) return false;
        try { p.Write(msg + "\n"); return true; }
        catch { return false; }
    }

    void Cerrar()
    {
        var p = sp;
        sp = null;
        if (p == null) return;
        try { if (p.IsOpen) { p.Write("L0\n"); p.Close(); } } catch { }
        try { p.Dispose(); } catch { }
    }

    void Desconectar()
    {
        corriendo = false;
        if (hilo != null && hilo.IsAlive) hilo.Join(500);
        Cerrar();
        hilo = null;
        Conectado = false;
    }

    void OnDestroy() { if (Instance == this) Desconectar(); }
    void OnApplicationQuit() => Desconectar();
}

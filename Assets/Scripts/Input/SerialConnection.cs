using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;

public class SerialConnection
{
    const int ReadTimeoutMs = 50;
    const int WriteTimeoutMs = 100;
    const double HandshakeSeconds = 7.0;
    const double StatusRequestDelaySeconds = 4.0;
    const int RetryDelayMs = 3000;
    const int RetryStepMs = 100;
    const int IdleSleepMs = 1;
    const int CloseWaitMs = 500;
    const int ReadBufferSize = 256;
    const int MaxLineLength = 64;
    const int MaxEchoLength = 80;
    const int ExtendedPortNumber = 10;
    const string ExtendedPortPrefix = @"\\.\";
    const string ComPrefix = "COM";
    const string RarelyArduinoPort = "COM1";
    const string LogPrefix = "[Arduino] ";

    readonly string preferredPort;
    readonly int baudRate;
    readonly ConcurrentQueue<SerialMessage> messages = new ConcurrentQueue<SerialMessage>();
    readonly byte[] readBuffer = new byte[ReadBufferSize];
    readonly StringBuilder partialLine = new StringBuilder();
    readonly StringBuilder receivedEcho = new StringBuilder();
    readonly List<string> completedLines = new List<string>();

    volatile SerialPort port;
    volatile bool running;
    volatile string status = "Buscando Arduino...";
    Thread worker;

    public SerialConnection(string preferredPort, int baudRate)
    {
        this.preferredPort = preferredPort;
        this.baudRate = baudRate;
    }

    public string Status => status;

    public void Open()
    {
        Close();
        running = true;
        worker = new Thread(Run) { IsBackground = true, Name = "ArduinoSerial" };
        worker.Start();
    }

    public void Close()
    {
        running = false;
        if (worker != null && worker.IsAlive) worker.Join(CloseWaitMs);
        worker = null;
        ClosePort();
    }

    public bool TryDequeue(out SerialMessage message) => messages.TryDequeue(out message);

    public bool Send(string text)
    {
        SerialPort current = port;
        if (current == null) return false;
        try
        {
            current.Write(text + "\n");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    void Run()
    {
        while (running)
        {
            if (port == null && !FindArduino())
            {
                WaitBeforeRetry();
                continue;
            }
            if (!ReadUntilDisconnected()) ClosePort();
        }
    }

    void WaitBeforeRetry()
    {
        for (int waited = 0; waited < RetryDelayMs && running; waited += RetryStepMs)
            Thread.Sleep(RetryStepMs);
    }

    bool FindArduino()
    {
        string[] available;
        try
        {
            available = SerialPort.GetPortNames();
        }
        catch (Exception e)
        {
            SetStatus("Error listando puertos: " + e.Message);
            return false;
        }

        string seen = available.Length == 0 ? "ninguno" : string.Join(", ", available);
        List<string> candidates = OrderCandidates(available);
        if (candidates.Count == 0)
        {
            SetStatus($"No hay puertos COM. Revisa el cable USB. Puertos vistos: {seen}. (Teclado: D F J K)");
            return false;
        }

        var errors = new StringBuilder();
        foreach (string name in candidates)
        {
            if (!running) return false;
            SetStatus($"Probando {name}...  (puertos vistos: {seen})");
            string error = TryConnect(name);
            if (error == null)
            {
                SetStatus($"Arduino conectado en {name}");
                return true;
            }
            errors.Append($"{name}: {error}  ");
        }
        SetStatus($"No se encontro el Arduino. {errors}| Puertos vistos: {seen}. Reintentando... (Teclado: D F J K)");
        return false;
    }

    List<string> OrderCandidates(string[] available)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredPort))
        {
            candidates.Add(preferredPort.Trim().ToUpperInvariant());
            return candidates;
        }

        // El Arduino suele ser el puerto con numero mas alto; COM1 casi nunca lo es.
        var sorted = new List<string>(available);
        sorted.Sort((a, b) => PortNumber(b).CompareTo(PortNumber(a)));
        foreach (string name in sorted)
            if (name != RarelyArduinoPort) candidates.Add(name);
        if (sorted.Contains(RarelyArduinoPort)) candidates.Add(RarelyArduinoPort);
        return candidates;
    }

    static int PortNumber(string name)
    {
        int number = 0;
        foreach (char c in name)
            if (char.IsDigit(c)) number = number * 10 + (c - '0');
        return number;
    }

    string TryConnect(string name)
    {
        string openError = OpenPort(name);
        return openError ?? WaitForHandshake();
    }

    string OpenPort(string name)
    {
        SerialPort candidate = null;
        try
        {
            // Windows necesita este prefijo desde COM10 en adelante.
            bool extended = name.StartsWith(ComPrefix) && PortNumber(name) >= ExtendedPortNumber;
            candidate = new SerialPort(extended ? ExtendedPortPrefix + name : name, baudRate)
            {
                ReadTimeout = ReadTimeoutMs,
                WriteTimeout = WriteTimeoutMs,
                NewLine = "\n",
                DtrEnable = true,
                RtsEnable = true
            };
            candidate.Open();
        }
        catch (UnauthorizedAccessException)
        {
            candidate?.Dispose();
            return "ocupado (Monitor Serie del IDE abierto?)";
        }
        catch (Exception e)
        {
            candidate?.Dispose();
            return "no abre (" + e.Message + ")";
        }

        port = candidate;
        partialLine.Clear();
        receivedEcho.Clear();
        return null;
    }

    string WaitForHandshake()
    {
        // Al abrir el puerto el Uno se reinicia (~2 s): no se le pide estado hasta que pueda responder.
        double start = GameClock.Now;
        bool statusRequested = false;
        while (running && GameClock.Now - start < HandshakeSeconds)
        {
            if (!statusRequested && GameClock.Now - start > StatusRequestDelaySeconds)
                statusRequested = Send(SerialProtocol.StatusRequest);

            try
            {
                foreach (string line in ReadLines())
                {
                    if (!SerialProtocol.IsKnownLine(line)) continue;
                    EnqueueLine(line, GameClock.Now);
                    messages.Enqueue(new SerialMessage { Type = SerialMessageType.Ready });
                    return null;
                }
            }
            catch (Exception e)
            {
                ClosePort();
                return "error leyendo (" + e.Message + ")";
            }
        }
        ClosePort();
        return DescribeSilence();
    }

    string DescribeSilence()
    {
        if (receivedEcho.Length == 0) return "no responde (no llego ningun dato; esta subido el sketch de PianoHero?)";

        string echo = receivedEcho.ToString();
        foreach (char c in echo)
            if (c < '\t' || c > '~') return "llegan datos ilegibles: revisa que el sketch use Serial.begin(115200)";
        return "no responde (llego: \"" + echo + "\")";
    }

    bool ReadUntilDisconnected()
    {
        while (running)
        {
            try
            {
                List<string> lines = ReadLines();
                double arrival = GameClock.Now;
                foreach (string line in lines) EnqueueLine(line, arrival);
            }
            catch (Exception)
            {
                if (running)
                {
                    messages.Enqueue(new SerialMessage { Type = SerialMessageType.ConnectionLost });
                    SetStatus("Se perdio la conexion con el Arduino. Reintentando... (Teclado: D F J K)");
                }
                return false;
            }
        }
        return true;
    }

    // BytesToRead es la forma mas confiable de leer sin bloquear en Windows.
    List<string> ReadLines()
    {
        completedLines.Clear();
        int available = port.BytesToRead;
        if (available <= 0)
        {
            Thread.Sleep(IdleSleepMs);
            return completedLines;
        }

        int count = port.Read(readBuffer, 0, Math.Min(available, readBuffer.Length));
        for (int i = 0; i < count; i++) AppendChar((char)readBuffer[i]);
        return completedLines;
    }

    void AppendChar(char c)
    {
        bool endOfLine = c == '\n' || c == '\r';
        if (receivedEcho.Length < MaxEchoLength) receivedEcho.Append(endOfLine ? ' ' : c);

        if (!endOfLine)
        {
            if (partialLine.Length < MaxLineLength) partialLine.Append(c);
            return;
        }
        if (partialLine.Length == 0) return;
        completedLines.Add(partialLine.ToString().Trim());
        partialLine.Clear();
    }

    void EnqueueLine(string line, double time)
    {
        if (SerialProtocol.TryParse(line, time, out SerialMessage message)) messages.Enqueue(message);
    }

    void ClosePort()
    {
        SerialPort current = port;
        port = null;
        if (current == null) return;
        try
        {
            if (current.IsOpen)
            {
                current.Write(SerialProtocol.LedCommand(0) + "\n");
                current.Close();
            }
            current.Dispose();
        }
        catch (Exception e)
        {
            // Pasa cuando se desconecta el cable: el puerto ya no existe.
            UnityEngine.Debug.LogWarning(LogPrefix + "No se pudo cerrar el puerto: " + e.Message);
        }
    }

    void SetStatus(string text)
    {
        status = text;
        UnityEngine.Debug.Log(LogPrefix + text);
    }
}

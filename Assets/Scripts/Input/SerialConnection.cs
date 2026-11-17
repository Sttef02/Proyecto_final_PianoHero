using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;

// Busca el Arduino, se conecta y lee sus mensajes en un hilo aparte para no trabar el juego.
public class SerialConnection
{
    const int ReadTimeoutMs = 50;
    const int WriteTimeoutMs = 100;
    const double HandshakeSeconds = 7.0;
    const double StatusRequestDelaySeconds = 4.0;
    const int RetryDelayMs = 3000;
    const int RetryStepMs = 100;
    const int CloseWaitMs = 500;
    const string LineEnding = "\n";
    const string ThreadName = "ArduinoSerial";
    const string LogPrefix = "[Arduino] ";
    const string KeyboardHint = "(Teclado: D F J K)";

    readonly string preferredPort;
    readonly int baudRate;
    readonly ConcurrentQueue<SerialMessage> messages = new ConcurrentQueue<SerialMessage>();
    readonly SerialLineReader reader = new SerialLineReader();

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
        worker = new Thread(Run) { IsBackground = true, Name = ThreadName };
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
            current.Write(text + LineEnding);
            return true;
        }
        catch (Exception)
        {
            // Si el cable se desconecto, el hilo de lectura lo detecta y vuelve a buscar el Arduino.
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
        List<string> candidates = SerialPortList.Candidates(available, preferredPort);
        if (candidates.Count == 0)
        {
            SetStatus($"No hay puertos COM. Revisa el cable USB. Puertos vistos: {seen}. {KeyboardHint}");
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
        SetStatus($"No se encontro el Arduino. {errors}| Puertos vistos: {seen}. Reintentando... {KeyboardHint}");
        return false;
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
            candidate = new SerialPort(SerialPortList.SystemName(name), baudRate)
            {
                ReadTimeout = ReadTimeoutMs,
                WriteTimeout = WriteTimeoutMs,
                NewLine = LineEnding,
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
        reader.Reset();
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
                foreach (string line in reader.ReadLines(port))
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
        return reader.DescribeSilence();
    }

    bool ReadUntilDisconnected()
    {
        while (running)
        {
            try
            {
                List<string> lines = reader.ReadLines(port);
                double arrival = GameClock.Now;
                foreach (string line in lines) EnqueueLine(line, arrival);
            }
            catch (Exception)
            {
                if (running)
                {
                    messages.Enqueue(new SerialMessage { Type = SerialMessageType.ConnectionLost });
                    SetStatus($"Se perdio la conexion con el Arduino. Reintentando... {KeyboardHint}");
                }
                return false;
            }
        }
        return true;
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
                current.Write(SerialProtocol.LedCommand(0) + LineEnding);
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

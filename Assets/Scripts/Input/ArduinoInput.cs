using System;
using UnityEngine;

// Requiere Player Settings > Api Compatibility Level = .NET Framework (puerto serial).
public class ArduinoInput : MonoBehaviour
{
    public const int LaneCount = 4;
    public const int StarModeLedCommand = 5;

    const string ArduinoSource = "Arduino";
    const string KeyboardSource = "Teclado";
    const string LogPrefix = "[PianoHero] ";
    static readonly string[] ButtonNames = { "B1 (verde)", "B2 (rojo)", "B3 (amarillo)", "B4 (azul)" };

    [Header("Puerto serial")]
    [Tooltip("Vacio = detectar automaticamente. Ejemplo: COM3 (el mismo puerto del IDE de Arduino).")]
    public string portName = "";
    [Min(1)]
    public int baudRate = 115200;

    [Header("Teclado de respaldo")]
    public bool keyboardEnabled = true;

    [Header("Depuracion")]
    [Tooltip("Escribe en la Console de Unity cada boton que se presiona o se suelta.")]
    public bool logButtons = true;

    public event Action<int, double> ButtonPressed;
    public event Action<int, double> ButtonReleased;

    public readonly bool[] Pressed = new bool[LaneCount];
    public float Knob01 { get; private set; }
    public bool HasKnobValue { get; private set; }
    public bool IsConnected { get; private set; }
    public int Navigation { get; private set; }
    public bool EscapePressed { get; private set; }
    public string ConnectionStatus => serial != null ? serial.Status : "";

    SerialConnection serial;
    int lastLedCommand = -1;

    void Start()
    {
        serial = new SerialConnection(portName, baudRate);
        serial.Open();
    }

    void Update()
    {
        ProcessSerialMessages();
        Navigation = 0;
        EscapePressed = false;
        if (keyboardEnabled) ReadKeyboard();
    }

    public void SetLeds(int command)
    {
        command = Mathf.Clamp(command, 0, StarModeLedCommand);
        if (!IsConnected || command == lastLedCommand) return;
        if (serial.Send(SerialProtocol.LedCommand(command))) lastLedCommand = command;
    }

    void ProcessSerialMessages()
    {
        while (serial.TryDequeue(out SerialMessage message))
        {
            switch (message.Type)
            {
                case SerialMessageType.Button:
                    SetPressed(message.Index, message.Value == 1, message.Time, ArduinoSource);
                    break;
                case SerialMessageType.Knob:
                    Knob01 = Mathf.Clamp01(message.Value / (float)SerialProtocol.KnobMaxValue);
                    HasKnobValue = true;
                    break;
                case SerialMessageType.Ready:
                    IsConnected = true;
                    lastLedCommand = -1;
                    serial.Send(SerialProtocol.StatusRequest);
                    break;
                case SerialMessageType.ConnectionLost:
                    IsConnected = false;
                    break;
            }
        }
    }

    void ReadKeyboard()
    {
        for (int lane = 0; lane < LaneCount; lane++)
        {
            if (KeyboardInput.LaneDown(lane)) SetPressed(lane, true, GameClock.Now, KeyboardSource);
            if (KeyboardInput.LaneUp(lane)) SetPressed(lane, false, GameClock.Now, KeyboardSource);
        }
        Navigation = KeyboardInput.NavigationDirection();
        EscapePressed = KeyboardInput.EscapeDown();
    }

    void SetPressed(int lane, bool down, double time, string source)
    {
        if (Pressed[lane] == down) return;
        Pressed[lane] = down;
        if (logButtons) Debug.Log($"{LogPrefix}{ButtonNames[lane]} {(down ? "PRESIONADO" : "soltado")}  -  {source}");

        if (down) ButtonPressed?.Invoke(lane, time);
        else ButtonReleased?.Invoke(lane, time);
    }

    void OnDestroy() => serial?.Close();

    void OnApplicationQuit() => serial?.Close();
}

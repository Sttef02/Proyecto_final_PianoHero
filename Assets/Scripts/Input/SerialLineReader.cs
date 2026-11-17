using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;

// Junta los bytes que llegan del puerto en lineas completas.
public class SerialLineReader
{
    const int ReadBufferSize = 256;
    const int MaxLineLength = 64;
    const int MaxEchoLength = 80;
    const int IdleSleepMs = 1;
    const char MinReadableChar = '\t';
    const char MaxReadableChar = '~';
    const char LineFeed = '\n';
    const char CarriageReturn = '\r';
    const char EchoLineBreak = ' ';

    readonly byte[] readBuffer = new byte[ReadBufferSize];
    readonly StringBuilder partialLine = new StringBuilder();
    readonly StringBuilder receivedEcho = new StringBuilder();
    readonly List<string> completedLines = new List<string>();

    public void Reset()
    {
        partialLine.Clear();
        receivedEcho.Clear();
    }

    // BytesToRead es la forma mas confiable de leer sin bloquear en Windows.
    public List<string> ReadLines(SerialPort port)
    {
        completedLines.Clear();
        int available = port.BytesToRead;
        if (available <= 0)
        {
            Thread.Sleep(IdleSleepMs);
            return completedLines;
        }

        int count = port.Read(readBuffer, 0, Math.Min(available, readBuffer.Length));
        for (int i = 0; i < count; i++) Append((char)readBuffer[i]);
        return completedLines;
    }

    // Explica por que no hubo respuesta, con lo poco que alcanzo a llegar.
    public string DescribeSilence()
    {
        if (receivedEcho.Length == 0) return "no responde (no llego ningun dato; esta subido el sketch de PianoHero?)";

        string echo = receivedEcho.ToString();
        foreach (char character in echo)
            if (character < MinReadableChar || character > MaxReadableChar)
                return "llegan datos ilegibles: revisa que el sketch use Serial.begin(115200)";
        return "no responde (llego: \"" + echo + "\")";
    }

    void Append(char character)
    {
        bool endOfLine = character == LineFeed || character == CarriageReturn;
        if (receivedEcho.Length < MaxEchoLength) receivedEcho.Append(endOfLine ? EchoLineBreak : character);

        if (!endOfLine)
        {
            if (partialLine.Length < MaxLineLength) partialLine.Append(character);
            return;
        }
        if (partialLine.Length == 0) return;
        completedLines.Add(partialLine.ToString().Trim());
        partialLine.Clear();
    }
}

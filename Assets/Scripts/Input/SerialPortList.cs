using System.Collections.Generic;
using System.Text;

public static class SerialPortList
{
    const string ComPrefix = "COM";
    const string RarelyArduinoPort = "COM1";
    const int ExtendedPortNumber = 10;
    const string ExtendedPortPrefix = @"\\.\";

    // Orden en que se prueban los puertos. Si hay uno elegido en el Inspector, solo se prueba ese.
    public static List<string> Candidates(string[] available, string preferredPort)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredPort))
        {
            candidates.Add(preferredPort.Trim().ToUpperInvariant());
            return candidates;
        }

        // El Arduino suele ser el puerto con numero mas alto; COM1 casi nunca lo es.
        var sorted = new List<string>(available);
        sorted.Sort((first, second) => PortNumber(second).CompareTo(PortNumber(first)));
        foreach (string name in sorted)
            if (name != RarelyArduinoPort) candidates.Add(name);
        if (sorted.Contains(RarelyArduinoPort)) candidates.Add(RarelyArduinoPort);
        return candidates;
    }

    // Windows necesita el prefijo \\.\ desde COM10 en adelante.
    public static string SystemName(string name)
    {
        bool extended = name.StartsWith(ComPrefix) && PortNumber(name) >= ExtendedPortNumber;
        return extended ? ExtendedPortPrefix + name : name;
    }

    static int PortNumber(string name)
    {
        var digits = new StringBuilder();
        foreach (char character in name)
            if (char.IsDigit(character)) digits.Append(character);
        return int.TryParse(digits.ToString(), out int number) ? number : 0;
    }
}

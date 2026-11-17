public static class SerialProtocol
{
    public const int KnobMaxValue = 1023;
    public const string StatusRequest = "?";

    const string ReadyLine = "READY";
    const string HeartbeatLine = "H";
    const string KnobPrefix = "P:";
    const string LedPrefix = "L";
    const char ButtonPrefix = 'B';
    const char ButtonSeparator = ':';
    const char PressedFlag = '1';
    const int ButtonLineLength = 4;
    const int ButtonCount = 4;

    public static bool TryParse(string line, double time, out SerialMessage message)
    {
        message = new SerialMessage { Time = time };

        if (line == ReadyLine)
        {
            message.Type = SerialMessageType.Ready;
            return true;
        }
        if (line.StartsWith(KnobPrefix) && int.TryParse(line.Substring(KnobPrefix.Length), out int knobValue))
        {
            message.Type = SerialMessageType.Knob;
            message.Value = knobValue;
            return true;
        }
        return TryParseButton(line, ref message);
    }

    public static bool IsKnownLine(string line) => line == HeartbeatLine || TryParse(line, 0, out _);

    public static string LedCommand(int value) => LedPrefix + value;

    static bool TryParseButton(string line, ref SerialMessage message)
    {
        if (line.Length < ButtonLineLength || line[0] != ButtonPrefix || line[2] != ButtonSeparator) return false;

        int button = line[1] - '1';
        if (button < 0 || button >= ButtonCount) return false;

        message.Type = SerialMessageType.Button;
        message.Index = button;
        message.Value = line[3] == PressedFlag ? 1 : 0;
        return true;
    }
}

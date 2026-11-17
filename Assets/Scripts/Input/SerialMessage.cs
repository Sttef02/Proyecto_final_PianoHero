public enum SerialMessageType
{
    Button,
    Knob,
    Ready,
    ConnectionLost
}

public struct SerialMessage
{
    public SerialMessageType Type;
    public int Index;
    public int Value;
    public double Time;
}

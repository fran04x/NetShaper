namespace NetShaper.Abstractions
{
    public enum LogCode : ushort
    {
        None = 0,
        EngineStarted = 1,
        EngineStopped = 2,
        PacketProcessed = 3,
        RecvFailed = 4,
        SendFailed = 5,
        InvalidPacket = 6,
        OperationAborted = 7,
        InvalidHandle = 8,
        InvalidParameter = 9
    }
}

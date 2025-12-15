namespace NetShaper.Abstractions
{
    public interface IPacketLogger
    {
        void Log(in PacketLogEntry entry);
    }
}
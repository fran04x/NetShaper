// NetShaper.Native/WinDivertAddress.cs
using System.Runtime.InteropServices;

namespace NetShaper.Native
{
    /// <summary>
    /// Internal WinDivert address structure.
    /// This is an implementation detail specific to WinDivert and should not leak to abstractions.
    /// Layout matches WINDIVERT_ADDRESS (WinDivert 2.2).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct WinDivertAddress
    {
        public long Timestamp;
        public uint IfIdx;
        public uint SubIfIdx;
        public byte Direction;
        public byte Loopback;
        public byte Impostor;
        public byte IPChecksum;
        public byte TCPChecksum;
        public byte UDPChecksum;
        public ushort Reserved1;
        public uint Reserved2;
    }
}

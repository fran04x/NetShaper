// NetShaper.Abstractions/IPacketCapture.cs
using System;
using System.Runtime.InteropServices;

namespace NetShaper.Abstractions
{
    public interface IPacketCapture : IDisposable
    {
        CaptureResult Open(string filter);
        CaptureResult Receive(Span<byte> buffer, out uint length, ref PacketMetadata metadata);
        CaptureResult ReceiveBatch(Span<byte> buffer, Span<PacketMetadata> metadataArray, out uint batchLength, out int packetCount);
        CaptureResult Send(ReadOnlySpan<byte> buffer, ref PacketMetadata metadata);
        void Shutdown();
        void CalculateChecksums(Span<byte> buffer, uint length, ref PacketMetadata metadata);
    }

    public enum CaptureResult
    {
        Success = 0,
        InvalidFilter = 1,
        InvalidHandle = 2,
        InvalidParameter = 3,
        OperationAborted = 4,
        ElementNotFound = 5,
        BufferTooSmall = 6,
        Unknown = 99
    }

    /// <summary>
    /// Packet metadata structure.
    /// Layout matches WINDIVERT_ADDRESS (WinDivert 2.2) for zero-copy interop.
    /// StructLayout is acceptable here as it defines a performance-critical contract.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketMetadata
    {
        public long Timestamp;
        public uint InterfaceIndex;
        public uint SubInterfaceIndex;
        public byte Direction;
        public byte Loopback;
        public byte Impostor;
        public byte IpChecksum;
        public byte TcpChecksum;
        public byte UdpChecksum;
        public ushort Reserved1;
        public uint Reserved2;
        
        // BATCH MODE SUPPORT: Length of packet in batch (populated by ReceiveBatch)
        public uint Length;
    }
}
// NetShaper.Native/WinDivertAdapter.cs
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NetShaper.Abstractions;

namespace NetShaper.Native
{
    public sealed partial class WinDivertAdapter : IPacketCapture
    {
        private const int MaxFilterLength = PacketCaptureConstants.MaxFilterLength;
        private const int ShutdownRecv = 1;

        private WinDivertHandle? _handle;
        private int _disposed;
        private int _shutdownFlag;  // 0 = not shutdown, 1 = shutdown initiated
        private int _activeOperations;  // Counter for concurrent Receive/Send operations

        static WinDivertAdapter()
        {
            ValidateStructLayout();
        }

        public CaptureResult Open(string filter)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                return CaptureResult.InvalidHandle;
            if (string.IsNullOrWhiteSpace(filter))
                return CaptureResult.InvalidFilter;
            if (filter.Length > MaxFilterLength)
                return CaptureResult.InvalidFilter;
            if (_handle != null && !_handle.IsInvalid)
                return CaptureResult.InvalidHandle;

            WinDivertHandle? handle = null;

            try
            {
                handle = WinDivertOpenNative(filter, 0, 0, 0);
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    // REMOVED: Console.WriteLine to prevent allocations in hot-path
                    // Error code is already returned via MapError(error) below
                    handle.Dispose();
                    return MapError(error);
                }

                _handle = handle;
            
                // Reset shutdown flag to allow re-open after shutdown (reentrancy support)
                Interlocked.Exchange(ref _shutdownFlag, 0);
            
                return CaptureResult.Success;
            }
            catch (Exception)
            {
                // REMOVED: Console.WriteLine to prevent allocations in hot-path
                // Exception is re-thrown to caller for proper handling
                handle?.Dispose();
                throw;
            }
        }

        public unsafe CaptureResult Receive(
            Span<byte> buffer,
            out uint length,
            ref PacketMetadata metadata)
        {
            length = 0;
            
            // Prevent disposal while operation is active
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                return CaptureResult.InvalidHandle;
            
            Interlocked.Increment(ref _activeOperations);
            
            try
            {
                WinDivertHandle? handle = _handle;
                if (handle == null || handle.IsInvalid)
                    return CaptureResult.InvalidHandle;
                
                if (buffer.IsEmpty)
                    return CaptureResult.BufferTooSmall;

                fixed (byte* pBuf = buffer)
                {
                    WinDivertAddress addr = ToWinDivertAddress(ref metadata);
                    WinDivertAddress* pAddr = &addr;

                    bool success = WinDivertRecvNative(
                        handle,
                        pBuf,
                        (uint)buffer.Length,
                        out length,
                        pAddr);

                    if (!success)
                        return MapError(Marshal.GetLastWin32Error());

                    metadata = FromWinDivertAddress(ref addr);
                    return CaptureResult.Success;
                }
            }
            catch (ObjectDisposedException)
            {
                return CaptureResult.InvalidHandle;
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
            }
        }

        public unsafe CaptureResult ReceiveBatch(
            Span<byte> buffer,
            Span<PacketMetadata> metadataArray,
            out uint batchLength,
            out int packetCount)
        {
            batchLength = 0;
            packetCount = 0;
            
            // Prevent disposal while operation is active
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                return CaptureResult.InvalidHandle;
            
            Interlocked.Increment(ref _activeOperations);
            
            try
            {
                WinDivertHandle? handle = _handle;
                if (handle == null || handle.IsInvalid)
                    return CaptureResult.InvalidHandle;
                
                if (buffer.IsEmpty || metadataArray.IsEmpty)
                    return CaptureResult.BufferTooSmall;

                const int MaxBatchSize = 64;
                int maxPackets = Math.Min(metadataArray.Length, MaxBatchSize);
                
                // Stack-allocate WinDivertAddress array for batch
                WinDivertAddress* pAddrArray = stackalloc WinDivertAddress[maxPackets];
                
                fixed (byte* pBuf = buffer)
                {
                    uint readLen = 0;  // ← CORRECTED: was ulong
                    uint addrLen = (uint)(maxPackets * sizeof(WinDivertAddress));
                    
                    // Call WinDivertRecvEx (batch mode)
                    bool success = WinDivertRecvExNative(
                        handle,
                        pBuf,
                        (uint)buffer.Length,
                        &readLen,  // ← CORRECTED: now uint* instead of ulong*
                        0,  // flags = 0 (synchronous)
                        pAddrArray,
                        &addrLen,
                        null);  // pOverlapped = null (synchronous)

                    if (!success)
                        return MapError(Marshal.GetLastWin32Error());

                    batchLength = readLen;  // ← No cast needed now
                    packetCount = (int)(addrLen / sizeof(WinDivertAddress));
                    
                    // Parse IP headers to extract packet lengths
                    // WinDivert packs packets tightly: [Pkt1][Pkt2][Pkt3]...
                    uint offset = 0;
                    for (int i = 0; i < packetCount; i++)
                    {
                        ref PacketMetadata meta = ref metadataArray[i];
                        ref WinDivertAddress addr = ref pAddrArray[i];
                        
                        // Convert WinDivertAddress to PacketMetadata
                        meta = FromWinDivertAddress(ref addr);
                        
                        // Parse packet length using helper method
                        uint packetLength = ParsePacketLength(pBuf, offset, readLen);
                        if (packetLength == 0)
                        {
                            meta.Length = 0;
                            break;  // Invalid or unknown packet
                        }
                        
                        meta.Length = packetLength;
                        offset += packetLength;
                    }

                    return CaptureResult.Success;
                }
            }
            catch (ObjectDisposedException)
            {
                return CaptureResult.InvalidHandle;
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
            }
        }

        public unsafe CaptureResult Send(
            ReadOnlySpan<byte> buffer,
            ref PacketMetadata metadata)
        {
            // Prevent disposal while operation is active
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                return CaptureResult.InvalidHandle;
            
            Interlocked.Increment(ref _activeOperations);
            
            try
            {
                WinDivertHandle? handle = _handle;
                if (handle == null || handle.IsInvalid)
                    return CaptureResult.InvalidHandle;
                
                if (buffer.IsEmpty)
                    return CaptureResult.InvalidParameter;

                fixed (byte* pBuf = buffer)
                {
                    WinDivertAddress addr = ToWinDivertAddress(ref metadata);
                    WinDivertAddress* pAddr = &addr;

                    bool success = WinDivertSendNative(
                        handle,
                        pBuf,
                        (uint)buffer.Length,
                        out uint _,
                        pAddr);

                    if (!success)
                        return MapError(Marshal.GetLastWin32Error());

                    return CaptureResult.Success;
                }
            }
            catch (ObjectDisposedException)
            {
                return CaptureResult.InvalidHandle;
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
            }
        }

        public void Shutdown()
        {
            // NOTE: Do NOT check _disposed here
            // Shutdown is a temporary pause operation, not final destruction
            // It must work even if the instance will be disposed later
            // This allows proper Start/Stop reentrancy as per rules v2 §71
            
            // Atomic shutdown flag - only one thread wins
            if (Interlocked.CompareExchange(ref _shutdownFlag, 1, 0) != 0)
                return;  // Already shutdown by another thread
            
            // Atomic swap: get handle and set to null in one operation
            WinDivertHandle? handle = Interlocked.Exchange(ref _handle, null);
            if (handle == null || handle.IsInvalid)
                return;

            // Shutdown reception first (unblocks WinDivertRecv calls)
            WinDivertShutdownNative(handle, ShutdownRecv);
            
            // Dispose handle (only this thread executes this)
            handle.Dispose();
        }

        public unsafe void CalculateChecksums(
            Span<byte> buffer,
            uint length,
            ref PacketMetadata metadata)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                return;
            if (buffer.IsEmpty || length == 0)
                return;

            fixed (byte* pBuf = buffer)
            {
                WinDivertAddress addr = ToWinDivertAddress(ref metadata);
                WinDivertAddress* pAddr = &addr;

                WinDivertHelperCalcChecksumsNative(pBuf, length, pAddr, 0);
            }
        }

        public void Dispose()
		{
			if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
			{
				// Wait for all active operations to complete before disposing
				// Uses SpinWait instead of locks (hot-path requirement)
				var spin = new System.Threading.SpinWait();
				while (Interlocked.CompareExchange(ref _activeOperations, 0, 0) != 0)
				{
					spin.SpinOnce();
					if (spin.Count > 10000)  // Timeout after ~10ms
						break;  // Force disposal if operations stuck
				}
				
				if (_handle != null && !_handle.IsInvalid)
				{
					// Shutdown orderly antes de cerrar handle (recv + send)
					WinDivertShutdownNative(_handle, NativeMethods.WinDivertShutdownBoth);
					_handle.Dispose();
				}
			}
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CaptureResult ValidateHandle(out WinDivertHandle? handle)
        {
            handle = null;
            
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
                return CaptureResult.InvalidHandle;

            handle = _handle;
            if (handle == null || handle.IsInvalid)
                return CaptureResult.InvalidHandle;

            return CaptureResult.Success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CaptureResult MapError(int code)
        {
            return code switch
            {
                995 => CaptureResult.OperationAborted,
                6 => CaptureResult.InvalidHandle,
                87 => CaptureResult.InvalidParameter,
                1168 => CaptureResult.ElementNotFound,
                _ => CaptureResult.Unknown
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint ParsePacketLength(byte* pPacket, uint offset, uint readLen)
        {
            if (offset >= readLen)
                return 0;

            byte* pPkt = pPacket + offset;
            byte versionIHL = pPkt[0];
            byte version = (byte)(versionIHL >> 4);

            return version switch
            {
                4 => ParseIPv4Length(pPkt),
                6 => ParseIPv6Length(pPkt),
                _ => 0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint ParseIPv4Length(byte* pPacket)
        {
            return (uint)((pPacket[2] << 8) | pPacket[3]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint ParseIPv6Length(byte* pPacket)
        {
            uint payloadLen = (uint)((pPacket[4] << 8) | pPacket[5]);
            return 40 + payloadLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static WinDivertAddress ToWinDivertAddress(ref PacketMetadata metadata)
        {
            return new WinDivertAddress
            {
                Timestamp = metadata.Timestamp,
                IfIdx = metadata.InterfaceIndex,
                SubIfIdx = metadata.SubInterfaceIndex,
                Direction = metadata.Direction,
                Loopback = metadata.Loopback,
                Impostor = metadata.Impostor,
                IPChecksum = metadata.IpChecksum,
                TCPChecksum = metadata.TcpChecksum,
                UDPChecksum = metadata.UdpChecksum,
                Reserved1 = metadata.Reserved1,
                Reserved2 = metadata.Reserved2
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PacketMetadata FromWinDivertAddress(ref WinDivertAddress addr)
        {
            return new PacketMetadata
            {
                Timestamp = addr.Timestamp,
                InterfaceIndex = addr.IfIdx,
                SubInterfaceIndex = addr.SubIfIdx,
                Direction = addr.Direction,
                Loopback = addr.Loopback,
                Impostor = addr.Impostor,
                IpChecksum = addr.IPChecksum,
                TcpChecksum = addr.TCPChecksum,
                UdpChecksum = addr.UDPChecksum,
                Reserved1 = addr.Reserved1,
                Reserved2 = addr.Reserved2
            };
        }

        [Conditional("DEBUG")]
        private static void ValidateStructLayout()
        {
            Debug.Assert(Marshal.SizeOf<WinDivertAddress>() == 28,
                "WinDivertAddress size mismatch - expected 28 bytes");
            Debug.Assert(Marshal.OffsetOf<WinDivertAddress>(nameof(WinDivertAddress.Timestamp)).ToInt32() == 0,
                "WinDivertAddress.Timestamp offset incorrect");
            Debug.Assert(Marshal.OffsetOf<WinDivertAddress>(nameof(WinDivertAddress.IfIdx)).ToInt32() == 8,
                "WinDivertAddress.IfIdx offset incorrect");
        }

        [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertOpen", StringMarshalling = StringMarshalling.Utf8)]
        private static partial WinDivertHandle WinDivertOpenNative(
            string filter,
            int layer,
            short priority,
            ulong flags);

        [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertRecv")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool WinDivertRecvNative(
            WinDivertHandle handle,
            byte* pPacket,
            uint packetLen,
            out uint readLen,
            WinDivertAddress* pAddr);

        [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertRecvEx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool WinDivertRecvExNative(
            WinDivertHandle handle,
            byte* pPacket,
            uint packetLen,
            uint* pRecvLen,      // ← CORRECTED: was ulong*, should be uint*
            ulong flags,
            WinDivertAddress* pAddr,
            uint* pAddrLen,      // ← CORRECTED: Input/Output, size in BYTES (not element count)
            void* pOverlapped);

        [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertSend")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool WinDivertSendNative(
            WinDivertHandle handle,
            byte* pPacket,
            uint packetLen,
            out uint writeLen,
            WinDivertAddress* pAddr);

        [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertShutdown")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool WinDivertShutdownNative(
            WinDivertHandle handle,
            int how);

        [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertHelperCalcChecksums")]
        private static unsafe partial void WinDivertHelperCalcChecksumsNative(
            byte* pPacket,
            uint packetLen,
            WinDivertAddress* pAddr,
            ulong flags);
    }
}
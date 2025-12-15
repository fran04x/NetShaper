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
            
            CaptureResult validation = ValidateHandle(out WinDivertHandle? handle);
            if (validation != CaptureResult.Success)
                return validation;
            
            if (buffer.IsEmpty)
                return CaptureResult.BufferTooSmall;

            try
            {
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
                // Handle was disposed during shutdown between our null check and usage
                // This is expected during rapid stop sequences - treat as InvalidHandle
                return CaptureResult.InvalidHandle;
            }
        }

        public unsafe CaptureResult Send(
            ReadOnlySpan<byte> buffer,
            ref PacketMetadata metadata)
        {
            CaptureResult validation = ValidateHandle(out WinDivertHandle? handle);
            if (validation != CaptureResult.Success)
                return validation;
            
            if (buffer.IsEmpty)
                return CaptureResult.InvalidParameter;

            try
            {
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
                // Handle was disposed during shutdown between our null check and usage
                // This is expected during rapid stop sequences - treat as InvalidHandle
                return CaptureResult.InvalidHandle;
            }
        }

        public void Shutdown()
        {
            // NOTE: Do NOT check _disposed here
            // Shutdown is a temporary pause operation, not final destruction
            // It must work even if the instance will be disposed later
            // This allows proper Start/Stop reentrancy as per rules v2 §71
            
            WinDivertHandle? handle = _handle;
            if (handle == null || handle.IsInvalid)
                return;

            // Shutdown reception first
            WinDivertShutdownNative(handle, ShutdownRecv);
            
            // Close and release the handle to allow restart
            _handle = null;
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
				if (_handle != null && !_handle.IsInvalid)
				{
					// Shutdown orderly antes de cerrar handle
					WinDivertShutdownNative(_handle, 0); // 0 = SHUT_RDWR
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
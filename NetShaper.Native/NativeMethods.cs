// NetShaper.Native\NativeMethods.cs
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NetShaper.Native
{
    public static partial class NativeMethods
    {
        private const string DllName = "WinDivert.dll";

        public const int LayerNetwork = 0;
        public const int WinDivertShutdownRecv = 1;
        public const int WinDivertShutdownSend = 2;
        public const int WinDivertShutdownBoth = 3;

        [LibraryImport(DllName, EntryPoint = "WinDivertOpen", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial WinDivertHandle WinDivertOpen(
            string filter,
            int layer,
            short priority,
            long flags);

        [LibraryImport(DllName, EntryPoint = "WinDivertShutdown", SetLastError = true)]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool WinDivertShutdown(
            WinDivertHandle handle, 
            int mode);
    }
}
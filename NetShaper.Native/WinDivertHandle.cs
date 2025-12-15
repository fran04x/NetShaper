using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NetShaper.Native
{
    public sealed partial class WinDivertHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public WinDivertHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return WinDivertClose(handle);
        }

        [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertClose", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool WinDivertClose(IntPtr handle);
    }

    // WinDivertAddress movido a NetShaper.Abstractions
}
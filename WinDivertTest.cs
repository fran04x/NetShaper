using System;
using System.Runtime.InteropServices;

namespace WinDivertTest
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Testing WinDivert.Open...");
            Console.WriteLine($"Running as: {Environment.UserName}");
            
            try
            {
                var handle = WinDivertOpen("outbound and udp.DstPort == 55556", 0, 0, 0);
                
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"ERROR: WinDivert.Open failed with Win32 error code: {error}");
                    Console.WriteLine($"Error description: {GetErrorDescription(error)}");
                    handle.Dispose();
                }
                else
                {
                    Console.WriteLine("SUCCESS: WinDivert.Open succeeded!");
                    handle.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        
        static string GetErrorDescription(int code)
        {
            return code switch
            {
                2 => "ERROR_FILE_NOT_FOUND - WinDivert64.sys missing",
                5 => "ERROR_ACCESS_DENIED - Need Administrator privileges",
                87 => "ERROR_INVALID_PARAMETER - Invalid filter",
                1168 => "ERROR_NOT_FOUND - Driver not found or not loaded",
                _ => $"Unknown error code {code}"
            };
        }
        
        [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertOpen", StringMarshalling = StringMarshalling.Utf8)]
        private static partial WinDivertHandle WinDivertOpen(
            string filter,
            int layer,
            short priority,
            ulong flags);
    }
    
    public class WinDivertHandle : SafeHandle
    {
        public WinDivertHandle() : base(IntPtr.Zero, true) { }
        
        public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);
        
        protected override bool ReleaseHandle()
        {
            return true;
        }
    }
}

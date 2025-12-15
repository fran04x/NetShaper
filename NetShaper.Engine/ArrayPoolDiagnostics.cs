// NetShaper.Engine/ArrayPoolDiagnostics.cs
using System.Diagnostics;
using System.Threading;

namespace NetShaper.Engine
{
    /// <summary>
    /// Diagnósticos para rastrear el balance de ArrayPool rent/return.
    /// Solo activo en builds DEBUG.
    /// </summary>
    internal static class ArrayPoolDiagnostics
    {
        private static int _rentCount;
        private static int _returnCount;

        public static void RecordRent()
        {
            Interlocked.Increment(ref _rentCount);
        }

        public static void RecordReturn()
        {
            Interlocked.Increment(ref _returnCount);
        }

        [Conditional("DEBUG")]
        public static void ValidateBalance()
        {
            // Simple read with memory barrier - good enough for DEBUG diagnostics
            // Potential race is acceptable since this is only for developer awareness
            int rents = Volatile.Read(ref _rentCount);
            int returns = Volatile.Read(ref _returnCount);
            
            Debug.Assert(rents == returns, 
                $"ArrayPool mismatch: Rent={rents}, Return={returns}");
        }
    }
}

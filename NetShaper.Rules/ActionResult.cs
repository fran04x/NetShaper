using System.Runtime.CompilerServices;

namespace NetShaper.Rules
{
    /// <summary>
    /// Accumulated result from rule pipeline evaluation.
    /// Values are max/OR accumulated, never overwritten.
    /// </summary>
    public struct ActionResult
    {
        /// <summary>Accumulated action flags.</summary>
        public ActionMask Mask;
        
        /// <summary>Maximum delay in ticks (Stopwatch.GetTimestamp units).</summary>
        public long DelayTicks;
        
        /// <summary>Maximum duplicate count (capped).</summary>
        public int DuplicateCount;
        
        /// <summary>OR-accumulated modify flags.</summary>
        public ModifyFlags ModifyFlags;
        
        /// <summary>Buffer pool index for inject packet (-1 = none).</summary>
        public int InjectPacketId;
        
        /// <summary>
        /// Creates a properly initialized ActionResult.
        /// Always use this factory instead of default.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ActionResult Create()
        {
            return new ActionResult { InjectPacketId = -1 };
        }
        
        /// <summary>
        /// Accumulates another action into this result.
        /// Uses max for numeric fields, OR for flags.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Accumulate(ActionMask mask, long delayTicks, int duplicateCount, ModifyFlags modifyFlags)
        {
            Mask |= mask;
            
            if (delayTicks > DelayTicks)
                DelayTicks = delayTicks;
            
            if (duplicateCount > DuplicateCount)
                DuplicateCount = duplicateCount;
            
            ModifyFlags |= modifyFlags;
        }
        
        /// <summary>
        /// Returns true if any short-circuit action is set.
        /// </summary>
        public readonly bool ShouldShortCircuit =>
            (Mask & (ActionMask.Drop | ActionMask.Blackhole)) != 0;
    }
}

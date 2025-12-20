using System.Runtime.InteropServices;

namespace NetShaper.Rules
{
    /// <summary>
    /// Per-rule state. 64 bytes with union overlays per rule type.
    /// Initialized in factory, not in pipeline.
    /// ONE overlay per rule type - do not mix fields.
    /// 
    /// ══════════════════════════════════════════════════════════
    /// FIELD USAGE MAP BY RULE TYPE
    /// ══════════════════════════════════════════════════════════
    /// 
    /// COMMON (all rules):
    ///   - LastTick        : last evaluation timestamp
    ///   - Counter         : packets processed
    /// 
    /// THROTTLE / BANDWIDTH (token bucket):
    ///   - TokenBucket     : current tokens
    ///   - TokensPerTick   : refill rate (tokens per SECOND, not per tick)
    ///   - MaxTokens       : bucket capacity
    /// 
    /// LOSSPATTERN:
    ///   - PatternMask     : drop bitmask (1=drop)
    ///   - PatternIndex    : current position
    ///   - PatternLength   : cycle length
    /// 
    /// JITTER / OUTOFORDER:
    ///   - RngState        : xorshift32 state
    ///   - MinDelayTicks   : min delay
    ///   - MaxDelayTicks   : max delay
    /// 
    /// LAG / BURST / ACKDELAY:
    ///   - FixedDelayTicks : fixed delay value
    /// 
    /// DUPLICATE:
    ///   - DuplicateCount  : copies to create
    /// 
    /// TAMPER (overlay reuse - FRAGILE):
    ///   - PatternIndex    : ModifyFlags cast to int
    /// 
    /// TCPRST (overlay reuse - FRAGILE):
    ///   - PatternIndex    : mode (0=continuous, 1=one-shot, 2=fired)
    /// 
    /// MTUCLAMP (overlay reuse):
    ///   - MaxTokens       : max packet size
    /// 
    /// WINDOWCLAMP (overlay reuse):
    ///   - TokenBucket     : max window size
    /// 
    /// DROP / BLACKHOLE / SYNDROP:
    ///   - (none)          : stateless rules
    /// 
    /// ══════════════════════════════════════════════════════════
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RuleState
    {
        // ═══════════════════════════════════════════════════════════
        // Common fields (all rules)
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>Last tick when rule was evaluated.</summary>
        [FieldOffset(0)] public long LastTick;
        
        /// <summary>General-purpose counter.</summary>
        [FieldOffset(8)] public long Counter;
        
        // ═══════════════════════════════════════════════════════════
        // Throttle / Bandwidth overlay (offset 16-31)
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>Current token count.</summary>
        [FieldOffset(16)] public int TokenBucket;
        
        /// <summary>Tokens added per tick.</summary>
        [FieldOffset(20)] public int TokensPerTick;
        
        /// <summary>Maximum tokens.</summary>
        [FieldOffset(24)] public int MaxTokens;
        
        // ═══════════════════════════════════════════════════════════
        // LossPattern overlay (offset 16-31)
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>Bitmask pattern for loss (1 = drop).</summary>
        [FieldOffset(16)] public ulong PatternMask;
        
        /// <summary>Current position in pattern.</summary>
        [FieldOffset(24)] public int PatternIndex;
        
        /// <summary>Pattern length in bits.</summary>
        [FieldOffset(28)] public int PatternLength;
        
        // ═══════════════════════════════════════════════════════════
        // Jitter overlay (offset 16-31)
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>RNG state (xorshift).</summary>
        [FieldOffset(16)] public uint RngState;
        
        /// <summary>Minimum delay in ticks.</summary>
        [FieldOffset(20)] public int MinDelayTicks;
        
        /// <summary>Maximum delay in ticks.</summary>
        [FieldOffset(24)] public int MaxDelayTicks;
        
        // ═══════════════════════════════════════════════════════════
        // Lag overlay (offset 16-23)
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>Fixed delay in ticks.</summary>
        [FieldOffset(16)] public long FixedDelayTicks;
        
        // ═══════════════════════════════════════════════════════════
        // Duplicate overlay (offset 16-19)
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>Number of duplicates to create.</summary>
        [FieldOffset(16)] public int DuplicateCount;
    }
}

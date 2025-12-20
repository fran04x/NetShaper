namespace NetShaper.Rules
{
    /// <summary>
    /// Flags indicating which packet modifications to apply.
    /// OR-accumulated across rules, never cleared.
    /// </summary>
    [Flags]
    public enum ModifyFlags : byte
    {
        None = 0,
        
        /// <summary>Truncate packet to MTU.</summary>
        Truncate = 1 << 0,
        
        /// <summary>Corrupt payload bytes.</summary>
        Corrupt = 1 << 1,
        
        /// <summary>Rewrite specific payload bytes.</summary>
        Rewrite = 1 << 2,
        
        /// <summary>Clamp TCP window size.</summary>
        WindowClamp = 1 << 3,
        
        /// <summary>Clamp MSS option.</summary>
        MssClamp = 1 << 4,
    }
}

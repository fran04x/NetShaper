namespace NetShaper.Rules
{
    /// <summary>
    /// Accumulated action flags from rule evaluation.
    /// None = Pass (no action needed).
    /// </summary>
    [Flags]
    public enum ActionMask : ushort
    {
        /// <summary>No action = Pass packet through.</summary>
        None = 0,
        
        /// <summary>Drop packet immediately. Short-circuits pipeline.</summary>
        Drop = 1 << 0,
        
        /// <summary>Silent drop (no response). Short-circuits pipeline.</summary>
        Blackhole = 1 << 1,
        
        /// <summary>Delay packet before sending.</summary>
        Delay = 1 << 2,
        
        /// <summary>Duplicate packet N times.</summary>
        Duplicate = 1 << 3,
        
        /// <summary>Modify packet contents.</summary>
        Modify = 1 << 4,
        
        /// <summary>Inject a new packet (one-shot queue).</summary>
        Inject = 1 << 5,
        
        // Bits 6-15 reserved for future use
    }
}

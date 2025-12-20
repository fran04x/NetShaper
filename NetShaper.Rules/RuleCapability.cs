namespace NetShaper.Rules
{
    /// <summary>
    /// Bitmask of active rule capabilities.
    /// Used to skip pipeline entirely if no rules active.
    /// </summary>
    [Flags]
    public enum RuleCapability : ushort
    {
        None = 0,
        
        HasDropRules = 1 << 0,
        HasDelayRules = 1 << 1,
        HasDuplicateRules = 1 << 2,
        HasModifyRules = 1 << 3,
        HasInjectRules = 1 << 4,
    }
}

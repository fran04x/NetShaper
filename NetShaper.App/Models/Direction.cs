namespace NetShaper.App.Models
{
    /// <summary>
    /// Traffic direction filter for rules.
    /// </summary>
    public enum Direction
    {
        /// <summary>Only inbound traffic.</summary>
        Inbound,
        
        /// <summary>Only outbound traffic.</summary>
        Outbound,
        
        /// <summary>Both directions (default).</summary>
        Both
    }
}

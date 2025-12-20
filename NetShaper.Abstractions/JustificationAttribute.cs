using System;

namespace NetShaper.Abstractions
{
    /// <summary>
    /// Provides justification for rule violations that are acceptable.
    /// Used to document why a constructor exceeds normal dependency limits, etc.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Constructor)]
    public sealed class JustificationAttribute : Attribute 
    {
        /// <summary>
        /// Creates a justification attribute with the specified reason.
        /// </summary>
        /// <param name="reason">The reason this violation is acceptable.</param>
        public JustificationAttribute(string reason)
        {
            if (reason == null)
                throw new ArgumentNullException(nameof(reason));
            Reason = reason;
        }
        
        public string Reason { get; }
    }
}

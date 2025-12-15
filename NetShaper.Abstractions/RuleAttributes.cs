// NetShaper.Abstractions/RuleAttributes.cs
using System;

namespace NetShaper.Abstractions
{
    /// <summary>
    /// Marks methods that are allowed to use async/Task in Engine namespace.
    /// These are typically initialization or configuration methods that run outside the hot path.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EngineSetupAttribute : Attribute { }

    /// <summary>
    /// Marks methods or classes that are application boundaries.
    /// These are allowed to catch generic Exception types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class BoundaryAttribute : Attribute { }

    /// <summary>
    /// Marks a class as the composition root for dependency injection.
    /// Each assembly should have at most one composition root.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CompositionRootAttribute : Attribute { }

    /// <summary>
    /// Provides justification for rule violations that are acceptable.
    /// Used to document why a constructor exceeds normal dependency limits, etc.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Constructor)]
    public sealed class JustificationAttribute : Attribute 
    {
        public string Reason { get; }
        
        /// <summary>
        /// Creates a justification attribute with the specified reason.
        /// </summary>
        /// <param name="reason">The reason this violation is acceptable.</param>
        public JustificationAttribute(string reason) => Reason = reason;
    }
}

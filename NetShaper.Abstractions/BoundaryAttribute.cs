using System;

namespace NetShaper.Abstractions
{
    /// <summary>
    /// Marks methods or classes that are application boundaries.
    /// These are allowed to catch generic Exception types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class BoundaryAttribute : Attribute { }
}

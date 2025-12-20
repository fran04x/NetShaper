using System;

namespace NetShaper.Abstractions
{
    /// <summary>
    /// Marks a class as the composition root for dependency injection.
    /// Each assembly should have at most one composition root.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CompositionRootAttribute : Attribute { }
}

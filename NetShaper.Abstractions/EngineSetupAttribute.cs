using System;

namespace NetShaper.Abstractions
{
    /// <summary>
    /// Marks methods that are allowed to use async/Task in Engine namespace.
    /// These are typically initialization or configuration methods that run outside the hot path.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EngineSetupAttribute : Attribute { }
}

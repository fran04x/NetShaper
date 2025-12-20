using System;

namespace NetShaper.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class EngineSetupAttribute : Attribute { }
}

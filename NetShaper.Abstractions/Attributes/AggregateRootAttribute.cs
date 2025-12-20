using System;

namespace NetShaper.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class AggregateRootAttribute : Attribute { }
}

using System;

namespace NetShaper.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class BufferOwnerAttribute : Attribute { }
}

using System;

namespace NetShaper.Abstractions.Attributes
{
    // === Arquitectura ===

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class EngineSetupAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class BoundaryAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CompositionRootAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HotPathAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ProtocolParserAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class BatchProcessorAttribute : Attribute { }

    // === Dominio ===

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class EntityAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class ValueObjectAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class AggregateRootAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class DomainEventAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DomainStateAttribute : Attribute { }

    // === Híbridos ===

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class EngineDomainAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class ZeroAllocationDomainAttribute : Attribute { }

    // === Uso / Aplicación ===

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UseCaseAttribute : Attribute { }

    // === Infraestructura de memoria ===

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class BufferOwnerAttribute : Attribute { }
}

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NetShaper.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NetShaperNormativeAnalyzer : DiagnosticAnalyzer
    {
        #region Rules
        private static readonly DiagnosticDescriptor UnsafeWithoutFixed = new("RED_R5_03A", "unsafe sin fixed en Engine", "Bloque unsafe en NetShaper.Engine debe contener fixed", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor DomainExceptionInEngine = new("RED_R6_10", "DomainException prohibida en Engine", "No se permite DomainException en NetShaper.Engine", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor UseCaseComplexityExceeded = new("DR07", "Complejidad ciclomática excedida", "[UseCase] no puede tener complejidad ciclomática > 5 en capa de aplicación", "NetShaper.Application", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor MissingInvariantMethod = new("DR04", "Invariante no declarada", "Tipo de dominio debe declarar ValidateInvariant / TryValidateInvariant", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor MutableValueObject = new("DR02", "ValueObject mutable", "[ValueObject] debe ser inmutable", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor InvalidDomainState = new("DR06", "Estado de dominio inválido", "[DomainState] debe ser enum", "NetShaper.Domain", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor GenericExceptionUsage = new("DR08", "Uso de Exception genérica", "Prohibido throw new System.Exception en dominio", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NoRegionsAllowed = new("R205", "Prohibido el uso de #region", "El uso de #region está prohibido", "Style", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NoVolatileInEngine = new("R307", "Prohibido 'volatile' en Engine", "El modificador 'volatile' está prohibido en NetShaper.Engine", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor UseLibraryImport = new("R502", "P/Invoke debe usar LibraryImport", "Reemplace [DllImport] con [LibraryImport]", "Security", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NoBinaryFormatter = new("R505", "BinaryFormatter está prohibido", "BinaryFormatter es inseguro", "Security", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor PrivateFieldNaming = new("R701", "Campos privados deben usar _camelCase", "El campo privado '{0}' debe seguir el formato _camelCase", "Naming", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor EntityInEngineMustBeStruct = new("DR03", "Entity en Engine debe ser un struct", "Las entidades en NetShaper.Engine deben ser 'struct'", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor DomainEventImmutable = new("DR09", "DomainEvent debe ser inmutable", "Los eventos de dominio deben ser inmutables", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor AsyncInEngineProhibited = new("R301", "Prohibido async en Engine", "'async' está prohibido en NetShaper.Engine excepto en [EngineSetup]", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor LinqInHotPathProhibited = new("R303", "Prohibido LINQ en Hot-Path", "LINQ está prohibido en NetShaper.Engine", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor StringConcatInLoopProhibited = new("R311", "Prohibida la concatenación de strings en bucles", "Use StringBuilder para concatenar strings dentro de bucles", "Performance", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor ToArrayToListInEngineProhibited = new("R403", "Prohibido ToArray/ToList en Engine", "ToArray/ToList está prohibido en NetShaper.Engine", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor ExplicitGcCollectProhibited = new("R1101", "Prohibido GC.Collect explícito", "Llamadas explícitas a GC.Collect() están prohibidas", "Performance", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor ThreadSleepInUiProhibited = new("R1201", "Prohibido Thread.Sleep en UI", "Thread.Sleep bloquea el hilo UI. Use await Task.Delay() en su lugar.", "UI", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor MissingCancellationToken = new("R312", "Falta CancellationToken en método async", "Los métodos asíncronos deben aceptar un CancellationToken", "Correctness", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor MissingAsyncSuffix = new("R704", "Falta sufijo 'Async' en método asíncrono", "El método asíncrono '{0}' debe terminar con el sufijo 'Async'", "Naming", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor DisposableFieldNeedsDisposing = new("R902", "Campo IDisposable requiere que la clase implemente IDisposable", "La clase '{0}' tiene campos IDisposable y debe implementar IDisposable", "Resource Management", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor AsyncVoidIsForEventHandlersOnly = new("R1202", "'async void' solo para manejadores de eventos", "Use 'async Task' en lugar de 'async void' fuera de manejadores de eventos", "Correctness", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor EmptyCatchBlockProhibited = new("R603", "Prohibido bloque catch vacío", "Los bloques catch no deben estar vacíos", "Correctness", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NoTodos = new("R206", "Comentario TODO/FIXME/HACK encontrado", "Se encontró un comentario '{0}'. Resuelva la tarea pendiente.", "Code Quality", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor ConstructorDependencyLimit = new("R103", "Límite de dependencias de constructor excedido", "El constructor tiene demasiados parámetros ({0}). El límite es 4.", "Architecture", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor CyclomaticComplexityExceeded = new("R201", "Complejidad ciclomática excedida", "La complejidad ciclomática de '{0}' es {1}, que excede el límite de {2}", "Complexity", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor OneClassPerFile = new("R204", "Un tipo público por archivo", "El archivo contiene más de un tipo público", "Style", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor BoxingInEngine = new("R304", "Boxing allocation detectado en Engine", "La conversión de '{0}' a '{1}' causa una asignación de memoria (boxing) en un hot-path", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor LockInEngineProhibited = new("R306", "Lock prohibido en Engine", "El uso de 'lock' está prohibido en NetShaper.Engine. Use mecanismos sin bloqueo.", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor PascalCaseNaming = new("R702", "Propiedades y métodos públicos deben usar PascalCase", "El miembro público '{0}' debe usar PascalCase", "Naming", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor MagicNumberProhibited = new("R1203", "Número mágico encontrado", "El número '{0}' es un número mágico. Use constantes con nombres descriptivos.", "Code Quality", DiagnosticSeverity.Warning, true);
        
        // FASE 1+2: Nuevas reglas críticas
        private static readonly DiagnosticDescriptor TaskInEngineProhibited = new("R302", "Prohibido Task en Engine", "El uso de Task está prohibido en NetShaper.Engine excepto en [EngineSetup]", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor DateTimeInEngineProhibited = new("R305", "Prohibido DateTime en Engine", "Use Stopwatch.GetTimestamp() en lugar de DateTime.Now/UtcNow o Environment.TickCount", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor ExceptionsAsControlFlow = new("R308", "Exceptions como control de flujo", "try-catch en un loop o catch(Exception) fuera de boundaries está prohibido", "Correctness", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor ArrayPoolRequired = new("R309", "ArrayPool obligatorio en Engine", "Use ArrayPool<byte>.Shared.Rent() en lugar de new byte[] (excepto arrays ≤16)", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor SpanMemoryPreferred = new("R310", "Preferir Span/Memory", "Use ReadOnlySpan<byte> o Memory<byte> en lugar de byte[] como parámetro público", "NetShaper.Engine", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor ArrayPoolOwnership = new("R401", "Ownership ArrayPool desbalanceado", "Cada Rent() debe tener su Return() correspondiente (o marcar método con [BufferOwner])", "Memory", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor BufferSizeNonStandard = new("R402", "Tamaño de buffer no estándar", "Use tamaños estándar: 1500, 2048, 4096, 8192, 65536 o constantes nombradas", "Memory", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor StructuredLoggingRequired = new("R601", "Logging estructurado requerido", "En NetShaper.Engine use structs para logging, no strings", "Logging", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor CatchExceptionOnlyBoundaries = new("R602", "catch(Exception) solo en boundaries", "catch(Exception) solo permitido en Main, [Boundary], Controllers", "Correctness", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor DisposableWithoutUsing = new("R901", "IDisposable sin using", "Las variables IDisposable deben usarse con 'using' o llamar .Dispose()", "Resource Management", DiagnosticSeverity.Error, true);
        
        // FASES 3-5: Reglas adicionales críticas
        private static readonly DiagnosticDescriptor CyclomaticComplexityUiInfra = new("R202", "Complejidad cicl omática UI/Infra excedida", "La complejidad cicl omática de '{0}' es {1}, que excede el límite de 10", "Complexity", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NestingDepthExceeded = new("R203", "Anidación máxima excedida", "El método '{0}' tiene anidación de {1} niveles, que excede el límite de {2}", "Complexity", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor StructuredLoggingRecommended = new("R604", "Use logging estructurado", "Preferir placeholders {{name}} en lugar de string literals en logs", "Logging", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor SafeHandleRequired = new("R501", "SafeHandle requerido para IntPtr", "Los métodos P/Invoke que retornan IntPtr deben usar SafeHandle", "Security", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor FixedOnlyInEngineNative = new("R504", "fixed solo en Engine/Native", "El statement 'fixed' solo está permitido en NetShaper.Engine y NetShaper.Native", "Security", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor HardcodedSecretDetected = new("R506", "Posible secret hardcoded", "Posible secret/password/key detectado en código fuente", "Security", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor MissingInputValidation = new("R507", "Falta validación de input", "Parámetros reference types deben validarse con guard clauses", "Security", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor NullableNotEnabled = new("R801", "Nullable no habilitado", "El proyecto debe tener Nullable habilitado globalmente", "Nullability", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NullableParameterInPublicApi = new("R802", "Parámetro nullable en API pública", "Los parámetros de APIs públicas no deben ser nullable", "Nullability", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor WarningsNotAsErrors = new("R803", "Warnings no configurados como errors", "TreatWarningsAsErrors debe estar habilitado en .csproj", "Code Quality", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor RedundantThis = new("R705", "'this.' redundante", "El uso de 'this.' es redundante (no hay shadowing)", "Style", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor VarPreferred = new("R706", "Preferir 'var' para tipos evidentes", "Use 'var' cuando el tipo es evidente (new T(), cast explícito)", "Style", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor MissingDomainAttribute = new("DR01", "Falta atributo de dominio", "Las clases en namespace Domain deben tener [Entity], [ValueObject], [AggregateRoot], [DomainEvent] o [UseCase]", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor ThrowInTechnicalDomain = new("DR05", "throw prohibido en dominio técnico", "Los statements 'throw' están prohibidos en NetShaper.Engine/Native (excepto [EngineSetup]/[Boundary])", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        
        // FASE 2: Enforcement Crítico
        private static readonly DiagnosticDescriptor EngineSetupBoundaryViolation = new("R314", "EngineSetup llamado desde hot-path", "Métodos [EngineSetup] solo pueden llamarse desde constructores, otros [EngineSetup] o Main", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor HotPathLinqViolation = new("R303H", "LINQ en [HotPath]", "Métodos marcados con [HotPath] no pueden usar LINQ", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor HotPathBoxingViolation = new("R304H", "Boxing en [HotPath]", "Métodos marcados con [HotPath] no pueden causar boxing allocations", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor HotPathArrayCreationViolation = new("R309H", "new byte[] en [HotPath]", "Métodos marcados con [HotPath] deben usar ArrayPool", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor HotPathComplexityViolation = new("R201H", "Complejidad en [HotPath]", "Métodos marcados con [HotPath] no pueden tener complejidad ciclomática >7", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        
        // Reglas adicionales
        private static readonly DiagnosticDescriptor MemberOrderViolation = new("R707", "Orden de miembros incorrecto", "El miembro '{0}' está fuera de orden. Orden esperado: const, static fields, fields, static ctor, ctor, props, methods", "Style", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor DINotViaConstructor = new("R1402", "Dependencias no inyectadas por constructor", "La clase '{0}' tiene dependencias que no se inyectan por constructor (evitar service locator)", "Architecture", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor MissingInvariantTests = new("DR10", "Faltan tests de invariantes", "El tipo de dominio '{0}' debe tener tests que validen sus invariantes", "NetShaper.Domain", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor TransactionalEventsInEngine = new("R1020", "Eventos transaccionales en Engine", "Los eventos en Engine no deben requerir transacciones (diseño no transaccional)", "NetShaper.Engine", DiagnosticSeverity.Warning, true);
        
        // Reglas final batch
        private static readonly DiagnosticDescriptor AbstractionsWithDependencies = new("R104", "Abstractions tiene dependencias", "NetShaper.Abstractions no debe depender de otros proyectos NetShaper (excepto System.*)", "Architecture", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor LowCohesion = new("R207", "Baja cohesión detectada", "La clase '{0}' tiene baja cohesión (campos/métodos no relacionados). Considerar dividir.", "Complexity", DiagnosticSeverity.Warning, true);
        #endregion

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnsafeWithoutFixed, DomainExceptionInEngine, UseCaseComplexityExceeded, MissingInvariantMethod, MutableValueObject, InvalidDomainState, GenericExceptionUsage, 
                NoRegionsAllowed, NoVolatileInEngine, UseLibraryImport, NoBinaryFormatter, PrivateFieldNaming, EntityInEngineMustBeStruct, DomainEventImmutable,
                AsyncInEngineProhibited, LinqInHotPathProhibited, StringConcatInLoopProhibited, ToArrayToListInEngineProhibited, ExplicitGcCollectProhibited, ThreadSleepInUiProhibited,
                MissingCancellationToken, MissingAsyncSuffix, DisposableFieldNeedsDisposing, AsyncVoidIsForEventHandlersOnly, EmptyCatchBlockProhibited, NoTodos,
                ConstructorDependencyLimit, CyclomaticComplexityExceeded, OneClassPerFile, BoxingInEngine, LockInEngineProhibited, PascalCaseNaming, MagicNumberProhibited,
                // FASE 1+2: Nuevas reglas
                TaskInEngineProhibited, DateTimeInEngineProhibited, ExceptionsAsControlFlow, ArrayPoolRequired, SpanMemoryPreferred,
                ArrayPoolOwnership, BufferSizeNonStandard, StructuredLoggingRequired, CatchExceptionOnlyBoundaries, DisposableWithoutUsing,
                // FASES 3-5: Reglas adicionales
                CyclomaticComplexityUiInfra, NestingDepthExceeded, StructuredLoggingRecommended, SafeHandleRequired, FixedOnlyInEngineNative,
                HardcodedSecretDetected, MissingInputValidation, 
                // REMOVED: NullableNotEnabled (R8.01), WarningsNotAsErrors (R8.03) - requieren MSBuild analyzer
                // NullableParameterInPublicApi mantenido (valida símbolos, NO proyecto)
                NullableParameterInPublicApi,
                RedundantThis, VarPreferred, MissingDomainAttribute, ThrowInTechnicalDomain,
                // FASE 2: Enforcement Crítico
                EngineSetupBoundaryViolation, HotPathLinqViolation, HotPathBoxingViolation, HotPathArrayCreationViolation, HotPathComplexityViolation,
                // Reglas adicionales
                MemberOrderViolation, DINotViaConstructor, MissingInvariantTests, TransactionalEventsInEngine,
                // Reglas final batch
                AbstractionsWithDependencies, LowCohesion);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // REMOVED: R8.01/R8.03 - requieren MSBuild analyzer, NO DiagnosticAnalyzer
            
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            context.RegisterSyntaxNodeAction(AnalyzeUnsafeBlock, SyntaxKind.UnsafeStatement);
            context.RegisterSyntaxNodeAction(AnalyzeThrow, SyntaxKind.ThrowStatement);
            context.RegisterSyntaxNodeAction(AnalyzeDllImport, SyntaxKind.Attribute);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeStringConcatInLoop, SyntaxKind.AddExpression);
            context.RegisterSyntaxNodeAction(AnalyzeEmptyCatch, SyntaxKind.CatchClause);
            context.RegisterSyntaxNodeAction(AnalyzeLockStatement, SyntaxKind.LockStatement);
            context.RegisterSyntaxNodeAction(AnalyzeLiterals, SyntaxKind.NumericLiteralExpression);
            context.RegisterSyntaxTreeAction(AnalyzeOneClassPerFile);
            context.RegisterSyntaxTreeAction(AnalyzeTodos);
            context.RegisterOperationAction(AnalyzeBoxingConversion, OperationKind.Conversion);
            
            // FASE 1+2: Nuevos registros
            context.RegisterSyntaxNodeAction(AnalyzeTaskUsage, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeDateTimeUsage, SyntaxKind.SimpleMemberAccessExpression);
            context.RegisterSyntaxNodeAction(AnalyzeExceptionControlFlow, SyntaxKind.TryStatement);
            context.RegisterSyntaxNodeAction(AnalyzeArrayCreationForPool, SyntaxKind.ArrayCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeArrayPoolBufferSize, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeEngineLogging, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeCatchExceptionBoundary, SyntaxKind.CatchClause);
            context.RegisterSyntaxNodeAction(AnalyzeDisposableVariable, SyntaxKind.VariableDeclarator);
            
            // FASES 3-5: Nuevos registros
            context.RegisterSyntaxNodeAction(AnalyzeFixedStatement, SyntaxKind.FixedStatement);
            context.RegisterSyntaxNodeAction(AnalyzeHardcodedSecrets, SyntaxKind.StringLiteralExpression);
            context.RegisterSyntaxNodeAction(AnalyzeRedundantThis, SyntaxKind.SimpleMemberAccessExpression);
            context.RegisterSyntaxNodeAction(AnalyzeVarUsage, SyntaxKind.VariableDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeThrowInTechnicalDomain, SyntaxKind.ThrowStatement);
            context.RegisterSyntaxNodeAction(AnalyzeStructuredLoggingRecommended, SyntaxKind.InvocationExpression);
            
            // FASE 2: Enforcement Crítico
            context.RegisterSyntaxNodeAction(AnalyzeEngineSetupBoundary, SyntaxKind.InvocationExpression);
        }

        #region Analyzers

        private static void AnalyzeBoxingConversion(OperationAnalysisContext context)
        {
            var conversionOperation = (IConversionOperation)context.Operation;
            var fromType = conversionOperation.Operand.Type;
            var toType = conversionOperation.Type;

            if (fromType == null || toType == null) return;

            bool isBoxing = fromType.IsValueType && (toType.SpecialType == SpecialType.System_Object || toType.TypeKind == TypeKind.Interface);
            if (!isBoxing) return;

            var isInEngine = context.Operation.Syntax.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;
            if (isInEngine)
            {
                context.ReportDiagnostic(Diagnostic.Create(BoxingInEngine, context.Operation.Syntax.GetLocation(), fromType.ToDisplayString(), toType.ToDisplayString()));
            }
            
            // HotPath: Enforcement adicional
            var containingMethod = context.Operation.Syntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod != null)
            {
                // Buscar el método en los símbolos del tipo contenedor
                var containingType = context.ContainingSymbol as INamedTypeSymbol;
                if (containingType != null)
                {
                    var methodSymbol = containingType.GetMembers()
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m => m.DeclaringSyntaxReferences.Any(r => r.GetSyntax() == containingMethod));
                    
                    if (methodSymbol != null)
                    {
                        var hasHotPath = methodSymbol.GetAttributes()
                            .Any(a => a.AttributeClass?.Name == "HotPathAttribute");
                        
                        if (hasHotPath)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                HotPathBoxingViolation,
                                context.Operation.Syntax.GetLocation(),
                                fromType.ToDisplayString(),
                                toType.ToDisplayString()));
                        }
                    }
                }
            }
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol type) return;

            AnalyzeDomainType(context, type);
            AnalyzeEntityInEngine(context, type);
            AnalyzeDomainEventImmutability(context, type);
            AnalyzeForBinaryFormatter(context, type);
            AnalyzeDisposableFields(context, type);
            
            // FASES 3-5
            AnalyzeDomainAttribute(context, type);
            
            // Reglas adicionales
            AnalyzeMemberOrder(context, type);
            AnalyzeDIViaConstructor(context, type);
            AnalyzeInvariantTests(context, type);
            AnalyzeTransactionalEvents(context, type);
            
            // Final batch
            AnalyzeAbstractionsDependencies(context, type);
            AnalyzeLowCohesion(context, type);
        }
        
        private static void AnalyzeField(SymbolAnalysisContext context) 
        {
            if (context.Symbol is not IFieldSymbol field) return;

            if (field.IsConst || field.IsImplicitlyDeclared || field.DeclaredAccessibility != Accessibility.Private) return;

            if (!field.Name.StartsWith("_"))
            {
                context.ReportDiagnostic(Diagnostic.Create(PrivateFieldNaming, field.Locations[0], field.Name));
            }

            AnalyzeVolatileInEngine(context, field);
        }
        
        private static void AnalyzeUnsafeBlock(SyntaxNodeAnalysisContext context)
        {
            var unsafeBlock = (UnsafeStatementSyntax)context.Node;
            
            var isInEngine = unsafeBlock.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;
            if (!isInEngine) return;

            // Verificar si contiene fixed statement
            bool hasFixed = unsafeBlock.DescendantNodes().OfType<FixedStatementSyntax>().Any();
            if (!hasFixed)
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsafeWithoutFixed, unsafeBlock.GetLocation()));
            }
        }

        private static void AnalyzeThrow(SyntaxNodeAnalysisContext context)
        {
            var throwStatement = (ThrowStatementSyntax)context.Node;
            if (throwStatement.Expression is not ObjectCreationExpressionSyntax creation) return;

            var symbolInfo = context.SemanticModel.GetSymbolInfo(creation.Type);
            var typeSymbol = symbolInfo.Symbol as INamedTypeSymbol;
            if (typeSymbol == null) return;

            var typeName = typeSymbol.ToDisplayString();

            // DR08: Prohibido System.Exception genérico
            if (typeName == "System.Exception")
            {
                context.ReportDiagnostic(Diagnostic.Create(GenericExceptionUsage, throwStatement.GetLocation()));
            }

            // RED_R6_10: DomainException prohibida en Engine
            if (typeName.Contains("DomainException"))
            {
                var isInEngine = throwStatement.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;
                if (isInEngine)
                {
                    context.ReportDiagnostic(Diagnostic.Create(DomainExceptionInEngine, throwStatement.GetLocation()));
                }
            }
        }

        private static void AnalyzeDllImport(SyntaxNodeAnalysisContext context)
        {
            var attribute = (AttributeSyntax)context.Node;
            var name = attribute.Name.ToString();
            
            if (name == "DllImport" || name == "DllImportAttribute")
            {
                context.ReportDiagnostic(Diagnostic.Create(UseLibraryImport, attribute.GetLocation()));
            }
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            var method = symbolInfo.Symbol as IMethodSymbol;
            if (method == null) return;

            var methodName = method.Name;
            var containingType = method.ContainingType?.ToDisplayString() ?? "";

            var isInEngine = invocation.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;

            // R303: LINQ prohibido en Engine
            if (isInEngine)
            {
                var linqMethods = new[] { "Where", "Select", "SelectMany", "OrderBy", "OrderByDescending", "GroupBy", 
                                         "Join", "Any", "All", "First", "FirstOrDefault", "Single", "SingleOrDefault",
                                         "Last", "LastOrDefault", "Count", "Sum", "Average", "Min", "Max", "Aggregate" };
                
                if (linqMethods.Contains(methodName) && containingType.StartsWith("System.Linq"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(LinqInHotPathProhibited, invocation.GetLocation()));
                }

                // R403: ToArray/ToList prohibido en Engine
                if (methodName == "ToArray" || methodName == "ToList")
                {
                    context.ReportDiagnostic(Diagnostic.Create(ToArrayToListInEngineProhibited, invocation.GetLocation()));
                }
            }
            
            // HotPath: Enforcement adicional para métodos marcados con [HotPath]
            var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod != null)
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
                if (methodSymbol != null)
                {
                    var hasHotPath = methodSymbol.GetAttributes()
                        .Any(a => a.AttributeClass?.Name == "HotPathAttribute");
                    
                    if (hasHotPath)
                    {
                        var linqMethods = new[] { "Where", "Select", "SelectMany", "OrderBy", "OrderByDescending", "GroupBy", 
                                                 "Join", "Any", "All", "First", "FirstOrDefault", "Single", "SingleOrDefault",
                                                 "Last", "LastOrDefault", "Count", "Sum", "Average", "Min", "Max", "Aggregate" };
                        
                        if (linqMethods.Contains(methodName) && containingType.StartsWith("System.Linq"))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(HotPathLinqViolation, invocation.GetLocation()));
                        }
                    }
                }
            }

            // R1101: GC.Collect prohibido
            if (containingType == "System.GC" && methodName == "Collect")
            {
                context.ReportDiagnostic(Diagnostic.Create(ExplicitGcCollectProhibited, invocation.GetLocation()));
            }

            // R1201: Thread.Sleep en UI
            if (containingType == "System.Threading.Thread" && methodName == "Sleep")
            {
                var isInUi = invocation.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.UI") ?? false;
                if (isInUi)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ThreadSleepInUiProhibited, invocation.GetLocation()));
                }
            }
        }

        private static void AnalyzeStringConcatInLoop(SyntaxNodeAnalysisContext context)
        {
            var addExpression = (BinaryExpressionSyntax)context.Node;
            
            // Verificar si es concatenación de strings
            var typeInfo = context.SemanticModel.GetTypeInfo(addExpression);
            if (typeInfo.Type?.SpecialType != SpecialType.System_String) return;

            // Verificar si está dentro de un loop
            var isInLoop = addExpression.Ancestors().Any(n => 
                n is ForStatementSyntax || 
                n is ForEachStatementSyntax || 
                n is WhileStatementSyntax || 
                n is DoStatementSyntax);

            if (isInLoop)
            {
                context.ReportDiagnostic(Diagnostic.Create(StringConcatInLoopProhibited, addExpression.GetLocation()));
            }
        }

        private static void AnalyzeEmptyCatch(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;
            
            // Verificar si el bloque está vacío
            var block = catchClause.Block;
            if (block.Statements.Count == 0 && block.DescendantTrivia().All(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                context.ReportDiagnostic(Diagnostic.Create(EmptyCatchBlockProhibited, catchClause.GetLocation()));
            }
        }

        private static void AnalyzeTodos(SyntaxTreeAnalysisContext context)
        {
            foreach (var trivia in context.Tree.GetRoot().DescendantTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                    continue;

                var text = trivia.ToString();
                var keywords = new[] { "TODO", "FIXME", "HACK" };
                
                foreach (var keyword in keywords)
                {
                    if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(NoTodos, trivia.GetLocation(), keyword));
                        break;
                    }
                }
            }
        }

        private static void AnalyzeDomainType(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var attributes = type.GetAttributes();
            
            // DR.01: Extendido para cubrir DomainEvent y UseCase
            var domainAttributes = new[] { "EntityAttribute", "ValueObjectAttribute", "AggregateRootAttribute", 
                                           "DomainEventAttribute", "UseCaseAttribute" };
            
            var hasDomainAttribute = attributes.Any(a => domainAttributes.Contains(a.AttributeClass?.Name));
            if (!hasDomainAttribute) return;

            // DR04: Verificar ValidateInvariant (solo para Entity/ValueObject/AggregateRoot)
            var requiresInvariant = attributes.Any(a => 
                a.AttributeClass?.Name == "EntityAttribute" || 
                a.AttributeClass?.Name == "ValueObjectAttribute" ||
                a.AttributeClass?.Name == "AggregateRootAttribute");
            
            if (requiresInvariant)
            {
                var hasValidateInvariant = type.GetMembers().OfType<IMethodSymbol>()
                    .Any(m => m.Name == "ValidateInvariant" || m.Name == "TryValidateInvariant");

                if (!hasValidateInvariant)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MissingInvariantMethod, type.Locations[0]));
                }
            }

            // DR02: ValueObject debe ser inmutable
            var isValueObject = attributes.Any(a => a.AttributeClass?.Name == "ValueObjectAttribute");
            if (isValueObject)
            {
                var hasSettableProperties = type.GetMembers().OfType<IPropertySymbol>()
                    .Any(p => p.SetMethod != null && p.SetMethod.DeclaredAccessibility == Accessibility.Public);

                var hasPublicFields = type.GetMembers().OfType<IFieldSymbol>()
                    .Any(f => f.DeclaredAccessibility == Accessibility.Public && !f.IsReadOnly && !f.IsConst);

                if (hasSettableProperties || hasPublicFields)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MutableValueObject, type.Locations[0]));
                }
            }
        }

        private static void AnalyzeEntityInEngine(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var isEntity = type.GetAttributes().Any(a => a.AttributeClass?.Name == "EntityAttribute");
            if (!isEntity) return;

            var nsName = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (!nsName.StartsWith("NetShaper.Engine")) return;

            // DR03: Entity en Engine debe ser struct
            if (type.TypeKind != TypeKind.Struct)
            {
                context.ReportDiagnostic(Diagnostic.Create(EntityInEngineMustBeStruct, type.Locations[0]));
            }
        }

        private static void AnalyzeDomainEventImmutability(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var isDomainEvent = type.GetAttributes().Any(a => a.AttributeClass?.Name == "DomainEventAttribute");
            if (!isDomainEvent) return;

            // DR09: DomainEvent debe ser inmutable
            bool isImmutable = true;

            // Verificar si es record (inmutable por defecto)
            if (type.IsRecord && type.TypeKind == TypeKind.Class)
            {
                // Records con init-only properties son inmutables
                isImmutable = true;
            }
            else if (type.TypeKind == TypeKind.Struct)
            {
                // Verificar readonly struct
                if (!type.IsReadOnly)
                {
                    isImmutable = false;
                }
            }
            else
            {
                // Class regular: verificar properties settables
                var hasSettableProperties = type.GetMembers().OfType<IPropertySymbol>()
                    .Any(p => p.SetMethod != null && p.SetMethod.DeclaredAccessibility != Accessibility.Private);

                if (hasSettableProperties)
                {
                    isImmutable = false;
                }
            }

            if (!isImmutable)
            {
                context.ReportDiagnostic(Diagnostic.Create(DomainEventImmutable, type.Locations[0]));
            }
        }

        private static void AnalyzeForBinaryFormatter(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            // R505: BinaryFormatter prohibido
            var usesFormatter = type.GetMembers().OfType<IMethodSymbol>().Any(m =>
            {
                if (m.DeclaringSyntaxReferences.Length == 0) return false;
                var syntax = m.DeclaringSyntaxReferences[0].GetSyntax();
                var text = syntax.ToString();
                return text.Contains("BinaryFormatter");
            });

            if (usesFormatter)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoBinaryFormatter, type.Locations[0]));
            }
        }

        private static void AnalyzeUseCaseComplexity(SymbolAnalysisContext context, IMethodSymbol method)
        {
            // DR07: Validar namespace antes de validar atributo
            var nsName = method.ContainingNamespace?.ToDisplayString() ?? "";
            if (!nsName.Contains(".Composition") && !nsName.Contains(".Application"))
                return; // Skip if not in application layer
            
            var isUseCase = method.ContainingType.GetAttributes().Any(a => a.AttributeClass?.Name == "UseCaseAttribute");
            if (!isUseCase) return;

            if (method.DeclaringSyntaxReferences.Length == 0) return;
            var syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
            var complexity = CalculateCyclomaticComplexity(syntax);

            // DR07: UseCase CC > 5
            if (complexity > 5)
            {
                context.ReportDiagnostic(Diagnostic.Create(UseCaseComplexityExceeded, method.Locations[0]));
            }
        }

        private static void AnalyzeAsyncInEngine(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (!method.IsAsync) return;
            
            var nsName = method.ContainingNamespace?.ToDisplayString() ?? "";
            if (!nsName.StartsWith("NetShaper.Engine")) return;
            
            if (method.GetAttributes().Any(a => a.AttributeClass?.Name == "EngineSetupAttribute"))
                return;
            
            context.ReportDiagnostic(Diagnostic.Create(AsyncInEngineProhibited, method.Locations[0]));
        }

        private static void AnalyzeVolatileInEngine(SymbolAnalysisContext context, IFieldSymbol field)
        {
            if (!field.IsVolatile) return;

            var nsName = field.ContainingNamespace?.ToDisplayString() ?? "";
            if (nsName.StartsWith("NetShaper.Engine"))
            {
                context.ReportDiagnostic(Diagnostic.Create(NoVolatileInEngine, field.Locations[0]));
            }
        }

        private static void AnalyzeAsyncNamingAndSignature(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (!method.IsAsync) return;

            // R704: Async suffix
            if (!method.Name.EndsWith("Async") && method.MethodKind != MethodKind.Constructor)
            {
                context.ReportDiagnostic(Diagnostic.Create(MissingAsyncSuffix, method.Locations[0], method.Name));
            }

            // R1202: async void solo para event handlers
            if (method.ReturnsVoid)
            {
                // Verificar si es event handler por naming convention
                bool isEventHandler = method.Parameters.Length == 2 && 
                                     method.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                                     method.Parameters[1].Type.Name.EndsWith("EventArgs");
                
                if (!isEventHandler)
                {
                    context.ReportDiagnostic(Diagnostic.Create(AsyncVoidIsForEventHandlersOnly, method.Locations[0]));
                }
            }

            // R312: CancellationToken requerido
            // Skip event handlers (verificados arriba)
            bool isEventHandlerMethod = method.Parameters.Length == 2 && 
                                 method.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                                 method.Parameters[1].Type.Name.EndsWith("EventArgs");
            
            if (!isEventHandlerMethod)
            {
                var hasCancellationToken = method.Parameters.Any(p => p.Type.Name == "CancellationToken");
                if (!hasCancellationToken && method.DeclaredAccessibility == Accessibility.Public)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MissingCancellationToken, method.Locations[0]));
                }
            }
        }

        private static void AnalyzeDisposableFields(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var disposableFields = type.GetMembers().OfType<IFieldSymbol>()
                .Where(f => f.Type.AllInterfaces.Any(i => i.Name == "IDisposable"))
                .ToList();

            if (disposableFields.Count == 0) return;

            var implementsIDisposable = type.AllInterfaces.Any(i => i.Name == "IDisposable");
            if (!implementsIDisposable)
            {
                context.ReportDiagnostic(Diagnostic.Create(DisposableFieldNeedsDisposing, type.Locations[0], type.Name));
            }
        }

        private static void AnalyzeMethod(SymbolAnalysisContext context) 
        {
            if (context.Symbol is not IMethodSymbol method) return;
            AnalyzeCyclomaticComplexity(context, method);
            AnalyzeAsyncInEngine(context, method);
            AnalyzeAsyncNamingAndSignature(context, method);
            AnalyzePascalCase(context, method);
            AnalyzeUseCaseComplexity(context, method);
            
            // FASE 1+2
            AnalyzeByteArrayParameter(context, method);
            AnalyzeArrayPoolBalance(context, method);
            
            // FASES 3-5
            AnalyzeNestingDepth(context, method);
            AnalyzeSafeHandleRequired(context, method);
            AnalyzeInputValidation(context, method);
            AnalyzeNullableParameter(context, method);
            
            if (method.MethodKind == MethodKind.Constructor) AnalyzeConstructorDependencies(context, method);
        }

        private static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IPropertySymbol property) return;
            AnalyzePascalCase(context, property);
        }

        private static int CalculateCyclomaticComplexity(SyntaxNode node) => 
            node.DescendantNodes().Count(n => 
                n is IfStatementSyntax || 
                n is ForStatementSyntax || 
                n is ForEachStatementSyntax || 
                n is WhileStatementSyntax || 
                n is DoStatementSyntax || 
                n is CaseSwitchLabelSyntax || 
                n is ConditionalExpressionSyntax ||
                n is BinaryExpressionSyntax binary && (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression))
            ) + 1;

        private static void AnalyzeCyclomaticComplexity(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (method.DeclaringSyntaxReferences.Length == 0) return;
            var syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
            var complexity = CalculateCyclomaticComplexity(syntax);

            var nsName = method.ContainingNamespace?.ToDisplayString() ?? "";
            int limit = 10; // Default

            if (nsName.StartsWith("NetShaper.Engine"))
            {
                limit = 7;
                // Verificar [ProtocolParser] para CC≤12
                if (method.GetAttributes().Any(a => a.AttributeClass?.Name == "ProtocolParserAttribute"))
                {
                    limit = 12;
                }
            }

            if (complexity > limit)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    CyclomaticComplexityExceeded, 
                    method.Locations[0], 
                    method.Name, 
                    complexity, 
                    limit
                ));
            }
            
            // HotPath: Validar complejidad en métodos con [HotPath]
            var hasHotPath = method.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "HotPathAttribute");
            
            if (hasHotPath && complexity > 7)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    HotPathComplexityViolation,
                    method.Locations[0],
                    method.Name,
                    complexity));
            }
        }

        private static void AnalyzeConstructorDependencies(SymbolAnalysisContext context, IMethodSymbol constructor)
        {
            var paramCount = constructor.Parameters.Length;
            const int limit = 4;

            // Excepto [CompositionRoot]
            if (constructor.ContainingType.GetAttributes().Any(a => a.AttributeClass?.Name == "CompositionRootAttribute"))
                return;

            if (paramCount > limit)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ConstructorDependencyLimit, 
                    constructor.Locations[0], 
                    paramCount
                ));
            }
        }

        private static void AnalyzeOneClassPerFile(SyntaxTreeAnalysisContext context)
        {
            var root = context.Tree.GetRoot();
            var publicTypes = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(t => t.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                .ToList();

            if (publicTypes.Count > 1)
            {
                foreach (var type in publicTypes.Skip(1))
                {
                    context.ReportDiagnostic(Diagnostic.Create(OneClassPerFile, type.Identifier.GetLocation()));
                }
            }
        }
        
        private static void AnalyzeLockStatement(SyntaxNodeAnalysisContext context)
        {
            var isInEngine = context.Node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;
            if (isInEngine)
            {
                context.ReportDiagnostic(Diagnostic.Create(LockInEngineProhibited, context.Node.GetLocation()));
            }
        }

        private static void AnalyzePascalCase(SymbolAnalysisContext context, ISymbol symbol)
        {
            if (symbol.DeclaredAccessibility != Accessibility.Public) return;
            
            if (symbol is IMethodSymbol method)
            {
                if (method.MethodKind == MethodKind.PropertyGet ||
                    method.MethodKind == MethodKind.PropertySet ||
                    method.MethodKind == MethodKind.Constructor ||
                    method.MethodKind == MethodKind.StaticConstructor ||
                    method.MethodKind == MethodKind.EventAdd ||
                    method.MethodKind == MethodKind.EventRemove ||
                    method.MethodKind == MethodKind.Destructor)
                {
                    return;
                }
            }
            
            if (string.IsNullOrEmpty(symbol.Name) || !char.IsUpper(symbol.Name[0]))
            {
                context.ReportDiagnostic(Diagnostic.Create(PascalCaseNaming, symbol.Locations[0], symbol.Name));
            }
        }

        private static void AnalyzeLiterals(SyntaxNodeAnalysisContext context)
        {
            var literal = (LiteralExpressionSyntax)context.Node;
            if (literal.Token.Value is not int and not long and not double) return;
            
            int value = literal.Token.Value switch
            {
                int i => i,
                long l => (int)l,
                _ => 0
            };
            
            // FIX: Whitelist for protocol constants (TCP/IP offsets)
            var protocolOffsets = new[] { 8, 20, 28, 40 }; // TCP header, IP header sizes
            if (protocolOffsets.Contains(value)) return;
            
            // FIX: Skip bitwise operations (shifts, masks)
            var parent = literal.Parent;
            if (parent is BinaryExpressionSyntax bin)
            {
                if (bin.IsKind(SyntaxKind.LeftShiftExpression) || 
                    bin.IsKind(SyntaxKind.RightShiftExpression) ||
                    bin.IsKind(SyntaxKind.BitwiseAndExpression) ||
                    bin.IsKind(SyntaxKind.BitwiseOrExpression))
                {
                    var bitConstants = new[] { 4, 8 }; // Common bit shifts
                    if (bitConstants.Contains(value)) return;
                }
            }
            
            // Common acceptable values
            if (value == 0 || value == 1 || value == 2 || value == -1 || value == 100 || value == 1000)
                return;

            // Verificar si es en campo const o readonly static
            if (literal.Parent is EqualsValueClauseSyntax equalsValue &&
                equalsValue.Parent is VariableDeclaratorSyntax varDeclarator &&
                varDeclarator.Parent is VariableDeclarationSyntax varDeclaration &&
                varDeclaration.Parent is FieldDeclarationSyntax fd)
            {
                bool hasConst = fd.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));
                if (hasConst) return;
                
                bool hasStatic = fd.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
                bool hasReadonly = fd.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
                if (hasStatic && hasReadonly)
                    return;
            }
            
            context.ReportDiagnostic(Diagnostic.Create(MagicNumberProhibited, literal.GetLocation(), literal.Token.ValueText));
        }

        // FASE 1+2: Nuevos analizadores

        private static void AnalyzeTaskUsage(SyntaxNodeAnalysisContext context)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(context.Node);
            var typeStr = typeInfo.Type?.ToDisplayString() ?? "";
            
            if (typeStr.StartsWith("System.Threading.Tasks.Task"))
            {
                var isInEngine = context.Node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;
                if (!isInEngine) return;
                
                // Verificar [EngineSetup]
                var method = context.Node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (method != null)
                {
                    var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
                    if (methodSymbol?.GetAttributes().Any(a => a.AttributeClass?.Name == "EngineSetupAttribute") ?? false)
                        return;
                }
                
                context.ReportDiagnostic(Diagnostic.Create(TaskInEngineProhibited, context.Node.GetLocation()));
            }
        }

        private static void AnalyzeDateTimeUsage(SyntaxNodeAnalysisContext context)
        {
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;
            var memberName = memberAccess.ToString();
            
            if (memberName == "DateTime.Now" || 
                memberName == "DateTime.UtcNow" ||
                memberName == "Environment.TickCount" ||
                memberName == "Environment.TickCount64")
            {
                var isInEngine = memberAccess.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;
                if (isInEngine)
                {
                    context.ReportDiagnostic(Diagnostic.Create(DateTimeInEngineProhibited, memberAccess.GetLocation()));
                }
            }
        }

        private static void AnalyzeExceptionControlFlow(SyntaxNodeAnalysisContext context)
        {
            var tryStatement = (TryStatementSyntax)context.Node;
            
            // Verificar try-catch en loop
            bool isInLoop = tryStatement.Ancestors().Any(n => 
                n is ForStatementSyntax || 
                n is ForEachStatementSyntax || 
                n is WhileStatementSyntax ||
                n is DoStatementSyntax);
            
            if (isInLoop)
            {
                foreach (var catchClause in tryStatement.Catches)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ExceptionsAsControlFlow, catchClause.GetLocation()));
                }
            }
        }

        private static void AnalyzeArrayCreationForPool(SyntaxNodeAnalysisContext context)
        {
            var arrayCreation = (ArrayCreationExpressionSyntax)context.Node;
            
            // Verificar si es byte[]
            var typeInfo = context.SemanticModel.GetTypeInfo(arrayCreation);
            if (typeInfo.Type is not IArrayTypeSymbol arrayType || 
                arrayType.ElementType.SpecialType != SpecialType.System_Byte)
                return;
            
            var isInEngine = arrayCreation.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;
            if (!isInEngine) return;
            
            // Verificar [EngineSetup]
            var method = arrayCreation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (method != null)
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
                if (methodSymbol?.GetAttributes().Any(a => a.AttributeClass?.Name == "EngineSetupAttribute") ?? false)
                    return;
            }
            
            // Verificar tamaño si es literal
            if (arrayCreation.Type.RankSpecifiers.Count > 0)
            {
                var rankSpecifier = arrayCreation.Type.RankSpecifiers[0];
                if (rankSpecifier.Sizes.Count > 0)
                {
                    var sizeExpr = rankSpecifier.Sizes[0];
                    var constValue = context.SemanticModel.GetConstantValue(sizeExpr);
                    if (constValue.HasValue)
                    {
                        var size = Convert.ToInt32(constValue.Value);
                        if (size <= 16) return; // Stackalloc pequeño OK
                    }
                }
            }
            
            // HotPath: Validar en métodos con [HotPath]
            var containingMethod = arrayCreation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod != null)
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
                if (methodSymbol != null)
                {
                    var hasHotPath = methodSymbol.GetAttributes()
                        .Any(a => a.AttributeClass?.Name == "HotPathAttribute");
                    
                    if (hasHotPath)
                    {
                        // Verificar tamaño para HotPath  
                        if (arrayCreation.Type.RankSpecifiers.Count > 0)
                        {
                            var rankSpecifier = arrayCreation.Type.RankSpecifiers[0];
                            if (rankSpecifier.Sizes.Count > 0)
                            {
                                var sizeExpr = rankSpecifier.Sizes[0];
                                var constValue = context.SemanticModel.GetConstantValue(sizeExpr);
                                if (constValue.HasValue)
                                {
                                    var size = Convert.ToInt32(constValue.Value);
                                    if (size > 16)
                                    {
                                        context.ReportDiagnostic(Diagnostic.Create(
                                            HotPathArrayCreationViolation,
                                            arrayCreation.GetLocation()));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            context.ReportDiagnostic(Diagnostic.Create(ArrayPoolRequired, arrayCreation.GetLocation()));
        }

        private static void AnalyzeByteArrayParameter(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (method.DeclaredAccessibility != Accessibility.Public) return;
            
            var nsName = method.ContainingNamespace?.ToDisplayString() ?? "";
            if (!nsName.StartsWith("NetShaper.Engine")) return;
            
            foreach (var param in method.Parameters)
            {
                if (param.Type is IArrayTypeSymbol array &&
                    array.ElementType.SpecialType == SpecialType.System_Byte)
                {
                    context.ReportDiagnostic(Diagnostic.Create(SpanMemoryPreferred, param.Locations[0]));
                }
            }
        }

        private static void AnalyzeArrayPoolBalance(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (method.DeclaringSyntaxReferences.Length == 0) return;
            var syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
            
            // Contar Rent y Return
            var invocations = syntax.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();
            
            int rentCount = invocations.Count(inv => 
                inv.ToString().Contains("ArrayPool") && inv.ToString().Contains(".Rent("));
            
            int returnCount = invocations.Count(inv => 
                inv.ToString().Contains("ArrayPool") && inv.ToString().Contains(".Return("));
            
            if (rentCount == 0) return; // No usa ArrayPool
            
            // Verificar excepciones
            bool hasBufferOwner = method.GetAttributes().Any(a => 
                a.AttributeClass?.Name == "BufferOwnerAttribute");
            
            bool returnsArray = method.ReturnType is IArrayTypeSymbol;
            
            if (rentCount != returnCount && !hasBufferOwner && !returnsArray)
            {
                context.ReportDiagnostic(Diagnostic.Create(ArrayPoolOwnership, method.Locations[0]));
            }
        }

        private static void AnalyzeArrayPoolBufferSize(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            // Detectar ArrayPool.Shared.Rent(N)
            var invocationStr = invocation.ToString();
            if (!invocationStr.Contains("ArrayPool") || !invocationStr.Contains(".Rent("))
                return;
            
            if (invocation.ArgumentList.Arguments.Count == 0) return;
            
            var arg = invocation.ArgumentList.Arguments[0];
            var constValue = context.SemanticModel.GetConstantValue(arg.Expression);
            
            if (constValue.HasValue)
            {
                var size = Convert.ToInt32(constValue.Value);
                var validSizes = new[] { 1500, 2048, 4096, 8192, 65536 };
                
                if (!validSizes.Contains(size))
                {
                    context.ReportDiagnostic(Diagnostic.Create(BufferSizeNonStandard, arg.GetLocation()));
                }
            }
        }

        private static void AnalyzeEngineLogging(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol method) return;
            
            // Detectar ILogger.Log*
            var containingType = method.ContainingType?.ToDisplayString() ?? "";
            if (!containingType.Contains("ILogger")) return;
            
            var isInEngine = invocation.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString().StartsWith("NetShaper.Engine") ?? false;
            if (!isInEngine) return;
            
            // Verificar si usa string literals o interpolation
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (arg.Expression is LiteralExpressionSyntax literal && literal.Token.ValueText is string ||
                    arg.Expression is InterpolatedStringExpressionSyntax)
                {
                    context.ReportDiagnostic(Diagnostic.Create(StructuredLoggingRequired, arg.GetLocation()));
                }
            }
        }

        private static void AnalyzeCatchExceptionBoundary(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;
            
            if (catchClause.Declaration?.Type.ToString() != "Exception") return;
            
            // Verificar si está en boundary
            var method = catchClause.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (method == null) return;
            
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
            if (methodSymbol == null) return;
            
            // Verificar atributo [Boundary]
            bool isBoundary = methodSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "BoundaryAttribute");
            bool isMain = method.Identifier.Text == "Main";
            
            // Verificar si es controller (simplificado)
            bool isController = methodSymbol.ContainingType?.BaseType?.Name.Contains("Controller") ?? false;
            
            bool isProgram = method.FirstAncestorOrSelf<CompilationUnitSyntax>()?.SyntaxTree.FilePath.EndsWith("Program.cs") ?? false;
            
            if (!isBoundary && !isMain && !isController && !isProgram)
            {
                context.ReportDiagnostic(Diagnostic.Create(CatchExceptionOnlyBoundaries, catchClause.GetLocation()));
            }
        }

        private static void AnalyzeDisposableVariable(SyntaxNodeAnalysisContext context)
        {
            var variable = (VariableDeclaratorSyntax)context.Node;
            
            if (variable.Parent?.Parent is not VariableDeclarationSyntax declaration) return;
            
            var typeInfo = context.SemanticModel.GetTypeInfo(declaration.Type);
            if (typeInfo.Type == null) return;
            
            // Verificar si implementa IDisposable
            bool isDisposable = typeInfo.Type.AllInterfaces.Any(i => i.Name == "IDisposable") ||
                               typeInfo.Type.Name == "IDisposable";
            
            if (!isDisposable) return;
            
            // Verificar si está en using statement
            bool isInUsing = variable.Ancestors().OfType<UsingStatementSyntax>().Any();
            
            // Verificar using declaration (C# 8+)
            bool isUsingDeclaration = declaration.Parent is LocalDeclarationStatementSyntax localDecl &&
                                     !localDecl.UsingKeyword.IsKind(SyntaxKind.None);
            
            if (isInUsing || isUsingDeclaration) return;
            
            // Verificar si hay .Dispose() llamado en el método  
            var method = variable.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (method != null)
            {
                var variableName = variable.Identifier.Text;
                bool hasDispose = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(inv => inv.ToString().Contains(variableName) && inv.ToString().Contains("Dispose"));
                
                if (hasDispose) return;
            }
            
            context.ReportDiagnostic(Diagnostic.Create(DisposableWithoutUsing, variable.GetLocation()));
        }

        // FASES 3-5: Métodos adicionales

        private static void AnalyzeNestingDepth(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (method.DeclaringSyntaxReferences.Length == 0) return;
            var syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
            int maxNesting = CalculateMaxNesting(syntax);
            var nsName = method.ContainingNamespace?.ToDisplayString() ?? "";
            int limit = 3;
            if (nsName.StartsWith("NetShaper.Engine"))
            {
                limit = 2;
                if (method.GetAttributes().Any(a => a.AttributeClass?.Name == "BatchProcessorAttribute")) limit = 4;
            }
            if (maxNesting > limit)
                context.ReportDiagnostic(Diagnostic.Create(NestingDepthExceeded, method.Locations[0], method.Name, maxNesting, limit));
        }

        private static int CalculateMaxNesting(SyntaxNode node, int currentDepth = 0)
        {
            int maxDepth = currentDepth;
            foreach (var child in node.ChildNodes())
            {
                int childDepth = currentDepth;
                if (child is IfStatementSyntax || child is ForStatementSyntax || child is ForEachStatementSyntax ||
                    child is WhileStatementSyntax || child is DoStatementSyntax || child is SwitchStatementSyntax || child is TryStatementSyntax)
                    childDepth++;
                maxDepth = Math.Max(maxDepth, CalculateMaxNesting(child, childDepth));
            }
            return maxDepth;
        }

        private static void AnalyzeSafeHandleRequired(SymbolAnalysisContext context, IMethodSymbol method)
        {
            var hasPInvoke = method.GetAttributes().Any(a => a.AttributeClass?.Name == "DllImportAttribute" || a.AttributeClass?.Name == "LibraryImportAttribute");
            if (hasPInvoke && method.ReturnType.SpecialType == SpecialType.System_IntPtr)
                context.ReportDiagnostic(Diagnostic.Create(SafeHandleRequired, method.Locations[0]));
        }

        private static void AnalyzeFixedStatement(SyntaxNodeAnalysisContext context)
        {
            var fixedStatement = (FixedStatementSyntax)context.Node;
            var nsName = fixedStatement.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString() ?? "";
            
            // R504: fixed SOLO permitido en Engine/Native (whitelist)
            if (!nsName.StartsWith("NetShaper.Engine") && !nsName.StartsWith("NetShaper.Native"))
            {
                context.ReportDiagnostic(Diagnostic.Create(FixedOnlyInEngineNative, fixedStatement.GetLocation()));
            }
        }

        // FASE 3: Mejoras de Heurísticas
        
        /// <summary>
        /// R5.06 Hardcoded Secrets Detection [HEURISTIC]
        /// LIMITACIÓN: Pattern-based detection (keywords + entropía).
        /// NO es análisis semántico real. Tiene whitelists para reducir FP.
        /// </summary>
        private static void AnalyzeHardcodedSecrets(SyntaxNodeAnalysisContext context)
        {
            var literal = (LiteralExpressionSyntax)context.Node;
            if (literal.Token.Value is not string text || text.Length < 8) return;

            // Whitelist: Archivos de test/benchmark
            var filePath = context.Node.SyntaxTree.FilePath;
            if (filePath.Contains(".Test.") || filePath.Contains(".Benchmark.") || 
                filePath.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase))
                return;

            // Whitelist: UUIDs/GUIDs
            if (System.Text.RegularExpressions.Regex.IsMatch(text, 
                @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"))
                return;

            // Whitelist: const fields
            if (literal.Parent is EqualsValueClauseSyntax equalsValue &&
                equalsValue.Parent is VariableDeclaratorSyntax varDecl &&
                varDecl.Parent is VariableDeclarationSyntax varDeclaration &&
                varDeclaration.Parent is FieldDeclarationSyntax fieldDecl)
            {
                if (fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                    return;
            }

            // Detección: keyword sospechoso + entropía alta
            var lowerText = text.ToLowerInvariant();
            var suspiciousKeywords = new[] { "password", "secret", "apikey", "api_key", "token", "private_key", "privatekey" };
            
            bool hasSuspiciousKeyword = suspiciousKeywords.Any(k => lowerText.Contains(k));
            
            if (hasSuspiciousKeyword && text.Length > 20)
            {
                var entropy = CalculateShannonEntropy(text);
                if (entropy > 4.5)
                {
                    context.ReportDiagnostic(Diagnostic.Create(HardcodedSecretDetected, literal.GetLocation()));
                }
            }
        }

        private static double CalculateShannonEntropy(string text)
        {
            var freq = text.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count() / (double)text.Length);
            return -freq.Values.Sum(p => p * Math.Log(p, 2));
        }

        private static void AnalyzeInputValidation(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (method.DeclaredAccessibility != Accessibility.Public || method.DeclaringSyntaxReferences.Length == 0) return;
            var syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
            
            foreach (var param in method.Parameters)
            {
                // FIX: Skip value types and ref structs (Span<T>, ReadOnlySpan<T>)
                if (param.Type.IsValueType || param.Type.IsRefLikeType)
                    continue; // Value types y Span<T> no pueden ser null
                    
                var paramName = param.Name;
                var paramIdentifier = $"({paramName})";
                
                bool hasGuard = syntax.ToString().Contains($"ArgumentNullException.ThrowIfNull({paramName})") ||
                               syntax.ToString().Contains($"throw new ArgumentNullException(nameof({paramName}))") ||
                               syntax.ToString().Contains($"if ({paramName} == null)") ||
                               syntax.ToString().Contains($"if ({paramName} is null)");
                
                if (!hasGuard)
                    context.ReportDiagnostic(Diagnostic.Create(MissingInputValidation, param.Locations[0]));
            }
        }

        private static void AnalyzeNullableParameter(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (method.DeclaredAccessibility != Accessibility.Public) return;
            foreach (var param in method.Parameters)
                if (!param.Type.IsValueType && param.NullableAnnotation == NullableAnnotation.Annotated)
                    context.ReportDiagnostic(Diagnostic.Create(NullableParameterInPublicApi, param.Locations[0]));
        }

        private static void AnalyzeArrayPoolCreation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!invocation.ToString().Contains("ArrayPool") || !invocation.ToString().Contains("Rent"))
                return;

            var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod == null) return;
            
            // Skip if method has [BufferOwner] (ownership transfer pattern)
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
            if (methodSymbol != null)
            {
                var hasBufferOwner = methodSymbol.GetAttributes()
                    .Any(a => a.AttributeClass?.Name == "BufferOwnerAttribute");
                if (hasBufferOwner) return; // Ownership transfer válido (DNS §7.03)
            }

            var methodText = containingMethod.ToString();
            int rentCount = System.Text.RegularExpressions.Regex.Matches(methodText, @"\.Rent\(").Count;
            int returnCount = System.Text.RegularExpressions.Regex.Matches(methodText, @"\.Return\(").Count;

            if (rentCount > returnCount)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ArrayPoolOwnership,
                    invocation.GetLocation(),
                    containingMethod.Identifier.Text));
            }
        }
        
        private static void AnalyzeRedundantThis(SyntaxNodeAnalysisContext context)
        {
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;
            if (memberAccess.Expression is not ThisExpressionSyntax) return;
            var enclosingMethod = memberAccess.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (enclosingMethod != null)
            {
                var hasShadowing = enclosingMethod.ParameterList?.Parameters.Any(p => p.Identifier.Text == memberAccess.Name.Identifier.Text) ?? false;
                if (!hasShadowing)
                    context.ReportDiagnostic(Diagnostic.Create(RedundantThis, memberAccess.Expression.GetLocation()));
            }
        }

        private static void AnalyzeVarUsage(SyntaxNodeAnalysisContext context)
        {
            var variableDeclaration = (VariableDeclarationSyntax)context.Node;
            if (variableDeclaration.Type.IsVar) return;
            var declarator = variableDeclaration.Variables.FirstOrDefault();
            if (declarator?.Initializer?.Value is ObjectCreationExpressionSyntax or CastExpressionSyntax)
                context.ReportDiagnostic(Diagnostic.Create(VarPreferred, variableDeclaration.Type.GetLocation()));
        }

        private static void AnalyzeDomainAttribute(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var nsName = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (!nsName.Contains("Domain") && !nsName.Contains("Abstractions")) return;
            if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct) return;
            var domainAttributes = new[] { "EntityAttribute", "ValueObjectAttribute", "AggregateRootAttribute", "DomainEventAttribute", "UseCaseAttribute" };
            var hasDomainAttribute = type.GetAttributes().Any(a => domainAttributes.Contains(a.AttributeClass?.Name ?? ""));
            if (!hasDomainAttribute && type.DeclaredAccessibility == Accessibility.Public && 
                !type.GetAttributes().Any(a => a.AttributeClass?.Name == "InfrastructureAttribute"))
                context.ReportDiagnostic(Diagnostic.Create(MissingDomainAttribute, type.Locations[0]));
        }

        private static void AnalyzeThrowInTechnicalDomain(SyntaxNodeAnalysisContext context)
        {
            var throwStatement = (ThrowStatementSyntax)context.Node;
            var nsName = throwStatement.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString() ?? "";
            if (!nsName.StartsWith("NetShaper.Engine") && !nsName.StartsWith("NetShaper.Native")) return;
            
            // Verificar si está en un constructor (tienen su propia sintaxis)
            var constructor = throwStatement.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
            if (constructor != null) return;  // Constructores están permitidos
            
            var method = throwStatement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (method != null)
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
                if (methodSymbol != null)
                {
                    // Excluir métodos marcados como setup o boundaries
                    if (methodSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "EngineSetupAttribute" || a.AttributeClass?.Name == "BoundaryAttribute"))
                        return;
                }
            }
            context.ReportDiagnostic(Diagnostic.Create(ThrowInTechnicalDomain, throwStatement.GetLocation()));
        }

        private static void AnalyzeStructuredLoggingRecommended(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol method || !(method.ContainingType?.ToDisplayString().Contains("ILogger") ?? false)) return;
            foreach (var arg in invocation.ArgumentList.Arguments)
                if (arg.Expression is LiteralExpressionSyntax literal && literal.Token.ValueText is string str && !str.Contains("{") && str.Length > 5)
                    context.ReportDiagnostic(Diagnostic.Create(StructuredLoggingRecommended, arg.GetLocation()));
        }

        // Reglas adicionales

        private static void AnalyzeMemberOrder(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var members = type.GetMembers().Where(m => !m.IsImplicitlyDeclared).OrderBy(m => m.Locations[0].SourceSpan.Start).ToList();
            if (members.Count < 2) return;
            
            int lastOrder = -1;
            foreach (var member in members)
            {
                int currentOrder = GetMemberOrderPriority(member);
                if (currentOrder < lastOrder)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MemberOrderViolation, member.Locations[0], member.Name));
                }
                lastOrder = currentOrder;  // FIX: Era Math.Max(lastOrder, currentOrder) - causaba FP
            }
        }

        private static int GetMemberOrderPriority(ISymbol member)
        {
            return member switch
            {
                IFieldSymbol f when f.IsConst => 0,
                IFieldSymbol f when f.IsStatic => 1,
                IFieldSymbol => 2,
                IMethodSymbol m when m.MethodKind == MethodKind.StaticConstructor => 3,
                IMethodSymbol m when m.MethodKind == MethodKind.Constructor => 4,
                IPropertySymbol p when p.IsStatic => 5,
                IPropertySymbol => 6,
                IMethodSymbol m when m.IsStatic => 7,
                IMethodSymbol => 8,
                _ => 99
            };
        }

        private static void AnalyzeDIViaConstructor(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class) return;
            var nsName = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (nsName.StartsWith("NetShaper.Engine") || nsName.StartsWith("NetShaper.UI")) return;
            
            // Buscar campos que parezcan dependencias (interfaces, servicios)
            var dependencyFields = type.GetMembers().OfType<IFieldSymbol>()
                .Where(f => !f.IsStatic && !f.IsConst && 
                           (f.Type.TypeKind == TypeKind.Interface || 
                            f.Type.Name.EndsWith("Service") || 
                            f.Type.Name.EndsWith("Repository") ||
                            f.Type.Name.EndsWith("Factory")))
                .ToList();
            
            if (dependencyFields.Count == 0) return;
            
            // Verificar si hay constructores que NO inyectan estas dependencias
            var constructors = type.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic).ToList();
            
            bool anyConstructorInjectsDeps = constructors.Any(ctor => ctor.Parameters.Length >= dependencyFields.Count / 2);
            
            if (!anyConstructorInjectsDeps && dependencyFields.Count > 2)
            {
                context.ReportDiagnostic(Diagnostic.Create(DINotViaConstructor, type.Locations[0], type.Name));
            }
        }

        private static void AnalyzeInvariantTests(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var nsName = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (!nsName.Contains("Domain")) return;
            
            // Solo aplicar a tipos con invariantes
            var hasInvariant = type.GetMembers().Any(m => 
                m.Name.Contains("Invariant") || 
                m.Name.Contains("Validate") || 
                m.GetAttributes().Any(a => a.AttributeClass?.Name == "InvariantAttribute"));
            
            if (!hasInvariant) return;
            
            // Verificar si existe clase de test (heurística: nombre del tipo + "Tests")
            // Nota: Esto es limitado ya que no podemos acceder fácilmente a los tests desde el analyzer
            // Solo reportamos como WARNING
            var hasTestAttribute = type.GetAttributes().Any(a => a.AttributeClass?.Name == "TestedAttribute");
            
            if (!hasTestAttribute)
            {
                context.ReportDiagnostic(Diagnostic.Create(MissingInvariantTests, type.Locations[0], type.Name));
            }
        }

        private static void AnalyzeTransactionalEvents(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var nsName = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (!nsName.StartsWith("NetShaper.Engine")) return;
            
            // Buscar eventos con nombres que sugieran transacciones
            var events = type.GetMembers().OfType<IEventSymbol>().ToList();
            foreach (var evt in events)
            {
                if (evt.Name.Contains("Transaction") || 
                    evt.Name.Contains("Commit") || 
                    evt.Name.Contains("Rollback") ||
                    evt.Type.Name.Contains("Transaction"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(TransactionalEventsInEngine, evt.Locations[0]));
                }
            }
        }

        // Final batch

        private static void AnalyzeAbstractionsDependencies(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            var nsName = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (!nsName.StartsWith("NetShaper.Abstractions")) return;
            
            // Buscar usos de tipos de otros namespaces NetShaper (excepto System.*)
            var members = type.GetMembers();
            foreach (var member in members)
            {
                ITypeSymbol typeToCheck = member switch
                {
                    IFieldSymbol field => field.Type,
                    IPropertySymbol prop => prop.Type,
                    IMethodSymbol method => method.ReturnType,
                    _ => null
                };
                
                if (typeToCheck != null)
                {
                    var typeName = typeToCheck.ToDisplayString();
                    if (typeName.StartsWith("NetShaper.") && 
                        !typeName.StartsWith("NetShaper.Abstractions") &&
                        !typeName.StartsWith("System."))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(AbstractionsWithDependencies, member.Locations[0]));
                    }
                }
            }
        }

        private static void AnalyzeLowCohesion(SymbolAnalysisContext context, INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class) return;
            
            // Excluir Engine/UI (infraestructura compleja legítima)
            var nsName = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (nsName.StartsWith("NetShaper.Engine") || nsName.StartsWith("NetShaper.UI"))
                return;
            
            var fields = type.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic && !f.IsConst).ToList();
            var methods = type.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsStatic).ToList();
            
            if (fields.Count < 3 || methods.Count < 3) return;
            
            // Heurística simple: contar cuántos métodos NO usan ningún campo
            int methodsNotUsingFields = 0;
            foreach (var method in methods)
            {
                if (method.DeclaringSyntaxReferences.Length == 0) continue;
                var syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
                
                bool usesAnyField = fields.Any(f => syntax.ToString().Contains(f.Name));
                if (!usesAnyField)
                    methodsNotUsingFields++;
            }
            
            // Si >50% de métodos no usan campos, posible baja cohesión
            if (methodsNotUsingFields > methods.Count / 2)
            {
                context.ReportDiagnostic(Diagnostic.Create(LowCohesion, type.Locations[0], type.Name));
            }
        }

        // FASE 2: Enforcement Crítico
        
        /// <summary>
        /// R3.14 EngineSetup Boundary Validation
        /// LIMITACIÓN: Call-graph directo SOLAMENTE. No interprocedural analysis.
        /// NO detecta: invocaciones transitivas (A→B→EngineSetup donde A no tiene [EngineSetup])
        /// SÍ detecta: invocaciones directas desde hot-path a [EngineSetup]
        /// Justificación: Roslyn DiagnosticAnalyzer no tiene CFG interprocedural completo
        /// </summary>
        private static void AnalyzeEngineSetupBoundary(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            
            if (symbolInfo.Symbol is not IMethodSymbol targetMethod) return;
            
            // Verificar si el método invocado tiene [EngineSetup]
            var hasEngineSetup = targetMethod.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "EngineSetupAttribute");
            
            if (!hasEngineSetup) return;
            
            // Verificar contexto del caller
            var callingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            var callingCtor = invocation.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
            
            // ✅ Permitido en constructores
            if (callingCtor != null) return;
            
            // ✅ Permitido en Main/Program.cs
            var sourceTree = invocation.SyntaxTree;
            if (sourceTree.FilePath.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase))
                return;
                
            if (callingMethod != null)
            {
                var callerSymbol = context.SemanticModel.GetDeclaredSymbol(callingMethod);
                if (callerSymbol != null)
                {
                    // ✅ Permitido si el caller también es [EngineSetup]
                    var callerHasEngineSetup = callerSymbol.GetAttributes()
                        .Any(a => a.AttributeClass?.Name == "EngineSetupAttribute");
                    
                    if (callerHasEngineSetup) return;
                    
                    // ✅ Permitido en método "Main"
                    if (callerSymbol.Name == "Main") return;
                }
            }
            
            // ❌ Invocación desde hot-path no permitida
            context.ReportDiagnostic(Diagnostic.Create(
                EngineSetupBoundaryViolation,
                invocation.GetLocation(),
                targetMethod.Name));
        }

        
        #endregion
    }
}

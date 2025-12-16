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
        private static readonly DiagnosticDescriptor UseCaseComplexityExceeded = new("DR07", "Complejidad ciclomática excedida", "[UseCase] no puede tener complejidad ciclomática > 5", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor MissingInvariantMethod = new("DR04", "Invariante no declarada", "Tipo de dominio debe declarar ValidateInvariant / TryValidateInvariant", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor MutableValueObject = new("DR02", "ValueObject mutable", "[ValueObject] debe ser inmutable", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor InvalidDomainState = new("DR06", "Estado de dominio inválido", "[DomainState] debe ser enum", "NetShaper.Domain", DiagnosticSeverity.Warning, true);
        private static readonly DiagnosticDescriptor GenericExceptionUsage = new("DR08", "Uso de Exception genérica", "Prohibido throw new System.Exception en dominio", "NetShaper.Domain", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NoRegionsAllowed = new("R205", "Prohibido el uso de #region", "El uso de #region está prohibido", "Style", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NoVolatileInEngine = new("R307", "Prohibido 'volatile' en Engine", "El modificador 'volatile' está prohibido en NetShaper.Engine", "NetShaper.Engine", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor UseLibraryImport = new("R502", "P/Invoke debe usar LibraryImport", "Reemplace [DllImport] con [LibraryImport]", "Security", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor NoBinaryFormatter = new("R505", "BinaryFormatter está prohibido", "BinaryFormatter es inseguro", "Security", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor PrivateFieldNaming = new("R701", "Campos privados deben usar _camelCase", "El campo privado '{0}' debe seguir el formato _camelCase", "Naming", DiagnosticSeverity.Error, true);
        private static readonly DiagnosticDescriptor EntityInEngineMustBeStruct = new("DR03", "Entity en Engine debe ser un struct", "Las entidades en NetShaper.Engine deben ser 'struct'", "NetShaper.Domain", DiagnosticSeverity.Error, true);
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
        #endregion

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UnsafeWithoutFixed, DomainExceptionInEngine, UseCaseComplexityExceeded, MissingInvariantMethod, MutableValueObject, InvalidDomainState, GenericExceptionUsage, 
                NoRegionsAllowed, NoVolatileInEngine, UseLibraryImport, NoBinaryFormatter, PrivateFieldNaming, EntityInEngineMustBeStruct, DomainEventImmutable,
                AsyncInEngineProhibited, LinqInHotPathProhibited, StringConcatInLoopProhibited, ToArrayToListInEngineProhibited, ExplicitGcCollectProhibited, ThreadSleepInUiProhibited,
                MissingCancellationToken, MissingAsyncSuffix, DisposableFieldNeedsDisposing, AsyncVoidIsForEventHandlersOnly, EmptyCatchBlockProhibited, NoTodos,
                ConstructorDependencyLimit, CyclomaticComplexityExceeded, OneClassPerFile, BoxingInEngine, LockInEngineProhibited, PascalCaseNaming, MagicNumberProhibited);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

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
            if (!isInEngine) return;

            context.ReportDiagnostic(Diagnostic.Create(BoxingInEngine, context.Operation.Syntax.GetLocation(), fromType.ToDisplayString(), toType.ToDisplayString()));
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context) { /*...*/ }
        private static void AnalyzeField(SymbolAnalysisContext context) 
        {
            if (context.Symbol is not IFieldSymbol field) return;

            if (field.IsConst || field.IsImplicitlyDeclared || field.DeclaredAccessibility != Accessibility.Private) return;

            if (!field.Name.StartsWith("_"))
            {
                context.ReportDiagnostic(Diagnostic.Create(PrivateFieldNaming, field.Locations[0], field.Name));
            }
        }
        private static void AnalyzeUnsafeBlock(SyntaxNodeAnalysisContext context) { /*...*/ }
        private static void AnalyzeThrow(SyntaxNodeAnalysisContext context) { /*...*/ }
        private static void AnalyzeDllImport(SyntaxNodeAnalysisContext context) { /*...*/ }
        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) { /*...*/ }
        private static void AnalyzeStringConcatInLoop(SyntaxNodeAnalysisContext context) { /*...*/ }
        private static void AnalyzeEmptyCatch(SyntaxNodeAnalysisContext context) { /*...*/ }
        private static void AnalyzeTodos(SyntaxTreeAnalysisContext context) { /*...*/ }
        private static void AnalyzeDomainType(SymbolAnalysisContext context, INamedTypeSymbol type) { /*...*/ }
        private static void AnalyzeEntityInEngine(SymbolAnalysisContext context, INamedTypeSymbol type) { /*...*/ }
        private static void AnalyzeDomainEventImmutability(SymbolAnalysisContext context, INamedTypeSymbol type) { /*...*/ }
        private static void AnalyzeForBinaryFormatter(SymbolAnalysisContext context, INamedTypeSymbol type) { /*...*/ }
        private static void AnalyzePrivateFieldNaming(SymbolAnalysisContext context, IFieldSymbol field) { /*...*/ }
        private static void AnalyzeUseCaseComplexity(SymbolAnalysisContext context, IMethodSymbol method) { /*...*/ }
        private static void AnalyzeAsyncInEngine(SymbolAnalysisContext context, IMethodSymbol method) { /*...*/ }
        private static void AnalyzeVolatileInEngine(SymbolAnalysisContext context, IFieldSymbol field) { /*...*/ }
        private static void AnalyzeAsyncNamingAndSignature(SymbolAnalysisContext context, IMethodSymbol method) { /*...*/ }
        private static void AnalyzeDisposableFields(SymbolAnalysisContext context, INamedTypeSymbol type) { /*...*/ }

        private static void AnalyzeMethod(SymbolAnalysisContext context) 
        {
            if (context.Symbol is not IMethodSymbol method) return;
            AnalyzeCyclomaticComplexity(context, method);
            AnalyzeAsyncInEngine(context, method);
            AnalyzeAsyncNamingAndSignature(context, method);
            AnalyzePascalCase(context, method);
            if (method.MethodKind == MethodKind.Constructor) AnalyzeConstructorDependencies(context, method);
        }

        private static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IPropertySymbol property) return;
            AnalyzePascalCase(context, property);
        }

        private static int CalculateCyclomaticComplexity(SyntaxNode node) => node.DescendantNodes().Count(n => n is IfStatementSyntax || n is ForStatementSyntax || n is ForEachStatementSyntax || n is WhileStatementSyntax || n is DoStatementSyntax || n is CaseSwitchLabelSyntax || n is ConditionalExpressionSyntax) + 1;

        private static void AnalyzeCyclomaticComplexity(SymbolAnalysisContext context, IMethodSymbol method) { /*...*/ }
        private static void AnalyzeConstructorDependencies(SymbolAnalysisContext context, IMethodSymbol constructor) { /*...*/ }
        private static void AnalyzeOneClassPerFile(SyntaxTreeAnalysisContext context) { /*...*/ }
        
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
            if (string.IsNullOrEmpty(symbol.Name) || !char.IsUpper(symbol.Name[0]))
            {
                context.ReportDiagnostic(Diagnostic.Create(PascalCaseNaming, symbol.Locations[0], symbol.Name));
            }
        }

        private static void AnalyzeLiterals(SyntaxNodeAnalysisContext context)
        {
            var literal = (LiteralExpressionSyntax)context.Node;
            if (!literal.Token.ValueText.All(char.IsDigit)) return; 
            var value = Convert.ToInt64(literal.Token.Value);
            if (value <= 2 && value >= -1) return;
            var parent = literal.Parent;
            if (parent is ConstantPatternSyntax || parent is CaseSwitchLabelSyntax || parent.Ancestors().OfType<AttributeSyntax>().Any()) return;
            if (parent.Parent is VariableDeclaratorSyntax v && v.Parent is VariableDeclarationSyntax vd && vd.Parent is FieldDeclarationSyntax fd && fd.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword) || m.IsKind(SyntaxKind.StaticKeyword) && m.IsKind(SyntaxKind.ReadOnlyKeyword))) return;
            context.ReportDiagnostic(Diagnostic.Create(MagicNumberProhibited, literal.GetLocation(), literal.Token.ValueText));
        }
        
        #endregion
    }
}

# NETSHAPER - REGLAS PARA ANALIZADOR ESTÁTICO

## 0. METADATOS DE ANÁLISIS

- **Motor:** Roslyn Analyzer + Custom Rules
- **Target:** C# 12, .NET 8
- **Severity Levels:** ERROR (bloquea build), WARNING (requiere justificación), INFO
- **Exit Codes:** 0 = pass, 1 = warnings, 2 = errors

---

## 1. ARQUITECTURA Y DEPENDENCIAS

### R1.01 - Dependencias de Assembly
- **Condición:** Assembly A → Assembly B solo si está en whitelist
- **Detección:** Análisis de referencias de proyecto (.csproj)
- **Whitelist:**
  - Abstractions → [ninguno]
  - Engine → [Abstractions]
  - Native → [Abstractions]
  - Infrastructure → [Abstractions]
  - Composition → [Abstractions, Engine, Native, Infrastructure]
  - UI → [Abstractions, Composition]
  - Benchmarks → [Abstractions, Composition]
  - StressTest → [Abstractions, Composition]
- **Violación:** Referencia fuera de whitelist
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R1.02 - Dependencias Cíclicas
- **Condición:** Grafo de dependencias debe ser DAG (Directed Acyclic Graph)
- **Detección:** Tarjan's algorithm sobre referencias de proyecto
- **Umbral:** 0 ciclos
- **Violación:** Ciclo detectado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R1.03 - Límite de Dependencias por Clase
- **Condición:** Constructor con N parámetros
- **Detección:** Contar parámetros de constructor público
- **Umbrales:**
  - ≤4 parámetros: PASS
  - >4 parámetros: ERROR
- **Excepción:** Clase decorada con `[CompositionRoot]` (sin límite).
- **Violación:** Excede umbral sin ser Root.
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R1.04 - Abstractions Sin Dependencias
- **Condición:** NetShaper.Abstractions.csproj no contiene referencias excepto BCL
- **Detección:** Parse .csproj, validar referencias
- **Whitelist BCL:** System.*, Microsoft.Extensions.Logging.Abstractions
- **Violación:** Referencia fuera de whitelist
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R1.05 - UI No Referencia Implementaciones
- **Condición:** Archivos en NetShaper.UI no contienen `using NetShaper.Engine;` ni `using NetShaper.Native;` ni `using NetShaper.Infrastructure;`
- **Detección:** Regex: `^\s*using\s+(NetShaper\.(Engine|Native|Infrastructure))\s*;`
- **Violación:** Match encontrado
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (eliminar using)

---

## 2. COMPLEJIDAD Y ESTRUCTURA

### R2.01 - Complejidad Ciclomática Engine
- **Condición:** Método en NetShaper.Engine namespace
- **Detección:** Calcular McCabe Cyclomatic Complexity
- **Umbrales:**
  - ≤7: PASS
  - >7: ERROR
- **Violación:** Excede umbral
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R2.02 - Complejidad Ciclomática UI/Infra
- **Condición:** Método en NetShaper.UI o NetShaper.Infrastructure namespace
- **Detección:** Calcular McCabe Cyclomatic Complexity
- **Umbral:** ≤10
- **Violación:** >10
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R2.03 - Anidación Máxima
- **Condición:** Método cualquiera
- **Detección:** Contar niveles de bloques anidados (if/for/while/foreach/switch/try)
- **Umbrales:**
  - NetShaper.Engine: ≤2 niveles (**Excepción:** ≤3 niveles permitidos exclusivamente dentro de métodos que procesan `ReceiveBatch` para iteración de buffers).
  - Resto de assemblies: ≤3 niveles
- **Violación:** Excede umbral
- **Severidad:** ERROR
- **Auto-fixable:** NO (sugerir guard clauses)

### R2.04 - Una Clase Por Archivo
- **Condición:** Archivo .cs
- **Detección:** Contar declaraciones `class`, `struct`, `interface`, `enum` (excluyendo nested)
- **Umbral:** 1 tipo público top-level
- **Excepción:** Tipos privados nested permitidos
- **Violación:** >1 tipo público top-level
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (split files)

### R2.05 - Sin Regiones
- **Condición:** Archivo .cs
- **Detección:** Regex: `^\s*#region`
- **Violación:** Match encontrado
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (eliminar #region/#endregion)

### R2.06 - Sin TODOs
- **Condición:** Archivo .cs en cualquier proyecto excepto *.StressTest
- **Detección:** Regex: `//\s*TODO|//\s*HACK|//\s*FIXME`
- **Violación:** Match encontrado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R2.07 - Cohesión LCOM4
- **Condición:** Clase con ≥3 métodos y ≥3 campos
- **Detección:** Calcular LCOM4 (Lack of Cohesion of Methods 4)
- **Umbral:** LCOM4 = 1 (una componente conexa)
- **Violación:** LCOM4 > 1
- **Severidad:** WARNING
- **Auto-fixable:** NO

---

## 3. HOT-PATH Y ENGINE

### R3.01 - Prohibido async en Engine
- **Condición:** Método en NetShaper.Engine namespace
- **Detección:** AST: MethodDeclarationSyntax con modifier `async`
- **Excepción:** Método con atributo `[EngineSetup]` permitido
- **Violación:** async sin [EngineSetup]
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.02 - Prohibido Task en Engine
- **Condición:** Expresión en NetShaper.Engine namespace
- **Detección:** AST: símbolos System.Threading.Tasks.Task, Task<T>, Task.Run, Task.StartNew
- **Excepción:** Método con `[EngineSetup]`
- **Violación:** Uso de Task sin excepción
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.03 - Prohibido LINQ en Hot-Path
- **Condición:** Método en NetShaper.Engine sin `[EngineSetup]`
- **Detección:** AST: InvocationExpressionSyntax donde método pertenece a System.Linq.Enumerable
- **Lista negra:** Where, Select, SelectMany, OrderBy, GroupBy, Join, ToArray, ToList, Any, All, First, Last, Count, Sum, etc.
- **Violación:** Invocación LINQ detectada
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.04 - Prohibido boxing en Engine
- **Condición:** Expresión en NetShaper.Engine namespace
- **Detección:** - Conversión value type → object/interface
  - Llamada a object.ToString() en value type
  - Interpolación string con value type: `$"{valueType}"`
- **Violación:** Boxing detectado
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (sugerir .ToString() explícito)

### R3.05 - Prohibido DateTime en Engine
- **Condición:** Expresión en NetShaper.Engine namespace
- **Detección:** AST: símbolos DateTime.Now, DateTime.UtcNow, Environment.TickCount, Environment.TickCount64
- **Whitelist:** Stopwatch.GetTimestamp()
- **Violación:** Uso de DateTime/TickCount
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (reemplazar con Stopwatch.GetTimestamp())

### R3.06 - Prohibido locks en Engine
- **Condición:** Statement en NetShaper.Engine namespace
- **Detección:** AST: LockStatementSyntax, uso de Monitor.Enter/Exit, Interlocked.* (excepto Interlocked.CompareExchange en CancellationToken flag)
- **Violación:** Lock detectado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.07 - Prohibido volatile en Engine
- **Condición:** Campo en NetShaper.Engine namespace
- **Detección:** AST: FieldDeclarationSyntax con modifier `volatile`
- **Violación:** volatile encontrado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.08 - Prohibido exceptions como control de flujo
- **Condición:** Bloque try-catch en método sin atributo `[Boundary]`
- **Detección:** - try-catch dentro de loop (for/foreach/while)
  - catch block vacío
  - catch(Exception) fuera de Main/boundaries
- **Patrón boundaries:** Main, métodos con [Boundary], Program.cs top-level
- **Violación:** catch inadecuado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.09 - Obligatorio ArrayPool en Engine
- **Condición:** `new byte[` en NetShaper.Engine namespace
- **Detección:** AST: ArrayCreationExpressionSyntax con tipo byte[]
- **Excepción:** Método con `[EngineSetup]` o tamaño ≤16 (stackalloc permitido)
- **Violación:** new byte[] sin excepción
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (sugerir ArrayPool<byte>.Shared.Rent)

### R3.10 - Obligatorio Span/Memory
- **Condición:** Parámetro de tipo byte[] en método público de Engine
- **Detección:** AST: ParameterSyntax con tipo byte[]
- **Sugerencia:** Reemplazar con ReadOnlySpan<byte> o Memory<byte>
- **Violación:** byte[] como parámetro
- **Severidad:** WARNING
- **Auto-fixable:** NO

### R3.11 - Prohibido string concatenation en loops
- **Condición:** Loop en cualquier namespace
- **Detección:** - Operador `+` con operandos string dentro de for/foreach/while
  - string.Concat dentro de loop
- **Violación:** Concatenación detectada
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (sugerir StringBuilder o ArrayPool)

### R3.12 - Obligatorio CancellationToken en async
- **Condición:** Método con modifier `async`
- **Detección:** AST: MethodDeclarationSyntax con `async` sin parámetro CancellationToken
- **Excepción:** Event handlers (delegates con sender, EventArgs)
- **Violación:** async sin CancellationToken
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (agregar parámetro = default)

---

## 4. MEMORIA

### R4.01 - Contadores ArrayPool
- **Condición:** Invocación ArrayPool<T>.Shared.Rent
- **Detección:** AST: buscar Rent sin Return correspondiente en todos los paths
- **Algoritmo:** Data flow analysis, contar Rent vs Return por método
- **Violación:** Desbalance Rent/Return
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R4.02 - Tamaños de Buffer Justificados
- **Condición:** ArrayPool<byte>.Shared.Rent(N)
- **Detección:** AST: extraer argumento N
- **Umbrales válidos:** [1500, 2048, 4096, 8192, 65536] + constantes nombradas
- **Violación:** N no está en whitelist y no es constante nombrada
- **Severidad:** WARNING
- **Auto-fixable:** NO

### R4.03 - Prohibido ToArray/ToList en Engine
- **Condición:** Invocación en NetShaper.Engine namespace
- **Detección:** AST: método .ToArray() o .ToList()
- **Excepción:** Método con `[EngineSetup]`
- **Violación:** ToArray/ToList sin excepción
- **Severidad:** ERROR
- **Auto-fixable:** NO

---

## 5. INTEROP Y SEGURIDAD

### R5.01 - Obligatorio SafeHandle
- **Condición:** Método con [DllImport] o [LibraryImport] que retorna IntPtr
- **Detección:** AST: AttributeSyntax DllImport/LibraryImport + tipo retorno IntPtr
- **Violación:** Retorna IntPtr en lugar de SafeHandle
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R5.02 - P/Invoke solo LibraryImport
- **Condición:** Atributo en método
- **Detección:** AST: [DllImport] attribute
- **Violación:** DllImport encontrado (debe ser LibraryImport)
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (reemplazar con LibraryImport)

### R5.03 - unsafe solo en Native
- **Condición:** Bloque unsafe
- **Detección:** AST: UnsafeStatementSyntax o método con modifier `unsafe`
- **Whitelist namespace:** NetShaper.Native
- **Violación:** unsafe fuera de Native
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R5.04 - fixed solo en Engine/Native
- **Condición:** Statement fixed
- **Detección:** AST: FixedStatementSyntax
- **Whitelist namespaces:** NetShaper.Engine, NetShaper.Native
- **Violación:** fixed fuera de whitelist
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R5.05 - Prohibido BinaryFormatter
- **Condición:** Uso de tipo
- **Detección:** AST: símbolo System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
- **Violación:** BinaryFormatter encontrado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R5.06 - Prohibido secrets hardcoded
- **Condición:** String literal o const
- **Detección:** - Regex en valores: `(password|secret|key|token|api[_-]?key)\s*=\s*["'][^"']{8,}`
  - Entropía Shannon >4.5 en strings >20 caracteres
- **Excepción:** Strings en archivos *.Test.cs
- **Violación:** Posible secret detectado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R5.07 - Validación de input obligatoria
- **Condición:** Método público que recibe parámetros
- **Detección:** AST: MethodDeclarationSyntax sin ArgumentNullException.ThrowIfNull o guard clause en primera línea
- **Aplicable a:** Parámetros reference types no-nullable
- **Violación:** Sin validación
- **Severidad:** WARNING
- **Auto-fixable:** SÍ (insertar ThrowIfNull)

---

## 6. LOGGING Y ERRORES

### R6.01 - No strings en Engine logging
- **Condición:** Invocación logger en NetShaper.Engine namespace
- **Detección:** AST: método que invoca ILogger.Log* con argumento string interpolation o concatenation
- **Permitido:** Log con struct como parámetro
- **Violación:** String en log
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R6.02 - catch(Exception) solo en boundaries
- **Condición:** CatchClauseSyntax
- **Detección:** AST: catch con tipo Exception
- **Whitelist:** - Método Main
  - Métodos con [Boundary] attribute
  - Archivos Program.cs
  - Métodos en controllers que heredan de base con [Controller]
- **Violación:** catch(Exception) fuera de whitelist
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R6.03 - Prohibido catch vacío
- **Condición:** CatchClauseSyntax
- **Detección:** AST: catch block sin statements o solo comentario
- **Violación:** Catch block vacío
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R6.04 - Logs estructurados
- **Condición:** Invocación ILogger.Log*
- **Detección:** AST: argumento debe ser interpolated string con placeholders {name} o struct
- **Violación:** String literal sin placeholders
- **Severidad:** WARNING
- **Auto-fixable:** PARCIAL

---

## 7. NAMING Y ESTILO

### R7.01 - Campos privados _camelCase
- **Condición:** Campo con modifier private
- **Detección:** AST: FieldDeclarationSyntax private sin readonly, nombre debe match `^_[a-z][a-zA-Z0-9]*$`
- **Violación:** No cumple patrón
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (renombrar)

### R7.02 - Propiedades PascalCase
- **Condición:** PropertyDeclarationSyntax
- **Detección:** Nombre debe match `^[A-Z][a-zA-Z0-9]*$`
- **Violación:** No cumple patrón
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (renombrar)

### R7.03 - Métodos PascalCase
- **Condición:** MethodDeclarationSyntax
- **Detección:** Nombre debe match `^[A-Z][a-zA-Z0-9]*$`
- **Violación:** No cumple patrón
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (renombrar)

### R7.04 - Async suffix obligatorio
- **Condición:** Método con modifier async
- **Detección:** AST: nombre no termina en "Async"
- **Excepción:** Event handlers (delegates con sender, EventArgs)
- **Violación:** async sin Async suffix
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (agregar Async)

### R7.05 - Prohibido this. redundante
- **Condición:** MemberAccessExpressionSyntax con this.
- **Detección:** AST: this.Member donde no hay shadowing
- **Violación:** this. innecesario
- **Severidad:** WARNING
- **Auto-fixable:** SÍ (eliminar this.)

### R7.06 - var obligatorio para tipos evidentes
- **Condición:** VariableDeclarationSyntax
- **Detección:** - Tipo explícito cuando RHS es `new TipoExplicito()`
  - Tipo explícito cuando RHS es cast explícito
- **Violación:** Tipo redundante
- **Severidad:** WARNING
- **Auto-fixable:** SÍ (reemplazar con var)

### R7.07 - Orden de miembros
- **Condición:** TypeDeclarationSyntax (class/struct)
- **Detección:** AST: verificar orden de miembros
- **Orden obligatorio:**
  1. Constantes (const)
  2. Campos estáticos (static fields)
  3. Campos de instancia (instance fields)
  4. Constructores (static → instance)
  5. Propiedades (static → instance)
  6. Métodos públicos (static → instance)
  7. Métodos privados (static → instance)
- **Violación:** Miembro fuera de orden
- **Severidad:** WARNING
- **Auto-fixable:** SÍ (reordenar)

---

## 8. NULLABILITY

### R8.01 - Nullable enable global
- **Condición:** Archivo .cs
- **Detección:** Parse: buscar `#nullable enable` o configuración en .csproj `<Nullable>enable</Nullable>`
- **Violación:** Nullable no habilitado
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (agregar en .csproj)

### R8.02 - APIs públicas no-nullable
- **Condición:** Método público con parámetros
- **Detección:** AST: ParameterSyntax reference type con `?`
- **Violación:** Parámetro nullable en API pública
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R8.03 - Warnings as errors
- **Condición:** Proyecto .csproj
- **Detección:** Parse: buscar `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` o `<WarningsAsErrors>nullable</WarningsAsErrors>`
- **Violación:** Warnings no configurados como errors
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (modificar .csproj)

---

## 9. DISPOSABLES

### R9.01 - IDisposable con using
- **Condición:** Invocación que retorna IDisposable
- **Detección:** Data flow analysis: variable IDisposable sin using/Dispose en todos los paths
- **Violación:** IDisposable sin dispose
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (wrap en using)

### R9.02 - Campos IDisposable con Dispose
- **Condición:** Clase con campo IDisposable
- **Detección:** AST: campo tipo IDisposable pero clase no implementa IDisposable
- **Violación:** Clase sin IDisposable teniendo campo disposable
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (agregar IDisposable, implementar Dispose)

---

## 10. TESTING

### R10.01 - Benchmarks con BenchmarkDotNet
- **Condición:** Clase en NetShaper.Benchmarks
- **Detección:** AST: clase sin atributo [MemoryDiagnoser] o sin métodos [Benchmark]
- **Violación:** Benchmark incompleto
- **Severidad:** WARNING
- **Auto-fixable:** NO

### R10.02 - Coverage ≥80% Engine
- **Condición:** Post-build en NetShaper.Engine
- **Detección:** Ejecutar coverlet, parsear report
- **Umbral:** Line coverage ≥80% en archivos sin [ExcludeFromCodeCoverage]
- **Violación:** Coverage <80%
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R10.03 - StressTest con ≥1000 iteraciones
- **Condición:** Método test con "StartStop" en nombre
- **Detección:** AST: buscar loop con iterations < 1000
- **Patrón:** `for (int i = 0; i < N; i++)` donde N < 1000
- **Violación:** Iteraciones insuficientes
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (cambiar límite a 1000)

---

## 11. PERFORMANCE

### R11.01 - Prohibido GC.Collect explícito
- **Condición:** Invocación GC.Collect
- **Detección:** AST: InvocationExpressionSyntax GC.Collect
- **Violación:** GC.Collect encontrado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R11.02 - StringBuilder en loops
- **Condición:** Loop con concatenación string
- **Detección:** string + dentro de for/foreach/while
- **Violación:** Concatenación en loop
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (sugerir StringBuilder)

---

## 12. UI

### R12.01 - Prohibido Thread.Sleep en UI
- **Condición:** Invocación Thread.Sleep en NetShaper.UI
- **Detección:** AST: InvocationExpressionSyntax Thread.Sleep
- **Violación:** Sleep en UI thread
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R12.02 - async void solo handlers
- **Condición:** Método async void
- **Detección:** AST: MethodDeclarationSyntax con async + void return
- **Excepción:** Método con nombre que termina en "Handler" o "_Click" o parámetros (object sender, EventArgs e)
- **Violación:** async void fuera de event handler
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R12.03 - Prohibido números mágicos
- **Condición:** LiteralExpressionSyntax numérico
- **Detección:** AST: literal >1 fuera de array indexers, no asignado a const/readonly
- **Whitelist valores:** 0, 1, -1, 2 (común en algoritmos)
- **Violación:** Número mágico >2
- **Severidad:** WARNING
- **Auto-fixable:** NO

---

## 13. ESTABILIDAD

### R13.01 - Prohibido cambiar firmas públicas
- **Condición:** Método/propiedad public
- **Detección:** Git diff: cambio en firma (parámetros, tipo retorno) sin [Obsolete] en versión anterior
- **Violación:** Breaking change sin deprecation
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R13.02 - Prohibido renombrar tipos públicos
- **Condición:** Class/interface/struct public
- **Detección:** Git diff: tipo eliminado + tipo agregado con similar estructura
- **Violación:** Probable rename
- **Severidad:** ERROR
- **Auto-fixable:** NO

---

## 14. COMPOSICIÓN

### R14.01 - Composition Root único
- **Condición:** Clase con [CompositionRoot] attribute
- **Detección:** AST: contar clases con attribute
- **Umbral:** 1 clase por assembly
- **Violación:** >1 Composition Root
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R14.02 - DI via constructor
- **Condición:** Clase con dependencias
- **Detección:** AST: campos de tipo interface no inicializados en declaración
- **Violación:** Campo interface sin constructor injection
- **Severidad:** WARNING
- **Auto-fixable:** NO

---

## 15. CHECKSUMS

### R15.01 - Recalcular checksums WinDivert
- **Condición:** Método que modifica paquetes en Engine
- **Detección:** AST: método que escribe a buffer byte[] sin invocar método con "Checksum" en nombre
- **Violación:** Modificación sin recalcular checksum
- **Severidad:** ERROR
- **Auto-fixable:** NO

---

## CONFIGURACIÓN DE SEVERIDADES

### ERROR (build fails)
- Violaciones arquitectura
- Violaciones hot-path
- Violaciones seguridad
- Violaciones memoria
- Breaking changes
- Complejidad Engine >7
- Anidación Engine >2 (con excepción en batching)

### WARNING (requiere justificación)
- Cohesión LCOM4 >1
- Coverage <80% (Si se permite fail en pipeline)

### INFO (métrica)
- Sugerencias de optimización
- Code smells menores

---

## ATRIBUTOS CUSTOM PARA EXCEPCIONES

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class EngineSetupAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class BoundaryAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public sealed class CompositionRootAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class JustificationAttribute : Attribute 
{
    public string Reason { get; }
    public JustificationAttribute(string reason) => Reason = reason;
}
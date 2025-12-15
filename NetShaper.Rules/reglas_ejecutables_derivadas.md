# NETSHAPER - REGLAS EJECUTABLES DERIVADAS v2.1

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
- **Condición:** Grafo de dependencias debe ser DAG
- **Detección:** Tarjan's algorithm sobre referencias
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
- **Excepción:** Clase con `[CompositionRoot]`
- **Violación:** Excede umbral sin ser Root
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R1.04 - Abstractions Sin Dependencias
- **Condición:** NetShaper.Abstractions.csproj
- **Detección:** Parse .csproj, validar referencias
- **Whitelist BCL:** System.*, Microsoft.Extensions.Logging.Abstractions
- **Violación:** Referencia fuera de whitelist
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R1.05 - UI No Referencia Implementaciones
- **Condición:** Archivos en NetShaper.UI
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
  - ≤7: PASS (general)
  - ≤12: PASS si método tiene `[ProtocolParser]`
  - >12: ERROR
- **Violación:** Excede umbral
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R2.02 - Complejidad Ciclomática UI/Infra
- **Condición:** Método en NetShaper.UI o NetShaper.Infrastructure
- **Detección:** Calcular McCabe Cyclomatic Complexity
- **Umbral:** ≤10
- **Violación:** >10
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R2.03 - Anidación Máxima
- **Condición:** Método cualquiera
- **Detección:** Contar niveles de bloques anidados (if/for/while/foreach/switch/try)
- **Umbrales:**
  - NetShaper.Engine: ≤2 niveles (general)
  - NetShaper.Engine con `[BatchProcessor]`: ≤4 niveles
  - Resto de assemblies: ≤3 niveles
- **Violación:** Excede umbral
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R2.04 - Una Clase Por Archivo
- **Condición:** Archivo .cs
- **Detección:** Contar declaraciones top-level public
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
- **Detección:** Calcular LCOM4
- **Umbral:** LCOM4 = 1
- **Violación:** LCOM4 > 1
- **Severidad:** WARNING
- **Auto-fixable:** NO

---

## 3. HOT-PATH Y ENGINE

### R3.01 - Prohibido async en Engine
- **Condición:** Método en NetShaper.Engine
- **Detección:** AST: MethodDeclarationSyntax con modifier `async`
- **Excepción:** Método con `[EngineSetup]`
- **Violación:** async sin [EngineSetup]
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.02 - Prohibido Task en Engine
- **Condición:** Expresión en NetShaper.Engine
- **Detección:** AST: símbolos System.Threading.Tasks.Task*
- **Excepción:** Método con `[EngineSetup]`
- **Violación:** Uso de Task sin excepción
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.03 - Prohibido LINQ en Hot-Path
- **Condición:** Método en NetShaper.Engine sin `[EngineSetup]`
- **Detección:** AST: InvocationExpressionSyntax donde método pertenece a System.Linq.Enumerable
- **Whitelist:** System.MemoryExtensions.*
- **Lista negra:** Where, Select, SelectMany, OrderBy, GroupBy, Join, ToArray, ToList, Any, All, First, Last, Count, Sum
- **Violación:** Invocación LINQ detectada
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.04 - Prohibido boxing en Engine
- **Condición:** Expresión en NetShaper.Engine
- **Detección:**
  - Conversión value type → object/interface
  - object.ToString() en value type
  - Interpolación string con value type: `$"{valueType}"`
- **Violación:** Boxing detectado
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (sugerir .ToString())

### R3.05 - Prohibido DateTime en Engine
- **Condición:** Expresión en NetShaper.Engine
- **Detección:** AST: símbolos DateTime.Now, DateTime.UtcNow, Environment.TickCount*
- **Whitelist:** Stopwatch.GetTimestamp()
- **Violación:** Uso de DateTime/TickCount
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (reemplazar con Stopwatch.GetTimestamp())

### R3.06 - Prohibido locks en Engine
- **Condición:** Statement en NetShaper.Engine
- **Detección:** AST: LockStatementSyntax, Monitor.Enter/Exit, Interlocked.* (excepto CompareExchange en flag)
- **Violación:** Lock detectado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.07 - Prohibido volatile en Engine
- **Condición:** Campo en NetShaper.Engine
- **Detección:** AST: FieldDeclarationSyntax con modifier `volatile`
- **Violación:** volatile encontrado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.08 - Prohibido exceptions como control de flujo
- **Condición:** Bloque try-catch en método sin `[Boundary]`
- **Detección:**
  - try-catch dentro de loop
  - catch block vacío
  - catch(Exception) fuera de Main/boundaries
- **Patrón boundaries:** Main, métodos con [Boundary], Program.cs
- **Violación:** catch inadecuado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R3.09 - Obligatorio ArrayPool en Engine
- **Condición:** `new byte[` en NetShaper.Engine
- **Detección:** AST: ArrayCreationExpressionSyntax con tipo byte[]
- **Excepción:** Método con `[EngineSetup]` o tamaño ≤16
- **Violación:** new byte[] sin excepción
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (sugerir ArrayPool)

### R3.10 - Obligatorio Span/Memory
- **Condición:** Parámetro byte[] en método público de Engine
- **Detección:** AST: ParameterSyntax con tipo byte[]
- **Sugerencia:** Reemplazar con ReadOnlySpan<byte> o Memory<byte>
- **Violación:** byte[] como parámetro
- **Severidad:** WARNING
- **Auto-fixable:** NO

### R3.11 - Prohibido string concatenation en loops
- **Condición:** Loop en cualquier namespace
- **Detección:**
  - Operador `+` con operandos string dentro de for/foreach/while
  - string.Concat dentro de loop
- **Violación:** Concatenación detectada
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (sugerir StringBuilder)

### R3.12 - Obligatorio CancellationToken en async
- **Condición:** Método con modifier `async`
- **Detección:** AST: MethodDeclarationSyntax con `async` sin parámetro CancellationToken
- **Excepción:** Event handlers
- **Violación:** async sin CancellationToken
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (agregar parámetro)

---

## 4. MEMORIA

### R4.01 - Ownership de ArrayPool
- **Condición:** Invocación ArrayPool<T>.Shared.Rent
- **Detección:** Data flow analysis
- **Excepciones:**
  - Método retorna buffer (ownership transfer)
  - Parámetro marcado `[BufferOwner]`
  - Return en mismo método
- **Algoritmo:** Contar Rent vs Return por método
- **Violación:** Desbalance sin ownership transfer
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R4.02 - Tamaños de Buffer Justificados
- **Condición:** ArrayPool<byte>.Shared.Rent(N)
- **Detección:** AST: extraer argumento N
- **Umbrales válidos:** [1500, 2048, 4096, 8192, 65536] + constantes nombradas
- **Violación:** N no en whitelist y no constante nombrada
- **Severidad:** WARNING
- **Auto-fixable:** NO

### R4.03 - Prohibido ToArray/ToList en Engine
- **Condición:** Invocación en NetShaper.Engine
- **Detección:** AST: método .ToArray() o .ToList()
- **Excepción:** Método con `[EngineSetup]`
- **Violación:** ToArray/ToList sin excepción
- **Severidad:** ERROR
- **Auto-fixable:** NO

---

## 5. INTEROP Y SEGURIDAD

### R5.01 - Obligatorio SafeHandle
- **Condición:** Método con [DllImport] o [LibraryImport] que retorna IntPtr
- **Detección:** AST: AttributeSyntax + tipo retorno IntPtr
- **Violación:** Retorna IntPtr en lugar de SafeHandle
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R5.02 - P/Invoke solo LibraryImport
- **Condición:** Atributo en método
- **Detección:** AST: [DllImport] attribute
- **Violación:** DllImport encontrado
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (reemplazar con LibraryImport)

### R5.03 - unsafe solo para fixed en Engine
- **Condición:** Bloque unsafe en NetShaper.Engine
- **Detección:** AST: UnsafeStatementSyntax o método con modifier `unsafe`
- **Permitido:**
  - NetShaper.Native (sin restricción)
  - NetShaper.Engine solo si contiene fixed statement
- **Violación:** unsafe sin fixed en Engine, o unsafe fuera de Native/Engine
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
- **Detección:**
  - Regex: `(password|secret|key|token|api[_-]?key)\s*=\s*["'][^"']{8,}`
  - Entropía Shannon >4.5 en strings >20 caracteres
- **Excepción:** Archivos *.Test.cs
- **Violación:** Posible secret detectado
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R5.07 - Validación de input obligatoria
- **Condición:** Método público que recibe parámetros
- **Detección:** AST: sin ArgumentNullException.ThrowIfNull o guard clause
- **Aplicable a:** Parámetros reference types no-nullable
- **Violación:** Sin validación
- **Severidad:** WARNING
- **Auto-fixable:** SÍ (insertar ThrowIfNull)

---

## 6. LOGGING Y ERRORES

### R6.01 - No strings en Engine logging
- **Condición:** Invocación logger en NetShaper.Engine
- **Detección:** AST: ILogger.Log* con string interpolation o concatenation
- **Permitido:** Log con struct como parámetro
- **Violación:** String en log
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R6.02 - catch(Exception) solo en boundaries
- **Condición:** CatchClauseSyntax
- **Detección:** AST: catch con tipo Exception
- **Whitelist:**
  - Método Main
  - Métodos con [Boundary]
  - Archivos Program.cs
  - Controllers
- **Violación:** catch(Exception) fuera de whitelist
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R6.03 - Prohibido catch vacío
- **Condición:** CatchClauseSyntax
- **Detección:** AST: catch block sin statements
- **Violación:** Catch block vacío
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R6.04 - Logs estructurados
- **Condición:** Invocación ILogger.Log*
- **Detección:** AST: argumento debe ser interpolated string con placeholders {name} o struct
- **Violación:** String literal sin placeholders
- **Severidad:** WARNING
- **Auto-fixable:** PARCIAL

### R6.10 - DomainException prohibida en Engine
- **Condición:** throw statement en NetShaper.Engine
- **Detección:** AST: throw expresión de tipo DomainException o derivado
- **Violación:** DomainException en Engine
- **Severidad:** ERROR
- **Auto-fixable:** NO

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
- **Excepción:** Event handlers
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
- **Detección:**
  - Tipo explícito cuando RHS es `new TipoExplicito()`
  - Tipo explícito cuando RHS es cast explícito
- **Violación:** Tipo redundante
- **Severidad:** WARNING
- **Auto-fixable:** SÍ (reemplazar con var)

### R7.07 - Orden de miembros
- **Condición:** TypeDeclarationSyntax
- **Detección:** AST: verificar orden
- **Orden obligatorio:**
  1. Constantes
  2. Campos estáticos
  3. Campos de instancia
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
- **Detección:** Parse: `#nullable enable` o en .csproj `<Nullable>enable</Nullable>`
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
- **Detección:** Parse: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- **Violación:** Warnings no configurados como errors
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (modificar .csproj)

---

## 9. DISPOSABLES

### R9.01 - IDisposable con using
- **Condición:** Invocación que retorna IDisposable
- **Detección:** Data flow analysis: variable IDisposable sin using/Dispose
- **Violación:** IDisposable sin dispose
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (wrap en using)

### R9.02 - Campos IDisposable con Dispose
- **Condición:** Clase con campo IDisposable
- **Detección:** AST: campo tipo IDisposable pero clase no implementa IDisposable
- **Violación:** Clase sin IDisposable teniendo campo disposable
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (agregar IDisposable)

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
- **Umbral:** Line coverage ≥80%
- **Violación:** Coverage <80%
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R10.03 - StressTest con ≥1000 iteraciones
- **Condición:** Método test con "StartStop" en nombre
- **Detección:** AST: loop con iterations < 1000
- **Patrón:** `for (int i = 0; i < N; i++)` donde N < 1000
- **Violación:** Iteraciones insuficientes
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (cambiar límite)

### R10.20 - Eventos Engine no transaccionales
- **Condición:** `[DomainEvent]` emitido en NetShaper.Engine
- **Detección:** Uso dentro de scope transaccional (TransactionScope, DbContext.SaveChanges)
- **Violación:** Evento Engine dentro de transacción
- **Severidad:** ERROR
- **Auto-fixable:** NO

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
- **Excepción:** Método termina en "Handler" o "_Click" o parámetros (object sender, EventArgs e)
- **Violación:** async void fuera de event handler
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R12.03 - Prohibido números mágicos
- **Condición:** LiteralExpressionSyntax numérico
- **Detección:** AST: literal >1 fuera de array indexers, no asignado a const/readonly
- **Whitelist valores:** 0, 1, -1, 2
- **Violación:** Número mágico >2
- **Severidad:** WARNING
- **Auto-fixable:** NO

---

## 13. ESTABILIDAD

### R13.01 - Prohibido cambiar firmas públicas
- **Condición:** Método/propiedad public
- **Detección:** Git diff: cambio en firma sin [Obsolete] en versión anterior
- **Violación:** Breaking change sin deprecation
- **Severidad:** ERROR
- **Auto-fixable:** NO

### R13.02 - Prohibido renombrar tipos públicos
- **Condición:** Class/interface/struct public
- **Detección:** Git diff: tipo eliminado + tipo agregado con estructura similar
- **Violación:** Probable rename
- **Severidad:** ERROR
- **Auto-fixable:** NO

---

## 14. COMPOSICIÓN

### R14.01 - Composition Root único
- **Condición:** Clase con [CompositionRoot]
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

## 16. DOMAIN RULES (MAPEO DR)

### DR.01 - Atributos dominio obligatorios
- **Condición:** Clase en namespace *.Domain o *.Abstractions
- **Detección:** AST: clase sin `[Entity]`, `[ValueObject]`, `[AggregateRoot]`, `[DomainEvent]`, o `[UseCase]`
- **Excepción:** Clases marcadas con [Infrastructure] o similares
- **Violación:** Clase dominio sin atributo
- **Severidad:** ERROR
- **Auto-fixable:** NO (requiere decisión arquitectónica)

### DR.02 - ValueObject inmutable
- **Condición:** Clase/struct con `[ValueObject]`
- **Detección:** AST: propiedades sin init-only o readonly
- **Algoritmo:**
  ```csharp
  foreach property in type:
      if property has setter && !init-only:
          FAIL
  ```
- **Violación:** Propiedad mutable en [ValueObject]
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (cambiar set a init)

### DR.03 - Entity en Engine es struct
- **Condición:** `[Entity]` en NetShaper.Engine namespace
- **Detección:** AST: declaración es `class` en lugar de `struct`
- **Violación:** Entity como class en Engine
- **Severidad:** ERROR
- **Auto-fixable:** NO (cambio semántico profundo)

### DR.04 - ValidateInvariant existe
- **Condición:** Tipo con `[Entity]`, `[ValueObject]`, o `[AggregateRoot]`
- **Detección:** AST: buscar método con nombre:
  - `ValidateInvariant()` (dominio puro)
  - `TryValidateInvariant(out ErrorCode)` (dominio técnico)
- **Algoritmo:**
  ```csharp
  if type has [Entity] or [Aggregate]:
      if namespace is Engine/Native:
          require TryValidateInvariant(out ErrorCode)
      else:
          require ValidateInvariant()
  ```
- **Violación:** Falta método validación
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (generar stub)

### DR.05 - Sin throw en dominio técnico
- **Condición:** Método en NetShaper.Engine o NetShaper.Native
- **Detección:** AST: `throw` statement
- **Excepción:** Métodos con `[EngineSetup]` o `[Boundary]`
- **Violación:** throw en hot-path técnico
- **Severidad:** ERROR
- **Auto-fixable:** NO

### DR.06 - Estado como enum
- **Condición:** Propiedad con `[DomainState]` o nombre "State" en `[Entity]`
- **Detección:** AST: tipo de propiedad
- **Tipos válidos:**
  - `enum`
  - `record` (discriminated union)
  - Tipo sealed con jerarquía (state pattern)
- **Tipos inválidos:**
  - `string`
  - `int` sin enum wrapper
  - Múltiples `bool` representando estado
- **Violación:** Estado sin tipado fuerte
- **Severidad:** WARNING
- **Auto-fixable:** NO

### DR.07 - UseCase simple
- **Condición:** Clase con `[UseCase]`
- **Detección:** Calcular Complejidad Ciclomática de métodos públicos
- **Umbral:** CC ≤5 por método
- **Violación:** Método de UseCase con CC >5
- **Severidad:** ERROR
- **Auto-fixable:** NO (sugerir extraer a Domain Service)

### DR.08 - Sin Exception genérico
- **Condición:** throw statement en namespace que contiene "Domain"
- **Detección:** AST: `throw new Exception(...)` o `throw new SystemException(...)`
- **Permitido:** DomainException derivados, ArgumentException, InvalidOperationException
- **Violación:** Exception genérico en dominio
- **Severidad:** ERROR
- **Auto-fixable:** NO

### DR.09 - DomainEvent inmutable
- **Condición:** Tipo con `[DomainEvent]`
- **Detección:** AST: debe ser `record` o todas las propiedades init-only
- **Algoritmo:**
  ```csharp
  if type is record:
      PASS
  else:
      foreach property:
          if property has setter && !init-only:
              FAIL
  ```
- **Violación:** Evento mutable
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (cambiar a record o init-only)

### DR.10 - Tests por invariante
- **Condición:** Clase con método ValidateInvariant
- **Detección:** Buscar en proyecto *.Tests clase con nombre {OriginalClass}Tests
- **Algoritmo:**
  - Buscar test que llame ValidateInvariant con datos válidos (debe PASS)
  - Buscar test que llame ValidateInvariant con datos inválidos (debe FAIL/throw)
- **Violación:** Invariante sin tests
- **Severidad:** WARNING
- **Auto-fixable:** NO

---

## 17. ATRIBUTOS CUSTOM

```csharp
// Arquitectura
[AttributeUsage(AttributeTargets.Method)]
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

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
public sealed class BufferOwnerAttribute : Attribute { }

// Dominio
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class EntityAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ValueObjectAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class AggregateRootAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DomainEventAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public sealed class UseCaseAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DomainStateAttribute : Attribute { }

// Híbridos
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class EngineDomainAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ZeroAllocationDomainAttribute : Attribute { }
```

---

## CONFIGURACIÓN DE SEVERIDADES

### ERROR (build fails)
- Violaciones arquitectura
- Violaciones hot-path
- Violaciones seguridad
- Violaciones memoria
- Breaking changes
- Complejidad Engine >7 (>12 con [ProtocolParser])
- Anidación Engine >2 (>4 con [BatchProcessor])
- Violaciones Domain Rules (DR.01-DR.05, DR.07-DR.09)

### WARNING (requiere justificación)
- Cohesión LCOM4 >1
- DR.06 (estado sin enum)
- DR.10 (tests faltantes)
- Sugerencias optimización

### INFO (métrica)
- Code smells menores

---

**Estado:** ACTIVO
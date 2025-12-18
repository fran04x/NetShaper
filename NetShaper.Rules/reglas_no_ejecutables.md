# NETSHAPER - REGLAS NO COMPATIBLES CON ROSLYN ANALYZER

> [!IMPORTANT]
> Las siguientes reglas **NO son ejecutables** mediante Roslyn DiagnosticAnalyzer.
> Requieren herramientas externas: MSBuild Analyzer, Git Diff Analysis, CI/CD pipelines, o análisis interprocedural fuera del alcance de Roslyn.
> Se mantienen documentadas para referencia y futura implementación con herramientas apropiadas.

---

## 1. ARQUITECTURA Y DEPENDENCIAS (MSBuild)

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
- **Razón no compatible:** Requiere parse de .csproj (MSBuild Analyzer)

### R1.02 - Dependencias Cíclicas
- **Condición:** Grafo de dependencias debe ser DAG
- **Detección:** Tarjan's algorithm sobre referencias
- **Umbral:** 0 ciclos
- **Violación:** Ciclo detectado
- **Severidad:** ERROR
- **Auto-fixable:** NO
- **Razón no compatible:** Requiere grafo de proyectos completo (MSBuild Analyzer)

### R1.04 - Abstractions Sin Dependencias
- **Condición:** NetShaper.Abstractions.csproj
- **Detección:** Parse .csproj, validar referencias
- **Whitelist BCL:** System.*, Microsoft.Extensions.Logging.Abstractions
- **Violación:** Referencia fuera de whitelist
- **Severidad:** ERROR
- **Auto-fixable:** NO
- **Razón no compatible:** Requiere parse de .csproj (MSBuild Analyzer)

### R1.05 - UI No Referencia Implementaciones
- **Condición:** Archivos en NetShaper.UI
- **Detección:** Regex: `^\s*using\s+(NetShaper\.(Engine|Native|Infrastructure))\s*;`
- **Violación:** Match encontrado
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (eliminar using)
- **Razón no compatible:** Requiere scan de archivos con regex, no AST analysis

---

## 4. MEMORIA (Git Diff)

### R4.01.b – Prohibido debilitar analizador de ownership
- **Condición:** Modificación de reglas de análisis relacionadas con ArrayPool<T>.Shared.Rent
- **Detección:** Diff en el analizador que:
 - Ignore constructores
 - Ignore Dispose
 - Considere comentarios como transferencia de ownership
- **Excepciones:** Ninguna
- **Algoritmo:** Inspección de cambios en el analizador buscando patrones de exclusión de flujo (constructor, Dispose, comment)
- **Violación:** Alteración del analizador para ocultar desbalance real de ownership
- **Severidad:** ERROR
- **Auto-fixable:** NO
- **Razón no compatible:** Meta-regla que requiere Git diff analysis

---

## 8. NULLABILITY (MSBuild)

### R8.01 - Nullable enable global
- **Condición:** Archivo .cs
- **Detección:** Parse: `#nullable enable` o en .csproj `<Nullable>enable</Nullable>`
- **Violación:** Nullable no habilitado
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (agregar en .csproj)
- **Razón no compatible:** Requiere modificación de .csproj (MSBuild Analyzer)

### R8.03 - Warnings as errors
- **Condición:** Proyecto .csproj
- **Detección:** Parse: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- **Violación:** Warnings no configurados como errors
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (modificar .csproj)
- **Razón no compatible:** Requiere modificación de .csproj (MSBuild Analyzer)

---

## 10. TESTING (CI/CD)

### R10.01 - Benchmarks con BenchmarkDotNet
- **Condición:** Clase en NetShaper.Benchmarks
- **Detección:** AST: clase sin atributo [MemoryDiagnoser] o sin métodos [Benchmark]
- **Violación:** Benchmark incompleto
- **Severidad:** WARNING
- **Auto-fixable:** NO
- **Razón no compatible:** Aplica solo a proyecto específico, alto costo de implementación

### R10.02 - Coverage ≥80% Engine
- **Condición:** Post-build en NetShaper.Engine
- **Detección:** Ejecutar coverlet, parsear report
- **Umbral:** Line coverage ≥80%
- **Violación:** Coverage <80%
- **Severidad:** ERROR
- **Auto-fixable:** NO
- **Razón no compatible:** Requiere ejecución post-build y herramienta de coverage

### R10.03 - StressTest con ≥1000 iteraciones
- **Condición:** Método test con "StartStop" en nombre
- **Detección:** AST: loop con iterations < 1000
- **Patrón:** `for (int i = 0; i < N; i++)` donde N < 1000
- **Violación:** Iteraciones insuficientes
- **Severidad:** ERROR
- **Auto-fixable:** SÍ (cambiar límite)
- **Razón no compatible:** Patrón muy específico de naming, alto costo/bajo valor

---

## 14. COMPOSICIÓN (Cross-Assembly)

### R14.01 - Composition Root único
- **Condición:** Clase con [CompositionRoot]
- **Detección:** AST: contar clases con attribute en TODOS los assemblies
- **Umbral:** 1 clase por assembly
- **Violación:** >1 Composition Root
- **Severidad:** ERROR
- **Auto-fixable:** NO
- **Razón no compatible:** Roslyn analiza un proyecto a la vez, requiere análisis cross-assembly

---

## 15. DOMAIN RULES (Cross-Project)

### DR.10 - Tests por invariante
- **Condición:** Clase con método ValidateInvariant
- **Detección:** Buscar en proyecto *.Tests clase con nombre {OriginalClass}Tests
- **Algoritmo:**
  - Buscar test que llame ValidateInvariant con datos válidos (debe PASS)
  - Buscar test que llame ValidateInvariant con datos inválidos (debe FAIL/throw)
- **Violación:** Invariante sin tests
- **Severidad:** WARNING
- **Auto-fixable:** NO
- **Razón no compatible:** Requiere análisis cross-project (Domain → Tests)

---

## 16. REGLAS DUPLICADAS

### R11.02 - StringBuilder en loops
- **Condición:** Loop con concatenación string
- **Detección:** string + dentro de for/foreach/while
- **Violación:** Concatenación en loop
- **Severidad:** ERROR
- **Auto-fixable:** PARCIAL (sugerir StringBuilder)
- **Razón no compatible:** ✅ YA CUBIERTA por R3.11 (StringConcatInLoopProhibited)
- **Acción:** Eliminada por duplicación

---

## 17. CHECKSUMS (CFG Interprocedural)

### R13.01 - Prohibido cambiar firmas públicas
- **Condición:** Método/propiedad public
- **Detección:** Git diff: cambio en firma sin [Obsolete] en versión anterior
- **Violación:** Breaking change sin deprecation
- **Severidad:** ERROR
- **Auto-fixable:** NO
- **Razón no compatible:** Requiere Git diff analysis entre commits/branches

### R13.02 - Prohibido renombrar tipos públicos
- **Condición:** Class/interface/struct public
- **Detección:** Git diff: tipo eliminado + tipo agregado con estructura similar
- **Violación:** Probable rename
- **Severidad:** ERROR
- **Auto-fixable:** NO
- **Razón no compatible:** Requiere Git diff analysis entre commits/branches

---

## 15. CHECKSUMS (CFG Interprocedural)

### R15.01 - Recalcular checksums WinDivert
- **Condición:** Método que modifica paquetes en Engine
- **Detección:** AST: método que escribe a buffer byte[] sin invocar método con "Checksum" en nombre
- **Violación:** Modificación sin recalcular checksum
- **Severidad:** ERROR
- **Auto-fixable:** NO
- **Razón no compatible:** Requiere Control Flow Graph interprocedural profundo, fuera del alcance de Roslyn DiagnosticAnalyzer estándar

---

## HERRAMIENTAS RECOMENDADAS

| Categoría | Herramienta Sugerida |
|-----------|---------------------|
| MSBuild Analysis | Custom MSBuild Task / Roslyn Source Generator |
| Git Diff Analysis | GitHub Actions + custom scripts / NDepend |
| Coverage | Coverlet + CI pipeline validation |
| CFG Interprocedural | Custom Roslyn IOperation walker con estado global |

---

**Estado:** ARCHIVADO (no ejecutable con tooling actual)

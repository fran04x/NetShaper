# NETSHAPER – DOCUMENTO NORMATIVO SUPERIOR v2.1

## 0. REGLA CERO
Si una regla no se cumple explícitamente, el resultado es **FAIL**.
Si existe ambigüedad normativa no resuelta en este documento, el resultado es **FAIL**.
Cualquier ERROR normativo se corrige en código productivo.
Modificar analizadores para hacer pasar código existente está prohibido.
Violación → **FAIL**.

Este documento es la **autoridad técnica suprema** del sistema.

---

## 1. JERARQUÍA NORMATIVA GLOBAL (INMUTABLE)
Orden absoluto de precedencia:
1. documento_normativo_superior.md (DNS)
2. domain_rules.md (DR)
3. ia_context_rules.md (ICR)
4. reglas_ejecutables_derivadas.md (RED)
5. ia_rules_analyzer.md (IRA)

**Principio de especificidad:** Regla específica prevalece sobre regla genérica, independiente del documento origen.

Todo documento nuevo debe declarar explícitamente su posición en esta jerarquía.

---

## 2. FRONTERA DOMINIO / TÉCNICO

### 2.01 Clasificación de componentes
- **NetShaper.Engine**: Dominio técnico de alto rendimiento (performance-first).
- **NetShaper.Abstractions**: Dominio puro DDD (correctness-first).
- **NetShaper.Infrastructure**: Adaptadores técnicos (no-dominio).
- **NetShaper.UI**: Presentación (no-dominio).
- **NetShaper.Composition**: Orquestación (dominio aplicación).
- **NetShaper.Native**: Interop sistema (no-dominio).

### 2.02 Aplicabilidad Domain_Rules
Las reglas de Domain_Rules.md **NO aplican** al Engine salvo mención explícita de compatibilidad con dominio técnico.

---

## 3. ESTRUCTURA DE SOLUCIÓN

Assemblies obligatorios:
- NetShaper.Abstractions
- NetShaper.Composition
- NetShaper.Engine
- NetShaper.Native
- NetShaper.Infrastructure
- NetShaper.UI

Restricciones:
- UI **no** referencia Engine, Native ni Infrastructure.
- Engine, Native e Infrastructure **no** se referencian entre sí.
- Abstractions **no** referencia ningún proyecto (excepto BCL permitida).
- Dependencias cíclicas prohibidas.
- Program/Test fuera de Engine.

Violación → FAIL.

---

## 4. ARQUITECTURA

- Clean Architecture estricta.
- Dependencias solo hacia adentro.
- Dominio sin referencias a UI ni Infra.
- Interfaces en capas superiores.
- Implementaciones en capas inferiores.
- Más de 4 dependencias solo en Composition Root.

---

## 5. ENGINE / HOT PATH (CRÍTICO)

### 5.01 Principios
- Modelo de captura: Single-thread por pipeline instance.
- Hot-path determinista.
- Heap allocation sostenido por paquete = 0.

### 5.02 Determinismo temporal
- Varianza de operación: ≤5%
- Re-ejecución con input idéntico → output ±5% latencia
- No depende de:
  - Wall-clock time (excepto Stopwatch.GetTimestamp)
  - Threading scheduler
  - GC timing

### 5.03 Excepción de inicialización
Se permite memoria dinámica y configuración **únicamente** durante arranque en métodos marcados con `[EngineSetup]`.

### 5.04 Prohibiciones en Engine (fuera de [EngineSetup])
- async / await
- Task / Task.Run / Task.StartNew
- LINQ (System.Linq.Enumerable)
- boxing
- strings temporales
- DateTime / Environment.TickCount*
- locks / Monitor / Semaphore / volatile
- exceptions como control de flujo
- ToArray / ToList
- DomainException

### 5.05 Permitido en Engine
- `unsafe` **exclusivamente para bloques `fixed`** (pinning).
- ArrayPool<T>
- Span / Memory / ref struct
- Stopwatch.GetTimestamp()
- Errores estructurados (structs / enums)

### 5.06 Obligatorio
- Validación de buffers
- Recalcular checksums siempre
- Start/Stop lock-free y reentrante

---

## 6. TIEMPO

- Única fuente: `Stopwatch.GetTimestamp()`
- Conversión: `ticks * (1_000_000_000.0 / Stopwatch.Frequency)` = nanosegundos
- Prohibido asumir Frequency constante entre sistemas
- Timers del sistema prohibidos

---

## 7. MEMORIA

### 7.01 Principios
- Heap sostenido = 0 en Engine (post-setup)
- Un buffer por hilo
- ArrayPool obligatorio
- Rent/Return balanceado o ownership transferido
- byte[] fuera de pool = FAIL (salvo `[EngineSetup]`)
- Sin GC Gen2 evitable

### 7.02 Validación de buffers obligatoria
- **Bounds check:** offset + size ≤ buffer.Length
- **Alignment:** dirección % 8 == 0 para structs >8 bytes
- **Checksum:** recalcular después de modificación
- **Size:** ArrayPool size solicitado ≤ buffer devuelto

Patrón obligatorio:
```csharp
if (offset + size > buffer.Length)
    return ErrorCode.BufferOverflow;
```

### 7.03 Ownership de buffers
- Método que hace `Rent` **NO está obligado** a hacer `Return` si transfiere ownership
- Ownership explícito mediante:
  - Retorno del buffer
  - Parámetro marcado `[BufferOwner]`
  - Documentación del método

---

## 8. CONCURRENCIA

### 8.01 Modelo obligatorio Engine
- Single-thread lógico por pipeline instance
- **O** Symmetric Multi-thread:
  - N pipelines independientes
  - Partitioning estático (ej: por CPU core)
  - Zero shared mutable state
  - Sin cross-thread communication (excepto shutdown flag)

### 8.02 Backpressure
- Drop paquetes si buffer lleno
- Incrementar counter atomic (int)
- Log nivel WARNING cada 1000 drops

### 8.03 Overflow
- Comportamiento: tail drop
- Sin bloqueo
- Sin espera

---

## 9. LOGGING

Engine:
- Sin IO directo
- Sin strings temporales
- Logs como structs
- Logger puede perder mensajes (fire-and-forget)

Formateo solo en Infra/UI.

---

## 10. ERRORES

### 10.01 Engine
- No traduce excepciones genéricas
- Usa errores estructurados (structs / enums)
- catch(Exception) solo en boundaries explícitos
- Error no detiene Engine salvo estado fatal documentado
- **DomainException prohibida en Engine**

### 10.02 Dominio puro
- DomainException permitida en Abstractions/Composition
- Tipado explícito de errores

---

## 11. TRANSACCIONES

### 11.01 Engine
- **No transaccional**
- Operaciones atómicas vía Interlocked solo para flags
- Estado distribuido prohibido

### 11.02 Infrastructure
- Transacciones ACID si DB/Storage
- IsolationLevel: ReadCommitted mínimo
- Timeout: ≤5 segundos

### 11.03 Eventos
- Engine: fire-and-forget, sin garantías
- Infrastructure: dentro de transacción commit

---

## 12. API Y ASYNC

- APIs públicas no-nullable
- Nullable enable global
- Warnings as errors
- Async siempre con CancellationToken
- Async prohibido en Engine

---

## 13. INTEROP Y SEGURIDAD

- P/Invoke solo con LibraryImport
- SafeHandle obligatorio
- unsafe solo para `fixed` en Engine/Native
- fixed solo en Engine/Native
- BinaryFormatter prohibido
- Secrets hardcoded prohibidos
- Validación estricta de input

---

## 14. CLEAN CODE

### 14.01 Complejidad ciclomática
- **≤7 Engine** (general)
- **≤12 Engine** en métodos marcados `[ProtocolParser]`
- ≤10 resto de assemblies

### 14.02 Anidación
- **≤2 Engine** (general)
- **≤4 Engine** en métodos marcados `[BatchProcessor]` (loop + validation + dispatch + action)
- ≤3 resto de assemblies

### 14.03 Estructura
- Métodos ≤40 líneas
- Una responsabilidad por clase
- Un archivo por clase
- Sin regiones
- Sin TODOs

---

## 15. PERFORMANCE

### 15.01 Targets mínimos Engine
- **PPS:** ≥100,000 paquetes/segundo (MTU 1500 bytes)
- **Latencia p99:** ≤100 microsegundos
- **Jitter:** ≤10 microsegundos
- **Heap sostenido:** 0 bytes (post-setup)
- **GC Gen2:** 0 colecciones durante operación normal

### 15.02 Prohibiciones
- Sin reallocs continuas
- Sin concatenación de strings en loops
- Sin GC.Collect explícito

### 15.03 Benchmarks
- BenchmarkDotNet obligatorio para cambios en Engine
- Reportar: Mean, P99, Allocated
- Regresión >5% en cualquier métrica = FAIL

---

## 16. TESTING

### 16.01 Stress test Start/Stop
- **Iteraciones:** ≥1000 ciclos
- **Métricas PASS:**
  - Heap growth: ≤1 MB después de 1000 ciclos
  - Handle leaks: 0 (pre vs post identical)
  - Thread leaks: 0 (pre vs post identical)
  - Exception count: 0
- **Ejecución:** 5 runs consecutivos, todos PASS

### 16.02 General
- Medir PPS, heap, handles, threads
- Crecimiento sostenido = FAIL
- Tests reproducibles sin UI

---

## 17. ESTABILIDAD INCREMENTAL

- No refactor fuera del diff
- No cambiar firmas públicas sin [Obsolete]
- No renombrar tipos públicos
- Compatibilidad AppState obligatoria

---

## 18. ATRIBUTOS NORMATIVOS

Definidos en NetShaper.Abstractions:

### 18.01 Arquitectura
- `[EngineSetup]`: Permite heap/async durante inicialización
- `[Boundary]`: Permite catch(Exception)
- `[CompositionRoot]`: Sin límite dependencias
- `[HotPath]`: Marca métodos críticos performance
- `[ProtocolParser]`: Permite CC≤12
- `[BatchProcessor]`: Permite anidación≤4
- `[BufferOwner]`: Marca ownership de buffer

### 18.02 Dominio
- `[Entity]`: Requiere Id + ValidateInvariant()
- `[ValueObject]`: Requiere inmutabilidad
- `[AggregateRoot]`: Raíz agregado + ValidateInvariant()
- `[DomainEvent]`: Inmutable
- `[UseCase]`: Caso de uso aplicación
- `[DomainState]`: Marca propiedad de estado

### 18.03 Híbridos
- `[EngineDomain]`: Dominio técnico, reglas DR relajadas
- `[ZeroAllocationDomain]`: Dominio con heap=0

---

## 19. GENERACIÓN Y AUDITORÍA

- Este documento define la norma
- Las reglas ejecutables derivadas (RED) son la única fuente de enforcement
- Toda regla crítica debe tener mapeo 1→N a reglas ejecutables
- Cualquier desviación sin regla ejecutable = FAIL

---

## 20. EXTENSIBILIDAD

Toda excepción normativa futura debe:
- Declararse en DNS
- Tener mapeo ejecutable en RED
- Documentar justificación técnica

---

**Autoridad:** Documento Normativo Superior NetShaper
**Estado:** ACTIVO
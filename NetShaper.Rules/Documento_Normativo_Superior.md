# NETSHAPER – DOCUMENTO NORMATIVO SUPERIOR

## 0. REGLA CERO
Si una regla no se cumple explícitamente, el resultado es **FAIL**.
Si existe ambigüedad, el resultado es **FAIL**.

Este documento define **principios y prohibiciones absolutas**. No es interpretable. La auditoría automática se realiza mediante **reglas ejecutables derivadas**.

---

## 1. PRIORIDAD ABSOLUTA (INMUTABLE)
El orden es estricto. En conflicto, gana la regla de mayor prioridad.

1. Correctitud funcional
2. Seguridad
3. Cero heap en Engine
4. Determinismo temporal
5. Simplicidad

---

## 2. ESTRUCTURA DE SOLUCIÓN

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

## 3. ARQUITECTURA

- Clean Architecture estricta.
- Dependencias solo hacia adentro.
- Dominio sin referencias a UI ni Infra.
- Interfaces en capas superiores.
- Implementaciones en capas inferiores.
- Más de 4 dependencias solo en Composition Root.

---

## 4. ENGINE / HOT PATH (CRÍTICO)

Principios:
- Modelo de captura: Single-thread o **Symmetric Multi-thread (Parallel Shared-Nothing)**.
- Hot-path determinista.
- Heap allocation por paquete = 0.

**Excepción de Inicialización:**
Se permite el uso de memoria dinámica y configuración **únicamente** durante la fase de arranque, siempre que el método esté marcado explícitamente con el atributo `[EngineSetup]`.

Prohibido en Engine (fuera de `[EngineSetup]`):
- async / await
- Task / Task.Run / Task.StartNew
- LINQ
- boxing
- strings temporales
- DateTime / Environment.TickCount*
- locks / Monitor / Semaphore / volatile
- exceptions como control de flujo
- ToArray / ToList

Obligatorio:
- ArrayPool<T>
- Span / Memory / ref struct
- Validación de buffers
- Recalcular checksums siempre
- Start/Stop lock-free y reentrante

---

## 5. TIEMPO

- Única fuente: Stopwatch.GetTimestamp().
- Conversión manual de unidades.
- Timers del sistema prohibidos.

---

## 6. MEMORIA

- Heap sostenido = 0 en Engine.
- Un buffer por hilo.
- ArrayPool obligatorio.
- Rent/Return balanceado.
- byte[] fuera de pool = FAIL (salvo `[EngineSetup]`).
- Sin GC Gen2 evitable.

---

## 7. CONCURRENCIA

- Hot-path: Single-thread lógico por instancia de procesamiento.
- Prohibido: Comunicación compleja entre hilos (locking, signaling). Permitido: Partitioning estático.
- Comunicación cross-thread lock-free.
- Backpressure definido.
- Overflow definido.

---

## 8. LOGGING

Engine:
- Sin IO.
- Sin strings.
- Logs como structs.
- Logger puede perder mensajes.

Formateo solo en Infra/UI.

---

## 9. ERRORES

- Engine no traduce excepciones genéricas.
- catch(Exception) solo en boundaries explícitos.
- Error no detiene Engine salvo estado fatal documentado.

---

## 10. API Y ASYNC

- APIs públicas no-nullable.
- Nullable enable global.
- Warnings as errors.
- Async siempre con CancellationToken.
- Async prohibido en Engine.

---

## 11. INTEROP Y SEGURIDAD

- P/Invoke solo con LibraryImport.
- SafeHandle obligatorio.
- unsafe solo en Native.
- fixed solo en Engine/Native.
- BinaryFormatter prohibido.
- Secrets hardcoded prohibidos.
- Validación estricta de input.

---

## 12. CLEAN CODE

- Complejidad Ciclomática: **≤7 Engine (Estricto)**, ≤10 resto.
- Anidación: **≤2 Engine**, ≤3 resto.
- Métodos ≤40 líneas.
- Una responsabilidad por clase.
- Un archivo por clase.
- Sin regiones.
- Sin TODOs.

---

## 13. PERFORMANCE

- Sin reallocs continuas.
- Sin concatenación de strings en loops.
- Benchmarks obligatorios para cambios en Engine.

---

## 14. TESTING

- Stress Start/Stop ≥1000 ciclos.
- Medir PPS, heap, handles, threads.
- Crecimiento sostenido = FAIL.
- Tests reproducibles sin UI.

---

## 15. ESTABILIDAD INCREMENTAL

- No refactor fuera del diff.
- No cambiar firmas públicas.
- No renombrar tipos.
- Compatibilidad AppState obligatoria.

---

## 16. GENERACIÓN Y AUDITORÍA

- Este documento define la norma.
- Las reglas ejecutables derivadas son la única fuente de enforcement.
- Toda regla crítica debe tener mapeo 1→N a reglas ejecutables.
- Cualquier desviación sin regla ejecutable = FAIL.

---

**Autoridad:** Documento Normativo Superior NetShaper.
**Estado:** Inmutable.
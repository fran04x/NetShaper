# DOMAIN_RULES – NORMATIVA DE DOMINIO v2.1

## 0. REGLA CERO
Si una regla de dominio no puede validarse mediante atributos o análisis estático, el resultado es **FAIL**.
Si el comportamiento viola una invariante declarada, **FAIL**.

---

## 1. ALCANCE Y CLASIFICACIÓN
Este documento rige la lógica de negocio. Para resolver conflictos de performance vs. abstracción, se definen dos contextos de dominio:

### 1.01 Dominio Puro (High-Level)
- **Ámbito:** `NetShaper.Abstractions`, `NetShaper.Composition`.
- **Características:** DDD Clásico, inmutabilidad por referencia, validación rica.
- **Restricciones:** Memory-safe, GC permitido (Gen0).

### 1.02 Dominio Técnico (Low-Level / Hot-Path)
- **Ámbito:** `NetShaper.Engine`, `NetShaper.Native`.
- **Características:** Zero-Allocation DDD.
- **Restricciones:** `struct` / `ref struct` obligatorios. Sin excepciones. Validación sin allocation.

---

## 2. PRIORIDAD
En conflicto, este es el orden estricto de prevalencia:
1. **Documento_Normativo_Superior.md** (Restricciones físicas y arquitectónicas).
2. **Domain_Rules.md** (Este documento - Lógica de negocio).
3. **IA_Context_Rules.md** (Comportamiento del agente).
4. **Reglas_Ejecutables_Derivadas.md** (Implementación del check).

*Resolución:* `Domain_Rules` subordina las instrucciones de IA, pero se somete a las restricciones de memoria/CPU del Normativo Superior.

---

## 3. MODELO DE DOMINIO

### 3.01 Identidad de Entidades
- **Dominio Puro:** Clases con atributo `[Entity]`. Debe implementar `IEquatable<Id>`.
- **Dominio Técnico:** `struct` con campo `readonly int/long Id`.
- **Regla:** Prohibido identidad por referencia de memoria en Dominio Técnico.

### 3.02 Value Objects
- **Dominio Puro:** `record` o `class` inmutable con atributo `[ValueObject]`.
- **Dominio Técnico:** `readonly struct` obligatoria.
- **Validación:** Igualdad estructural obligatoria.

### 3.03 Agregados
- **Condición:** Clase/Struct marcada con `[AggregateRoot]`.
- **Restricción:** Solo la raíz es accesible desde repositorios o casos de uso.

---

## 4. INVARIANTES

### 4.01 Declaración Obligatoria
- Todo tipo marcado como `[Entity]`, `[ValueObject]` o `[AggregateRoot]` **debe** implementar un método de validación.
- **Firma Dominio Puro:** `void ValidateInvariant()` (Lanza excepción).
- **Firma Dominio Técnico:** `bool TryValidateInvariant(out ErrorCode error)` (Sin excepciones).

### 4.02 Momento de Validación
- Constructor: Obligatorio.
- Métodos de Mutación: Obligatorio llamar a validación al finalizar o usar tipos seguros (Parse-don't-validate).

---

## 5. TRANSICIONES DE ESTADO

### 5.01 Estados Explícitos
- El estado debe modelarse como `enum` o `polymorphic record` (Union Type).
- **Prohibido:** Strings mágicos o booleanos múltiples (`isProcessing`, `isDone`) para representar estado de negocio.
- **Atributo:** Propiedad de estado debe marcarse con `[DomainState]`.

### 5.02 Máquina de Estados
- Las transiciones válidas deben estar encapsuladas en el Agregado.
- Si `State A` no puede pasar a `State C` directamente, el código debe impedirlo estáticamente o mediante validación.

---

## 6. CASOS DE USO Y SERVICIOS

### 6.01 Orquestación vs Lógica
- Los servicios de aplicación (`[UseCase]`) solo orquestan.
- **Umbral de complejidad:** `[UseCase]` no puede tener Complejidad Ciclomática > 5.
- Si la lógica es compleja, pertenece a una Entidad o Domain Service.

### 6.02 Idempotencia
- Comandos (`void` o mutantes) deben ser idempotentes por diseño donde sea técnicamente viable.

---

## 7. ERRORES DE DOMINIO

### 7.01 Tipado de Errores
- **Dominio Puro:** Usar excepciones personalizadas heredando de `DomainException`.
- **Dominio Técnico:** Usar `Result<T>` o `ErrorCode` enum. **Prohibido lanzar excepciones** en flujo de control (conforme a DNS §9).

### 7.02 Prohibición Genérica
- Prohibido `throw new Exception("mensaje")` en cualquier capa de dominio.
- Prohibido `catch (Exception)` dentro de lógica de dominio.

---

## 8. EVENTOS DE DOMINIO

### 8.01 Definición e Inmutabilidad
- Atributo: `[DomainEvent]`.
- Estructura: `record` (Puro) o `readonly struct` (Técnico).
- Semántica: Verbo en pasado (`PacketDropped`, `ConfigUpdated`).

### 8.02 Consistencia
- **Dominio Puro:** Eventual consistency permitida.
- **Dominio Técnico:** Fire-and-forget. No se garantiza entrega si el Engine colapsa (prioridad throughput).
- **Restricción:** No disparar eventos si la invariante del agregado no se cumple.

---

## 9. TESTING DE DOMINIO

### 9.01 Cobertura de Reglas
- Cada clase con `ValidateInvariant` debe tener tests unitarios para casos válidos e inválidos.
- Cada transición de estado posible debe tener un test.

### 9.02 Aislamiento
- Tests de dominio no deben mockear I/O, base de datos ni red. Deben ser puros y deterministas.

---

## 10. REGLAS EJECUTABLES REQUERIDAS (Mapeo)
Para que este documento sea válido, el analizador estático debe implementar las siguientes reglas derivadas (prefijo DR):

| ID | Regla | Target | Severidad |
| :--- | :--- | :--- | :--- |
| **DR.01** | Tipos de dominio deben tener atributo (`[Entity]`, etc) | All Domain | ERROR |
| **DR.02** | `[ValueObject]` debe ser inmutable (`readonly`) | All Domain | ERROR |
| **DR.03** | `[Entity]` en Engine debe ser `struct` | Technical Domain | ERROR |
| **DR.04** | Existencia de `ValidateInvariant` o `TryValidateInvariant` | All Domain | ERROR |
| **DR.05** | Prohibido `throw` en Dominio Técnico | Technical Domain | ERROR |
| **DR.06** | Estado modelado solo con `enum` dentro de Entidades | All Domain | WARNING |
| **DR.07** | `[UseCase]` complejidad ciclomática > 5 | Application | ERROR |
| **DR.08** | Prohibido `System.Exception` genérico | All Domain | ERROR |
| **DR.09** | `[DomainEvent]` debe ser inmutable | All Domain | ERROR |
| **DR.10** | Tests unitarios para cada `ValidateInvariant` | All Domain | WARNING |

---

## 11. AUTORIDAD
Este documento define la correctitud lógica del sistema.
Cualquier desviación técnica para cumplir el `Documento_Normativo_Superior` (ej. optimizaciones unsafe) debe estar encapsulada dentro del **Dominio Técnico** y justificada.

Estado: **ACTIVO v2.1**
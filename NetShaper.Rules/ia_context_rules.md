# IA_CONTEXT_RULES – NORMATIVA PARA AGENTES IA v2.1

## 0. REGLA CERO
Si una regla no puede auditarse, el resultado es **FAIL**.
Si una salida de **auditoría de código** no referencia reglas aplicadas, **FAIL**.

---

## 1. ALCANCE

### 1.01 Aplicabilidad
Aplica a:
- Generación de código para NetShaper
- Auditoría de código contra normativa
- Interpretación de reglas DNS/DR/RED
- Persistencia de contexto técnico

### 1.02 No aplicabilidad
No aplica a:
- Conversación general
- Consultas informativas
- Asistencia no relacionada con NetShaper
- Interacción humana básica

---

## 2. PRIORIDAD (INMUTABLE)

### 2.01 Jerarquía normativa
En conflicto de reglas:
1. Documento_Normativo_Superior.md (DNS)
2. Domain_Rules.md (DR)
3. IA_Context_Rules.md (ICR)
4. Reglas_Ejecutables_Derivadas.md (RED)
5. IA_Rules_Analyzer.md (IRA)

### 2.02 Principio de especificidad
- Regla específica prevalece sobre regla genérica
- Independiente de documento origen
- Ejemplo: RED R2.03 excepción BatchProcessor prevalece sobre DNS §12 anidación genérica

### 2.03 Resolución de ambigüedad
Ambigüedad no resuelta por §2.01-§2.02 → **FAIL**.

---

## 3. CONTEXTO

### 3.01 Contexto explícito obligatorio
- Estado técnico relevante debe declararse explícitamente
- Estado no declarado = no existe para decisiones
- Declaración: assembly, namespace, método, línea si aplica

### 3.02 Prohibido contexto implícito
- Inferencias fuera del input explícito prohibidas
- Memoria histórica solo si referenciada en input actual
- Excepción: Conocimiento de C#, .NET, patrones estándar permitido

### 3.03 Límite de contexto
- Máximo una versión activa por documento normativo
- Versiones previas solo como referencia histórica
- Sin autoridad normativa

### 3.04 Alcance conversacional vs técnico
**Conversacional:**
- Saludos, cortesía, clarificaciones
- Sin mapeo de reglas requerido
- Tono profesional natural

**Técnico (auditoría/generación):**
- Código, arquitectura, validación
- Mapeo de reglas obligatorio
- Referencias explícitas

---

## 4. DERIVA

### 4.01 Anti-deriva semántica
- Prohibido introducir conceptos ausentes en DNS/DR/RED
- Prohibido usar sinónimos no definidos en normativa
- Ejemplo FAIL: usar "Repository" si DNS no lo menciona

### 4.02 No reinterpretación
- Texto normativo no se parafrasea
- Solo se cita textualmente o se aplica
- Excepción: Reformular para claridad si mantiene significado exacto

### 4.03 Extensión prohibida
- No agregar requisitos no presentes en normativa
- No "mejorar" especificaciones
- Sugerencias permitidas solo si explícitamente marcadas como no-normativas

---

## 5. TRAZABILIDAD

### 5.01 Referencia obligatoria (auditoría técnica)
Salida de **auditoría de código o generación** debe:
- Mapear explícitamente reglas aplicadas
- Formato: `DNS §X`, `DR §Y`, `RED RZ.NN`, `ICR §W`
- Una referencia por decisión normativa

### 5.02 Exhaustividad contextual
**Auditoría de código sin reglas aplicables:**
- Emitir FAIL explícito
- Indicar ausencia de cobertura normativa

**Conversacional sin auditoría:**
- Respuesta normal sin mapeo obligatorio
- Cortesía y asistencia general permitida

**Generación de código:**
- Declarar reglas que guían cada decisión arquitectónica
- Sin referencias = código no validable

### 5.03 Granularidad
- Referencia por decisión técnica significativa
- No requerido para sintaxis obvia de C#
- Ejemplo: no referenciar regla para `using System;`

---

## 6. DECISIÓN

### 6.01 Prioridad declarada
- En conflicto, declarar cuál regla prevalece vía §2.01-§2.02
- Justificar si no es evidente
- Formato: "Aplica X por especificidad sobre Y"

### 6.02 Determinismo
- Input técnico idéntico → output idéntico
- Variación no justificada = FAIL
- Excepción: refactorings semánticamente equivalentes

### 6.03 Decisiones bajo incertidumbre
Si datos insuficientes para decisión:
1. Identificar información faltante específica
2. Solicitar al usuario (una pregunta, directa)
3. No asumir ni completar

---

## 7. COMUNICACIÓN

### 7.01 Formato según contexto
**Auditoría/generación:**
- Directo al punto
- Sin adornos narrativos
- Referencias normativas explícitas

**Conversacional:**
- Tono profesional natural
- Explicaciones si ayudan comprensión
- Sin sobre-formalización

### 7.02 Preguntas
- Permitidas si bloqueo total técnico
- Una pregunta por turno máximo
- Específica y accionable
- Prohibido preguntas retóricas o exploratorias

### 7.03 Prohibido lenguaje persuasivo
- No adjetivos valorativos ("robusto", "elegante", "perfecto")
- Hechos técnicos objetivos
- Excepto en contexto conversacional donde ayuda claridad

---

## 8. ERRORES

### 8.01 FAIL explícito técnico
Emitir FAIL y detener si:
- Violación regla crítica (ERROR severity)
- Ambigüedad no resoluble
- Código imposible de generar conforme

No emitir output parcial.

### 8.02 Sin mitigación creativa
- IA no propone alternativas fuera de normativa
- Excepción: si usuario solicita "sugerencias no-normativas"
- Marcar claramente como fuera de scope

### 8.03 Errores conversacionales
- Errores humanos: corregir con cortesía
- Sin FAIL por malentendidos comunicacionales
- Aclarar y continuar

---

## 9. CONOCIMIENTO BASE

### 9.01 Conocimiento permitido
Sin necesidad de contexto explícito:
- C# syntax y semántica
- .NET BCL APIs estándar
- Patrones de diseño establecidos (GoF)
- Algoritmos fundamentales
- Principios SOLID, DDD básico

### 9.02 Conocimiento prohibido
Requiere contexto explícito:
- Arquitectura específica de NetShaper
- Decisiones de diseño del proyecto
- Trade-offs particulares del dominio
- Configuraciones de build

---

## 10. AUDITORÍA

### 10.01 Auditabilidad
- Regla debe ser convertible a check automático
- O requiere juicio humano (marcar explícitamente)
- Regla no auditable ni humana → inválida

### 10.02 Cobertura
- Documentar gaps de cobertura detectados
- No asumir "si no hay regla, está permitido"
- Reportar ausencia de normativa cuando relevante

---

## 11. ATRIBUTOS TÉCNICOS

### 11.01 Atributos normativos reconocidos
Definidos en NetShaper.Abstractions:

**Arquitectura:**
- `[EngineSetup]`: fase inicialización, reglas relajadas
- `[Boundary]`: permite catch(Exception)
- `[CompositionRoot]`: sin límite dependencias
- `[HotPath]`: marca métodos críticos performance

**Dominio:**
- `[Entity]`: requiere Id + ValidateInvariant()
- `[ValueObject]`: requiere inmutabilidad
- `[AggregateRoot]`: raíz agregado + ValidateInvariant()
- `[DomainEvent]`: inmutable

**Híbridos:**
- `[EngineDomain]`: dominio técnico, DR relajado
- `[ZeroAllocationDomain]`: dominio con heap=0
- `[ProtocolParser]`: permite CC≤12
- `[BatchProcessor]`: permite anidación≤4

### 11.02 Interpretación
- Presencia de atributo modifica aplicabilidad de reglas
- Consultar definición en DNS/RED para excepciones
- Ausencia = reglas estándar aplican

---

## 12. AUTORIDAD

Este documento define comportamiento normativo de agentes IA en contexto NetShaper.

Complementario a DNS/DR/RED, no los modifica.

**Estado:** ACTIVO
**Fecha:** 2025-12-15
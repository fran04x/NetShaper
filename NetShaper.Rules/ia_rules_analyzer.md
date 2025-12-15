# IA_RULES_ANALYZER – ESPECIFICACIÓN v2.0

## 0. PROPÓSITO
Auditar salidas de agentes IA contra IA_Context_Rules.md.
Resultado: PASS / FAIL / SKIP (si no aplicable).

---

## 1. INPUT

```json
{
  "input": "texto completo del prompt usuario",
  "output": "texto completo de la respuesta IA",
  "context_type": "technical | conversational | mixed",
  "active_documents": [
    "Documento_Normativo_Superior.md",
    "Domain_Rules.md", 
    "IA_Context_Rules.md",
    "Reglas_Ejecutables_Derivadas.md"
  ],
  "document_versions": {
    "DNS": "1.0",
    "DR": "1.0",
    "ICR": "2.0",
    "RED": "1.0"
  }
}
```

---

## 2. OUTPUT

```json
{
  "result": "PASS | FAIL | SKIP",
  "context_type_detected": "technical | conversational | mixed",
  "violations": [
    {
      "rule": "IR.x.y",
      "severity": "ERROR | WARNING",
      "description": "...",
      "location": "output line N"
    }
  ],
  "metrics": {
    "rules_referenced": 5,
    "technical_decisions": 8,
    "coverage_ratio": 0.625
  },
  "notes": "opcional"
}
```

**Resultado:**
- FAIL: violations con ERROR > 0
- SKIP: context_type=conversational y sin auditoría técnica
- PASS: resto

---

## 3. PIPELINE

1. **Clasificación de contexto**
   - Detectar si input es técnico, conversacional o mixto
   - Algoritmo: §4.01

2. **Normalización**
   - Tokenización
   - Identificación de bloques código vs texto

3. **Checks estructurales**
   - Presencia de referencias (si técnico)
   - Formato de referencias

4. **Checks semánticos**
   - Deriva conceptual
   - Alucinaciones normativas

5. **Checks de determinismo**
   - Re-ejecución (si aplicable)

6. **Resolución de prioridad**
   - Conflictos entre reglas

7. **Emisión resultado**

---

## 4. CLASIFICACIÓN DE CONTEXTO

### 4.01 Algoritmo detección
```python
def classify_context(input_text: str) -> str:
    technical_keywords = [
        'código', 'code', 'class', 'method', 'namespace',
        'assembly', 'violación', 'auditar', 'generar',
        'implementar', 'refactorizar', 'arquitectura',
        'DNS', 'RED', 'Engine', 'NetShaper'
    ]
    
    conversational_keywords = [
        'hola', 'gracias', 'ayuda', 'explica',
        '¿qué es?', '¿cómo funciona?', '¿por qué?',
        'no entiendo', 'puedes', 'ayudarme'
    ]
    
    tech_score = count_keywords(input_text, technical_keywords)
    conv_score = count_keywords(input_text, conversational_keywords)
    
    # Heurística
    if tech_score == 0 and conv_score > 0:
        return "conversational"
    elif tech_score > conv_score * 2:
        return "technical"
    else:
        return "mixed"
```

### 4.02 Aplicabilidad de reglas
- `conversational`: Solo IR.7.01, IR.8.03 aplican
- `technical`: Todas las reglas aplican
- `mixed`: Aplicar por segmento

---

## 5. REGLAS EJECUTABLES

### IR.0.01 – Referencia obligatoria (técnico)
**Condición:** output técnico (generación código / auditoría)
**Detección:** 
- Regex: `(DNS|DR|ICR|RED|IRA)\s+§?\d+(\.\d+)?` o `R\d+\.\d+`
- Mínimo: 1 referencia por decisión técnica mayor
**Excepciones:**
- Sintaxis C# obvia (using, namespace)
- Respuesta a query conversacional puro
**Violación:** Output técnico sin referencias
**Severity:** ERROR

---

### IR.3.01 – Contexto implícito prohibido
**Condición:** output técnico
**Detección:** 
- Diff semántico input vs output
- Conceptos en output no presentes ni deducibles de input
- Algoritmo: embedding cosine similarity por concepto
**Umbral:** similarity < 0.70 para concepto técnico → investigar
**Whitelist:** 
- Conocimiento C#/.NET estándar (ICR §9.01)
- Patrones GoF
- Principios SOLID
**Violación:** Concepto técnico NetShaper específico sin contexto
**Severity:** ERROR

---

### IR.3.02 – Memoria histórica no referenciada
**Condición:** cualquier output
**Detección:**
- Output menciona decisiones previas sin citar turno
- Formato esperado: "como mencioné en [turno N]"
**Violación:** Referencia a histórico sin explicitación
**Severity:** WARNING

---

### IR.4.01 – Deriva semántica
**Condición:** output técnico con términos de dominio
**Detección:**
- Extraer términos técnicos de output
- Buscar en DNS/DR/RED/ICR
- Embedding similarity vs documentos normativos
**Algoritmo:**
- Modelo: sentence-transformers/all-MiniLM-L6-v2
- Métrica: cosine similarity
- Umbral: < 0.65 → posible deriva
**Whitelist manual:** términos C#, .NET, patrones estándar
**Violación:** Término técnico no presente en normativa
**Severity:** ERROR

---

### IR.4.02 – Parafraseo normativo prohibido
**Condición:** output cita regla normativa
**Detección:**
- Identificar claims sobre normas: "DNS dice que...", "según RED..."
- Extraer texto citado
- Fuzzy match contra texto real normativo
**Algoritmo:**
- Levenshtein ratio (python: difflib.SequenceMatcher.ratio())
- Umbral: < 0.90 → parafraseo excesivo
**Excepción:** Reformulación marcada con "en otras palabras" permitida
**Violación:** Cita con match < 0.90
**Severity:** ERROR

---

### IR.5.02 – Exhaustividad contextual
**Condición:** depende de context_type
**Detección:**

**Si technical:**
- Contar decisiones técnicas en output
- Contar referencias normativas
- Ratio = referencias / decisiones
- Umbral: ratio ≥ 0.50

**Si conversational:**
- SKIP esta regla

**Violación:** ratio < 0.50 en contexto técnico
**Severity:** WARNING (no ERROR, permite decisiones menores)

---

### IR.6.02 – Determinismo (técnico)
**Condición:** output técnico reproducible
**Detección:**
- Re-ejecutar prompt 2 veces adicionales
- Comparar outputs semánticamente
- Diff tolerante: orden enumeraciones, sinónimos técnicos OK
**Algoritmo:**
```python
def semantic_diff(out1: str, out2: str) -> float:
    # Normalizar espacios, puntuación
    norm1 = normalize(out1)
    norm2 = normalize(out2)
    
    # Embedding similarity
    emb1 = embed(norm1)
    emb2 = embed(norm2)
    sim = cosine_similarity(emb1, emb2)
    
    return sim

# Umbral: similarity ≥ 0.95
```
**Excepción:** Timestamps, ejemplos con datos aleatorios
**Violación:** similarity < 0.95 entre runs
**Severity:** WARNING

---

### IR.7.01 – Inferencia no solicitada prohibida
**Condición:** output técnico
**Detección:**
- Buscar verbos modales no presentes en input: "debería", "podría", "sería mejor"
- Buscar introducciones de requisitos: "también necesitas", "además debes"
**Excepción:** 
- Contexto conversacional: sugerencias permitidas
- Marcadas explícitamente: "Sugerencia no-normativa:"
**Violación:** Requisito técnico no solicitado sin marca
**Severity:** ERROR

---

### IR.7.02 – Comunicación por contexto
**Condición:** cualquier output
**Detección:**
- Si conversational: verificar tono no excesivamente formal
- Si technical: verificar ausencia de adornos ("perfecto", "robusto")
**Algoritmo:** Lista negra de términos valorativos en contexto técnico
**Violación:** Lenguaje inadecuado al contexto
**Severity:** WARNING

---

### IR.8.01 – FAIL técnico explícito
**Condición:** violación ERROR severity en contexto técnico
**Detección:**
- Si existe violación ERROR
- Verificar que output no contiene solución parcial
**Violación:** Output parcial ante ERROR
**Severity:** ERROR (meta-violación)

---

### IR.8.03 – Errores conversacionales
**Condición:** malentendido comunicacional
**Detección:**
- Usuario expresa frustración
- Output previo malinterpretó claramente
**Comportamiento esperado:** Aclaración cortés, sin FAIL
**Violación:** FAIL emitido por malentendido comunicacional
**Severity:** ERROR (sobre-enforcement)

---

## 6. PRIORIDAD

Orden resolución conflictos:
1. DNS (autoridad máxima técnica)
2. DR (autoridad dominio)
3. ICR (autoridad comportamiento IA)
4. RED (enforcement específico)
5. IRA (auditoría meta)

**Principio especificidad:** Regla específica prevalece sobre genérica independiente de documento.

**Conflicto no resuelto → FAIL.**

---

## 7. MÉTRICAS SALIDA

### 7.01 Coverage ratio
```
coverage_ratio = rules_referenced / technical_decisions
```
- `technical_decisions`: count de decisiones arquitectónicas/técnicas significativas
- `rules_referenced`: count único de reglas citadas
- Óptimo: ≥0.60

### 7.02 Determinism score
```
determinism_score = semantic_similarity(run1, run2, run3)
```
- Promedio similarity entre 3 ejecuciones
- Óptimo: ≥0.95

### 7.03 Drift score
```
drift_score = novel_concepts_count / total_technical_concepts
```
- `novel_concepts`: términos no en normativa ni whitelist
- Óptimo: ≤0.10

---

## 8. CONFIGURACIÓN

### 8.01 Severity levels
- **ERROR:** Bloquea, resultado = FAIL
- **WARNING:** Marca para revisión, resultado puede ser PASS
- **INFO:** Métricas, no afecta resultado

### 8.02 Thresholds configurables
```json
{
  "ir_4_01_embedding_threshold": 0.65,
  "ir_4_02_fuzzy_match_threshold": 0.90,
  "ir_5_02_coverage_ratio": 0.50,
  "ir_6_02_determinism_threshold": 0.95,
  "ir_7_02_formality_tolerance": "medium"
}
```

---

## 9. EXTENSIÓN

### 9.01 Nuevas reglas
- Prefijo: IR.{seccion}.{numero}
- Todas las reglas deben especificar:
  - Condición clara
  - Algoritmo de detección
  - Umbrales numéricos
  - Excepciones
  - Severity

### 9.02 Versionado
- Cambio de algoritmo → minor version bump
- Nueva regla → minor version bump  
- Cambio breaking (umbral más estricto) → major version bump

---

## 10. ESTADO

Analyzer sin memoria persistente.
Cada ejecución es aislada.
Re-entrante y thread-safe.

**Estado:** ACTIVO v2.0
**Fecha:** 2025-12-15
**Cambios v2.0:**
- §1: context_type en input
- §2: SKIP como resultado
- §4: clasificación contexto
- §5: reglas contextuales actualizadas
- IR.3.02: memoria histórica
- IR.7.02: comunicación contextual
- IR.8.03: errores conversacionales
- §7: métricas detalladas
- §8: configuración explicita

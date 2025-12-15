# NetShaper Performance Benchmarks

## Benchmark Suites

### 1. **EngineBenchmarks** - Throughput
Mide el rendimiento de procesamiento de paquetes bajo carga sostenida.

**Benchmarks:**
- `SmallPackets` - 10,000 paquetes de 64 bytes
- `MediumPackets` - 10,000 paquetes de 512 bytes  
- `LargePackets` - 10,000 paquetes de 1400 bytes

**Métricas:**
- Tiempo total de procesamiento
- Allocaciones de memoria
- Threading overhead

---

### 2. **LatencyJitterBenchmark** - Latencia/Jitter ✨ NUEVO
Mide latencia individual por paquete y variabilidad (jitter) para certificación de nivel.

**Benchmarks:**
- `MeasureLatency` - 1,000 mediciones individuales de round-trip
- `MeasureJitter` - Variabilidad de latencia bajo carga sostenida

**Métricas medidas:**
- **Latencia:**
  - P50 (mediana)
  - P95 (95th percentile)
  - P99 (99th percentile)
  - Min/Max/Mean
  
- **Jitter:**
  - Desviación estándar
  - Jitter promedio
  - Jitter máximo
  - P99 jitter

---

## Niveles de Certificación

### 🥇 **Nivel 1: Excelente** (Target actual)
```
Latencia:
  P50 < 50 μs
  P95 < 100 μs
  P99 < 200 μs

Jitter:
  Avg < 20 μs
  Max < 100 μs
  P99 < 80 μs
```

### 🥈 **Nivel 2: Bueno**
```
Latencia:
  P50 < 100 μs
  P95 < 250 μs
  P99 < 500 μs

Jitter:
  Avg < 50 μs
  Max < 200 μs
  P99 < 150 μs
```

### 🥉 **Nivel 3: Aceptable**
```
Latencia:
  P50 < 200 μs
  P95 < 500 μs
  P99 < 1 ms

Jitter:
  Avg < 100 μs
  Max < 500 μs
  P99 < 300 μs
```

---

## Cómo Ejecutar

### Opción 1: Ambos benchmarks
```powershell
cd NetShaper.Benchmarks
dotnet run --configuration Release
```

### Opción 2: Solo latencia/jitter
```powershell
dotnet run --configuration Release --filter "*LatencyJitter*"
```

### Opción 3: Solo throughput
```powershell
dotnet run --configuration Release --filter "*EngineBenchmarks*"
```

---

## Interpretación de Resultados

### Latencia
- **P50 (mediana):** Latencia típica esperada - debe ser baja
- **P95:** 95% de paquetes procesados más rápido - objetivo crítico
- **P99:** Casos extremos - importante para consistencia

### Jitter
- **Avg (promedio):** Variabilidad media - indica estabilidad
- **Max:** Peor caso observado - importante para real-time
- **P99:** Variabilidad extrema - certificación de calidad

---

## Ejemplo de Salida

```
| Method           | Mean     | StdDev   | P50_us | P95_us | P99_us |
|----------------- |---------:|---------:|-------:|-------:|-------:|
| MeasureLatency   | 45.2 μs  | 8.3 μs   | 42.1   | 58.7   | 125.4  |
| MeasureJitter    | 38.9 μs  | 12.1 μs  | 15.2   | 68.3   | 145.7  |
```

**Resultado:** ✅ **Nivel 1 (Excelente)** - P95 < 100μs, P99 < 200μs

---

## Notas de Implementación

1. **Warmup:** 100 paquetes antes de medición
2. **Samples:** 1,000 mediciones para estadísticas confiables
3. **Precisión:** Usa `Stopwatch.GetTimestamp()` (alta resolución)
4. **Isolation:** Puerto dedicado (55557) separado de throughput tests

## Mejoras Futuras

- [ ] Benchmark bajo diferentes load patterns (burst, constante, sinusoidal)
- [ ] Medición de tail latency (P9999)
- [ ] Comparación con/sin rate limiting
- [ ] Benchmark multi-threaded concurrente

# API — Plantillas de requests desde la app móvil

Base URL: `http://<IP_SERVIDOR>:8080`

---

## POST /register

**Request:**
```
POST /register
Content-Type: application/json

{
  "legajo": "",
  "nombre": ""
}
```

**Response (200):**
```json
{ "ok": true }
```

**Response (400):**
```json
{ "error": "Legajo y nombre son obligatorios" }
```

---

## GET /solve — onda 2D

**Request:**
```
GET /solve?ecuacion=onda2d&L=5&T=3&c=1&inicial=seno
```

| Parámetro | Opciones |
|-----------|----------|
| `L` | 1 – 10 |
| `T` | 1 – 60 |
| `c` | 0.1 – 2 |
| `inicial` | `seno` · `triangular` · `gauss` |

**Response (200):**
```json
{
  "sync": false,
  "ecuacion": "onda2d",
  "result": [[[0.0, 0.1], [0.2, 0.3]], [[0.0, 0.05], [0.1, 0.15]]]
}
```

**Response (400):**
```json
{ "error": "Parametros fuera de rango" }
```

**Response (429 — rate limit):**
```json
{
  "error": "Demasiadas peticiones",
  "waitSeconds": 3,
  "message": "Espera 3s antes de reintentar"
}
```

---

## GET /solve — calor 2D

**Request:**
```
GET /solve?ecuacion=calor2d&L=5&T=10&c=0.5&inicial=gauss
```

| Parámetro | Opciones |
|-----------|----------|
| `L` | 1 – 20 |
| `T` | 1 – 60 |
| `c` | 0.1 – 2 (difusividad térmica α) |
| `inicial` | `gauss` · `bordes` |

**Response (200):**
```json
{
  "sync": false,
  "ecuacion": "calor2d",
  "result": [[[0.0, 0.0], [0.0, 0.0]], [[0.01, 0.02], [0.02, 0.01]]]
}
```

**Response (400):**
```json
{ "error": "Parametros fuera de rango" }
```

**Response (429 — rate limit):**
```json
{
  "error": "Demasiadas peticiones",
  "waitSeconds": 3,
  "message": "Espera 3s antes de reintentar"
}
```

---

## POST /sync/set — activar sincronización (solo asistente)

**Request:**
```
POST /sync/set
Content-Type: application/json

{
  "ecuacion": "onda2d",
  "L": 5,
  "T": 3,
  "c": 1,
  "inicial": "seno"
}
```

**Response (200):**
```json
{ "ok": true }
```

**Response (403 — sin rol de asistente):**
```json
{ "error": "Se requiere rol de asistente" }
```

**Response (400):**
```json
{ "error": "Parametros fuera de rango" }
```

---

## POST /sync/clear — desactivar sincronización (solo asistente)

**Request:**
```
POST /sync/clear
```

**Response (200):**
```json
{ "ok": true }
```

**Response (403 — sin rol de asistente):**
```json
{ "error": "Se requiere rol de asistente" }
```

---

## Nota sobre `result`

`result[frame][i][j]` es el valor de la solución en el frame `frame`, fila `i`, columna `j`.  
El array tiene hasta 200 frames. Las dimensiones `i` y `j` dependen de `L`:  
- onda2d: `n = ceil(L × 8)`, mínimo 16  
- calor2d: `n = ceil(L × 5)`, mínimo 10

## Nota sobre `sync`

Cuando la respuesta de `/solve` trae `sync: true`, significa que los parámetros enviados en el request fueron ignorados y se usaron los del asistente. La app puede usar este campo para mostrar un indicador visual al alumno.

package main

import (
	"net/http"
	"strconv"

	"github.com/gin-gonic/gin"
)

// registerRequest representa el cuerpo JSON esperado en POST /register.
type registerRequest struct {
	Legajo string `json:"legajo"`
	Nombre string `json:"nombre"`
}

// registerHandler registra un alumno nuevo o actualiza su ultimo request.
// Responde:
//   - 200 si el registro fue exitoso
//   - 400 si el cuerpo JSON es invalido o faltan campos
func registerHandler(c *gin.Context) {
	var req registerRequest

	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Cuerpo de solicitud inválido"})
		return
	}

	if req.Legajo == "" || req.Nombre == "" {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Legajo y nombre son obligatorios"})
		return
	}

	estado.RegistrarCliente(c.ClientIP(), req.Legajo, req.Nombre)

	c.JSON(http.StatusOK, gin.H{"ok": true})
}

// solveHandler calcula la solución de la ecuación seleccionada.
// Si hay sincronización activa, ignora los parámetros del alumno
// y usa los del asistente. Devuelve la matriz solución junto con
// el estado de sincronización para que la app pueda reaccionar.
//
// Parámetros esperados (query params):
//   - ecuacion : ecuación a resolver  ("onda2d", "calor")
//   - L        : longitud del dominio [1, 10]
//   - T        : tiempo total         [1, 10]
//   - c        : velocidad de onda (onda2d) o difusividad (calor) [0.1, 2]
//   - inicial  : condición inicial    ("seno", "triangular", "gauss")
//
// Responde:
//   - 200 con { "sync": bool, "ecuacion": string, "result": ... }
//   - 400 si los parámetros son inválidos o están fuera de rango
func solveHandler(c *gin.Context) {

	// Actualizar ultimo request del alumno si ya esta registrado.
	estado.ActualizarRequest(c.ClientIP())

	// Verificar si hay sincronizacion activa.
	syncActivo, params := estado.EstadoSync()

	var L, T, cw, gamma float64
	var inicial, ecuacion string
	var amortiguamiento bool
	var exc excitacionOpts

	if syncActivo {
		L = params.L
		T = params.T
		cw = params.C
		inicial = params.Inicial
		ecuacion = params.Ecuacion
		amortiguamiento = params.Amortiguamiento
		gamma = params.Gamma
		exc = excitacionOpts{
			Activa:     params.Excitacion,
			Forma:      params.FormaExcitacion,
			Frecuencia: params.FrecExcitacion,
		}
	} else {
		Ls, ok1 := c.GetQuery("L")
		Ts, ok2 := c.GetQuery("T")
		cs, ok3 := c.GetQuery("c")
		inicial, _ = c.GetQuery("inicial")
		ecuacion, _ = c.GetQuery("ecuacion")

		if !ok1 || !ok2 || !ok3 {
			c.JSON(http.StatusBadRequest, gin.H{"error": "Faltan parámetros en la consulta"})
			return
		}

		var err1, err2, err3 error
		L, err1 = strconv.ParseFloat(Ls, 64)
		T, err2 = strconv.ParseFloat(Ts, 64)
		cw, err3 = strconv.ParseFloat(cs, 64)

		if err1 != nil || err2 != nil || err3 != nil {
			c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros inválidos"})
			return
		}

		lMax := 10.0
		if ecuacion == "calor2d" {
			lMax = 20.0
		}
		if L < 1 || L > lMax || T < 1 || T > 100 || cw < 0.1 || cw > 2 {
			c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros fuera de rango"})
			return
		}

		if gs, ok := c.GetQuery("gamma"); ok {
			if g, err := strconv.ParseFloat(gs, 64); err == nil && g >= 0 && g <= 1 {
				gamma = g
				amortiguamiento = gamma > 0
			}
		}

		if fs, ok := c.GetQuery("frecExcitacion"); ok {
			if f, err := strconv.ParseFloat(fs, 64); err == nil && f > 0 && f <= 20 {
				forma, _ := c.GetQuery("formaExcitacion")
				if forma == "" {
					forma = "seno"
				}
				exc = excitacionOpts{Activa: true, Forma: forma, Frecuencia: f}
			}
		}

		if inicial == "modo" {
			as, _ := c.GetQuery("modoA")
			bs, _ := c.GetQuery("modoB")
			a, errA := strconv.Atoi(as)
			b, errB := strconv.Atoi(bs)
			if errA != nil || a < 1 {
				a = 1
			}
			if a > 5 {
				a = 5
			}
			if errB != nil || b < 1 {
				b = 1
			}
			if b > 5 {
				b = 5
			}
			inicial = "modo:" + strconv.Itoa(a) + ":" + strconv.Itoa(b)
		}
	}

	var result interface{}
	switch ecuacion {
	case "calor2d":
		result = ecuacion_calor_2d(L, T, cw, inicial)
	default:
		ecuacion = "onda2d"
		effectiveGamma := 0.0
		if amortiguamiento {
			effectiveGamma = gamma
		}
		result = ecuacion_onda_2d(L, T, cw, effectiveGamma, exc, inicial)
	}

	c.JSON(http.StatusOK, gin.H{
		"sync":     syncActivo,
		"ecuacion": ecuacion,
		"result":   result,
	})

}

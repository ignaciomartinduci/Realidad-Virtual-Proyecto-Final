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

// solveHandler calcula la solución de la ecuación de onda.
// Si hay sincronización activa, ignora los parámetros del alumno
// y usa los del asistente. Devuelve la matriz solución junto con
// el estado de sincronización para que la app pueda reaccionar.
//
// Parámetros esperados (query params):
//   - L       : longitud del dominio [1, 10]
//   - T       : tiempo total         [1, 10]
//   - c       : velocidad de onda    [0.1, 2]
//   - inicial : condición inicial    ("seno", "triangular", "gauss")
//
// Responde:
//   - 200 con { "sync": bool, "result": [[...], ...] }
//   - 400 si los parámetros son inválidos o estan fuera de rango
func solveHandler(c *gin.Context) {

	// Actualizar ultimo request del alumno si ya esta registrado.
	estado.ActualizarRequest(c.ClientIP())

	// Verificar si hay sincronizacion activa.
	syncActivo, params := estado.EstadoSync()

	var L, T, cw float64
	var inicial string

	if syncActivo {
		// Usar los parametros forzados por el asistente.
		L = params.L
		T = params.T
		cw = params.C
		inicial = params.Inicial
	} else {
		// Leer y validar los parametros del alumno.
		Ls, ok1 := c.GetQuery("L")
		Ts, ok2 := c.GetQuery("T")
		cs, ok3 := c.GetQuery("c")
		inicial, _ = c.GetQuery("inicial")

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

		if L < 1 || L > 10 || T < 1 || T > 10 || cw < 0.1 || cw > 2 {
			c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros fuera de rango"})
			return
		}
	}

	result := ecuacion_onda(L, T, cw, inicial)

	c.JSON(http.StatusOK, gin.H{
		"sync":   syncActivo,
		"result": result,
	})

}

// syncSetRequest representa el cuerpo JSON esperado en POST /sync/set.
type syncSetRequest struct {
	L       float64 `json:"L"`
	T       float64 `json:"T"`
	C       float64 `json:"c"`
	Inicial string  `json:"inicial"`
}

// syncSetHandler activa la sincronizacion con los parametros enviados por el asistente.
// Solo puede ser llamado por un cliente con rol de asistente.
// Responde:
//   - 200 si la sincronizacion se activo correctamente
//   - 400 si el cuerpo JSON es invalido o los parametros estan fuera de rango
//   - 403 si el cliente no tiene rol de asistente
func syncSetHandler(c *gin.Context) {
	// Verificar que el solicitante sea asistente.
	cliente := estado.BuscarCliente(c.ClientIP())
	if cliente == nil || cliente.Rol != rolAsistente {
		c.JSON(http.StatusForbidden, gin.H{"error": "Se requiere rol de asistente"})
		return
	}

	var req syncSetRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Cuerpo de solicitud inválido"})
		return
	}

	if req.L < 1 || req.L > 10 || req.T < 1 || req.T > 10 || req.C < 0.1 || req.C > 2 {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros fuera de rango"})
		return
	}

	estado.ActivarSync(parametrosSync{
		L:       req.L,
		T:       req.T,
		C:       req.C,
		Inicial: req.Inicial,
	})

	c.JSON(http.StatusOK, gin.H{"ok": true})
}

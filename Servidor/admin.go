package main

import (
	"net/http"

	"github.com/gin-gonic/gin"
)

// clienteResponse es la representacion de un cliente para el panel docente.
type clienteResponse struct {
	IP            string `json:"ip"`
	Legajo        string `json:"legajo"`
	Nombre        string `json:"nombre"`
	UltimoRequest string `json:"ultimoRequest"`
}

// syncSetRequest representa el cuerpo JSON esperado en POST /admin/sync/set.
type syncSetRequest struct {
	Ecuacion        string  `json:"ecuacion"`
	L               float64 `json:"L"`
	T               float64 `json:"T"`
	C               float64 `json:"c"`
	Inicial         string  `json:"inicial"`
	Amortiguamiento bool    `json:"amortiguamiento"`
	Gamma           float64 `json:"gamma"`
	Excitacion      bool    `json:"excitacion"`
	FormaExcitacion string  `json:"formaExcitacion"`
	FrecExcitacion  float64 `json:"frecExcitacion"`
}

type syncStatusResponse struct {
	Activo          bool    `json:"activo"`
	Version         uint64  `json:"version"`
	Ecuacion        string  `json:"ecuacion"`
	L               float64 `json:"l"`
	T               float64 `json:"t"`
	C               float64 `json:"c"`
	Inicial         string  `json:"inicial"`
	Amortiguamiento bool    `json:"amortiguamiento"`
	Gamma           float64 `json:"gamma"`
	Excitacion      bool    `json:"excitacion"`
	FormaExcitacion string  `json:"formaExcitacion"`
	FrecExcitacion  float64 `json:"frecExcitacion"`
}

// syncStatusHandler es consultado periodicamente por las aplicaciones. No
// ejecuta el solver: solo devuelve el estado y su numero de version.
func syncStatusHandler(c *gin.Context) {
	estado.ActualizarRequest(c.ClientIP())
	activo, p, version := estado.EstadoSyncVersionado()
	c.JSON(http.StatusOK, syncStatusResponse{
		Activo: activo, Version: version,
		Ecuacion: p.Ecuacion, L: p.L, T: p.T, C: p.C, Inicial: p.Inicial,
		Amortiguamiento: p.Amortiguamiento, Gamma: p.Gamma,
		Excitacion: p.Excitacion, FormaExcitacion: p.FormaExcitacion,
		FrecExcitacion: p.FrecExcitacion,
	})
}

// clientesHandler devuelve la lista de todos los alumnos registrados.
// GET /admin/clients
func clientesHandler(c *gin.Context) {
	clientes := estado.Clientes()

	lista := make([]clienteResponse, 0, len(clientes))
	for _, cl := range clientes {
		lista = append(lista, clienteResponse{
			IP:            cl.IP,
			Legajo:        cl.Legajo,
			Nombre:        cl.Nombre,
			UltimoRequest: cl.UltimoRequest.Format("15:04:05"),
		})
	}

	c.JSON(http.StatusOK, gin.H{"clientes": lista})
}

// syncClearHandler desactiva la sincronizacion desde el panel del profesor.
// POST /admin/sync/clear
func syncClearHandler(c *gin.Context) {
	estado.DesactivarSync()
	c.JSON(http.StatusOK, gin.H{"ok": true})
}

// syncSetAdminHandler permite al profesor activar la sincronizacion
// directamente desde el panel web, sin necesitar rol de asistente.
// POST /admin/sync/set
func syncSetAdminHandler(c *gin.Context) {
	var req syncSetRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Cuerpo de solicitud inválido"})
		return
	}
	if req.Ecuacion != "onda2d" && req.Ecuacion != "calor2d" {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Ecuación inválida"})
		return
	}

	lMax := 10.0
	if req.Ecuacion == "calor2d" {
		lMax = 20.0
	}
	if req.L < 1 || req.L > lMax || req.T < 1 || req.T > 100 || req.C < 0.1 || req.C > 2 {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros fuera de rango"})
		return
	}

	estado.ActivarSync(parametrosSync{
		Ecuacion:        req.Ecuacion,
		L:               req.L,
		T:               req.T,
		C:               req.C,
		Inicial:         req.Inicial,
		Amortiguamiento: req.Amortiguamiento,
		Gamma:           req.Gamma,
		Excitacion:      req.Excitacion,
		FormaExcitacion: req.FormaExcitacion,
		FrecExcitacion:  req.FrecExcitacion,
	})

	c.JSON(http.StatusOK, gin.H{"ok": true})
}

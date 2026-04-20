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
	Rol           string `json:"rol"`
	UltimoRequest string `json:"ultimoRequest"`
}

// clientesHandler devuelve la lista de todos los alumnos registrados.
// GET /admin/clients
func clientesHandler(c *gin.Context) {
	clientes := estado.Clientes()

	lista := make([]clienteResponse, 0, len(clientes))
	for _, cl := range clientes {
		rolStr := "alumno"
		if cl.Rol == rolAsistente {
			rolStr = "asistente"
		}
		lista = append(lista, clienteResponse{
			IP:            cl.IP,
			Legajo:        cl.Legajo,
			Nombre:        cl.Nombre,
			Rol:           rolStr,
			UltimoRequest: cl.UltimoRequest.Format("15:04:05"),
		})
	}

	c.JSON(http.StatusOK, gin.H{"clientes": lista})
}

// promoverRequest representa el cuerpo JSON esperado en POST /admin/promote.
type promoverRequest struct {
	IP string `json:"ip"`
}

// promoverHandler otorga el rol de asistente al cliente con la IP indicada.
// POST /admin/promote
func promoverHandler(c *gin.Context) {
	var req promoverRequest

	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Cuerpo de solicitud inválido"})
		return
	}

	if !estado.PromoverAsistente(req.IP) {
		c.JSON(http.StatusNotFound, gin.H{"error": "Cliente no encontrado"})
		return
	}

	c.JSON(http.StatusOK, gin.H{"ok": true})
}

// demoverHandler revoca el rol de asistente del cliente con la IP indicada.
// POST /admin/demote
func demoverHandler(c *gin.Context) {
	var req promoverRequest // reutilizamos el mismo struct, solo necesita IP

	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Cuerpo de solicitud inválido"})
		return
	}

	if !estado.DemoverAsistente(req.IP) {
		c.JSON(http.StatusNotFound, gin.H{"error": "Cliente no encontrado"})
		return
	}

	c.JSON(http.StatusOK, gin.H{"ok": true})
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

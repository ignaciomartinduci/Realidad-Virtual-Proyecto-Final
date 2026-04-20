package main

import (
	"github.com/gin-gonic/gin"
)

// Código de acceso del profesor. Hardcodeado por decisión de diseño.
const codigoProfesor = "LlXxBb"

func main() {

	router := gin.Default()

	// Sirve la pantalla de login
	router.StaticFile("/", "./web/login.html")

	// Autenticación del profesor
	router.POST("/auth", authHandler)

	// Panel docente — protegido por token de un solo uso
	router.GET("/panel", panelHandler)

	// Rutas protegidas del panel docente (requieren haber pasado /auth)
	// Se iran agregando a medida que se desarrollen los modulos
	admin := router.Group("/admin")
	{
		admin.GET("/clients", clientesHandler)       // lista de alumnos registrados
		admin.POST("/promote", promoverHandler)      // otorgar rol asistente
		admin.POST("/demote", demoverHandler)        // revocar rol asistente
		admin.POST("/sync/clear", syncClearHandler)  // liberar sincronizacion
		admin.POST("/sync/set", syncSetAdminHandler) // profesor activa sincronizacion
	}

	// Rutas de la app movil
	router.POST("/register", registerHandler)                 // registro de alumno
	router.GET("/solve", RateLimitMiddleware(), solveHandler) // calculo — limitado a 1 req/5s por IP
	router.POST("/sync/set", syncSetHandler)                  // asistente activa sincronizacion

	router.Run(":8080")
}

package main

import (
	"flag"

	"github.com/gin-gonic/gin"
)

// Código de acceso del profesor. Hardcodeado por decisión de diseño.
const codigoProfesor = "123"

func main() {
	port := flag.String("port", "", "puerto del servidor (omitir para usar la GUI)")
	flag.Parse()

	if *port != "" {
		startGinServer(*port)
	} else {
		runLauncherGUI()
	}
}

func setupRouter() *gin.Engine {
	router := gin.Default()

	router.StaticFile("/", "./web/login.html")
	router.POST("/auth", authHandler)
	router.GET("/panel", panelHandler)

	admin := router.Group("/admin")
	{
		admin.GET("/clients", clientesHandler)
		admin.POST("/sync/clear", syncClearHandler)
		admin.POST("/sync/set", syncSetAdminHandler)
	}

	router.POST("/register", registerHandler)
	router.GET("/sync/status", syncStatusHandler)
	router.POST("/solve/wave2d", RateLimitMiddleware(), waveSolveUnityHandler)
	router.POST("/solve/heat2d", RateLimitMiddleware(), heatSolveUnityHandler)
	router.GET("/solve", RateLimitMiddleware(), solveHandler)

	return router
}

package main

import (
	"fmt"
	"math"
	"net/http"
	"sync"
	"time"

	"github.com/gin-gonic/gin"
	"golang.org/x/time/rate"
)

// authRequest representa el cuerpo JSON esperado en POST /auth.
type authRequest struct {
	Codigo string `json:"codigo"`
}

// ipsAutorizadas almacena las IPs que pasaron el login del profesor.
var (
	ipsAutorizadas   = make(map[string]bool)
	ipsAutorizadasMu sync.RWMutex
)

var limiters sync.Map

func getLimiter(ip string) *rate.Limiter {
	if v, ok := limiters.Load(ip); ok {
		return v.(*rate.Limiter)
	}
	lim := rate.NewLimiter(rate.Every(5*time.Second), 1)
	actual, _ := limiters.LoadOrStore(ip, lim)
	return actual.(*rate.Limiter)
}

func RateLimitMiddleware() gin.HandlerFunc {
	return func(c *gin.Context) {
		ip := c.ClientIP()
		lim := getLimiter(ip)

		res := lim.Reserve()
		if !res.OK() {
			c.AbortWithStatusJSON(http.StatusTooManyRequests, gin.H{"error": "Demasiadas peticiones"})
			return
		}

		delay := res.Delay()
		if delay <= 0 {
			c.Next()
			return
		}

		res.Cancel()
		sec := int(math.Ceil(delay.Seconds()))
		c.Header("Retry-After", fmt.Sprintf("%d", sec))
		c.AbortWithStatusJSON(http.StatusTooManyRequests, gin.H{
			"error":       "Demasiadas peticiones",
			"waitSeconds": sec,
			"message":     fmt.Sprintf("Espera %ds antes de reintentar", sec),
		})
	}
}

// autorizarIP registra una IP como autorizada para acceder al panel.
func autorizarIP(ip string) {
	ipsAutorizadasMu.Lock()
	defer ipsAutorizadasMu.Unlock()
	ipsAutorizadas[ip] = true
	fmt.Println("[auth] IP autorizada:", ip)
}

// ipAutorizada devuelve true si la IP ya paso el login.
func ipAutorizada(ip string) bool {
	ipsAutorizadasMu.RLock()
	defer ipsAutorizadasMu.RUnlock()
	return ipsAutorizadas[ip]
}

// authHandler verifica el código de acceso del profesor.
// Si es correcto, registra la IP del solicitante como autorizada.
// Responde:
//   - 200 si el código es correcto
//   - 401 si el código es incorrecto
//   - 400 si el cuerpo JSON es inválido
func authHandler(c *gin.Context) {
	var req authRequest

	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Cuerpo de solicitud inválido"})
		return
	}

	if req.Codigo != codigoProfesor {
		c.JSON(http.StatusUnauthorized, gin.H{"error": "Código incorrecto"})
		return
	}

	autorizarIP(c.ClientIP())
	c.JSON(http.StatusOK, gin.H{"ok": true})
}

// panelHandler sirve el panel.html solo si la IP del solicitante
// fue previamente autorizada via POST /auth.
// Si no esta autorizada, redirige al login.
func panelHandler(c *gin.Context) {
	if !ipAutorizada(c.ClientIP()) {
		c.Redirect(http.StatusFound, "/")
		return
	}

	c.File("./web/panel.html")
}

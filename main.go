package main

import (
	"fmt"
	"math"
	"net/http"
	"strconv"
	"time"

	"sync"

	"github.com/gin-gonic/gin"
	"golang.org/x/time/rate"
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

		// Mirar cuánto falta sin consumir token
		res := lim.Reserve()
		if !res.OK() { // imposible reservar -> rechaza
			c.AbortWithStatusJSON(429, gin.H{"error": "Demasiadas peticiones"})
			return
		}
		delay := res.Delay() // cuánto habría que esperar
		if delay <= 0 {
			// estamos dentro del cupo: dejamos la reserva activa
			c.Next()
			return
		}
		// demasiado pronto: cancelamos la reserva y rechazamos
		res.Cancel()

		sec := int(math.Ceil(delay.Seconds()))
		c.Header("Retry-After", fmt.Sprintf("%d", sec))
		c.AbortWithStatusJSON(429, gin.H{
			"error":       "Demasiadas peticiones",
			"waitSeconds": sec,
			"message":     fmt.Sprintf("Espera %ds antes de reintentar", sec),
		})
	}
}

func solve(c *gin.Context) {

	L, ok := c.GetQuery("L")
	T, ok2 := c.GetQuery("T")
	cw, ok3 := c.GetQuery("c")
	inicial, ok4 := c.GetQuery("inicial")

	L_f, err := strconv.ParseFloat(L, 64)
	T_f, err2 := strconv.ParseFloat(T, 64)
	cw_f, err3 := strconv.ParseFloat(cw, 64)

	if L_f > 10 || L_f < 1 || T_f > 10 || T_f < 1 || cw_f < 0.1 || cw_f > 2 {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros fuera de rango"})
		return
	}

	if err != nil || err2 != nil || err3 != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros inválidos"})
		return
	}

	if !ok || !ok2 || !ok3 || !ok4 {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Faltan parámetros en la consulta"})
		return
	}

	res := ecuacion_onda(L_f, T_f, cw_f, inicial)

	c.JSON(http.StatusOK, gin.H{"result": res})
}

func ecuacion_onda(L, T, c float64, inicial string) [][]float64 {

	var n_x int = int(math.Ceil(L * 100))
	d_x := L / float64(n_x)
	var CFL float64 = 0.98
	var n_t int = int(math.Ceil(T * c / (CFL * d_x)))
	d_t := T / float64(n_t)

	var u_0_t float64 = 0
	var u_L_t float64 = 0

	u := make([][]float64, n_t)
	for i := range u {
		u[i] = make([]float64, n_x)
	}

	switch inicial {
	case "seno":
		for j := 0; j < n_x; j++ {
			u[0][j] = math.Sin(math.Pi * float64(j) * d_x)
		}
	case "triangular":
		for j := 0; j < n_x; j++ {
			if float64(j)*d_x < L/2 {
				u[0][j] = (2 / L) * float64(j) * d_x
			}
			if float64(j)*d_x >= L/2 {
				u[0][j] = 2 - (2/L)*float64(j)*d_x
			}
		}
	case "gauss":
		for j := 0; j < n_x; j++ {
			u[0][j] = math.Exp(-math.Pow((float64(j)*d_x-L/2)/(L/10), 2))
		}
	default:
		for j := 0; j < n_x; j++ {
			u[0][j] = math.Sin(math.Pi * float64(j) * d_x)
		}
	}

	for i := 0; i < n_t; i++ { // Condiciones de frontera u(0,t) = 0 y u(L,t) = 0
		u[i][0] = u_0_t
		u[i][n_x-1] = u_L_t
	}

	var1 := (c * d_t / d_x) * (c * d_t / d_x)

	for j := 1; j < n_x-1; j++ {
		u[1][j] = u[0][j] + 0.5*var1*(u[0][j+1]-2*u[0][j]+u[0][j-1])
	}

	for i := 2; i < n_t; i++ {
		for j := 1; j < n_x-1; j++ {
			u[i][j] = 2*u[i-1][j] - u[i-2][j] +
				var1*(u[i-1][j+1]-2*u[i-1][j]+u[i-1][j-1])
		}
	}

	return u

}

func main() {

	route := gin.Default()

	route.GET("/solve", RateLimitMiddleware(), solve)

	route.NoRoute(func(c *gin.Context) {
		c.File("./web/index.html")
	})
	route.Run(":8080")

}

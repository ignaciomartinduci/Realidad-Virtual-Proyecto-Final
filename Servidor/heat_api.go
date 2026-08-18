package main

import (
	"net/http"

	"github.com/gin-gonic/gin"
)

type heatSolveRequest struct {
	L       float64 `json:"l"`
	T       float64 `json:"t"`
	Alpha   float64 `json:"alpha"`
	Inicial string  `json:"inicial"`
}

type heatParametersResponse struct {
	L       float64 `json:"l"`
	T       float64 `json:"t"`
	Alpha   float64 `json:"alpha"`
	Inicial string  `json:"inicial"`
}

type heatSolveResponse struct {
	Sync       bool                   `json:"sync"`
	Ecuacion   string                 `json:"ecuacion"`
	Parametros heatParametersResponse `json:"parametros"`
	Result     waveGridResponse       `json:"result"`
}

// heatSolveUnityHandler resuelve el metodo numerico exclusivamente en el
// servidor y devuelve una grilla plana compatible con JsonUtility de Unity.
func heatSolveUnityHandler(c *gin.Context) {
	estado.ActualizarRequest(c.ClientIP())

	var req heatSolveRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Cuerpo de solicitud inválido"})
		return
	}

	syncActivo, syncParams := estado.EstadoSync()
	heatSync := syncActivo && syncParams.Ecuacion == "calor2d"
	if heatSync {
		req.L = syncParams.L
		req.T = syncParams.T
		req.Alpha = syncParams.C
		req.Inicial = syncParams.Inicial
	}

	if req.L < 1 || req.L > 20 || req.T < 1 || req.T > 100 || req.Alpha < 0.1 || req.Alpha > 2 {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros fuera de rango"})
		return
	}

	switch req.Inicial {
	case "gauss", "bordes", "placa":
	default:
		c.JSON(http.StatusBadRequest, gin.H{"error": "Condición inicial inválida"})
		return
	}

	frames := ecuacion_calor_2d(req.L, req.T, req.Alpha, req.Inicial)
	c.JSON(http.StatusOK, heatSolveResponse{
		Sync:     heatSync,
		Ecuacion: "calor2d",
		Parametros: heatParametersResponse{
			L: req.L, T: req.T, Alpha: req.Alpha, Inicial: req.Inicial,
		},
		Result: flattenWaveFrames(frames),
	})
}

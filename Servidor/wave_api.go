package main

import (
	"fmt"
	"math"
	"net/http"

	"github.com/gin-gonic/gin"
)

// waveSolveRequest es el contrato de entrada usado por la aplicación Unity.
type waveSolveRequest struct {
	L               float64   `json:"l"`
	T               float64   `json:"t"`
	C               float64   `json:"c"`
	Inicial         string    `json:"inicial"`
	ModoA           int       `json:"modoA"`
	ModoB           int       `json:"modoB"`
	Amortiguamiento bool      `json:"amortiguamiento"`
	Gamma           float64   `json:"gamma"`
	Excitacion      bool      `json:"excitacion"`
	FormaExcitacion string    `json:"formaExcitacion"`
	FrecExcitacion  float64   `json:"frecExcitacion"`
	InitialWidth    int       `json:"initialWidth"`
	InitialHeight   int       `json:"initialHeight"`
	InitialValues   []float64 `json:"initialValues"`
}

// waveParametersResponse informa los parámetros efectivamente usados. Pueden
// diferir de los solicitados si el profesor activó la sincronización.
type waveParametersResponse struct {
	L               float64 `json:"l"`
	T               float64 `json:"t"`
	C               float64 `json:"c"`
	Inicial         string  `json:"inicial"`
	ModoA           int     `json:"modoA"`
	ModoB           int     `json:"modoB"`
	Amortiguamiento bool    `json:"amortiguamiento"`
	Gamma           float64 `json:"gamma"`
	Excitacion      bool    `json:"excitacion"`
	FormaExcitacion string  `json:"formaExcitacion"`
	FrecExcitacion  float64 `json:"frecExcitacion"`
}

// waveGridResponse aplana los frames para que Unity pueda deserializarlos con
// JsonUtility. El valor (frame, x, y) está en:
// values[frame*width*height + x*height + y].
type waveGridResponse struct {
	Width      int       `json:"width"`
	Height     int       `json:"height"`
	FrameCount int       `json:"frameCount"`
	Values     []float32 `json:"values"`
}

// waveSolveResponse es la respuesta completa para el cliente Unity.
type waveSolveResponse struct {
	Sync       bool                   `json:"sync"`
	Ecuacion   string                 `json:"ecuacion"`
	Parametros waveParametersResponse `json:"parametros"`
	Result     waveGridResponse       `json:"result"`
}

// waveSolveUnityHandler calcula la onda íntegramente en el servidor y devuelve
// frames listos para visualizar. Unity no ejecuta ningún paso numérico.
func waveSolveUnityHandler(c *gin.Context) {
	estado.ActualizarRequest(c.ClientIP())

	var req waveSolveRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Cuerpo de solicitud inválido"})
		return
	}

	syncActivo, syncParams := estado.EstadoSync()
	waveSync := syncActivo && syncParams.Ecuacion == "onda2d"
	if waveSync {
		req.L = syncParams.L
		req.T = syncParams.T
		req.C = syncParams.C
		req.Inicial = syncParams.Inicial
		req.Amortiguamiento = syncParams.Amortiguamiento
		req.Gamma = syncParams.Gamma
		req.Excitacion = syncParams.Excitacion
		req.FormaExcitacion = syncParams.FormaExcitacion
		req.FrecExcitacion = syncParams.FrecExcitacion
		req.InitialWidth = 0
		req.InitialHeight = 0
		req.InitialValues = nil
	}

	if req.L < 1 || req.L > 10 || req.T < 1 || req.T > 100 || req.C < 0.1 || req.C > 2 {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Parámetros fuera de rango"})
		return
	}

	switch req.Inicial {
	case "seno", "triangular", "gauss":
		// Condición inicial válida.
	case "modo":
		if req.ModoA < 1 {
			req.ModoA = 1
		}
		if req.ModoA > 5 {
			req.ModoA = 5
		}
		if req.ModoB < 1 {
			req.ModoB = 1
		}
		if req.ModoB > 5 {
			req.ModoB = 5
		}
		req.Inicial = fmt.Sprintf("modo:%d:%d", req.ModoA, req.ModoB)
	case "personalizada":
		if req.InitialWidth < 2 || req.InitialWidth > 128 ||
			req.InitialHeight < 2 || req.InitialHeight > 128 ||
			len(req.InitialValues) != req.InitialWidth*req.InitialHeight {
			c.JSON(http.StatusBadRequest, gin.H{"error": "Grilla inicial personalizada invalida"})
			return
		}
		for _, value := range req.InitialValues {
			if math.IsNaN(value) || math.IsInf(value, 0) || value < -2 || value > 2 {
				c.JSON(http.StatusBadRequest, gin.H{"error": "Valores iniciales fuera de rango"})
				return
			}
		}
	default:
		if a, b, ok := parseWaveMode(req.Inicial); ok {
			req.ModoA = a
			req.ModoB = b
			req.Inicial = fmt.Sprintf("modo:%d:%d", a, b)
			break
		}
		c.JSON(http.StatusBadRequest, gin.H{"error": "Condición inicial inválida"})
		return
	}

	effectiveGamma := 0.0
	if req.Amortiguamiento && req.Gamma >= 0 && req.Gamma <= 1 {
		effectiveGamma = req.Gamma
	}

	exc := excitacionOpts{}
	if req.Excitacion && req.FrecExcitacion > 0 && req.FrecExcitacion <= 20 {
		forma := req.FormaExcitacion
		if forma == "" {
			forma = "seno"
		}
		exc = excitacionOpts{Activa: true, Forma: forma, Frecuencia: req.FrecExcitacion}
	}

	var frames [][][]float64
	if req.Inicial == "personalizada" {
		frames = ecuacion_onda_2d_personalizada(
			req.L, req.T, req.C, effectiveGamma, exc, req.InitialValues, req.InitialWidth, req.InitialHeight,
		)
	} else {
		frames = ecuacion_onda_2d(req.L, req.T, req.C, effectiveGamma, exc, req.Inicial)
	}
	result := flattenWaveFrames(frames)

	responseInitial := req.Inicial
	if _, _, ok := parseWaveMode(req.Inicial); ok {
		responseInitial = "modo"
	}

	c.JSON(http.StatusOK, waveSolveResponse{
		Sync:     waveSync,
		Ecuacion: "onda2d",
		Parametros: waveParametersResponse{
			L: req.L, T: req.T, C: req.C, Inicial: responseInitial,
			ModoA: req.ModoA, ModoB: req.ModoB,
			Amortiguamiento: effectiveGamma > 0, Gamma: effectiveGamma,
			Excitacion: exc.Activa, FormaExcitacion: exc.Forma, FrecExcitacion: exc.Frecuencia,
		},
		Result: result,
	})
}

func flattenWaveFrames(frames [][][]float64) waveGridResponse {
	if len(frames) == 0 || len(frames[0]) == 0 || len(frames[0][0]) == 0 {
		return waveGridResponse{Values: make([]float32, 0)}
	}

	width := len(frames[0])
	height := len(frames[0][0])
	values := make([]float32, 0, len(frames)*width*height)

	for _, frame := range frames {
		for x := 0; x < width; x++ {
			for y := 0; y < height; y++ {
				values = append(values, float32(frame[x][y]))
			}
		}
	}

	return waveGridResponse{
		Width:      width,
		Height:     height,
		FrameCount: len(frames),
		Values:     values,
	}
}

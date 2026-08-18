package main

import (
	"math"
	"strconv"
	"strings"
)

// copyGrid copia una grilla 2D.
func copyGrid(g [][]float64) [][]float64 {
	cp := make([][]float64, len(g))
	for i, row := range g {
		cp[i] = append([]float64{}, row...)
	}
	return cp
}

// newGrid crea una grilla 2D de n x n inicializada en cero.
func newGrid(n int) [][]float64 {
	g := make([][]float64, n)
	for i := range g {
		g[i] = make([]float64, n)
	}
	return g
}

// ecuacion_onda_2d resuelve la ecuación de onda 2D:
//
//	∂²u/∂t² = c² · (∂²u/∂x² + ∂²u/∂y²)
//
// Dominio cuadrado [0,L]×[0,L], Dirichlet homogéneas en todo el borde.
// Condición de velocidad inicial: ∂u/∂t|t=0 = 0.
// Esquema leapfrog explícito, CFL para 2D: c·dt/dx ≤ 1/√2.
// excitacionOpts agrupa los parámetros de la fuente forzada externa.
type excitacionOpts struct {
	Activa     bool
	Forma      string  // "seno"
	Frecuencia float64 // Hz
}

func parseWaveMode(initial string) (int, int, bool) {
	parts := strings.Split(initial, ":")
	if len(parts) != 3 || parts[0] != "modo" {
		return 1, 1, false
	}
	a, errA := strconv.Atoi(parts[1])
	b, errB := strconv.Atoi(parts[2])
	if errA != nil || errB != nil {
		return 1, 1, false
	}
	if a < 1 {
		a = 1
	}
	if a > 5 {
		a = 5
	}
	if b < 1 {
		b = 1
	}
	if b > 5 {
		b = 5
	}
	return a, b, true
}

func ecuacion_onda_2d(L, T, c, gamma float64, exc excitacionOpts, inicial string) [][][]float64 {
	return ecuacion_onda_2d_con_inicial(L, T, c, gamma, exc, inicial, nil, 0, 0)
}

// ecuacion_onda_2d_personalizada interpola la grilla dibujada en Unity a la
// resolucion numerica elegida por el servidor.
func ecuacion_onda_2d_personalizada(L, T, c, gamma float64, exc excitacionOpts, values []float64, width, height int) [][][]float64 {
	return ecuacion_onda_2d_con_inicial(L, T, c, gamma, exc, "personalizada", values, width, height)
}

func ecuacion_onda_2d_con_inicial(
	L, T, c, gamma float64,
	exc excitacionOpts,
	inicial string,
	customValues []float64,
	customWidth, customHeight int,
) [][][]float64 {
	n := int(math.Ceil(L * 8))
	if n < 16 {
		n = 16
	}
	d := L / float64(n)

	CFL := 0.9 / math.Sqrt2
	d_t := CFL * d / c
	n_t := int(math.Ceil(T / d_t))
	d_t = T / float64(n_t)
	r := (c * d_t / d) * (c * d_t / d)

	maxFrames := 1000
	step := 1
	if n_t > maxFrames {
		step = n_t / maxFrames
	}

	out := make([][][]float64, 0, maxFrames)

	pp := newGrid(n)  // t-2
	p := newGrid(n)   // t-1
	cur := newGrid(n) // t
	modeA, modeB, isMode := parseWaveMode(inicial)

	for i := 1; i < n-1; i++ {
		for j := 1; j < n-1; j++ {
			x := float64(i) * d
			y := float64(j) * d
			switch {
			case inicial == "personalizada":
				u := float64(i) / float64(n-1)
				v := float64(j) / float64(n-1)
				pp[i][j] = sampleWaveInitialGrid(customValues, customWidth, customHeight, u, v)
			case inicial == "gauss":
				pp[i][j] = math.Exp(-math.Pow((x-L/2)/(L/5), 2) - math.Pow((y-L/2)/(L/5), 2))
			case inicial == "triangular":
				tx := 1 - math.Abs(2*x/L-1)
				ty := 1 - math.Abs(2*y/L-1)
				pp[i][j] = tx * ty
			case isMode:
				pp[i][j] = math.Sin(float64(modeA)*math.Pi*x/L) *
					math.Sin(float64(modeB)*math.Pi*y/L)
			default: // seno
				pp[i][j] = math.Sin(math.Pi*x/L) * math.Sin(math.Pi*y/L)
			}
		}
	}
	out = append(out, copyGrid(pp))

	// Factores de amortiguamiento para el esquema leapfrog:
	// u^(n+1) = (2*u^n - u^(n-1)*(1-γ*dt) + r*∇²u^n + dt²*F) / (1+γ*dt)
	dampM := 1 - gamma*d_t
	dampD := 1 + gamma*d_t

	// Precalcular la grilla espacial de la excitación (independiente del tiempo).
	excShape := newGrid(n)
	if exc.Activa {
		for i := 1; i < n-1; i++ {
			for j := 1; j < n-1; j++ {
				x := float64(i) * d
				y := float64(j) * d
				// La fuerza adopta la forma espacial del modo seleccionado.
				excShape[i][j] = math.Sin(float64(modeA)*math.Pi*x/L) *
					math.Sin(float64(modeB)*math.Pi*y/L)
			}
		}
	}
	// A=0.25 en la PDE: a resonancia la amplitud crece ~0.3 por segundo sobre la condición
	// inicial; a 5×f₀ la respuesta estacionaria es <2% — contraste claramente distinguible.
	excAmp := 0.15 * d_t * d_t

	// primer paso temporal (velocidad inicial = 0)
	for i := 1; i < n-1; i++ {
		for j := 1; j < n-1; j++ {
			p[i][j] = pp[i][j] + 0.5*r*(pp[i+1][j]+pp[i-1][j]+pp[i][j+1]+pp[i][j-1]-4*pp[i][j])
		}
	}
	if step == 1 {
		out = append(out, copyGrid(p))
	}

	for t := 2; t < n_t; t++ {
		tCurrent := float64(t) * d_t
		forzado := 0.0
		if exc.Activa {
			forzado = math.Sin(2 * math.Pi * exc.Frecuencia * tCurrent)
		}
		for i := 1; i < n-1; i++ {
			for j := 1; j < n-1; j++ {
				cur[i][j] = (2*p[i][j] - dampM*pp[i][j] +
					r*(p[i+1][j]+p[i-1][j]+p[i][j+1]+p[i][j-1]-4*p[i][j]) +
					excAmp*forzado*excShape[i][j]) / dampD
			}
		}
		if t%step == 0 {
			out = append(out, copyGrid(cur))
		}
		pp, p, cur = p, cur, pp
	}

	return out
}

func sampleWaveInitialGrid(values []float64, width, height int, u, v float64) float64 {
	x := math.Max(0, math.Min(float64(width-1), u*float64(width-1)))
	y := math.Max(0, math.Min(float64(height-1), v*float64(height-1)))
	x0 := int(math.Floor(x))
	y0 := int(math.Floor(y))
	x1 := int(math.Min(float64(width-1), float64(x0+1)))
	y1 := int(math.Min(float64(height-1), float64(y0+1)))
	tx := x - float64(x0)
	ty := y - float64(y0)

	v00 := values[x0*height+y0]
	v10 := values[x1*height+y0]
	v01 := values[x0*height+y1]
	v11 := values[x1*height+y1]
	a := v00*(1-tx) + v10*tx
	b := v01*(1-tx) + v11*tx
	return a*(1-ty) + b*ty
}

// ecuacion_calor_2d resuelve la ecuación de calor 2D sobre una placa cuadrada:
//
//	∂u/∂t = α · (∂²u/∂x² + ∂²u/∂y²)
//
// Condiciones iniciales disponibles:
//   - "gauss"   : pulso gaussiano centrado, bordes fijos a 0
//   - "bordes"  : bordes calientes (u=1) fijos, interior inicialmente frío
//   - "placa"   : toda la placa caliente (u=1), bordes fijos a 0
//
// Esquema explícito FTCS, estable para α·dt/dx² ≤ 0.25.
func ecuacion_calor_2d(L, T, alpha float64, inicial string) [][][]float64 {

	// cantidad de puntos de malla espacial
	n := int(math.Ceil(L * 5))
	if n < 10 { // cantidad minima de puntos es 10
		n = 10
	}
	d := L / float64(n) // calcular el paso espacial

	r_max := 0.20                  // maximo valor de la condición de estabilidad
	d_t := r_max * d * d / alpha   // calculo el paso temporal
	n_t := int(math.Ceil(T / d_t)) // cantidad de pasos temporales
	d_t = T / float64(n_t)         // recalcular paso temporal para que encaje exactamente en T
	r := alpha * d_t / (d * d)     // calculo del r obtenido

	maxFrames := 1000 // cantidad maxima de frames a devolver.
	step := 1
	if n_t > maxFrames {
		step = n_t / maxFrames
	}

	out := make([][][]float64, 0, maxFrames)

	prev := newGrid(n)
	curr := newGrid(n)

	// Bordes calientes: BC=1 fijo (fuente de calor en los extremos), interior frío al inicio.
	// Gaussiana/seno: BC=0, calor concentrado en el interior.
	bcVal := 0.0
	if inicial == "bordes" {
		bcVal = 1.0
	}

	for i := 1; i < n-1; i++ {
		for j := 1; j < n-1; j++ {
			x := float64(i) * d
			y := float64(j) * d
			switch inicial {
			case "gauss":
				prev[i][j] = math.Exp(-(math.Pow((x-L/2)/(L/5), 2) + math.Pow((y-L/2)/(L/5), 2)))
			case "bordes":
				prev[i][j] = 0.0 // interior frío; el calor entra desde los extremos
			default:
				prev[i][j] = math.Sin(math.Pi*x/L) * math.Sin(math.Pi*y/L)
			}
		}
	}
	// Aplicar condición de contorno
	for k := 0; k < n; k++ {
		prev[k][0] = bcVal
		prev[k][n-1] = bcVal
		prev[0][k] = bcVal
		prev[n-1][k] = bcVal
	}
	out = append(out, copyGrid(prev))

	for t := 1; t < n_t; t++ {
		for i := 1; i < n-1; i++ {
			for j := 1; j < n-1; j++ {
				curr[i][j] = prev[i][j] + r*(prev[i+1][j]+prev[i-1][j]+prev[i][j+1]+prev[i][j-1]-4*prev[i][j])
			}
		}
		// Mantener contorno fijo
		for k := 0; k < n; k++ {
			curr[k][0] = bcVal
			curr[k][n-1] = bcVal
			curr[0][k] = bcVal
			curr[n-1][k] = bcVal
		}
		if t%step == 0 {
			out = append(out, copyGrid(curr))
		}
		prev, curr = curr, prev
	}

	return out
}

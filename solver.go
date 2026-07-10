package main

import (
	"math"
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
func ecuacion_onda_2d(L, T, c float64, inicial string) [][][]float64 {
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

	maxFrames := 200
	step := 1
	if n_t > maxFrames {
		step = n_t / maxFrames
	}

	out := make([][][]float64, 0, maxFrames)

	pp := newGrid(n) // t-2
	p := newGrid(n)  // t-1
	cur := newGrid(n) // t

	for i := 1; i < n-1; i++ {
		for j := 1; j < n-1; j++ {
			x := float64(i) * d
			y := float64(j) * d
			switch inicial {
			case "gauss":
				pp[i][j] = math.Exp(-math.Pow((x-L/2)/(L/5), 2) - math.Pow((y-L/2)/(L/5), 2))
			case "triangular":
				tx := 1 - math.Abs(2*x/L-1)
				ty := 1 - math.Abs(2*y/L-1)
				pp[i][j] = tx * ty
			default: // seno
				pp[i][j] = math.Sin(math.Pi*x/L) * math.Sin(math.Pi*y/L)
			}
		}
	}
	out = append(out, copyGrid(pp))

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
		for i := 1; i < n-1; i++ {
			for j := 1; j < n-1; j++ {
				cur[i][j] = 2*p[i][j] - pp[i][j] +
					r*(p[i+1][j]+p[i-1][j]+p[i][j+1]+p[i][j-1]-4*p[i][j])
			}
		}
		if t%step == 0 {
			out = append(out, copyGrid(cur))
		}
		pp, p, cur = p, cur, pp
	}

	return out
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
	n := int(math.Ceil(L * 5))
	if n < 10 {
		n = 10
	}
	d := L / float64(n)

	r_max := 0.20
	d_t := r_max * d * d / alpha
	n_t := int(math.Ceil(T / d_t))
	d_t = T / float64(n_t)
	r := alpha * d_t / (d * d)

	maxFrames := 200
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
		prev[k][0] = bcVal; prev[k][n-1] = bcVal
		prev[0][k] = bcVal; prev[n-1][k] = bcVal
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
			curr[k][0] = bcVal; curr[k][n-1] = bcVal
			curr[0][k] = bcVal; curr[n-1][k] = bcVal
		}
		if t%step == 0 {
			out = append(out, copyGrid(curr))
		}
		prev, curr = curr, prev
	}

	return out
}


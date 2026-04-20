package main

import (
	"math"
)

// ecuacion_onda resuelve la ecuación de onda 1D:
//
//	∂²u/∂t² = c² · ∂²u/∂x²
//
// mediante diferencias finitas explícitas (esquema leapfrog de segundo orden).
// Condiciones de borde: Dirichlet homogéneas — u(0,t) = u(L,t) = 0.
// Condición de velocidad inicial: ∂u/∂t|t=0 = 0 (cuerda en reposo).
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
			u[0][j] = math.Sin(math.Pi * float64(j) * d_x / L)
		}
	case "triangular":
		for j := 0; j < n_x; j++ {
			if float64(j)*d_x < L/2 {
				u[0][j] = (2 / L) * float64(j) * d_x
			} else {
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

	for i := 0; i < n_t; i++ {
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

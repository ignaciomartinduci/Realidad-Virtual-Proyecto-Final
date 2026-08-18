package main

import "testing"

func TestFlattenWaveFrames(t *testing.T) {
	frames := [][][]float64{
		{{1, 2}, {3, 4}},
		{{5, 6}, {7, 8}},
	}

	result := flattenWaveFrames(frames)
	if result.Width != 2 || result.Height != 2 || result.FrameCount != 2 {
		t.Fatalf("dimensiones inesperadas: %+v", result)
	}

	want := []float32{1, 2, 3, 4, 5, 6, 7, 8}
	if len(result.Values) != len(want) {
		t.Fatalf("cantidad de valores: got %d, want %d", len(result.Values), len(want))
	}
	for i := range want {
		if result.Values[i] != want[i] {
			t.Fatalf("values[%d]: got %v, want %v", i, result.Values[i], want[i])
		}
	}
}

func TestWaveSolverProducesUnityCompatibleGrid(t *testing.T) {
	frames := ecuacion_onda_2d(1, 1, 1, 0, excitacionOpts{}, "gauss")
	result := flattenWaveFrames(frames)

	if result.Width < 16 || result.Height != result.Width || result.FrameCount < 2 {
		t.Fatalf("grilla inesperada: %dx%d, frames=%d", result.Width, result.Height, result.FrameCount)
	}
	if len(result.Values) != result.Width*result.Height*result.FrameCount {
		t.Fatalf("payload inconsistente: %d valores", len(result.Values))
	}
}

func TestWaveSolverAcceptsCustomInitialGrid(t *testing.T) {
	initial := []float64{
		0, 0, 0,
		0, 1, 0,
		0, 0, 0,
	}
	frames := ecuacion_onda_2d_personalizada(1, 1, 1, 0, excitacionOpts{}, initial, 3, 3)
	if len(frames) < 2 {
		t.Fatalf("se esperaban varios cuadros, se recibieron %d", len(frames))
	}

	first := frames[0]
	center := len(first) / 2
	if first[center][center] <= 0.5 {
		t.Fatalf("la condicion personalizada no fue interpolada: centro=%v", first[center][center])
	}
	for index := range first {
		if first[index][0] != 0 || first[index][len(first)-1] != 0 ||
			first[0][index] != 0 || first[len(first)-1][index] != 0 {
			t.Fatal("los bordes personalizados deben permanecer fijos en cero")
		}
	}
}

func TestWaveSolverProducesSelectedMode(t *testing.T) {
	frames := ecuacion_onda_2d(1, 1, 1, 0, excitacionOpts{}, "modo:2:1")
	if len(frames) == 0 {
		t.Fatal("el solver no devolvio cuadros para el modo")
	}
	first := frames[0]
	quarter := len(first) / 4
	threeQuarters := 3 * len(first) / 4
	if first[quarter][quarter] <= 0 {
		t.Fatalf("el primer lobulo del modo (2,1) debe ser positivo: %v", first[quarter][quarter])
	}
	if first[threeQuarters][quarter] >= 0 {
		t.Fatalf("el segundo lobulo del modo (2,1) debe ser negativo: %v", first[threeQuarters][quarter])
	}
}

func TestParseWaveModeRejectsMalformedValues(t *testing.T) {
	if _, _, ok := parseWaveMode("modo:"); ok {
		t.Fatal("un modo incompleto no debe ser aceptado")
	}
	a, b, ok := parseWaveMode("modo:9:0")
	if !ok || a != 5 || b != 1 {
		t.Fatalf("limites inesperados para el modo: (%d,%d), ok=%v", a, b, ok)
	}
}

func TestHeatSolverProducesUnityCompatibleGrid(t *testing.T) {
	frames := ecuacion_calor_2d(1, 1, 0.5, "gauss")
	result := flattenWaveFrames(frames)
	if result.Width < 10 || result.Height != result.Width || result.FrameCount < 2 {
		t.Fatalf("grilla de calor inesperada: %dx%d, frames=%d", result.Width, result.Height, result.FrameCount)
	}
	if len(result.Values) != result.Width*result.Height*result.FrameCount {
		t.Fatalf("payload de calor inconsistente: %d valores", len(result.Values))
	}
}

func TestSyncVersionChangesOnSetAndClear(t *testing.T) {
	e := &estadoGlobal{clientes: make(map[string]*Cliente)}
	_, _, initialVersion := e.EstadoSyncVersionado()
	e.ActivarSync(parametrosSync{Ecuacion: "onda2d", L: 5, T: 3, C: 1, Inicial: "gauss"})
	active, parameters, setVersion := e.EstadoSyncVersionado()
	if !active || parameters.Ecuacion != "onda2d" || setVersion <= initialVersion {
		t.Fatalf("estado sincronizado inesperado: active=%v params=%+v version=%d", active, parameters, setVersion)
	}
	e.DesactivarSync()
	active, _, clearVersion := e.EstadoSyncVersionado()
	if active || clearVersion <= setVersion {
		t.Fatalf("la liberacion no incremento la version: active=%v version=%d", active, clearVersion)
	}
}

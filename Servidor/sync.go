package main

import (
	"sync"
	"time"
)

// Cliente representa un dispositivo conectado via app movil.
type Cliente struct {
	IP            string
	Legajo        string
	Nombre        string
	UltimoRequest time.Time
}

// parametrosSync contiene los parámetros de la ecuación que el profesor
// fuerza a todos los alumnos cuando la sincronización está activa.
type parametrosSync struct {
	Ecuacion        string
	L               float64
	T               float64
	C               float64
	Inicial         string
	Amortiguamiento bool
	Gamma           float64
	Excitacion      bool
	FormaExcitacion string
	FrecExcitacion  float64
}

// estadoGlobal centraliza todo el estado en memoria del servidor.
type estadoGlobal struct {
	mu       sync.RWMutex
	clientes map[string]*Cliente // clave: IP
	sync     struct {
		activo     bool
		parametros parametrosSync
		version    uint64
	}
}

// estado es la instancia única compartida por todos los handlers.
var estado = &estadoGlobal{
	clientes: make(map[string]*Cliente),
}

// RegistrarCliente agrega un cliente nuevo o actualiza su ultimo request
// si ya existia. Siempre conserva el rol que tenia asignado.
func (e *estadoGlobal) RegistrarCliente(ip, legajo, nombre string) {
	e.mu.Lock()
	defer e.mu.Unlock()

	if c, existe := e.clientes[ip]; existe {
		c.Legajo = legajo
		c.Nombre = nombre
		c.UltimoRequest = time.Now()
		return
	}

	e.clientes[ip] = &Cliente{
		IP:            ip,
		Legajo:        legajo,
		Nombre:        nombre,
		UltimoRequest: time.Now(),
	}
}

// ActualizarRequest actualiza el timestamp de ultimo request de un cliente
// ya registrado. Si el cliente no existe, no hace nada.
func (e *estadoGlobal) ActualizarRequest(ip string) {
	e.mu.Lock()
	defer e.mu.Unlock()

	if c, existe := e.clientes[ip]; existe {
		c.UltimoRequest = time.Now()
	}
}

// Clientes devuelve todos los clientes registrados desde que el servidor arranco.
func (e *estadoGlobal) Clientes() []*Cliente {
	e.mu.RLock()
	defer e.mu.RUnlock()

	lista := make([]*Cliente, 0, len(e.clientes))
	for _, c := range e.clientes {
		lista = append(lista, c)
	}
	return lista
}

// ActivarSync activa la sincronizacion con los parametros dados.
func (e *estadoGlobal) ActivarSync(p parametrosSync) {
	e.mu.Lock()
	defer e.mu.Unlock()

	e.sync.activo = true
	e.sync.parametros = p
	e.sync.version++
}

// DesactivarSync desactiva la sincronizacion.
func (e *estadoGlobal) DesactivarSync() {
	e.mu.Lock()
	defer e.mu.Unlock()

	e.sync.activo = false
	e.sync.parametros = parametrosSync{}
	e.sync.version++
}

// EstadoSyncVersionado permite a los clientes detectar un cambio sin volver a
// descargar una solucion numerica en cada consulta.
func (e *estadoGlobal) EstadoSyncVersionado() (bool, parametrosSync, uint64) {
	e.mu.RLock()
	defer e.mu.RUnlock()
	return e.sync.activo, e.sync.parametros, e.sync.version
}

// EstadoSync devuelve si la sincronizacion esta activa y los parametros actuales.
func (e *estadoGlobal) EstadoSync() (bool, parametrosSync) {
	e.mu.RLock()
	defer e.mu.RUnlock()

	return e.sync.activo, e.sync.parametros
}

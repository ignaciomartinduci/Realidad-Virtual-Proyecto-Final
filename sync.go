package main

import (
	"sync"
	"time"
)

// rol representa el nivel de acceso de un cliente en la app movil.
type rol int

const (
	rolAlumno    rol = iota
	rolAsistente rol = iota
)

// Cliente representa un dispositivo conectado via app movil.
type Cliente struct {
	IP            string
	Legajo        string
	Nombre        string
	Rol           rol
	UltimoRequest time.Time
}

// parametrosSync contiene los parámetros de la ecuación que el asistente
// fuerza a todos los alumnos cuando la sincronización está activa.
type parametrosSync struct {
	L       float64
	T       float64
	C       float64
	Inicial string
}

// estadoGlobal centraliza todo el estado en memoria del servidor.
type estadoGlobal struct {
	mu       sync.RWMutex
	clientes map[string]*Cliente // clave: IP
	sync     struct {
		activo     bool
		parametros parametrosSync
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
		c.UltimoRequest = time.Now()
		return
	}

	e.clientes[ip] = &Cliente{
		IP:            ip,
		Legajo:        legajo,
		Nombre:        nombre,
		Rol:           rolAlumno,
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

// BuscarCliente devuelve el cliente con la IP dada, o nil si no existe.
func (e *estadoGlobal) BuscarCliente(ip string) *Cliente {
	e.mu.RLock()
	defer e.mu.RUnlock()
	return e.clientes[ip]
}

// PromoverAsistente otorga el rol de asistente al cliente con la IP dada.
// Devuelve false si el cliente no existe o no esta activo.
func (e *estadoGlobal) PromoverAsistente(ip string) bool {
	e.mu.Lock()
	defer e.mu.Unlock()

	c, existe := e.clientes[ip]
	if !existe {
		return false
	}

	c.Rol = rolAsistente
	return true
}

// DemoverAsistente revoca el rol de asistente del cliente con la IP dada.
// Devuelve false si el cliente no existe.
func (e *estadoGlobal) DemoverAsistente(ip string) bool {
	e.mu.Lock()
	defer e.mu.Unlock()

	c, existe := e.clientes[ip]
	if !existe {
		return false
	}

	c.Rol = rolAlumno
	return true
}

// ActivarSync activa la sincronizacion con los parametros dados.
func (e *estadoGlobal) ActivarSync(p parametrosSync) {
	e.mu.Lock()
	defer e.mu.Unlock()

	e.sync.activo = true
	e.sync.parametros = p
}

// DesactivarSync desactiva la sincronizacion.
func (e *estadoGlobal) DesactivarSync() {
	e.mu.Lock()
	defer e.mu.Unlock()

	e.sync.activo = false
	e.sync.parametros = parametrosSync{}
}

// EstadoSync devuelve si la sincronizacion esta activa y los parametros actuales.
func (e *estadoGlobal) EstadoSync() (bool, parametrosSync) {
	e.mu.RLock()
	defer e.mu.RUnlock()

	return e.sync.activo, e.sync.parametros
}

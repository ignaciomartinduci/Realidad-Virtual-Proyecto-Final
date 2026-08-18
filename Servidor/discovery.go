package main

import (
	"encoding/json"
	"log"
	"net"
	"os"
)

const (
	discoveryPort    = 47777
	discoveryRequest = "RVPROYECTO_DISCOVER_V1"
)

type discoveryResponse struct {
	Service string `json:"service"`
	Name    string `json:"name"`
	Port    string `json:"port"`
}

// startDiscoveryResponder escucha consultas UDP de los telefonos en la red
// local. La IP no se incluye en el mensaje: el cliente utiliza la direccion
// real desde la que recibe la respuesta, evitando anunciar una interfaz falsa.
func startDiscoveryResponder(httpPort string) *net.UDPConn {
	address := &net.UDPAddr{IP: net.IPv4zero, Port: discoveryPort}
	connection, err := net.ListenUDP("udp4", address)
	if err != nil {
		log.Printf("Descubrimiento automatico no disponible: %v", err)
		return nil
	}

	hostname, err := os.Hostname()
	if err != nil || hostname == "" {
		hostname = "Servidor RV"
	}
	payload, _ := json.Marshal(discoveryResponse{
		Service: "RVPROYECTO",
		Name:    hostname,
		Port:    httpPort,
	})

	go func() {
		buffer := make([]byte, 512)
		for {
			n, remote, readErr := connection.ReadFromUDP(buffer)
			if readErr != nil {
				return
			}
			if string(buffer[:n]) != discoveryRequest {
				continue
			}
			if _, writeErr := connection.WriteToUDP(payload, remote); writeErr != nil {
				log.Printf("No se pudo responder al descubrimiento de %s: %v", remote, writeErr)
			}
		}
	}()

	log.Printf("Descubrimiento automatico activo en UDP %d", discoveryPort)
	return connection
}

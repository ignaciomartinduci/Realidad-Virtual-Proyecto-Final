//go:build windows

package main

import (
	"context"
	"os"
	"os/signal"
	"syscall"
	"time"
)

var kernel32 = syscall.NewLazyDLL("kernel32.dll")
var procSetConsoleCtrlHandler = kernel32.NewProc("SetConsoleCtrlHandler")

func init() {
	// Captura CTRL_CLOSE_EVENT (cerrar ventana CMD), CTRL_LOGOFF y CTRL_SHUTDOWN.
	cb := syscall.NewCallback(func(ctrlType uint32) uintptr {
		shutdownGin()
		return 0 // 0 = dejar que el handler por defecto termine el proceso
	})
	procSetConsoleCtrlHandler.Call(cb, 1)

	// Captura también Ctrl+C y SIGTERM (útil en modo headless --port).
	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, os.Interrupt, syscall.SIGTERM)
	go func() {
		<-sigCh
		shutdownGin()
	}()
}

func shutdownGin() {
	guiMu.Lock()
	srv := activeGinSrv
	guiMu.Unlock()
	if srv == nil {
		return
	}
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()
	srv.Shutdown(ctx)
}

package main

import (
	"context"
	"encoding/json"
	"fmt"
	"net"
	"net/http"
	"os/exec"
	"runtime"
	"sync"
	"time"
)

// estado del servidor gin gestionado por la GUI
var (
	guiMu         sync.Mutex
	activeGinSrv  *http.Server
	serverRunning bool
)

// runLauncherGUI abre la página de configuración en el navegador y nunca retorna.
// Gestiona el ciclo de vida completo: lanzar / detener / relanzar.
func runLauncherGUI() {
	mux := http.NewServeMux()
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "text/html; charset=utf-8")
		fmt.Fprint(w, launcherHTML(localIPs()))
	})
	mux.HandleFunc("/launch", handleLaunch)
	mux.HandleFunc("/stop", handleStop)
	mux.HandleFunc("/status", handleStatus)

	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		// fallback sin GUI
		startGinServer("8080")
		return
	}
	guiAddr := fmt.Sprintf("http://127.0.0.1:%d", ln.Addr().(*net.TCPAddr).Port)
	go (&http.Server{Handler: mux}).Serve(ln)
	openBrowser(guiAddr)

	// Bloquear indefinidamente; la vida del proceso la controla la GUI / el CMD.
	select {}
}

func handleLaunch(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body struct {
		Port string `json:"port"`
	}
	json.NewDecoder(r.Body).Decode(&body)
	if body.Port == "" {
		body.Port = "8080"
	}

	guiMu.Lock()
	already := serverRunning
	guiMu.Unlock()

	w.Header().Set("Content-Type", "application/json")
	if already {
		json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": "ya corriendo"})
		return
	}

	go startGinServer(body.Port)
	json.NewEncoder(w).Encode(map[string]interface{}{"ok": true})
}

func handleStop(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	guiMu.Lock()
	srv := activeGinSrv
	guiMu.Unlock()

	if srv != nil {
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		srv.Shutdown(ctx)
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(map[string]interface{}{"ok": true})
}

func handleStatus(w http.ResponseWriter, r *http.Request) {
	guiMu.Lock()
	running := serverRunning
	guiMu.Unlock()
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(map[string]interface{}{"running": running})
}

// startGinServer arranca el servidor gin en el puerto dado.
// Bloquea hasta que el servidor sea detenido (Shutdown).
func startGinServer(port string) {
	router := setupRouter()
	discovery := startDiscoveryResponder(port)
	if discovery != nil {
		defer discovery.Close()
	}
	srv := &http.Server{
		Addr:    ":" + port,
		Handler: router,
	}

	guiMu.Lock()
	activeGinSrv = srv
	serverRunning = true
	guiMu.Unlock()

	srv.ListenAndServe() // retorna cuando Shutdown es llamado

	guiMu.Lock()
	activeGinSrv = nil
	serverRunning = false
	guiMu.Unlock()
}

// ── helpers ────────────────────────────────────────────────────────────────

func localIPs() []string {
	var ips []string
	ifaces, err := net.Interfaces()
	if err != nil {
		return ips
	}
	for _, iface := range ifaces {
		if iface.Flags&net.FlagUp == 0 || iface.Flags&net.FlagLoopback != 0 {
			continue
		}
		addrs, err := iface.Addrs()
		if err != nil {
			continue
		}
		for _, addr := range addrs {
			var ip net.IP
			switch v := addr.(type) {
			case *net.IPNet:
				ip = v.IP
			case *net.IPAddr:
				ip = v.IP
			}
			if ip == nil || ip.To4() == nil {
				continue
			}
			ips = append(ips, ip.String())
		}
	}
	return ips
}

func openBrowser(url string) {
	var cmd *exec.Cmd
	switch runtime.GOOS {
	case "windows":
		cmd = exec.Command("cmd", "/c", "start", url)
	case "darwin":
		cmd = exec.Command("open", url)
	default:
		cmd = exec.Command("xdg-open", url)
	}
	cmd.Start()
}

func launcherHTML(ips []string) string {
	ipItems := ""
	for _, ip := range ips {
		ipItems += fmt.Sprintf(`<div class="ip-chip">%s</div>`, ip)
	}
	if ipItems == "" {
		ipItems = `<div class="ip-chip muted">No se detectaron IPs locales</div>`
	}

	return `<!doctype html>
<html lang="es">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Entorno de Simulación — Lanzador</title>
<style>
  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
  :root {
    --bg:       #0f1117;
    --surface:  #1a1d27;
    --surface2: #22263a;
    --border:   #2e3250;
    --accent:   #4f7df7;
    --accent-h: #3b69e8;
    --red:      #f87171;
    --red-h:    #ef4444;
    --green:    #34d399;
    --text:     #e2e6f0;
    --muted:    #7b82a0;
    --radius:   12px;
  }
  body {
    min-height: 100vh;
    display: flex; align-items: center; justify-content: center;
    background: var(--bg);
    color: var(--text);
    font-family: 'Segoe UI', system-ui, sans-serif;
    font-size: 15px;
  }
  .card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: var(--radius);
    padding: 40px 44px;
    width: 100%; max-width: 460px;
    box-shadow: 0 24px 60px rgba(0,0,0,.5);
  }
  .logo { display: flex; align-items: center; gap: 14px; margin-bottom: 28px; }
  .logo-icon {
    width: 44px; height: 44px;
    background: var(--accent); border-radius: 10px;
    display: flex; align-items: center; justify-content: center;
  }
  .logo-text h1 { font-size: 1.15rem; font-weight: 700; letter-spacing: -.01em; }
  .logo-text p  { font-size: 0.78rem; color: var(--muted); margin-top: 2px; }
  hr { border: none; border-top: 1px solid var(--border); margin: 24px 0; }
  .section-label {
    font-size: 0.72rem; font-weight: 600;
    letter-spacing: .08em; text-transform: uppercase;
    color: var(--muted); margin-bottom: 10px;
  }
  .ip-list { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 24px; }
  .ip-chip {
    background: var(--surface2); border: 1px solid var(--border);
    border-radius: 6px; padding: 5px 12px;
    font-size: 0.88rem;
    font-family: 'Cascadia Code','Consolas',monospace;
    color: var(--green);
  }
  .ip-chip.muted { color: var(--muted); }
  .field label { display: block; font-size: 0.82rem; font-weight: 500; color: var(--muted); margin-bottom: 7px; }
  .field input {
    width: 100%;
    background: var(--surface2); border: 1px solid var(--border);
    border-radius: 8px; padding: 10px 14px;
    font-size: 1rem;
    font-family: 'Cascadia Code','Consolas',monospace;
    color: var(--text); outline: none; transition: border-color .15s;
  }
  .field input:focus { border-color: var(--accent); }
  .hint { font-size: 0.76rem; color: var(--muted); margin-top: 6px; }
  .btn {
    margin-top: 16px; width: 100%; padding: 13px;
    color: #fff; border: none; border-radius: 8px;
    font-size: 0.95rem; font-weight: 600; cursor: pointer;
    display: flex; align-items: center; justify-content: center; gap: 8px;
    transition: background .15s, transform .1s; letter-spacing: .01em;
  }
  .btn:active:not(:disabled) { transform: scale(.98); }
  .btn:disabled { opacity: .5; cursor: default; }
  .btn-launch { background: var(--accent); }
  .btn-launch:hover:not(:disabled) { background: var(--accent-h); }
  .btn-stop   { background: var(--red); }
  .btn-stop:hover:not(:disabled)   { background: var(--red-h); }

  /* estado corriendo */
  .status-bar {
    display: none;
    margin-top: 20px;
    padding: 10px 14px;
    border-radius: 8px;
    border: 1px solid var(--border);
    background: var(--surface2);
    align-items: center; gap: 10px;
  }
  .status-bar.visible { display: flex; }
  .dot {
    width: 9px; height: 9px; border-radius: 50%; flex-shrink: 0;
  }
  .dot.green { background: var(--green); animation: pulse 1.4s ease-in-out infinite; }
  .dot.gray  { background: var(--muted); }
  .status-text { font-size: 0.85rem; font-weight: 600; }
  .status-text.green { color: var(--green); }
  .status-text.gray  { color: var(--muted); }

  .urls { margin-top: 14px; display: flex; flex-direction: column; gap: 6px; }
  .urls a {
    color: var(--accent);
    font-family: monospace; font-size: 0.87rem;
    text-decoration: none;
    background: var(--surface2); border: 1px solid var(--border);
    border-radius: 6px; padding: 6px 12px;
    transition: border-color .15s;
  }
  .urls a:hover { border-color: var(--accent); }

  @keyframes pulse {
    0%,100% { box-shadow: 0 0 0 0 rgba(52,211,153,.6); }
    50%      { box-shadow: 0 0 0 6px rgba(52,211,153,0); }
  }
</style>
</head>
<body>
<div class="card">
  <div class="logo">
    <div class="logo-icon">
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M12 2L2 7l10 5 10-5-10-5z"/><path d="M2 17l10 5 10-5"/><path d="M2 12l10 5 10-5"/>
      </svg>
    </div>
    <div class="logo-text">
      <h1>Entorno de Simulación</h1>
      <p>Servidor de ecuaciones diferenciales</p>
    </div>
  </div>

  <div class="section-label">Direcciones IP detectadas</div>
  <div class="ip-list">` + ipItems + `</div>

  <hr>

  <div class="field">
    <label for="port">Puerto del servidor</label>
    <input id="port" type="number" value="8080" min="1024" max="65535" placeholder="8080">
    <p class="hint">Los alumnos y Unity se conectarán a esta IP:puerto desde la misma red.</p>
  </div>

  <button class="btn btn-launch" id="btnLaunch" onclick="launch()">
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><polygon points="5 3 19 12 5 21 5 3"/></svg>
    Lanzar servidor
  </button>

  <button class="btn btn-stop" id="btnStop" style="display:none" onclick="stop()">
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2"/></svg>
    Detener servidor
  </button>

  <div class="status-bar" id="statusBar">
    <div class="dot green" id="statusDot"></div>
    <span class="status-text green" id="statusText">Servidor activo</span>
  </div>

  <div class="urls" id="urlList" style="display:none"></div>
</div>

<script>
  var currentPort = '8080';
  var pollTimer   = null;

  function setRunning(port) {
    currentPort = port;
    document.getElementById('btnLaunch').style.display = 'none';
    document.getElementById('port').closest('.field').style.display = 'none';
    document.getElementById('btnStop').style.display  = 'flex';
    document.getElementById('statusBar').classList.add('visible');
    document.getElementById('statusDot').className  = 'dot green';
    document.getElementById('statusText').className = 'status-text green';
    document.getElementById('statusText').textContent = 'Servidor activo';

    var ips = Array.from(document.querySelectorAll('.ip-chip:not(.muted)')).map(function(el){ return el.textContent; });
    if (!ips.length) ips = ['localhost'];
    var urlList = document.getElementById('urlList');
    urlList.innerHTML = '';
    urlList.style.display = 'flex';
    ips.forEach(function(ip) {
      var a = document.createElement('a');
      a.href = 'http://' + ip + ':' + port;
      a.target = '_blank';
      a.textContent = 'http://' + ip + ':' + port;
      urlList.appendChild(a);
    });

    pollTimer = setInterval(pollStatus, 2000);
  }

  function setStopped() {
    clearInterval(pollTimer);
    document.getElementById('statusDot').className  = 'dot gray';
    document.getElementById('statusText').className = 'status-text gray';
    document.getElementById('statusText').textContent = 'Servidor detenido';

    var btnStop = document.getElementById('btnStop');
    btnStop.style.display = 'none';
    btnStop.disabled = false;
    btnStop.innerHTML =
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2"/></svg> Detener servidor';

    document.getElementById('urlList').style.display = 'none';
    document.getElementById('btnLaunch').style.display = 'flex';
    document.getElementById('btnLaunch').disabled = false;
    document.getElementById('port').closest('.field').style.display = 'block';
    document.getElementById('btnLaunch').innerHTML =
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><polygon points="5 3 19 12 5 21 5 3"/></svg> Lanzar servidor';
  }

  async function pollStatus() {
    try {
      var r = await fetch('/status');
      var d = await r.json();
      if (!d.running) setStopped();
    } catch(_) {}
  }

  async function launch() {
    var port = document.getElementById('port').value || '8080';
    var btn  = document.getElementById('btnLaunch');
    btn.disabled = true;
    btn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> Iniciando…';
    try {
      await fetch('/launch', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ port: port })
      });
    } catch(_) {}
    await new Promise(function(r){ setTimeout(r, 900); });
    setRunning(port);
  }

  async function stop() {
    var btn = document.getElementById('btnStop');
    btn.disabled = true;
    btn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> Deteniendo…';
    try {
      await fetch('/stop', { method: 'POST' });
    } catch(_) {}
    await new Promise(function(r){ setTimeout(r, 600); });
    setStopped();
  }
</script>
</body>
</html>`
}

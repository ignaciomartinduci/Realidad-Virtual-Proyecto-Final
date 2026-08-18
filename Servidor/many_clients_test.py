"""
Stress test: múltiples clientes simulados contra el servidor Go.
Requiere: pip install requests rich
"""

import threading
import time
import random
import requests
from datetime import datetime
from rich.console import Console, Group
from rich.table import Table
from rich.live import Live
from rich.panel import Panel
from rich.text import Text
from rich import box

BASE_URL = "http://localhost:8080"
NUM_CLIENTS = 40
REQUEST_INTERVAL_MIN = 2.0   # segundos mínimo entre requests por cliente
REQUEST_INTERVAL_MAX = 8.0   # segundos máximo

NOMBRES = [
    ("100001", "Ana García"),
    ("100002", "Bruno Méndez"),
    ("100003", "Carla Ibáñez"),
    ("100004", "Diego Torres"),
    ("100005", "Elena Ruiz"),
    ("100006", "Franco López"),
    ("100007", "Gisela Molina"),
    ("100008", "Hernán Díaz"),
    ("100009", "Ivana Castro"),
    ("100010", "Julián Romero"),
    ("100011", "Karen Suárez"),
    ("100012", "Lucas Fernández"),
    ("100013", "Martina Aguirre"),
    ("100014", "Nicolás Herrera"),
    ("100015", "Olivia Peralta"),
    ("100016", "Pablo Soria"),
    ("100017", "Quimey Vera"),
    ("100018", "Rodrigo Acosta"),
    ("100019", "Sofía Benítez"),
    ("100020", "Tomás Cabrera"),
    ("100021", "Valentina Daza"),
    ("100022", "Walter Espinoza"),
    ("100023", "Ximena Figueroa"),
    ("100024", "Yamil Godoy"),
    ("100025", "Zoe Huerta"),
    ("100026", "Agustín Ibarra"),
    ("100027", "Belén Juárez"),
    ("100028", "Camilo Keller"),
    ("100029", "Delfina Luna"),
    ("100030", "Emiliano Mazo"),
    ("100031", "Florencia Noriega"),
    ("100032", "Gastón Ortega"),
    ("100033", "Hana Ponce"),
    ("100034", "Ignacio Quiroga"),
    ("100035", "Jimena Rivas"),
    ("100036", "Kevin Salinas"),
    ("100037", "Luciana Tapia"),
    ("100038", "Maximiliano Urquiza"),
    ("100039", "Natalia Vargas"),
    ("100040", "Omar Zanetti"),
]

SOLVE_REQUESTS = [
    {"ecuacion": "onda2d",  "L": 3,  "T": 50, "c": 0.5, "inicial": "seno"},
    {"ecuacion": "onda2d",  "L": 5,  "T": 60, "c": 1.0, "inicial": "gauss"},
    {"ecuacion": "onda2d",  "L": 4,  "T": 75, "c": 1.5, "inicial": "triangular"},
    {"ecuacion": "onda2d",  "L": 8,  "T": 55, "c": 0.8, "inicial": "seno"},
    {"ecuacion": "onda2d",  "L": 2,  "T": 90, "c": 2.0, "inicial": "gauss"},
    {"ecuacion": "onda2d",  "L": 5,  "T": 80, "c": 1.0, "inicial": "triangular"},
    {"ecuacion": "onda2d",  "L": 7,  "T": 65, "c": 1.2, "inicial": "seno"},
    {"ecuacion": "onda2d",  "L": 10, "T": 100,"c": 1.2, "inicial": "triangular"},
    {"ecuacion": "onda2d",  "L": 6,  "T": 70, "c": 0.9, "inicial": "gauss"},
    {"ecuacion": "onda2d",  "L": 9,  "T": 85, "c": 0.7, "inicial": "seno"},
]


class ClientState:
    def __init__(self, legajo, nombre, client_id):
        self.legajo         = legajo
        self.nombre         = nombre
        self.simulated_ip   = f"192.168.1.{client_id}"
        self.registered     = False
        self.register_error = None
        self.requests_ok    = 0
        self.requests_err   = 0
        self.requests_429   = 0
        self.last_request   = "—"
        self.last_status    = "—"
        self.last_ecuacion  = "—"
        self.lock           = threading.Lock()


def headers(client: ClientState) -> dict:
    return {"X-Forwarded-For": client.simulated_ip}


def register(client: ClientState):
    try:
        r = requests.post(
            f"{BASE_URL}/register",
            json={"legajo": client.legajo, "nombre": client.nombre},
            headers=headers(client),
            timeout=5,
        )
        with client.lock:
            if r.status_code == 200:
                client.registered = True
            else:
                client.register_error = f"HTTP {r.status_code}"
    except Exception as e:
        with client.lock:
            client.register_error = str(e)[:30]


def do_solve(client: ClientState):
    params = random.choice(SOLVE_REQUESTS)
    try:
        r = requests.get(
            f"{BASE_URL}/solve",
            params=params,
            headers=headers(client),
            timeout=30,
        )
        now = datetime.now().strftime("%H:%M:%S")
        with client.lock:
            client.last_request  = now
            client.last_ecuacion = params["ecuacion"]
            if r.status_code == 200:
                client.requests_ok  += 1
                client.last_status   = "200 OK"
            elif r.status_code == 429:
                client.requests_429 += 1
                client.last_status   = "429 rate limit"
            else:
                client.requests_err += 1
                client.last_status   = f"{r.status_code} error"
    except Exception as e:  
        with client.lock:
            client.requests_err += 1
            client.last_status   = str(e)[:20]
            client.last_request  = datetime.now().strftime("%H:%M:%S")


def client_loop(client: ClientState, stop_event: threading.Event):
    register(client)
    if not client.registered:
        return

    while not stop_event.is_set():
        do_solve(client)
        wait = random.uniform(REQUEST_INTERVAL_MIN, REQUEST_INTERVAL_MAX)
        stop_event.wait(wait)


def make_half_table(clients: list[ClientState]) -> Table:
    table = Table(
        box=box.SIMPLE_HEAVY,
        show_header=True,
        header_style="bold white",
        expand=False,
    )
    table.add_column("#",         style="dim",      width=4)
    table.add_column("Nombre",    style="bold",     width=16)
    table.add_column("R",         justify="center", width=3)
    table.add_column("OK",        justify="right",  style="green",  width=4)
    table.add_column("429",       justify="right",  style="yellow", width=4)
    table.add_column("ERR",       justify="right",  style="red",    width=4)
    table.add_column("Estado",    width=14)
    table.add_column("Ec.",       width=8)

    for c in clients:
        with c.lock:
            reg_text = Text("✓", style="green") if c.registered else \
                       Text("✗", style="red")   if c.register_error else \
                       Text("…", style="dim")
            status_style = "green"  if "200" in c.last_status else \
                           "yellow" if "429" in c.last_status else \
                           "red"
            table.add_row(
                c.legajo[-3:],
                c.nombre[:16],
                reg_text,
                str(c.requests_ok),
                str(c.requests_429),
                str(c.requests_err),
                Text(c.last_status, style=status_style),
                c.last_ecuacion[:8],
            )
    return table


def build_layout(clients, elapsed):
    total_ok  = sum(c.requests_ok  for c in clients)
    total_429 = sum(c.requests_429 for c in clients)
    total_err = sum(c.requests_err for c in clients)
    total_req = total_ok + total_429 + total_err
    rps = total_req / elapsed if elapsed > 0 else 0

    mid = len(clients) // 2
    left  = make_half_table(clients[:mid])
    right = make_half_table(clients[mid:])

    stats = (
        f"[bold cyan]Stress test — {BASE_URL}[/bold cyan]   "
        f"clientes: [bold]{NUM_CLIENTS}[/bold]   "
        f"tiempo: [bold]{elapsed:.0f}s[/bold]   "
        f"req/s: [bold]{rps:.2f}[/bold]   "
        f"[green]OK {total_ok}[/green]  "
        f"[yellow]429 {total_429}[/yellow]  "
        f"[red]ERR {total_err}[/red]   "
        f"[dim]Ctrl+C para detener[/dim]"
    )

    # Tabla contenedora con dos columnas: izquierda y derecha
    outer = Table.grid(padding=(0, 2))
    outer.add_column()
    outer.add_column()
    outer.add_row(left, right)

    return Panel(
        Group(Text.from_markup(stats), outer),
        border_style="cyan",
    )


def main():
    console = Console()
    clients = [ClientState(leg, nom, i + 1) for i, (leg, nom) in enumerate(NOMBRES[:NUM_CLIENTS])]
    stop_event = threading.Event()
    threads = []

    for c in clients:
        t = threading.Thread(target=client_loop, args=(c, stop_event), daemon=True)
        t.start()
        threads.append(t)

    start = time.time()
    try:
        with Live(console=console, refresh_per_second=2, screen=True) as live:
            while True:
                elapsed = time.time() - start
                live.update(build_layout(clients, elapsed))
                time.sleep(0.5)
    except KeyboardInterrupt:
        pass
    finally:
        stop_event.set()
        console.print("\n[bold yellow]Deteniendo clientes...[/bold yellow]")
        for t in threads:
            t.join(timeout=3)

        elapsed = time.time() - start
        console.print(build_layout(clients, elapsed))

        total_ok  = sum(c.requests_ok  for c in clients)
        total_429 = sum(c.requests_429 for c in clients)
        total_err = sum(c.requests_err for c in clients)
        console.print(
            f"\n[bold]Resumen final:[/bold] "
            f"[green]{total_ok} OK[/green]  "
            f"[yellow]{total_429} rate-limited[/yellow]  "
            f"[red]{total_err} errores[/red]  "
            f"en {elapsed:.1f}s"
        )


if __name__ == "__main__":
    main()

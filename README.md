# Simulación interactiva de ecuaciones diferenciales en Unity

Este proyecto fue desarrollado como una herramienta didáctica para la cátedra de
Matemática Avanzada. Permite visualizar en un teléfono Android la evolución de la
ecuación de onda 2D y de la ecuación de calor 2D, utilizando una aplicación creada en
Unity y un servidor implementado en Go.

Los métodos numéricos se ejecutan íntegramente en el servidor. La aplicación envía los
parámetros del modelo, recibe las grillas temporales calculadas y las representa como
una placa tridimensional animada. El teléfono funciona además como dispositivo IHM:
la orientación de la cámara se controla mediante sus sensores y la interacción se
realiza desde la pantalla táctil.

## Funciones principales

- Simulación de la ecuación de onda 2D y de la ecuación de calor 2D.
- Cálculo numérico centralizado en el servidor Go.
- Modificación de parámetros físicos desde la aplicación.
- Edición de la condición inicial de la onda mediante sus nodos.
- Representación mediante malla, nodos y escala de colores.
- Modo de pantalla plana y modo estereoscópico para Google Cardboard.
- Detección automática del servidor en la red local mediante UDP.
- Conexión manual por dirección IP como alternativa.
- Panel docente para consultar alumnos conectados y sincronizar un mismo modelo,
  parámetros y estado entre todos los teléfonos.

## Estructura del proyecto

```text
Repositorio/
├── AplicacionUnity/          Proyecto de Unity para Android
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
├── Servidor/                 Servidor Go, panel web y solvers numéricos
├── APK/
│   └── RV_aplicacion.apk     Aplicación Android lista para instalar
├── Documentacion/
│   ├── Informe_Final.pdf     Informe completo del proyecto
│   └── Fuentes_LaTeX/        Archivos editables del informe
└── Videos/                   Demostraciones del servidor, panel y aplicación
```

## Requisitos

- Computadora con Windows, Linux o macOS.
- Go 1.25.1.
- Unity 6000.4.2f1 con Android Build Support, SDK, NDK y OpenJDK, solamente si se
  desea abrir o volver a compilar el proyecto de Unity.
- Teléfono con Android 8.0, API 26, o superior.
- Computadora y teléfonos conectados a la misma red Wi-Fi.
- Puertos TCP 8080 y UDP 47777 habilitados en la red local.

## Puesta en funcionamiento

### 1. Ejecutar el servidor

Abrir una terminal dentro de `Servidor` y ejecutar:

```bash
go mod download
go run .
```

El lanzador permite iniciar el servicio en el puerto 8080 y muestra las direcciones
IPv4 disponibles. Si el firewall solicita autorización, debe permitirse el acceso en
redes privadas.

También puede iniciarse directamente, sin el lanzador gráfico:

```bash
go run . -port 8080
```

### 2. Abrir el panel docente

Con el servidor activo, abrir en la computadora:

```text
http://localhost:8080
```

El código de acceso docente se encuentra configurado en `Servidor/main.go`. Desde el
panel se pueden consultar los estudiantes registrados, ejecutar modelos y activar o
desactivar la sincronización de los clientes.

### 3. Instalar y utilizar la aplicación Android

1. Copiar `APK/RV_aplicacion.apk` al teléfono.
2. Habilitar temporalmente la instalación desde esa fuente e instalar el APK.
3. Conectar el teléfono a la misma red Wi-Fi que la computadora.
4. Abrir la aplicación, ingresar nombre y legajo, y pulsar **Conectar**.
5. Esperar la detección automática del servidor. Si no se encuentra, ingresar
   manualmente una de las direcciones mostradas por el lanzador, por ejemplo
   `http://192.168.1.20:8080`.

En el teléfono no debe utilizarse `localhost`, porque esa dirección identifica al
propio dispositivo Android y no a la computadora donde se ejecuta el servidor.

### 4. Abrir o compilar el proyecto de Unity

1. Abrir Unity Hub y seleccionar **Add project from disk**.
2. Elegir la carpeta `AplicacionUnity`.
3. Esperar la importación de paquetes y la compilación de scripts.
4. Abrir **File > Build Profiles**, seleccionar Android y utilizar **Switch Platform**
   si fuera necesario.
5. Verificar el orden de las escenas: `MenuScene`, `HelloCardboard` y `HeatPlate`.
6. Pulsar **Build** para generar un nuevo APK.

Unity reconstruirá automáticamente las carpetas `Library`, `Temp`, `Logs` y demás
archivos locales que no se incluyen en el repositorio.

## Videos demostrativos

- [`INF_RV_CODIGO_SERVER.mp4`](Videos/INF_RV_CODIGO_SERVER.mp4): estructura y
  funcionamiento del servidor y de los métodos numéricos.
- [`INF_RV_PANEL.mp4`](Videos/INF_RV_PANEL.mp4): utilización del panel docente.
- [`INF_RV_APK_CELULAR.mp4`](Videos/INF_RV_APK_CELULAR.mp4): instalación y uso de la
  aplicación en el teléfono.

## Documentación

El funcionamiento completo, la arquitectura, los algoritmos numéricos, la comunicación
cliente-servidor y el procedimiento de instalación se describen en
[`Documentacion/Informe_Final.pdf`](Documentacion/Informe_Final.pdf).

## Archivos grandes y GitHub

Los videos y el APK están configurados para utilizar Git LFS. Antes de agregar los
archivos al repositorio por primera vez, instalar Git LFS y ejecutar:

```bash
git lfs install
git add .
git commit -m "Carga inicial del proyecto"
```

Esta configuración es necesaria porque dos de los videos superan el límite de tamaño
admitido por los archivos comunes de GitHub.

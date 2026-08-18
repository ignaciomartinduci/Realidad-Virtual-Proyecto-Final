using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;



public class MenuController : MonoBehaviour
{
    private const int DiscoveryPort = 47777;
    private const string DiscoveryRequest = "RVPROYECTO_DISCOVER_V1";

    [Serializable]
    private class DiscoveryResponse
    {
        public string service;
        public string name;
        public string port;
    }

    public TMP_InputField inputNombre;
    public TMP_InputField inputIP;
    public TMP_InputField inputLegajo;
    public TMP_Text textoError;

    private bool conexionEnCurso;
    private ResponsiveMenuUI responsiveView;

    public void Start()
    {
        responsiveView = ResponsiveMenuUI.Build(this);
        if (inputNombre == null || inputLegajo == null || inputIP == null)
        {
            Debug.LogError("No se pudo construir la interfaz adaptable del menu.");
            return;
        }

        // El campo queda disponible solo como alternativa para redes que
        // bloqueen el broadcast entre dispositivos.
        inputIP.text = string.Empty;
    }

    public void Ingresar()
    {
        if (conexionEnCurso)
            return;

        string servidor = inputIP.text.Trim().TrimEnd('/');
        string nombre = inputNombre.text.Trim();
        string legajo = inputLegajo.text.Trim();

        if (string.IsNullOrWhiteSpace(nombre) ||
            string.IsNullOrWhiteSpace(legajo))
        {
            MostrarMensaje("Completá el nombre y el legajo.", true);
            return;
        }

        conexionEnCurso = true;
        responsiveView?.SetBusy(true);
        if (string.IsNullOrWhiteSpace(servidor))
        {
            MostrarMensaje("Buscando servidor en la red local...");
            StartCoroutine(DescubrirYRegistrar(nombre, legajo));
            return;
        }

        servidor = NormalizarServidor(servidor);
        MostrarMensaje("Conectando...");
        StartCoroutine(RegistrarUsuario(servidor, nombre, legajo));
    }

    private IEnumerator DescubrirYRegistrar(string nombre, string legajo)
    {
        Task<string> discoveryTask = DescubrirServidor(4500);
        while (!discoveryTask.IsCompleted)
            yield return null;

        string servidor = null;
        if (discoveryTask.Status == TaskStatus.RanToCompletion)
            servidor = discoveryTask.Result;

        if (string.IsNullOrEmpty(servidor))
        {
            conexionEnCurso = false;
            responsiveView?.SetBusy(false);
            MostrarMensaje("No se encontró el servidor. Verificá la red o ingresá la dirección manualmente.", true);
            yield break;
        }

        inputIP.text = servidor;
        MostrarMensaje("Servidor encontrado. Conectando...", false, true);
        yield return RegistrarUsuario(servidor, nombre, legajo);
    }

    private static async Task<string> DescubrirServidor(int timeoutMilliseconds)
    {
        try
        {
            using (UdpClient client = new UdpClient(0))
            {
                client.EnableBroadcast = true;
                byte[] probe = Encoding.UTF8.GetBytes(DiscoveryRequest);
                await client.SendAsync(probe, probe.Length,
                    new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
                while (DateTime.UtcNow < deadline)
                {
                    int remaining = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                    Task<UdpReceiveResult> receiveTask = client.ReceiveAsync();
                    Task completed = await Task.WhenAny(receiveTask, Task.Delay(remaining));
                    if (completed != receiveTask)
                        return null;

                    UdpReceiveResult packet = receiveTask.Result;
                    DiscoveryResponse response = JsonUtility.FromJson<DiscoveryResponse>(
                        Encoding.UTF8.GetString(packet.Buffer));
                    if (response == null || response.service != "RVPROYECTO" ||
                        !int.TryParse(response.port, out int port) || port < 1 || port > 65535)
                        continue;

                    return $"http://{packet.RemoteEndPoint.Address}:{port}";
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("No se pudo descubrir el servidor: " + exception.Message);
        }

        return null;
    }

    IEnumerator RegistrarUsuario(string servidor, string nombre, string legajo)
    {
        servidor = NormalizarServidor(servidor);
        PlayerPrefs.SetString("Nombre", nombre);
        PlayerPrefs.SetString("IP", servidor);
        PlayerPrefs.SetString("Legajo", legajo);
        PlayerPrefs.Save();

        RegisterRequest datos = new RegisterRequest();

        datos.nombre = nombre;
        datos.legajo = legajo;

        string json = JsonUtility.ToJson(datos);

        Debug.Log("JSON enviado:");
        Debug.Log(json);

        string url = servidor + "/register";

        UnityWebRequest request = new UnityWebRequest(url, "POST");

        byte[] body = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        string detalleError = request.responseCode > 0
            ? $"HTTP {request.responseCode}: {request.error}"
            : request.error;

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Respuesta del servidor:");
            Debug.Log(request.downloadHandler.text);

            conexionEnCurso = false;
            SceneManager.LoadScene("HelloCardboard");
        }
        else
        {
            Debug.LogError($"Error al conectar con {url}: {detalleError}");
            MostrarMensaje($"No se pudo conectar: {detalleError}", true);
            conexionEnCurso = false;
            responsiveView?.SetBusy(false);
        }

        request.Dispose();
    }

    private static string NormalizarServidor(string servidor)
    {
        servidor = (servidor ?? string.Empty).Trim().TrimEnd('/');
        if (!servidor.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !servidor.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            servidor = "http://" + servidor;
        return servidor;
    }

    private void MostrarMensaje(string mensaje, bool esError = false, bool esExito = false)
    {
        responsiveView?.SetStatus(mensaje, esError, esExito);
        if (textoError != null)
        {
            textoError.text = mensaje;
            textoError.color = esError
                ? new Color(1f, 0.37f, 0.42f)
                : esExito ? new Color(0.14f, 0.84f, 0.54f) : Color.white;
        }
    }
}

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class HeatSimulationRequest
{
    public float l;
    public float t;
    public float alpha;
    public string inicial;
}

[Serializable]
public class HeatSimulationParameters
{
    public float l;
    public float t;
    public float alpha;
    public string inicial;
}

[Serializable]
public class HeatGridResult
{
    public int width;
    public int height;
    public int frameCount;
    public float[] values;
}

[Serializable]
public class HeatSimulationResponse
{
    public bool sync;
    public string ecuacion;
    public HeatSimulationParameters parametros;
    public HeatGridResult result;
}

public class HeatPlate2D : MonoBehaviour
{
    [Header("Visualizacion")]
    public Material heatMaterial;
    [SerializeField, Min(1f)] private float playbackFramesPerSecond = 30f;
    [SerializeField] private bool loopPlayback = true;
    [SerializeField, Range(0f, 0.5f)] private float temperatureHeight = 0.32f;

    [Header("Malla visual")]
    [SerializeField, Range(2, 20)] private int gridSubdivisions = 10;
    [SerializeField, Range(0.25f, 2f)] private float gridLineWidth = 0.75f;
    [SerializeField, Range(0f, 1f)] private float gridStrength = 0.62f;

    [Header("Parametros enviados al servidor")]
    [SerializeField, Range(1f, 20f)] private float domainLength = 10f;
    [SerializeField, Range(1f, 100f)] private float simulationDuration = 10f;
    [SerializeField, Range(0.1f, 2f)] private float diffusivity = 0.5f;
    [SerializeField] private string initialCondition = "gauss";

    [Header("Comunicacion")]
    [SerializeField, Min(5f)] private float minimumRequestInterval = 5.2f;

    public event Action<string, bool> StatusChanged;
    public float DomainLength => domainLength;
    public float SimulationDuration => simulationDuration;
    public float Diffusivity => diffusivity;
    public string InitialCondition => initialCondition;

    private HeatGridResult simulation;
    private Texture2D colorTexture;
    private Texture2D displacementTexture;
    private Color32[] colorPixels;
    private Color32[] displacementPixels;
    private int currentFrame;
    private float playbackAccumulator;
    private bool simulationReady;
    private string serverUrl;
    private Coroutine requestProcessor;
    private bool hasQueuedRequest;
    private HeatSimulationRequest queuedRequest;
    private float lastRequestStartedAt = float.NegativeInfinity;

    private void Start()
    {
        SimulationSyncCoordinator.EnsureExists();
        HeatParameterPanel panel = GetComponent<HeatParameterPanel>();
        if (panel == null)
            panel = gameObject.AddComponent<HeatParameterPanel>();
        panel.Initialize(this);

        if (heatMaterial == null)
        {
            SetStatus("No hay un material asignado para mostrar la placa termica.", true);
            return;
        }

        heatMaterial.SetFloat("_Cull", 0f);
        serverUrl = NormalizeServerUrl(PlayerPrefs.GetString("IP", string.Empty));
        if (string.IsNullOrEmpty(serverUrl))
        {
            SetStatus("No se encontro la direccion del servidor.", true);
            return;
        }

        QueueSimulation();
    }

    public void RefreshFromServerSync()
    {
        if (!string.IsNullOrEmpty(serverUrl))
        {
            SetStatus("Sincronizando con el modelo del profesor...", false);
            QueueSimulation();
        }
    }

    public bool TryUpdateParameters(float length, float duration, float alpha,
        string condition, out string error)
    {
        condition = (condition ?? string.Empty).Trim().ToLowerInvariant();
        if (length < 1f || length > 20f)
        {
            error = "L debe estar entre 1 y 20.";
            return false;
        }
        if (duration < 1f || duration > 100f)
        {
            error = "T debe estar entre 1 y 100 segundos.";
            return false;
        }
        if (alpha < 0.1f || alpha > 2f)
        {
            error = "La difusividad alpha debe estar entre 0,1 y 2.";
            return false;
        }
        if (condition != "gauss" && condition != "bordes" && condition != "placa")
        {
            error = "La condicion inicial no es valida.";
            return false;
        }
        if (string.IsNullOrEmpty(serverUrl))
        {
            error = "No hay una direccion de servidor configurada.";
            return false;
        }

        domainLength = length;
        simulationDuration = duration;
        diffusivity = alpha;
        initialCondition = condition;
        QueueSimulation();
        error = string.Empty;
        return true;
    }

    private void QueueSimulation()
    {
        queuedRequest = new HeatSimulationRequest
        {
            l = domainLength,
            t = simulationDuration,
            alpha = diffusivity,
            inicial = initialCondition
        };
        hasQueuedRequest = true;
        if (requestProcessor == null)
            requestProcessor = StartCoroutine(ProcessQueuedRequests());
        else
            SetStatus("Cambio guardado. Se enviara cuando el servidor este disponible.", false);
    }

    private IEnumerator ProcessQueuedRequests()
    {
        while (hasQueuedRequest)
        {
            HeatSimulationRequest requestData = queuedRequest;
            hasQueuedRequest = false;
            float wait = minimumRequestInterval - (Time.realtimeSinceStartup - lastRequestStartedAt);
            if (wait > 0f)
            {
                SetStatus($"Esperando {Mathf.CeilToInt(wait)} s para actualizar...", false);
                yield return new WaitForSecondsRealtime(wait);
                if (hasQueuedRequest)
                {
                    requestData = queuedRequest;
                    hasQueuedRequest = false;
                }
            }
            lastRequestStartedAt = Time.realtimeSinceStartup;
            yield return RequestSimulation(requestData);
        }
        requestProcessor = null;
    }

    private IEnumerator RequestSimulation(HeatSimulationRequest requestData)
    {
        string url = serverUrl + "/solve/heat2d";
        string json = JsonUtility.ToJson(requestData);
        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 60;
            SetStatus("Calculando la solucion termica en el servidor...", false);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 429)
                {
                    queuedRequest = requestData;
                    hasQueuedRequest = true;
                    SetStatus("El servidor esta ocupado. La actualizacion se reintentara automaticamente.", false);
                    yield break;
                }
                SetStatus($"No se pudo actualizar el calor ({request.responseCode}): {request.error}", true);
                yield break;
            }

            HeatSimulationResponse response;
            try
            {
                response = JsonUtility.FromJson<HeatSimulationResponse>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                Debug.LogError("Respuesta termica invalida: " + exception.Message);
                SetStatus("El servidor devolvio una solucion termica invalida.", true);
                yield break;
            }

            if (!InitializePlayback(response))
            {
                SetStatus("La solucion termica recibida esta incompleta.", true);
                yield break;
            }

            if (response.parametros != null)
            {
                domainLength = response.parametros.l;
                simulationDuration = response.parametros.t;
                diffusivity = response.parametros.alpha;
                initialCondition = response.parametros.inicial;
            }

            string syncText = response.sync ? " (parametros sincronizados por el profesor)" : string.Empty;
            SetStatus($"Calor actualizado: {simulation.frameCount} cuadros{syncText}.", false);
        }
    }

    private bool InitializePlayback(HeatSimulationResponse response)
    {
        if (response == null || response.result == null || response.result.values == null)
            return false;
        HeatGridResult result = response.result;
        long expected = (long)result.width * result.height * result.frameCount;
        if (result.width < 2 || result.height < 2 || result.frameCount < 1 ||
            expected != result.values.LongLength)
            return false;

        if (colorTexture != null)
            Destroy(colorTexture);
        if (displacementTexture != null)
            Destroy(displacementTexture);

        simulation = result;
        colorTexture = CreateTexture(result.width, result.height);
        displacementTexture = CreateTexture(result.width, result.height);
        colorPixels = new Color32[result.width * result.height];
        displacementPixels = new Color32[result.width * result.height];
        heatMaterial.SetTexture("_MainTex", colorTexture);
        heatMaterial.SetTexture("_Displacement", displacementTexture);
        currentFrame = 0;
        playbackAccumulator = 0f;
        simulationReady = true;
        DisplayFrame(0);
        return true;
    }

    private static Texture2D CreateTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private void Update()
    {
        if (!simulationReady || simulation.frameCount <= 1)
            return;
        playbackAccumulator += Time.deltaTime;
        float frameDuration = 1f / playbackFramesPerSecond;
        while (playbackAccumulator >= frameDuration)
        {
            playbackAccumulator -= frameDuration;
            int next = currentFrame + 1;
            if (next >= simulation.frameCount)
            {
                if (!loopPlayback)
                    return;
                next = 0;
            }
            DisplayFrame(next);
        }
    }

    private void DisplayFrame(int frameIndex)
    {
        int count = simulation.width * simulation.height;
        int offset = frameIndex * count;
        for (int index = 0; index < count; index++)
        {
            float temperature = Mathf.Clamp01(simulation.values[offset + index]);
            float encodedHeight = Mathf.Clamp01(0.5f + temperature * temperatureHeight);
            byte heightChannel = (byte)Mathf.RoundToInt(encodedHeight * 255f);
            displacementPixels[index] = new Color32(heightChannel, heightChannel, heightChannel, 255);
            Color heatColor = HeatColor(temperature);
            if (IsGridPixel(index))
                heatColor = Color.Lerp(heatColor, Color.white, gridStrength);
            colorPixels[index] = heatColor;
        }
        colorTexture.SetPixels32(colorPixels);
        colorTexture.Apply(false, false);
        displacementTexture.SetPixels32(displacementPixels);
        displacementTexture.Apply(false, false);
        currentFrame = frameIndex;
    }

    private static Color HeatColor(float value)
    {
        if (value < 0.25f)
            return Color.Lerp(new Color(0.08f, 0.10f, 0.14f), new Color(1f, 0.95f, 0f), value / 0.25f);
        if (value < 0.55f)
            return Color.Lerp(new Color(1f, 0.95f, 0f), new Color(1f, 0.35f, 0f), (value - 0.25f) / 0.30f);
        return Color.Lerp(new Color(1f, 0.35f, 0f), new Color(1f, 0f, 0f), (value - 0.55f) / 0.45f);
    }

    private bool IsGridPixel(int pixelIndex)
    {
        int x = pixelIndex / simulation.height;
        int y = pixelIndex % simulation.height;
        float sx = x * gridSubdivisions / (simulation.width - 1f);
        float sy = y * gridSubdivisions / (simulation.height - 1f);
        float tx = gridLineWidth * gridSubdivisions / (simulation.width - 1f);
        float ty = gridLineWidth * gridSubdivisions / (simulation.height - 1f);
        return Mathf.Abs(sx - Mathf.Round(sx)) <= tx || Mathf.Abs(sy - Mathf.Round(sy)) <= ty;
    }

    private void SetStatus(string message, bool error)
    {
        StatusChanged?.Invoke(message, error);
        if (error)
            Debug.LogError("HeatPlate2D: " + message);
    }

    private static string NormalizeServerUrl(string value)
    {
        string server = (value ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(server))
            return string.Empty;
        if (!server.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            server = "http://" + server;
        return server;
    }

    private void OnDestroy()
    {
        if (colorTexture != null)
            Destroy(colorTexture);
        if (displacementTexture != null)
            Destroy(displacementTexture);
    }
}

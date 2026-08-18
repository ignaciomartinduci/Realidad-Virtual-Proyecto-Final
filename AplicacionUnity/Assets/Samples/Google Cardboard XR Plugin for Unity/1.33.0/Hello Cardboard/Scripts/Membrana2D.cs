using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

[Serializable]
public class WaveSimulationRequest
{
    public float l;
    public float t;
    public float c;
    public string inicial;
    public int modoA;
    public int modoB;
    public bool amortiguamiento;
    public float gamma;
    public bool excitacion;
    public string formaExcitacion;
    public float frecExcitacion;
    public int initialWidth;
    public int initialHeight;
    public float[] initialValues;
}

[Serializable]
public class WaveSimulationParameters
{
    public float l;
    public float t;
    public float c;
    public string inicial;
    public int modoA;
    public int modoB;
    public bool amortiguamiento;
    public float gamma;
    public bool excitacion;
    public string formaExcitacion;
    public float frecExcitacion;
}

[Serializable]
public class WaveGridResult
{
    public int width;
    public int height;
    public int frameCount;
    public float[] values;
}

[Serializable]
public class WaveSimulationResponse
{
    public bool sync;
    public string ecuacion;
    public WaveSimulationParameters parametros;
    public WaveGridResult result;
}

public class Membrana2D : MonoBehaviour
{
    [Header("Visualizacion")]
    public Material waveMaterial;
    public Texture2D waveTexture;
    [SerializeField] private float floatToColorMultiplier = 0.45f;
    [SerializeField, Min(1f)] private float playbackFramesPerSecond = 30f;
    [SerializeField] private bool loopPlayback = true;

    [Header("Malla visual de la placa")]
    [SerializeField, Range(2, 20)] private int surfaceGridSubdivisions = 10;
    [SerializeField, Range(0.25f, 2f)] private float surfaceGridLineWidth = 0.75f;
    [SerializeField, Range(0f, 1f)] private float surfaceGridStrength = 0.58f;
    [SerializeField] private Color surfaceGridColor = new Color(0.86f, 0.96f, 1f, 1f);

    [Header("Nodos visibles")]
    [SerializeField, Range(0.02f, 0.2f)] private float nodePointSize = 0.085f;

    [Header("Edicion de condicion inicial")]
    [SerializeField, Range(0.005f, 0.05f)] private float editHeightPerDegree = 0.022f;
    [SerializeField, Range(0.04f, 0.3f)] private float editBrushRadius = 0.12f;
    [SerializeField, Range(0.05f, 0.4f)] private float nodeSelectionRadius = 0.16f;

    [Header("Parametros enviados al servidor")]
    [SerializeField, Range(1f, 10f)] private float domainLength = 10f;
    [SerializeField, Range(1f, 100f)] private float simulationDuration = 10f;
    [SerializeField, Range(0.1f, 2f)] private float waveSpeed = 1f;
    [SerializeField] private string initialCondition = "gauss";
    [SerializeField, Range(1, 5)] private int modeA = 1;
    [SerializeField, Range(1, 5)] private int modeB = 1;
    [SerializeField] private bool dampingEnabled;
    [SerializeField, Range(0f, 1f)] private float dampingGamma = 0.5f;
    [SerializeField] private bool excitationEnabled;
    [SerializeField, Range(0.1f, 10f)] private float excitationFrequencyMultiplier = 1f;

    [Header("Comunicacion")]
    [Tooltip("El servidor acepta como maximo una peticion cada cinco segundos por dispositivo.")]
    [SerializeField, Min(5f)] private float minimumRequestInterval = 5.2f;

    public event Action<string, bool> StatusChanged;
    public event Action<bool, string> InitialConditionEditChanged;

    public float DomainLength => domainLength;
    public float SimulationDuration => simulationDuration;
    public float WaveSpeed => waveSpeed;
    public string InitialCondition => initialCondition;
    public int ModeA => modeA;
    public int ModeB => modeB;
    public bool DampingEnabled => dampingEnabled;
    public float DampingGamma => dampingGamma;
    public bool ExcitationEnabled => excitationEnabled;
    public float ExcitationFrequencyMultiplier => excitationFrequencyMultiplier;
    public float NaturalFrequency => GetNaturalFrequency(
        domainLength, waveSpeed,
        initialCondition == "modo" ? modeA : 1,
        initialCondition == "modo" ? modeB : 1);
    public float ExcitationFrequency => excitationFrequencyMultiplier * NaturalFrequency;
    public bool IsEditingInitialCondition => editingInitialCondition;

    private WaveGridResult simulation;
    private Color32[] framePixels;
    private Color32[] displacementPixels;
    private int currentFrame;
    private float playbackAccumulator;
    private bool simulationReady;
    private string serverUrl;
    private ParticleSystem nodeParticleSystem;
    private ParticleSystem.Particle[] nodeParticles;
    private Vector3[] nodeBasePositions;
    private Vector2[] nodeUvs;
    private Material nodeMaterial;
    private Texture2D nodeTexture;
    private Texture2D displacementTexture;

    private Coroutine requestProcessor;
    private bool hasQueuedRequest;
    private WaveSimulationRequest queuedRequest;
    private float lastRequestStartedAt = float.NegativeInfinity;
    private bool playbackPaused;
    private bool editingInitialCondition;
    private bool waitingForCustomSimulation;
    private float[] editableInitialValues;
    private float[] selectionBaseValues;
    private int selectedNode = -1;
    private int hoveredNode = -1;
    private float selectionStartElevation;
    private float selectionStartDisplacement;
    private float lastEditedDisplacement = float.NaN;
    private Camera editCamera;

    private void Start()
    {
        SimulationSyncCoordinator.EnsureExists();
        WaveParameterPanel panel = GetComponent<WaveParameterPanel>();
        if (panel == null)
            panel = gameObject.AddComponent<WaveParameterPanel>();

        panel.Initialize(this);

        if (waveMaterial == null)
        {
            SetStatus("No hay un material asignado para mostrar la onda.", true);
            return;
        }

        // La placa debe verse tanto desde arriba como desde abajo.
        waveMaterial.SetFloat("_Cull", 0f);

        serverUrl = NormalizeServerUrl(PlayerPrefs.GetString("IP", string.Empty));
        if (string.IsNullOrEmpty(serverUrl))
        {
            SetStatus("No se encontro la direccion del servidor.", true);
            return;
        }

        QueueSimulation(domainLength, simulationDuration, waveSpeed, initialCondition);
    }

    public void RefreshFromServerSync()
    {
        if (!string.IsNullOrEmpty(serverUrl))
        {
            SetStatus("Sincronizando con el modelo del profesor...", false);
            QueueSimulation(domainLength, simulationDuration, waveSpeed, initialCondition);
        }
    }

    public bool TryUpdateParameters(
        float length,
        float duration,
        float speed,
        string condition,
        int selectedModeA,
        int selectedModeB,
        bool useDamping,
        float gamma,
        bool useExcitation,
        float frequencyMultiplier,
        out string error)
    {
        condition = NormalizeInitialCondition(condition);

        if (length < 1f || length > 10f)
        {
            error = "L debe estar entre 1 y 10.";
            return false;
        }

        if (duration < 1f || duration > 100f)
        {
            error = "T debe estar entre 1 y 100 segundos.";
            return false;
        }

        if (speed < 0.1f || speed > 2f)
        {
            error = "c debe estar entre 0,1 y 2.";
            return false;
        }

        if (condition != "gauss" && condition != "seno" &&
            condition != "triangular" && condition != "modo")
        {
            error = "La condicion inicial no es valida.";
            return false;
        }

        if (selectedModeA < 1 || selectedModeA > 5 || selectedModeB < 1 || selectedModeB > 5)
        {
            error = "Los indices del modo a y b deben estar entre 1 y 5.";
            return false;
        }

        if (gamma < 0f || gamma > 1f)
        {
            error = "El amortiguamiento gamma debe estar entre 0 y 1.";
            return false;
        }

        if (frequencyMultiplier < 0.1f || frequencyMultiplier > 10f)
        {
            error = "El multiplo de frecuencia debe estar entre 0,1 y 10.";
            return false;
        }

        int frequencyModeA = condition == "modo" ? selectedModeA : 1;
        int frequencyModeB = condition == "modo" ? selectedModeB : 1;
        float naturalFrequency = GetNaturalFrequency(
            length, speed, frequencyModeA, frequencyModeB);
        if (useExcitation && frequencyMultiplier * naturalFrequency > 20f)
        {
            error = "La frecuencia excitadora no puede superar 20 Hz.";
            return false;
        }

        if (string.IsNullOrEmpty(serverUrl))
        {
            error = "No hay una direccion de servidor configurada.";
            return false;
        }

        domainLength = length;
        simulationDuration = duration;
        waveSpeed = speed;
        initialCondition = condition;
        modeA = selectedModeA;
        modeB = selectedModeB;
        dampingEnabled = useDamping;
        dampingGamma = gamma;
        excitationEnabled = useExcitation;
        excitationFrequencyMultiplier = frequencyMultiplier;
        QueueSimulation(length, duration, speed, condition);

        error = string.Empty;
        return true;
    }

    public bool BeginInitialConditionEdit(out string error)
    {
        if (!simulationReady || simulation == null || waveTexture == null || nodeBasePositions == null)
        {
            error = "Todavia no hay una onda disponible para editar.";
            return false;
        }

        if (waitingForCustomSimulation)
        {
            error = "Espera a que el servidor termine la solucion anterior.";
            return false;
        }

        int valuesPerFrame = simulation.width * simulation.height;
        editableInitialValues = new float[valuesPerFrame];
        Array.Copy(simulation.values, currentFrame * valuesPerFrame, editableInitialValues, 0, valuesPerFrame);

        playbackPaused = true;
        editingInitialCondition = true;
        selectedNode = -1;
        hoveredNode = -1;
        editCamera = Camera.main;
        UploadEditableInitialCondition();
        InitialConditionEditChanged?.Invoke(true,
            "Apunta a un nodo blanco y toca la pantalla. Inclina el telefono para subirlo o bajarlo; toca otra vez para fijarlo.");
        error = string.Empty;
        return true;
    }

    public void CancelInitialConditionEdit()
    {
        if (!editingInitialCondition)
            return;

        editingInitialCondition = false;
        playbackPaused = false;
        selectedNode = -1;
        hoveredNode = -1;
        editableInitialValues = null;
        selectionBaseValues = null;
        DisplayFrame(currentFrame);
        InitialConditionEditChanged?.Invoke(false, string.Empty);
        SetStatus("Edicion cancelada. La simulacion continua.", false);
    }

    public bool SubmitInitialConditionEdit(out string error)
    {
        if (!editingInitialCondition || editableInitialValues == null)
        {
            error = "No hay una condicion inicial en edicion.";
            return false;
        }

        queuedRequest = new WaveSimulationRequest
        {
            l = domainLength,
            t = simulationDuration,
            c = waveSpeed,
            inicial = "personalizada",
            modoA = modeA,
            modoB = modeB,
            amortiguamiento = dampingEnabled,
            gamma = dampingEnabled ? dampingGamma : 0f,
            excitacion = excitationEnabled,
            formaExcitacion = excitationEnabled ? "seno" : string.Empty,
            frecExcitacion = excitationEnabled ? ExcitationFrequency : 0f,
            initialWidth = simulation.width,
            initialHeight = simulation.height,
            initialValues = (float[])editableInitialValues.Clone()
        };
        hasQueuedRequest = true;
        editingInitialCondition = false;
        waitingForCustomSimulation = true;
        selectedNode = -1;
        hoveredNode = -1;
        InitialConditionEditChanged?.Invoke(false, string.Empty);

        if (requestProcessor == null)
            requestProcessor = StartCoroutine(ProcessQueuedRequests());
        else
            SetStatus("Condicion personalizada guardada. Se enviara cuando el servidor este disponible.", false);

        error = string.Empty;
        return true;
    }

    private void QueueSimulation(float length, float duration, float speed, string condition)
    {
        queuedRequest = new WaveSimulationRequest
        {
            l = length,
            t = duration,
            c = speed,
            inicial = NormalizeInitialCondition(condition),
            modoA = modeA,
            modoB = modeB,
            amortiguamiento = dampingEnabled,
            gamma = dampingEnabled ? dampingGamma : 0f,
            excitacion = excitationEnabled,
            formaExcitacion = excitationEnabled ? "seno" : string.Empty,
            frecExcitacion = excitationEnabled ? ExcitationFrequency : 0f
        };
        hasQueuedRequest = true;

        if (requestProcessor == null)
            requestProcessor = StartCoroutine(ProcessQueuedRequests());
        else
            SetStatus("Cambio guardado. Se enviara en cuanto el servidor este disponible.", false);
    }

    private IEnumerator ProcessQueuedRequests()
    {
        while (hasQueuedRequest)
        {
            WaveSimulationRequest requestData = queuedRequest;
            hasQueuedRequest = false;

            float waitSeconds = minimumRequestInterval - (Time.realtimeSinceStartup - lastRequestStartedAt);
            if (waitSeconds > 0f)
            {
                SetStatus($"Esperando {Mathf.CeilToInt(waitSeconds)} s para actualizar...", false);
                yield return new WaitForSecondsRealtime(waitSeconds);

                // Mientras se esperaba pudo llegar un cambio mas nuevo.
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

    private IEnumerator RequestSimulation(WaveSimulationRequest requestData)
    {
        string json = JsonUtility.ToJson(requestData);
        string url = serverUrl + "/solve/wave2d";

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 60;

            SetStatus("Calculando la nueva solucion en el servidor...", false);
            Debug.Log(requestData.inicial == "personalizada"
                ? $"Solicitando onda personalizada ({requestData.initialWidth}x{requestData.initialHeight}) al servidor."
                : $"Solicitando simulacion de onda al servidor: {json}");
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
                string detail = request.responseCode == 429
                    ? "El servidor esta ocupado. El cambio quedo pendiente; volve a intentarlo en unos segundos."
                    : $"No se pudo actualizar la onda ({request.responseCode}): {request.error}";
                SetStatus(detail, true);
                Debug.LogError($"{detail}\n{request.downloadHandler.text}");
                RestoreCustomEditAfterFailure(requestData, detail);
                yield break;
            }

            WaveSimulationResponse response;
            try
            {
                response = JsonUtility.FromJson<WaveSimulationResponse>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                SetStatus("El servidor devolvio una respuesta que no se pudo interpretar.", true);
                Debug.LogError($"Respuesta de simulacion invalida: {exception.Message}");
                RestoreCustomEditAfterFailure(requestData,
                    "El servidor devolvio una respuesta que no se pudo interpretar.");
                yield break;
            }

            if (!TryInitializePlayback(response))
            {
                SetStatus("La solucion recibida esta incompleta.", true);
                RestoreCustomEditAfterFailure(requestData, "La solucion recibida esta incompleta.");
                yield break;
            }

            if (response.parametros != null)
            {
                domainLength = response.parametros.l;
                simulationDuration = response.parametros.t;
                waveSpeed = response.parametros.c;
                if (response.parametros.inicial != "personalizada")
                    initialCondition = NormalizeInitialCondition(response.parametros.inicial);
                if (initialCondition == "modo")
                {
                    modeA = Mathf.Clamp(response.parametros.modoA, 1, 5);
                    modeB = Mathf.Clamp(response.parametros.modoB, 1, 5);
                }
                dampingEnabled = response.parametros.amortiguamiento;
                dampingGamma = response.parametros.gamma;
                excitationEnabled = response.parametros.excitacion;
                float naturalFrequency = NaturalFrequency;
                if (excitationEnabled && naturalFrequency > 0.0001f)
                    excitationFrequencyMultiplier = response.parametros.frecExcitacion / naturalFrequency;
            }

            waitingForCustomSimulation = false;
            playbackPaused = false;
            editableInitialValues = null;
            selectionBaseValues = null;

            string syncText = response.sync ? " (parametros sincronizados por el profesor)" : string.Empty;
            SetStatus($"Onda actualizada: {simulation.frameCount} cuadros{syncText}.", false);
            Debug.Log($"Simulacion recibida: {simulation.frameCount} frames de {simulation.width}x{simulation.height}.");
        }
    }

    private void RestoreCustomEditAfterFailure(WaveSimulationRequest requestData, string detail)
    {
        if (requestData.inicial != "personalizada")
            return;

        waitingForCustomSimulation = false;
        editingInitialCondition = true;
        playbackPaused = true;
        InitialConditionEditChanged?.Invoke(true,
            detail + " La forma editada se conservo; podes volver a enviarla o cancelarla.");
    }

    private bool TryInitializePlayback(WaveSimulationResponse response)
    {
        if (response == null || response.result == null || response.result.values == null)
        {
            Debug.LogError("El servidor devolvio una respuesta de simulacion vacia.");
            return false;
        }

        WaveGridResult result = response.result;
        long expectedValues = (long)result.width * result.height * result.frameCount;
        if (result.width <= 0 || result.height <= 0 || result.frameCount <= 0 ||
            expectedValues != result.values.LongLength)
        {
            Debug.LogError($"Dimensiones de simulacion inconsistentes. Esperados: {expectedValues}; recibidos: {result.values.LongLength}.");
            return false;
        }

        Texture2D previousTexture = waveTexture;
        Texture2D previousDisplacementTexture = displacementTexture;
        simulation = result;
        waveTexture = new Texture2D(result.width, result.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        displacementTexture = new Texture2D(result.width, result.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        framePixels = new Color32[result.width * result.height];
        displacementPixels = new Color32[result.width * result.height];

        InitializeVisibleNodes();

        waveMaterial.SetTexture("_MainTex", waveTexture);
        waveMaterial.SetTexture("_Displacement", displacementTexture);

        if (previousTexture != null)
            Destroy(previousTexture);
        if (previousDisplacementTexture != null)
            Destroy(previousDisplacementTexture);

        currentFrame = 0;
        playbackAccumulator = 0f;
        simulationReady = true;
        DisplayFrame(0);
        return true;
    }

    private void Update()
    {
        if (editingInitialCondition)
        {
            UpdateInitialConditionEditor();
            return;
        }

        if (playbackPaused)
            return;

        if (!simulationReady || simulation.frameCount <= 1)
            return;

        playbackAccumulator += Time.deltaTime;
        float frameInterval = 1f / playbackFramesPerSecond;

        while (playbackAccumulator >= frameInterval)
        {
            playbackAccumulator -= frameInterval;
            int nextFrame = currentFrame + 1;

            if (nextFrame >= simulation.frameCount)
            {
                if (!loopPlayback)
                {
                    simulationReady = false;
                    return;
                }

                nextFrame = 0;
            }

            DisplayFrame(nextFrame);
        }
    }

    private void DisplayFrame(int frameIndex)
    {
        int valuesPerFrame = simulation.width * simulation.height;
        int offset = frameIndex * valuesPerFrame;

        for (int index = 0; index < valuesPerFrame; index++)
        {
            float value = simulation.values[offset + index];
            float normalized = Mathf.Clamp01(0.5f + value * floatToColorMultiplier);
            byte channel = (byte)Mathf.RoundToInt(normalized * 255f);
            displacementPixels[index] = new Color32(channel, channel, channel, 255);
            framePixels[index] = WaveSurfaceColor(value, index, simulation.width, simulation.height);
        }

        waveTexture.SetPixels32(framePixels);
        waveTexture.Apply(false, false);
        displacementTexture.SetPixels32(displacementPixels);
        displacementTexture.Apply(false, false);
        currentFrame = frameIndex;
        UpdateVisibleNodes();
    }

    // Escala divergente luminosa y saturada: rojo para desplazamiento positivo,
    // verde cerca de cero y azul para desplazamiento negativo.
    private static Color32 WaveAmplitudeColor(float value)
    {
        float v = Mathf.Clamp(value, -1f, 1f);
        float magnitude = Mathf.Pow(Mathf.Abs(v), 0.55f);
        float hue = v >= 0f
            ? Mathf.Lerp(0.34f, 0f, magnitude)
            : Mathf.Lerp(0.34f, 0.62f, magnitude);
        float saturation = Mathf.Lerp(0.88f, 1f, magnitude);
        float brightness = Mathf.Lerp(0.90f, 1f, magnitude);
        return (Color32)Color.HSVToRGB(hue, saturation, brightness);
    }

    private Color32 WaveSurfaceColor(float value, int pixelIndex, int width, int height)
    {
        Color baseColor = WaveAmplitudeColor(value);
        if (!IsSurfaceGridPixel(pixelIndex, width, height))
            return (Color32)baseColor;

        return (Color32)Color.Lerp(baseColor, surfaceGridColor, surfaceGridStrength);
    }

    private bool IsSurfaceGridPixel(int pixelIndex, int width, int height)
    {
        if (surfaceGridSubdivisions < 2 || width < 2 || height < 2)
            return false;

        int gridX = pixelIndex / height;
        int gridY = pixelIndex % height;
        float subdivisionX = gridX * surfaceGridSubdivisions / (width - 1f);
        float subdivisionY = gridY * surfaceGridSubdivisions / (height - 1f);
        float thresholdX = surfaceGridLineWidth * surfaceGridSubdivisions / (width - 1f);
        float thresholdY = surfaceGridLineWidth * surfaceGridSubdivisions / (height - 1f);

        return Mathf.Abs(subdivisionX - Mathf.Round(subdivisionX)) <= thresholdX ||
               Mathf.Abs(subdivisionY - Mathf.Round(subdivisionY)) <= thresholdY;
    }

    private void UpdateInitialConditionEditor()
    {
        if (editCamera == null)
            editCamera = Camera.main;
        if (editCamera == null || editableInitialValues == null)
            return;

        if (selectedNode >= 0)
        {
            float currentElevation = GetCameraElevation();
            float displacement = Mathf.Clamp(
                selectionStartDisplacement +
                (currentElevation - selectionStartElevation) * editHeightPerDegree,
                -1f,
                1f);

            if (float.IsNaN(lastEditedDisplacement) ||
                Mathf.Abs(displacement - lastEditedDisplacement) > 0.001f)
            {
                ApplyNodeDisplacement(selectedNode, displacement);
                lastEditedDisplacement = displacement;
            }
        }
        else
        {
            int previousHovered = hoveredNode;
            hoveredNode = FindNodeAtScreenCenter();
            if (previousHovered != hoveredNode)
                UpdateVisibleNodes();
        }

        if (!WasSelectionPressed())
            return;

        if (selectedNode >= 0)
        {
            selectedNode = -1;
            selectionBaseValues = null;
            InitialConditionEditChanged?.Invoke(true,
                "Nodo fijado. Apunta a otro nodo y toca para seleccionarlo, o envia la condicion al servidor.");
            UpdateVisibleNodes();
            return;
        }

        if (hoveredNode < 0)
        {
            InitialConditionEditChanged?.Invoke(true,
                "No hay un nodo bajo el cursor. Apunta a un punto blanco interior y vuelve a tocar.");
            return;
        }

        selectedNode = hoveredNode;
        selectionBaseValues = (float[])editableInitialValues.Clone();
        selectionStartElevation = GetCameraElevation();
        selectionStartDisplacement = GetNodeDisplacement(selectedNode);
        lastEditedDisplacement = selectionStartDisplacement;
        InitialConditionEditChanged?.Invoke(true,
            "Nodo seleccionado. Inclina el telefono hacia arriba o abajo y toca para fijar la nueva altura.");
        UpdateVisibleNodes();
    }

    private int FindNodeAtScreenCenter()
    {
        Ray ray = new Ray(editCamera.transform.position, editCamera.transform.forward);
        int bestNode = -1;
        float bestDistance = float.PositiveInfinity;

        for (int index = 0; index < nodeBasePositions.Length; index++)
        {
            Vector2 uv = nodeUvs[index];
            // Los bordes permanecen fijos en cero (condicion de Dirichlet).
            if (uv.x < 0.001f || uv.x > 0.999f || uv.y < 0.001f || uv.y > 0.999f)
                continue;

            Vector3 worldPosition = transform.TransformPoint(nodeParticles[index].position);
            Vector3 toNode = worldPosition - ray.origin;
            float forwardDistance = Vector3.Dot(toNode, ray.direction);
            if (forwardDistance <= 0f)
                continue;

            float perpendicularDistance = Vector3.Cross(ray.direction, toNode).magnitude;
            float allowedDistance = nodeSelectionRadius + forwardDistance * 0.012f;
            if (perpendicularDistance <= allowedDistance && perpendicularDistance < bestDistance)
            {
                bestDistance = perpendicularDistance;
                bestNode = index;
            }
        }

        return bestNode;
    }

    private void ApplyNodeDisplacement(int nodeIndex, float targetDisplacement)
    {
        if (selectionBaseValues == null)
            return;

        float startValue = selectionStartDisplacement / (2f * floatToColorMultiplier);
        float targetValue = targetDisplacement / (2f * floatToColorMultiplier);
        float valueDelta = targetValue - startValue;
        Vector2 centerUv = nodeUvs[nodeIndex];

        for (int gridX = 0; gridX < simulation.width; gridX++)
        {
            for (int gridY = 0; gridY < simulation.height; gridY++)
            {
                int index = gridX * simulation.height + gridY;
                if (gridX == 0 || gridY == 0 ||
                    gridX == simulation.width - 1 || gridY == simulation.height - 1)
                {
                    editableInitialValues[index] = 0f;
                    continue;
                }

                Vector2 uv = new Vector2(
                    gridY / (simulation.height - 1f),
                    gridX / (simulation.width - 1f));
                float squareDistance = (uv - centerUv).sqrMagnitude;
                float influence = Mathf.Exp(-squareDistance / (2f * editBrushRadius * editBrushRadius));
                editableInitialValues[index] = Mathf.Clamp(
                    selectionBaseValues[index] + valueDelta * influence, -2f, 2f);
            }
        }

        UploadEditableInitialCondition();
    }

    private float GetNodeDisplacement(int nodeIndex)
    {
        float encoded = displacementTexture.GetPixelBilinear(nodeUvs[nodeIndex].x, nodeUvs[nodeIndex].y).r;
        return (encoded - 0.5f) * 2f;
    }

    private void UploadEditableInitialCondition()
    {
        if (editableInitialValues == null)
            return;

        for (int index = 0; index < editableInitialValues.Length; index++)
        {
            float normalized = Mathf.Clamp01(
                0.5f + editableInitialValues[index] * floatToColorMultiplier);
            byte channel = (byte)Mathf.RoundToInt(normalized * 255f);
            displacementPixels[index] = new Color32(channel, channel, channel, 255);
            framePixels[index] = WaveSurfaceColor(
                editableInitialValues[index], index, simulation.width, simulation.height);
        }

        waveTexture.SetPixels32(framePixels);
        waveTexture.Apply(false, false);
        displacementTexture.SetPixels32(displacementPixels);
        displacementTexture.Apply(false, false);
        UpdateVisibleNodes();
    }

    private float GetCameraElevation()
    {
        return Mathf.Asin(Mathf.Clamp(editCamera.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    private bool WasSelectionPressed()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            int pointerId = touchscreen.primaryTouch.touchId.ReadValue();
            return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(pointerId);
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();

        return false;
    }

    private void InitializeVisibleNodes()
    {
        if (nodeParticleSystem == null)
            CreateNodeParticleSystem();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("Membrana2D: la placa no tiene una malla para ubicar sus nodos.");
            nodeParticles = null;
            return;
        }

        Mesh plateMesh = meshFilter.sharedMesh;
        nodeBasePositions = plateMesh.vertices;
        nodeUvs = plateMesh.uv;
        if (nodeUvs == null || nodeUvs.Length != nodeBasePositions.Length)
        {
            Debug.LogError("Membrana2D: la malla de la placa no tiene coordenadas UV validas.");
            nodeParticles = null;
            return;
        }

        int visibleNodeCount = nodeBasePositions.Length;

        // Cada punto representa exactamente un vertice de la placa.
        nodeParticles = new ParticleSystem.Particle[visibleNodeCount];

        ParticleSystem.MainModule main = nodeParticleSystem.main;
        main.maxParticles = nodeParticles.Length;
    }

    private void CreateNodeParticleSystem()
    {
        GameObject nodeObject = new GameObject("Wave Numerical Nodes");
        nodeObject.transform.SetParent(transform, false);

        nodeParticleSystem = nodeObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = nodeParticleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = nodeParticleSystem.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = nodeParticleSystem.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = nodeObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        Shader nodeShader = Resources.Load<Shader>("WaveNode");
        if (nodeShader == null)
        {
            Debug.LogError("Membrana2D: no se encontro el shader de los nodos.");
            return;
        }

        nodeTexture = CreateCircularNodeTexture();
        nodeMaterial = new Material(nodeShader)
        {
            name = "Wave Nodes Runtime Material"
        };
        nodeMaterial.SetTexture("_MainTex", nodeTexture);
        particleRenderer.sharedMaterial = nodeMaterial;
    }

    private Texture2D CreateCircularNodeTexture()
    {
        const int textureSize = 32;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "Wave Node Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[textureSize * textureSize];
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.46f;
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.InverseLerp(radius - 2f, radius, distance);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void UpdateVisibleNodes()
    {
        if (nodeParticleSystem == null || nodeParticles == null || nodeBasePositions == null)
            return;

        int visibleNodeCount = nodeBasePositions.Length;

        for (int index = 0; index < visibleNodeCount; index++)
        {
            // Se muestrea la misma textura y la misma coordenada UV que usa el
            // shader, por lo que cada punto coincide con un vertice real.
            float encoded = displacementTexture.GetPixelBilinear(nodeUvs[index].x, nodeUvs[index].y).r;
            float displacement = (encoded - 0.5f) * 2f;
            Vector3 basePosition = nodeBasePositions[index];
            basePosition.y += displacement;

            Color color = Color.white;
            if (editingInitialCondition && index == selectedNode)
                color = new Color(1f, 0.72f, 0.12f);
            else if (editingInitialCondition && index == hoveredNode)
                color = new Color(0.25f, 0.95f, 1f);

            SetNodeParticle(index, basePosition, color);
        }

        nodeParticleSystem.SetParticles(nodeParticles, nodeParticles.Length);
    }

    private void SetNodeParticle(int index, Vector3 localPosition, Color color)
    {
        ParticleSystem.Particle particle = nodeParticles[index];
        particle.position = localPosition;
        particle.startColor = color;
        particle.startSize = nodePointSize;
        particle.remainingLifetime = 100000f;
        nodeParticles[index] = particle;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusChanged?.Invoke(message, isError);
        if (isError)
            Debug.LogError("Membrana2D: " + message);
    }

    private static string NormalizeInitialCondition(string value)
    {
        string condition = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (condition == "gaussiana")
            return "gauss";
        return condition;
    }

    private static float GetNaturalFrequency(float length, float speed, int selectedModeA, int selectedModeB)
    {
        if (length <= 0f)
            return 0f;

        return speed * Mathf.Sqrt(
            selectedModeA * selectedModeA + selectedModeB * selectedModeB) / (2f * length);
    }

    private static string NormalizeServerUrl(string value)
    {
        string server = (value ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(server))
            return string.Empty;

        if (!server.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            server = "http://" + server;
        }

        return server;
    }

    private void OnDestroy()
    {
        if (waveTexture != null)
            Destroy(waveTexture);
        if (displacementTexture != null)
            Destroy(displacementTexture);
        if (nodeMaterial != null)
            Destroy(nodeMaterial);
        if (nodeTexture != null)
            Destroy(nodeTexture);
    }
}

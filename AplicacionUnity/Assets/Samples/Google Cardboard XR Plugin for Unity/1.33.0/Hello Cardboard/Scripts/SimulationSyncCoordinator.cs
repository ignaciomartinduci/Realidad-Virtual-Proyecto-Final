using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

[Serializable]
public class SimulationSyncStatus
{
    public bool activo;
    public long version;
    public string ecuacion;
    public float l;
    public float t;
    public float c;
    public string inicial;
    public bool amortiguamiento;
    public float gamma;
    public bool excitacion;
    public string formaExcitacion;
    public float frecExcitacion;
}

/// <summary>
/// Consulta el estado liviano de sincronizacion del docente y permanece vivo
/// al cambiar de escena. El solver numerico nunca se ejecuta en el cliente.
/// </summary>
public class SimulationSyncCoordinator : MonoBehaviour
{
    private const string WaveScene = "HelloCardboard";
    private const string HeatScene = "HeatPlate";
    private static SimulationSyncCoordinator instance;

    [SerializeField, Range(0.75f, 10f)] private float pollInterval = 1.5f;
    private string serverUrl;
    private long lastAppliedVersion = -1;
    private bool changingScene;

    public static void EnsureExists()
    {
        if (instance != null)
            return;
        GameObject coordinator = new GameObject("Simulation Sync Coordinator");
        instance = coordinator.AddComponent<SimulationSyncCoordinator>();
        DontDestroyOnLoad(coordinator);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        serverUrl = NormalizeServerUrl(PlayerPrefs.GetString("IP", string.Empty));
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(serverUrl))
            yield break;

        while (true)
        {
            yield return PollSyncStatus();
            yield return new WaitForSecondsRealtime(pollInterval);
        }
    }

    private IEnumerator PollSyncStatus()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/sync/status"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                yield break;

            SimulationSyncStatus status;
            try
            {
                status = JsonUtility.FromJson<SimulationSyncStatus>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                yield break;
            }

            if (status == null || !status.activo)
            {
                changingScene = false;
                lastAppliedVersion = status != null ? status.version : lastAppliedVersion;
                yield break;
            }

            string targetScene = status.ecuacion == "calor2d" ? HeatScene : WaveScene;
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != targetScene)
            {
                if (!changingScene)
                {
                    changingScene = true;
                    lastAppliedVersion = status.version;
                    SceneManager.LoadScene(targetScene);
                }
                yield break;
            }

            changingScene = false;
            if (status.version == lastAppliedVersion)
                yield break;

            lastAppliedVersion = status.version;
            if (targetScene == WaveScene)
            {
                Membrana2D wave = FindAnyObjectByType<Membrana2D>();
                if (wave != null)
                    wave.RefreshFromServerSync();
            }
            else
            {
                HeatPlate2D heat = FindAnyObjectByType<HeatPlate2D>();
                if (heat != null)
                    heat.RefreshFromServerSync();
            }
        }
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
}

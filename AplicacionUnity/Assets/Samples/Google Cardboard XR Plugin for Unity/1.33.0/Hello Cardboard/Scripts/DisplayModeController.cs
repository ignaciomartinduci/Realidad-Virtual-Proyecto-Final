using Google.XR.Cardboard;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mantiene el modo de visualizacion elegido y ofrece el control tactil para
/// alternar entre la vista monoscopica y Google Cardboard.
/// </summary>
public class DisplayModeController : MonoBehaviour
{
    private const string DisplayModePreference = "rv_display_mode";

    private bool runtimeVrActive;
    private bool changingMode;
    private GameObject controlsCanvas;

    public static bool IsVrModeRequested => PlayerPrefs.GetInt(DisplayModePreference, 0) == 1;

    public void SetRuntimeVrActive(bool isActive)
    {
        runtimeVrActive = isActive;
        BuildModeControls();
    }

    private void Update()
    {
        if (!runtimeVrActive || changingMode)
            return;

        if (Api.IsCloseButtonPressed)
        {
            RequestMode(false);
            return;
        }

        if (Api.IsGearButtonPressed)
            Api.ScanDeviceParams();

        Api.UpdateScreenParams();
    }

    public void ToggleMode()
    {
        RequestMode(!runtimeVrActive);
    }

    public void RequestFlatMode()
    {
        RequestMode(false);
    }

    private void RequestMode(bool useVr)
    {
        if (changingMode)
            return;

        changingMode = true;
        PlayerPrefs.SetInt(DisplayModePreference, useVr ? 1 : 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void ForceFlatPreference()
    {
        PlayerPrefs.SetInt(DisplayModePreference, 0);
        PlayerPrefs.Save();
    }

    private void BuildModeControls()
    {
        if (controlsCanvas != null)
            Destroy(controlsCanvas);

        // Cardboard ya proporciona su propio botón de cierre en la esquina
        // superior izquierda. En VR no agregamos controles superpuestos.
        if (runtimeVrActive)
            return;

        EnsureEventSystem();

        controlsCanvas = new GameObject("Display Mode Controls", typeof(RectTransform));
        Canvas canvas = controlsCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = controlsCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        controlsCanvas.AddComponent<GraphicRaycaster>();

        CreateModeButton(canvas.transform, new Vector2(0.5f, 1f), "MODO VR", true);
    }

    private void CreateModeButton(Transform parent, Vector2 anchor, string label, bool enterVr)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -28f);
        rect.sizeDelta = runtimeVrActive ? new Vector2(210f, 58f) : new Vector2(230f, 64f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = enterVr
            ? new Color(0.08f, 0.48f, 0.95f, 0.96f)
            : new Color(0.035f, 0.055f, 0.09f, 0.90f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => RequestMode(enterVr));

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.fontSize = 25;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystem = new GameObject("Display Mode EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }
}

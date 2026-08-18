using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HeatParameterPanel : MonoBehaviour
{
    private static readonly string[] Conditions = { "gauss", "bordes" };
    private static readonly string[] ConditionLabels = { "Gaussiana", "Bordes calientes" };
    private readonly Color panelColor = new Color(0.035f, 0.055f, 0.09f, 0.94f);
    private readonly Color fieldColor = new Color(0.10f, 0.14f, 0.20f, 0.98f);
    private readonly Color accentColor = new Color(1f, 0.34f, 0.05f, 1f);

    private HeatPlate2D solver;
    private Canvas canvas;
    private GameObject panel;
    private GameObject showButton;
    private InputField lengthField;
    private InputField durationField;
    private InputField alphaField;
    private Text conditionText;
    private Text statusText;
    private Text heightText;
    private int conditionIndex;
    private float initialHeight;
    private Coroutine autoSubmit;
    private Font font;
    private bool initialized;

    public void Initialize(HeatPlate2D heatSolver)
    {
        if (initialized)
            return;
        initialized = true;
        solver = heatSolver;
        solver.StatusChanged += OnStatusChanged;
        initialHeight = solver.transform.position.y;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildInterface();
        LoadValues();

        if (DisplayModeController.IsVrModeRequested)
        {
            panel.SetActive(false);
            showButton.SetActive(false);
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        GameObject eventSystem = new GameObject("Heat UI EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Heat Parameters Canvas", typeof(RectTransform));
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = UiObject("Heat Panel", canvas.transform);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(590f, 690f);
        panel.AddComponent<Image>().color = panelColor;
        VerticalLayoutGroup vertical = panel.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(20, 20, 18, 18);
        vertical.spacing = 10f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        CreateHeader();
        CreateText("Descripcion", panel.transform,
            "La solucion numerica se calcula en el servidor y la placa muestra su evolucion termica.",
            22, TextAnchor.MiddleLeft, 68f, new Color(0.86f, 0.90f, 0.96f));
        lengthField = CreateParameterRow("Longitud L", "1 a 20", "10");
        durationField = CreateParameterRow("Duracion T (s)", "1 a 100", "10");
        alphaField = CreateParameterRow("Difusividad alpha", "0,1 a 2", "0.5");
        CreateConditionRow();
        CreateHeightRow();
        Button update = CreateButton("Actualizar ahora", panel.transform, accentColor, 55f);
        update.onClick.AddListener(SubmitNow);
        statusText = CreateText("Estado", panel.transform, "Cargando solucion termica...", 21,
            TextAnchor.MiddleLeft, 62f, new Color(1f, 0.84f, 0.65f));

        showButton = CreateButton("Calor", canvas.transform, panelColor, 58f).gameObject;
        RectTransform showRect = showButton.GetComponent<RectTransform>();
        showRect.anchorMin = showRect.anchorMax = showRect.pivot = new Vector2(0f, 1f);
        showRect.anchoredPosition = new Vector2(24f, -24f);
        showRect.sizeDelta = new Vector2(190f, 58f);
        showButton.GetComponent<Button>().onClick.AddListener(() => SetVisible(true));
        showButton.SetActive(false);

        lengthField.onEndEdit.AddListener(_ => ScheduleSubmit());
        durationField.onEndEdit.AddListener(_ => ScheduleSubmit());
        alphaField.onEndEdit.AddListener(_ => ScheduleSubmit());
    }

    private void CreateHeader()
    {
        GameObject row = CreateRow("Header", panel.transform, 58f);
        Text title = CreateText("Title", row.transform, "ECUACION DEL CALOR", 28,
            TextAnchor.MiddleLeft, 58f, Color.white);
        title.fontStyle = FontStyle.Bold;
        title.GetComponent<LayoutElement>().flexibleWidth = 1f;
        Button wave = CreateButton("Onda", row.transform, new Color(0.12f, 0.55f, 0.92f), 48f);
        wave.GetComponent<LayoutElement>().preferredWidth = 100f;
        wave.GetComponent<LayoutElement>().flexibleWidth = 0f;
        wave.onClick.AddListener(() => SceneManager.LoadScene("HelloCardboard"));
        Button hide = CreateButton("Ocultar", row.transform, fieldColor, 48f);
        hide.GetComponent<LayoutElement>().preferredWidth = 110f;
        hide.GetComponent<LayoutElement>().flexibleWidth = 0f;
        hide.onClick.AddListener(() => SetVisible(false));
    }

    private InputField CreateParameterRow(string labelValue, string hint, string initialValue)
    {
        GameObject row = CreateRow(labelValue, panel.transform, 62f);
        Text label = CreateText(labelValue + " Label", row.transform, labelValue, 24,
            TextAnchor.MiddleLeft, 62f, Color.white);
        label.GetComponent<LayoutElement>().preferredWidth = 270f;
        label.GetComponent<LayoutElement>().flexibleWidth = 0f;
        return CreateInput(labelValue + " Input", row.transform, hint, initialValue);
    }

    private void CreateConditionRow()
    {
        GameObject row = CreateRow("Condicion", panel.transform, 62f);
        Text label = CreateText("Condicion Label", row.transform, "Condicion inicial", 24,
            TextAnchor.MiddleLeft, 62f, Color.white);
        label.GetComponent<LayoutElement>().preferredWidth = 270f;
        label.GetComponent<LayoutElement>().flexibleWidth = 0f;
        Button condition = CreateButton(ConditionLabels[0], row.transform, fieldColor, 58f);
        conditionText = condition.GetComponentInChildren<Text>();
        condition.onClick.AddListener(() =>
        {
            conditionIndex = (conditionIndex + 1) % Conditions.Length;
            conditionText.text = ConditionLabels[conditionIndex];
            ScheduleSubmit();
        });
    }

    private void CreateHeightRow()
    {
        GameObject row = CreateRow("Altura", panel.transform, 64f);
        heightText = CreateText("Altura Label", row.transform, "Altura: 0 m", 23,
            TextAnchor.MiddleLeft, 64f, Color.white);
        heightText.GetComponent<LayoutElement>().preferredWidth = 190f;
        heightText.GetComponent<LayoutElement>().flexibleWidth = 0f;
        Slider slider = CreateHorizontalSlider(row.transform);
        slider.minValue = -4f;
        slider.maxValue = 4f;
        slider.value = 0f;
        slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(SetHeight);
    }

    private Slider CreateHorizontalSlider(Transform parent)
    {
        GameObject sliderObject = UiObject("Height Slider", parent);
        LayoutElement layout = sliderObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 58f;
        layout.flexibleWidth = 1f;
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;

        GameObject backgroundObject = UiObject("Background", sliderObject.transform);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = fieldColor;
        Stretch(backgroundObject.GetComponent<RectTransform>(), 0f);

        GameObject fillArea = UiObject("Fill Area", sliderObject.transform);
        Stretch(fillArea.GetComponent<RectTransform>(), 12f);
        GameObject fillObject = UiObject("Fill", fillArea.transform);
        Image fill = fillObject.AddComponent<Image>();
        fill.color = accentColor;
        Stretch(fillObject.GetComponent<RectTransform>(), 0f);

        GameObject handleArea = UiObject("Handle Slide Area", sliderObject.transform);
        Stretch(handleArea.GetComponent<RectTransform>(), 12f);
        GameObject handleObject = UiObject("Handle", handleArea.transform);
        Image handle = handleObject.AddComponent<Image>();
        handle.color = Color.white;
        handleObject.GetComponent<RectTransform>().sizeDelta = new Vector2(42f, 52f);
        slider.fillRect = fillObject.GetComponent<RectTransform>();
        slider.handleRect = handleObject.GetComponent<RectTransform>();
        slider.targetGraphic = handle;
        return slider;
    }

    private InputField CreateInput(string name, Transform parent, string hint, string initialValue)
    {
        GameObject fieldObject = UiObject(name, parent);
        fieldObject.AddComponent<Image>().color = fieldColor;
        InputField field = fieldObject.AddComponent<InputField>();
        field.lineType = InputField.LineType.SingleLine;
        field.keyboardType = TouchScreenKeyboardType.DecimalPad;
        field.text = initialValue;
        LayoutElement layout = fieldObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 58f;
        layout.flexibleWidth = 1f;
        Text text = CreateText("Text", fieldObject.transform, initialValue, 27,
            TextAnchor.MiddleCenter, 58f, Color.white);
        Stretch(text.rectTransform, 12f);
        Text placeholder = CreateText("Placeholder", fieldObject.transform, hint, 23,
            TextAnchor.MiddleCenter, 58f, new Color(0.62f, 0.68f, 0.76f, 0.8f));
        Stretch(placeholder.rectTransform, 12f);
        field.textComponent = text;
        field.placeholder = placeholder;
        return field;
    }

    private void LoadValues()
    {
        lengthField.text = Format(solver.DomainLength);
        durationField.text = Format(solver.SimulationDuration);
        alphaField.text = Format(solver.Diffusivity);
        conditionIndex = 0;
        for (int i = 0; i < Conditions.Length; i++)
            if (Conditions[i] == solver.InitialCondition)
                conditionIndex = i;
        conditionText.text = ConditionLabels[conditionIndex];
    }

    private void ScheduleSubmit()
    {
        if (autoSubmit != null)
            StopCoroutine(autoSubmit);
        autoSubmit = StartCoroutine(SubmitAfterDelay());
    }

    private IEnumerator SubmitAfterDelay()
    {
        SetStatus("Cambio detectado. Preparando actualizacion...", false);
        yield return new WaitForSecondsRealtime(0.8f);
        autoSubmit = null;
        Submit();
    }

    private void SubmitNow()
    {
        if (autoSubmit != null)
        {
            StopCoroutine(autoSubmit);
            autoSubmit = null;
        }
        Submit();
    }

    private void Submit()
    {
        if (!Parse(lengthField.text, out float length) ||
            !Parse(durationField.text, out float duration) ||
            !Parse(alphaField.text, out float alpha))
        {
            SetStatus("Completa todos los valores numericos.", true);
            return;
        }
        if (!solver.TryUpdateParameters(length, duration, alpha,
            Conditions[conditionIndex], out string error))
        {
            SetStatus(error, true);
            return;
        }
        SetStatus("Parametros enviados al servidor...", false);
    }

    private void SetHeight(float offset)
    {
        Vector3 position = solver.transform.position;
        position.y = initialHeight + offset;
        solver.transform.position = position;
        string sign = offset > 0.005f ? "+" : string.Empty;
        heightText.text = "Altura: " + sign + Format(offset) + " m";
    }

    private void OnStatusChanged(string message, bool error)
    {
        SetStatus(message, error);
        if (!error && message.StartsWith("Calor actualizado"))
            LoadValues();
    }

    private void SetStatus(string message, bool error)
    {
        if (statusText == null)
            return;
        statusText.text = message;
        statusText.color = error ? new Color(1f, 0.45f, 0.38f) : new Color(1f, 0.84f, 0.65f);
    }

    private void SetVisible(bool visible)
    {
        panel.SetActive(visible);
        showButton.SetActive(!visible);
    }

    private GameObject CreateRow(string name, Transform parent, float height)
    {
        GameObject row = UiObject(name, parent);
        row.AddComponent<LayoutElement>().preferredHeight = height;
        HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 12f;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;
        return row;
    }

    private Button CreateButton(string label, Transform parent, Color color, float height)
    {
        GameObject obj = UiObject(label + " Button", parent);
        obj.AddComponent<Image>().color = color;
        Button button = obj.AddComponent<Button>();
        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.flexibleWidth = 1f;
        Text text = CreateText("Label", obj.transform, label, 24, TextAnchor.MiddleCenter, height, Color.white);
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform, 8f);
        return button;
    }

    private Text CreateText(string name, Transform parent, string value, int size,
        TextAnchor alignment, float height, Color color)
    {
        GameObject obj = UiObject(name, parent);
        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        obj.AddComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private static GameObject UiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, 2f);
        rect.offsetMax = new Vector2(-padding, -2f);
    }

    private static bool Parse(string value, out float result)
    {
        string normalized = (value ?? string.Empty).Trim().Replace(',', '.');
        return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static string Format(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    private void OnDestroy()
    {
        if (solver != null)
            solver.StatusChanged -= OnStatusChanged;
        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}

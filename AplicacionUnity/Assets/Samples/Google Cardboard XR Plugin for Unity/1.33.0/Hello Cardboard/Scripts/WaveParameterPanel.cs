using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Panel tactil creado en tiempo de ejecucion para no depender de una jerarquia
/// de UI concreta en la escena. Al ser Screen Space Overlay permanece fijo aunque
/// la camara se mueva con el giroscopio.
/// </summary>
public class WaveParameterPanel : MonoBehaviour
{
    private static readonly string[] Conditions = { "gauss", "seno", "triangular", "modo" };
    private static readonly string[] ConditionLabels = { "Gaussiana", "Seno", "Triangular", "Modo (a,b)" };

    private readonly Color panelColor = new Color(0.035f, 0.055f, 0.09f, 0.94f);
    private readonly Color fieldColor = new Color(0.10f, 0.14f, 0.20f, 0.98f);
    private readonly Color accentColor = new Color(0.12f, 0.55f, 0.92f, 1f);

    private Membrana2D solver;
    private Canvas canvas;
    private GameObject panel;
    private GameObject showButton;
    private GameObject heightPanel;
    private GameObject heightShowButton;
    private GameObject editButton;
    private GameObject editPanel;
    private GameObject editCrosshair;
    private Text editInstructionText;
    private InputField lengthField;
    private InputField durationField;
    private InputField speedField;
    private InputField gammaField;
    private InputField frequencyMultiplierField;
    private InputField modeAField;
    private InputField modeBField;
    private GameObject modeRow;
    private Text conditionButtonText;
    private Button dampingButton;
    private Button excitationButton;
    private Text dampingButtonText;
    private Text excitationButtonText;
    private Text frequencyInfoText;
    private Text statusText;
    private Text heightValueText;
    private Slider heightSlider;
    private Font font;
    private int conditionIndex;
    private bool dampingEnabled;
    private bool excitationEnabled;
    private float initialPlateHeight;
    private Coroutine autoSubmitRoutine;
    private bool initialized;
    private bool parametersWereVisible;
    private bool heightWasVisible;

    public void Initialize(Membrana2D waveSolver)
    {
        if (initialized)
            return;

        initialized = true;
        solver = waveSolver;
        solver.StatusChanged += OnSolverStatusChanged;
        solver.InitialConditionEditChanged += OnInitialConditionEditChanged;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        EnsureEventSystem();
        BuildInterface();
        BuildHeightControl();
        BuildInitialConditionEditor();
        LoadValuesFromSolver();
        SetStatus("Cargando solucion inicial...", false);

        if (DisplayModeController.IsVrModeRequested)
            HideFlatControlsForVr();
    }

    private void HideFlatControlsForVr()
    {
        panel.SetActive(false);
        showButton.SetActive(false);
        heightPanel.SetActive(false);
        heightShowButton.SetActive(false);
        editButton.SetActive(false);
        editPanel.SetActive(false);
        editCrosshair.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystem = new GameObject("Wave UI EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Wave Parameters Canvas", typeof(RectTransform));
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = CreateUiObject("Panel", canvas.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(570f, 930f);
        panel.AddComponent<Image>().color = panelColor;

        VerticalLayoutGroup vertical = panel.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(20, 20, 18, 18);
        vertical.spacing = 10f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        CreateHeader(panel.transform);
        CreateText("Descripcion", panel.transform,
            "Modifica los parametros. Al terminar de editar, la solucion se recalcula en el servidor.",
            23, TextAnchor.MiddleLeft, 72f, new Color(0.82f, 0.88f, 0.95f));

        lengthField = CreateParameterRow(panel.transform, "Longitud L", "1 a 10", 10f);
        durationField = CreateParameterRow(panel.transform, "Duracion T (s)", "1 a 100", 10f);
        speedField = CreateParameterRow(panel.transform, "Velocidad c", "0,1 a 2", 1f);
        CreateConditionRow(panel.transform);
        CreateModeRow(panel.transform);
        CreateDampingRow(panel.transform);
        CreateExcitationRow(panel.transform);
        frequencyInfoText = CreateText("Frecuencias", panel.transform, string.Empty, 21,
            TextAnchor.MiddleLeft, 42f, new Color(0.72f, 0.90f, 1f));

        Button updateButton = CreateButton("Actualizar ahora", panel.transform, accentColor, 55f);
        updateButton.onClick.AddListener(SubmitNow);

        statusText = CreateText("Estado", panel.transform, string.Empty, 22,
            TextAnchor.MiddleLeft, 62f, new Color(0.72f, 0.90f, 1f));

        showButton = CreateButton("Parametros", canvas.transform, panelColor, 58f).gameObject;
        RectTransform showRect = showButton.GetComponent<RectTransform>();
        showRect.anchorMin = new Vector2(0f, 1f);
        showRect.anchorMax = new Vector2(0f, 1f);
        showRect.pivot = new Vector2(0f, 1f);
        showRect.anchoredPosition = new Vector2(24f, -24f);
        showRect.sizeDelta = new Vector2(210f, 58f);
        showButton.GetComponent<Button>().onClick.AddListener(() => SetPanelVisible(true));
        showButton.SetActive(false);

        lengthField.onEndEdit.AddListener(_ => ScheduleAutomaticSubmit());
        durationField.onEndEdit.AddListener(_ => ScheduleAutomaticSubmit());
        speedField.onEndEdit.AddListener(_ => ScheduleAutomaticSubmit());
        gammaField.onEndEdit.AddListener(_ => ScheduleAutomaticSubmit());
        frequencyMultiplierField.onEndEdit.AddListener(_ => ScheduleAutomaticSubmit());
        modeAField.onEndEdit.AddListener(_ => ScheduleAutomaticSubmit());
        modeBField.onEndEdit.AddListener(_ => ScheduleAutomaticSubmit());
        lengthField.onValueChanged.AddListener(_ => UpdateFrequencyInfo());
        speedField.onValueChanged.AddListener(_ => UpdateFrequencyInfo());
        frequencyMultiplierField.onValueChanged.AddListener(_ => UpdateFrequencyInfo());
        modeAField.onValueChanged.AddListener(_ => UpdateFrequencyInfo());
        modeBField.onValueChanged.AddListener(_ => UpdateFrequencyInfo());
    }

    private void CreateHeader(Transform parent)
    {
        GameObject row = CreateRow("Cabecera", parent, 58f);
        Text title = CreateText("Titulo", row.transform, "PARAMETROS DE LA ONDA", 29,
            TextAnchor.MiddleLeft, 58f, Color.white);
        title.fontStyle = FontStyle.Bold;
        title.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

        Button heat = CreateButton("Calor", row.transform, new Color(1f, 0.34f, 0.05f), 48f);
        LayoutElement heatLayout = heat.gameObject.GetComponent<LayoutElement>();
        heatLayout.preferredWidth = 100f;
        heatLayout.flexibleWidth = 0f;
        heat.onClick.AddListener(() => SceneManager.LoadScene("HeatPlate"));

        Button hide = CreateButton("Ocultar", row.transform, fieldColor, 48f);
        LayoutElement hideLayout = hide.gameObject.GetComponent<LayoutElement>();
        hideLayout.preferredWidth = 125f;
        hideLayout.flexibleWidth = 0f;
        hide.onClick.AddListener(() => SetPanelVisible(false));
    }

    private void BuildHeightControl()
    {
        initialPlateHeight = solver.transform.position.y;

        heightPanel = CreateUiObject("Height Control Panel", canvas.transform);
        RectTransform panelRect = heightPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-24f, 0f);
        panelRect.sizeDelta = new Vector2(220f, 690f);
        heightPanel.AddComponent<Image>().color = panelColor;

        Text title = CreateText("Height Title", heightPanel.transform, "ALTURA", 27,
            TextAnchor.MiddleCenter, 50f, Color.white);
        title.fontStyle = FontStyle.Bold;
        SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(10f, -62f), new Vector2(-10f, -12f));

        heightValueText = CreateText("Height Value", heightPanel.transform, "0,00 m", 25,
            TextAnchor.MiddleCenter, 50f, new Color(0.72f, 0.90f, 1f));
        SetAnchoredRect(heightValueText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(10f, -112f), new Vector2(-10f, -66f));

        heightSlider = CreateVerticalSlider(heightPanel.transform);
        RectTransform sliderRect = heightSlider.GetComponent<RectTransform>();
        SetAnchoredRect(sliderRect, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(48f, 145f), new Vector2(-48f, -120f));
        heightSlider.minValue = -3f;
        heightSlider.maxValue = 3f;
        heightSlider.value = 0f;
        heightSlider.onValueChanged.AddListener(SetPlateHeight);

        Button resetButton = CreateButton("Centrar", heightPanel.transform, accentColor, 52f);
        SetAnchoredRect(resetButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(16f, 78f), new Vector2(-16f, 130f));
        resetButton.onClick.AddListener(() => heightSlider.value = 0f);

        Button hideButton = CreateButton("Ocultar", heightPanel.transform, fieldColor, 50f);
        SetAnchoredRect(hideButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(16f, 16f), new Vector2(-16f, 66f));
        hideButton.onClick.AddListener(() => SetHeightControlVisible(false));

        heightShowButton = CreateButton("Altura", canvas.transform, panelColor, 58f).gameObject;
        RectTransform showRect = heightShowButton.GetComponent<RectTransform>();
        showRect.anchorMin = new Vector2(1f, 0.5f);
        showRect.anchorMax = new Vector2(1f, 0.5f);
        showRect.pivot = new Vector2(1f, 0.5f);
        showRect.anchoredPosition = new Vector2(-24f, 0f);
        showRect.sizeDelta = new Vector2(180f, 58f);
        heightShowButton.GetComponent<Button>().onClick.AddListener(() => SetHeightControlVisible(true));
        heightShowButton.SetActive(false);
    }

    private void BuildInitialConditionEditor()
    {
        editButton = CreateButton("Editar condicion inicial", canvas.transform, accentColor, 60f).gameObject;
        RectTransform editButtonRect = editButton.GetComponent<RectTransform>();
        editButtonRect.anchorMin = new Vector2(0f, 0f);
        editButtonRect.anchorMax = new Vector2(0f, 0f);
        editButtonRect.pivot = new Vector2(0f, 0f);
        editButtonRect.anchoredPosition = new Vector2(24f, 24f);
        editButtonRect.sizeDelta = new Vector2(330f, 60f);
        editButton.GetComponent<Button>().onClick.AddListener(BeginInitialConditionEdit);

        editPanel = CreateUiObject("Initial Condition Edit Panel", canvas.transform);
        RectTransform panelRect = editPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 24f);
        panelRect.sizeDelta = new Vector2(1040f, 190f);
        editPanel.AddComponent<Image>().color = panelColor;

        editInstructionText = CreateText("Edit Instructions", editPanel.transform, string.Empty, 24,
            TextAnchor.MiddleCenter, 95f, Color.white);
        SetAnchoredRect(editInstructionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(24f, -105f), new Vector2(-24f, -12f));

        Button sendButton = CreateButton("Enviar al servidor", editPanel.transform, accentColor, 58f);
        SetAnchoredRect(sendButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f),
            new Vector2(24f, 18f), new Vector2(-8f, 76f));
        sendButton.onClick.AddListener(SubmitInitialConditionEdit);

        Button cancelButton = CreateButton("Cancelar", editPanel.transform, fieldColor, 58f);
        SetAnchoredRect(cancelButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f),
            new Vector2(8f, 18f), new Vector2(-24f, 76f));
        cancelButton.onClick.AddListener(() => solver.CancelInitialConditionEdit());

        editCrosshair = CreateUiObject("Edit Crosshair", canvas.transform);
        Text crosshairText = editCrosshair.AddComponent<Text>();
        crosshairText.font = font;
        crosshairText.text = "+";
        crosshairText.fontSize = 52;
        crosshairText.fontStyle = FontStyle.Bold;
        crosshairText.alignment = TextAnchor.MiddleCenter;
        crosshairText.color = new Color(0.25f, 0.95f, 1f, 0.95f);
        crosshairText.raycastTarget = false;
        RectTransform crosshairRect = editCrosshair.GetComponent<RectTransform>();
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        crosshairRect.sizeDelta = new Vector2(70f, 70f);
        crosshairRect.anchoredPosition = Vector2.zero;

        editPanel.SetActive(false);
        editCrosshair.SetActive(false);
    }

    private Slider CreateVerticalSlider(Transform parent)
    {
        GameObject sliderObject = CreateUiObject("Height Slider", parent);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.BottomToTop;

        GameObject backgroundObject = CreateUiObject("Background", sliderObject.transform);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.10f, 0.14f, 0.20f, 1f);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.38f, 0f);
        backgroundRect.anchorMax = new Vector2(0.62f, 1f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0.38f, 0f);
        fillAreaRect.anchorMax = new Vector2(0.62f, 1f);
        fillAreaRect.offsetMin = new Vector2(0f, 12f);
        fillAreaRect.offsetMax = new Vector2(0f, -12f);

        GameObject fillObject = CreateUiObject("Fill", fillArea.transform);
        Image fill = fillObject.AddComponent<Image>();
        fill.color = accentColor;
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleArea = CreateUiObject("Handle Slide Area", sliderObject.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(0f, 18f);
        handleAreaRect.offsetMax = new Vector2(0f, -18f);

        GameObject handleObject = CreateUiObject("Handle", handleArea.transform);
        Image handle = handleObject.AddComponent<Image>();
        handle.color = Color.white;
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(82f, 46f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        return slider;
    }

    private InputField CreateParameterRow(Transform parent, string label, string hint, float initialValue)
    {
        GameObject row = CreateRow(label, parent, 62f);
        Text labelText = CreateText(label + " Label", row.transform, label, 25,
            TextAnchor.MiddleLeft, 62f, Color.white);
        LayoutElement labelLayout = labelText.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 245f;
        labelLayout.flexibleWidth = 0f;

        return CreateInputField(label + " Input", row.transform, hint, initialValue.ToString(CultureInfo.InvariantCulture));
    }

    private void CreateConditionRow(Transform parent)
    {
        GameObject row = CreateRow("Condicion inicial", parent, 62f);
        Text label = CreateText("Condicion Label", row.transform, "Condicion inicial", 25,
            TextAnchor.MiddleLeft, 62f, Color.white);
        LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 245f;
        labelLayout.flexibleWidth = 0f;

        Button conditionButton = CreateButton("Gaussiana", row.transform, fieldColor, 58f);
        conditionButtonText = conditionButton.GetComponentInChildren<Text>();
        conditionButton.onClick.AddListener(CycleCondition);
    }

    private void CreateModeRow(Transform parent)
    {
        modeRow = CreateRow("Modo de vibracion", parent, 62f);
        Text label = CreateText("Modo Label", modeRow.transform, "Modo (a, b)", 25,
            TextAnchor.MiddleLeft, 62f, Color.white);
        LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 245f;
        labelLayout.flexibleWidth = 0f;

        modeAField = CreateInputField("Mode A Input", modeRow.transform, "a: 1 a 5", "1");
        modeBField = CreateInputField("Mode B Input", modeRow.transform, "b: 1 a 5", "1");
        modeRow.SetActive(false);
    }

    private void CreateDampingRow(Transform parent)
    {
        GameObject row = CreateRow("Amortiguamiento", parent, 62f);
        dampingButton = CreateButton("Amort.: NO", row.transform, fieldColor, 58f);
        dampingButtonText = dampingButton.GetComponentInChildren<Text>();
        LayoutElement toggleLayout = dampingButton.GetComponent<LayoutElement>();
        toggleLayout.preferredWidth = 245f;
        toggleLayout.flexibleWidth = 0f;
        dampingButton.onClick.AddListener(ToggleDamping);

        gammaField = CreateInputField("Gamma Input", row.transform, "gamma: 0 a 1", "0.5");
    }

    private void CreateExcitationRow(Transform parent)
    {
        GameObject row = CreateRow("Excitacion", parent, 62f);
        excitationButton = CreateButton("Excitacion: NO", row.transform, fieldColor, 58f);
        excitationButtonText = excitationButton.GetComponentInChildren<Text>();
        LayoutElement toggleLayout = excitationButton.GetComponent<LayoutElement>();
        toggleLayout.preferredWidth = 245f;
        toggleLayout.flexibleWidth = 0f;
        excitationButton.onClick.AddListener(ToggleExcitation);

        frequencyMultiplierField = CreateInputField(
            "Frequency Multiplier Input", row.transform, "multiplo: 0,1 a 10", "1");
    }

    private InputField CreateInputField(string name, Transform parent, string placeholder, string initialValue)
    {
        GameObject fieldObject = CreateUiObject(name, parent);
        Image image = fieldObject.AddComponent<Image>();
        image.color = fieldColor;

        InputField field = fieldObject.AddComponent<InputField>();
        field.lineType = InputField.LineType.SingleLine;
        field.contentType = InputField.ContentType.Standard;
        field.keyboardType = TouchScreenKeyboardType.DecimalPad;
        field.characterValidation = InputField.CharacterValidation.None;
        field.text = initialValue;

        LayoutElement layout = fieldObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 58f;
        layout.flexibleWidth = 1f;

        Text valueText = CreateText("Text", fieldObject.transform, initialValue, 27,
            TextAnchor.MiddleCenter, 58f, Color.white);
        StretchWithPadding(valueText.rectTransform, 12f, 12f, 2f, 2f);

        Text placeholderText = CreateText("Placeholder", fieldObject.transform, placeholder, 24,
            TextAnchor.MiddleCenter, 58f, new Color(0.62f, 0.68f, 0.76f, 0.8f));
        placeholderText.fontStyle = FontStyle.Italic;
        StretchWithPadding(placeholderText.rectTransform, 12f, 12f, 2f, 2f);

        field.textComponent = valueText;
        field.placeholder = placeholderText;
        return field;
    }

    private GameObject CreateRow(string name, Transform parent, float height)
    {
        GameObject row = CreateUiObject(name, parent);
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = height;

        HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 12f;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;
        return row;
    }

    private Button CreateButton(string label, Transform parent, Color color, float height)
    {
        GameObject buttonObject = CreateUiObject(label + " Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.flexibleWidth = 1f;

        Text text = CreateText("Label", buttonObject.transform, label, 25,
            TextAnchor.MiddleCenter, height, Color.white);
        text.fontStyle = FontStyle.Bold;
        StretchWithPadding(text.rectTransform, 8f, 8f, 2f, 2f);
        return button;
    }

    private Text CreateText(string name, Transform parent, string value, int size,
        TextAnchor alignment, float height, Color color)
    {
        GameObject textObject = CreateUiObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void StretchWithPadding(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void LoadValuesFromSolver()
    {
        lengthField.text = FormatNumber(solver.DomainLength);
        durationField.text = FormatNumber(solver.SimulationDuration);
        speedField.text = FormatNumber(solver.WaveSpeed);

        conditionIndex = 0;
        for (int i = 0; i < Conditions.Length; i++)
        {
            if (Conditions[i] == solver.InitialCondition)
            {
                conditionIndex = i;
                break;
            }
        }

        conditionButtonText.text = ConditionLabels[conditionIndex];
        modeAField.text = solver.ModeA.ToString(CultureInfo.InvariantCulture);
        modeBField.text = solver.ModeB.ToString(CultureInfo.InvariantCulture);
        UpdateModeVisibility();
        dampingEnabled = solver.DampingEnabled;
        excitationEnabled = solver.ExcitationEnabled;
        gammaField.text = FormatNumber(solver.DampingGamma);
        frequencyMultiplierField.text = FormatNumber(solver.ExcitationFrequencyMultiplier);
        UpdateToggleVisuals();
        UpdateFrequencyInfo();
    }

    private void CycleCondition()
    {
        conditionIndex = (conditionIndex + 1) % Conditions.Length;
        conditionButtonText.text = ConditionLabels[conditionIndex];
        UpdateModeVisibility();
        UpdateFrequencyInfo();
        ScheduleAutomaticSubmit();
    }

    private void UpdateModeVisibility()
    {
        modeRow.SetActive(Conditions[conditionIndex] == "modo");
    }

    private void ToggleDamping()
    {
        dampingEnabled = !dampingEnabled;
        UpdateToggleVisuals();
        ScheduleAutomaticSubmit();
    }

    private void ToggleExcitation()
    {
        excitationEnabled = !excitationEnabled;
        UpdateToggleVisuals();
        UpdateFrequencyInfo();
        ScheduleAutomaticSubmit();
    }

    private void UpdateToggleVisuals()
    {
        dampingButtonText.text = dampingEnabled ? "Amort.: SI" : "Amort.: NO";
        excitationButtonText.text = excitationEnabled ? "Excitacion: SI" : "Excitacion: NO";
        dampingButton.GetComponent<Image>().color = dampingEnabled ? accentColor : fieldColor;
        excitationButton.GetComponent<Image>().color = excitationEnabled ? accentColor : fieldColor;
    }

    private void UpdateFrequencyInfo()
    {
        if (frequencyInfoText == null)
            return;

        float length = solver.DomainLength;
        float speed = solver.WaveSpeed;
        float multiplier = solver.ExcitationFrequencyMultiplier;
        int selectedModeA = Conditions[conditionIndex] == "modo" ? solver.ModeA : 1;
        int selectedModeB = Conditions[conditionIndex] == "modo" ? solver.ModeB : 1;
        if (TryParseNumber(lengthField.text, out float parsedLength))
            length = parsedLength;
        if (TryParseNumber(speedField.text, out float parsedSpeed))
            speed = parsedSpeed;
        if (TryParseNumber(frequencyMultiplierField.text, out float parsedMultiplier))
            multiplier = parsedMultiplier;
        if (Conditions[conditionIndex] == "modo")
        {
            if (TryParseModeIndex(modeAField.text, out int parsedModeA))
                selectedModeA = parsedModeA;
            if (TryParseModeIndex(modeBField.text, out int parsedModeB))
                selectedModeB = parsedModeB;
        }

        if (length <= 0f)
        {
            frequencyInfoText.text = "Frecuencia natural: completa L y c.";
            return;
        }

        float naturalFrequency = speed * Mathf.Sqrt(
            selectedModeA * selectedModeA + selectedModeB * selectedModeB) / (2f * length);
        float excitationFrequency = multiplier * naturalFrequency;
        string modeLabel = $"f{selectedModeA}{selectedModeB}";
        frequencyInfoText.text = excitationEnabled
            ? $"Seno: {modeLabel} = {FormatNumber(naturalFrequency)} Hz  |  fexc = {FormatNumber(excitationFrequency)} Hz"
            : $"Frecuencia natural {modeLabel} = {FormatNumber(naturalFrequency)} Hz";
    }

    private void ScheduleAutomaticSubmit()
    {
        if (autoSubmitRoutine != null)
            StopCoroutine(autoSubmitRoutine);
        autoSubmitRoutine = StartCoroutine(SubmitAfterDelay());
    }

    private IEnumerator SubmitAfterDelay()
    {
        SetStatus("Cambio detectado. Preparando actualizacion...", false);
        yield return new WaitForSecondsRealtime(0.8f);
        autoSubmitRoutine = null;
        SubmitValues();
    }

    private void SubmitNow()
    {
        if (autoSubmitRoutine != null)
        {
            StopCoroutine(autoSubmitRoutine);
            autoSubmitRoutine = null;
        }

        SubmitValues();
    }

    private void SubmitValues()
    {
        if (!TryParseNumber(lengthField.text, out float length) ||
            !TryParseNumber(durationField.text, out float duration) ||
            !TryParseNumber(speedField.text, out float speed) ||
            !TryParseNumber(gammaField.text, out float gamma) ||
            !TryParseNumber(frequencyMultiplierField.text, out float frequencyMultiplier))
        {
            SetStatus("Completa todos los valores numericos.", true);
            return;
        }

        if (!TryParseModeIndex(modeAField.text, out int selectedModeA) ||
            !TryParseModeIndex(modeBField.text, out int selectedModeB))
        {
            SetStatus("Los indices del modo a y b deben ser enteros entre 1 y 5.", true);
            return;
        }

        if (!solver.TryUpdateParameters(
            length, duration, speed, Conditions[conditionIndex],
            selectedModeA, selectedModeB,
            dampingEnabled, gamma, excitationEnabled, frequencyMultiplier,
            out string error))
        {
            SetStatus(error, true);
            return;
        }

        SetStatus("Parametros cargados. Enviando al servidor...", false);
    }

    private void OnSolverStatusChanged(string message, bool isError)
    {
        SetStatus(message, isError);

        if (!isError && message.StartsWith("Onda actualizada"))
            LoadValuesFromSolver();
    }

    private void BeginInitialConditionEdit()
    {
        if (!solver.BeginInitialConditionEdit(out string error))
            SetStatus(error, true);
    }

    private void SubmitInitialConditionEdit()
    {
        if (!solver.SubmitInitialConditionEdit(out string error))
            SetStatus(error, true);
    }

    private void OnInitialConditionEditChanged(bool active, string message)
    {
        if (active)
        {
            if (!editPanel.activeSelf)
            {
                parametersWereVisible = panel.activeSelf;
                heightWasVisible = heightPanel.activeSelf;
            }

            panel.SetActive(false);
            showButton.SetActive(false);
            heightPanel.SetActive(false);
            heightShowButton.SetActive(false);
            editButton.SetActive(false);
            editPanel.SetActive(true);
            editCrosshair.SetActive(true);
            editInstructionText.text = message;
            return;
        }

        editPanel.SetActive(false);
        editCrosshair.SetActive(false);
        editButton.SetActive(true);
        panel.SetActive(parametersWereVisible);
        showButton.SetActive(!parametersWereVisible);
        heightPanel.SetActive(heightWasVisible);
        heightShowButton.SetActive(!heightWasVisible);
    }

    private void SetStatus(string message, bool isError)
    {
        if (statusText == null)
            return;

        statusText.text = message;
        statusText.color = isError
            ? new Color(1f, 0.48f, 0.42f)
            : new Color(0.72f, 0.90f, 1f);
    }

    private void SetPanelVisible(bool visible)
    {
        panel.SetActive(visible);
        showButton.SetActive(!visible);
    }

    private void SetHeightControlVisible(bool visible)
    {
        heightPanel.SetActive(visible);
        heightShowButton.SetActive(!visible);
    }

    private void SetPlateHeight(float offset)
    {
        Vector3 position = solver.transform.position;
        position.y = initialPlateHeight + offset;
        solver.transform.position = position;

        string sign = offset > 0.005f ? "+" : string.Empty;
        heightValueText.text = sign + FormatNumber(offset) + " m";
    }

    private static bool TryParseNumber(string value, out float result)
    {
        string normalized = (value ?? string.Empty).Trim().Replace(',', '.');
        return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseModeIndex(string value, out int result)
    {
        result = 0;
        if (!TryParseNumber(value, out float parsed))
            return false;

        int rounded = Mathf.RoundToInt(parsed);
        if (!Mathf.Approximately(parsed, rounded) || rounded < 1 || rounded > 5)
            return false;

        result = rounded;
        return true;
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    private void OnDestroy()
    {
        if (solver != null)
        {
            solver.StatusChanged -= OnSolverStatusChanged;
            solver.InitialConditionEditChanged -= OnInitialConditionEditChanged;
        }

        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Construye el menu de ingreso con una composicion adaptable a pantallas
/// Android horizontales, incluyendo la zona segura de camaras y recortes.
/// </summary>
public sealed class ResponsiveMenuUI : MonoBehaviour
{
    private static readonly Color BackgroundBottom = Html("#061426");
    private static readonly Color BackgroundTop = Html("#123A68");
    private static readonly Color CardColor = new Color(0.035f, 0.11f, 0.20f, 0.96f);
    private static readonly Color FieldColor = new Color(0.08f, 0.18f, 0.29f, 0.98f);
    private static readonly Color Cyan = Html("#28B8F5");
    private static readonly Color Turquoise = Html("#24D6B0");
    private static readonly Color PrimaryText = Html("#F4F8FC");
    private static readonly Color SecondaryText = Html("#A9BCD0");
    private static readonly Color Success = Html("#24D68A");
    private static readonly Color Error = Html("#FF5E6C");

    private MenuController controller;
    private RectTransform safeArea;
    private RectTransform brandColumn;
    private RectTransform card;
    private GameObject manualInputGroup;
    private TMP_Text manualLinkLabel;
    private TMP_Text statusText;
    private Image statusDot;
    private Button connectButton;
    private TMP_Text connectButtonLabel;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private bool manualInputVisible;

    private static Sprite roundedSprite;
    private static Sprite circularSprite;

    public static ResponsiveMenuUI Build(MenuController target)
    {
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MenuController: no se encontro el Canvas del menu.");
            return null;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        // El controlador se conserva. Los controles antiguos se reemplazan
        // por una jerarquia que usa anclajes y escalado uniforme.
        for (int index = canvas.transform.childCount - 1; index >= 0; index--)
        {
            Transform child = canvas.transform.GetChild(index);
            if (child != target.transform)
                Destroy(child.gameObject);
        }

        GameObject root = CreateRect("Responsive Menu", canvas.transform);
        Stretch(root.GetComponent<RectTransform>());
        ResponsiveMenuUI view = root.AddComponent<ResponsiveMenuUI>();
        view.controller = target;
        view.BuildVisualTree();
        return view;
    }

    private void BuildVisualTree()
    {
        MenuGradientGraphic background = gameObject.AddComponent<MenuGradientGraphic>();
        background.BottomColor = BackgroundBottom;
        background.TopColor = BackgroundTop;
        background.raycastTarget = false;

        GameObject ambient = CreateRect("Ambient Glow", transform);
        Stretch(ambient.GetComponent<RectTransform>());
        Image ambientImage = ambient.AddComponent<Image>();
        ambientImage.color = new Color(0.02f, 0.32f, 0.62f, 0.10f);
        ambientImage.raycastTarget = false;

        GameObject safeObject = CreateRect("Safe Area", transform);
        safeArea = safeObject.GetComponent<RectTransform>();
        Stretch(safeArea);

        GameObject brand = CreateRect("Application Identity", safeArea);
        brandColumn = brand.GetComponent<RectTransform>();

        TMP_Text upperTitle = CreateText("Main Title", brandColumn,
            "SIMULADOR DE", 58f, FontStyles.Bold, PrimaryText, TextAlignmentOptions.BottomLeft);
        SetAnchors(upperTitle.rectTransform, 0f, 0.69f, 1f, 0.88f, 0f, 0f, 0f, 0f);

        TMP_Text lowerTitle = CreateText("Accent Title", brandColumn,
            "ECUACIONES 2D", 72f, FontStyles.Bold, Cyan, TextAlignmentOptions.TopLeft);
        SetAnchors(lowerTitle.rectTransform, 0f, 0.50f, 1f, 0.71f, 0f, 0f, 0f, 0f);

        TMP_Text subtitle = CreateText("Subtitle", brandColumn,
            "Onda  •  Calor  •  Modos de vibración", 28f, FontStyles.Normal,
            SecondaryText, TextAlignmentOptions.TopLeft);
        SetAnchors(subtitle.rectTransform, 0f, 0.42f, 1f, 0.51f, 0f, 0f, 0f, 0f);

        GameObject waveObject = CreateRect("Scientific Wave", brandColumn);
        RectTransform waveRect = waveObject.GetComponent<RectTransform>();
        SetAnchors(waveRect, -0.04f, -0.04f, 1.06f, 0.44f, 0f, 0f, 0f, 0f);
        MenuWaveGraphic wave = waveObject.AddComponent<MenuWaveGraphic>();
        wave.color = Cyan;
        wave.raycastTarget = false;

        BuildCard();
        ApplySafeAreaAndLayout(true);
    }

    private void BuildCard()
    {
        GameObject cardBorder = CreateRect("Login Card Border", safeArea);
        card = cardBorder.GetComponent<RectTransform>();
        Image borderImage = cardBorder.AddComponent<Image>();
        borderImage.sprite = GetRoundedSprite();
        borderImage.type = Image.Type.Sliced;
        borderImage.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.46f);
        borderImage.raycastTarget = false;

        Shadow shadow = cardBorder.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(0f, -12f);

        GameObject cardSurface = CreateRect("Card Surface", card);
        RectTransform surfaceRect = cardSurface.GetComponent<RectTransform>();
        Stretch(surfaceRect, 2.5f, 2.5f, 2.5f, 2.5f);
        Image surface = cardSurface.AddComponent<Image>();
        surface.sprite = GetRoundedSprite();
        surface.type = Image.Type.Sliced;
        surface.color = CardColor;

        GameObject contentObject = CreateRect("Card Content", surfaceRect);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        Stretch(content, 56f, 56f, 42f, 42f);

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text heading = CreateText("Card Heading", content,
            "Ingreso del estudiante", 42f, FontStyles.Bold, PrimaryText,
            TextAlignmentOptions.Center);
        SetLayout(heading.gameObject, 72f);

        AddSpacer(content, 4f);
        CreateLabel(content, "Nombre");
        controller.inputNombre = CreateInput(content, "Ingresá tu nombre", false, 78f);

        AddSpacer(content, 5f);
        CreateLabel(content, "Legajo");
        controller.inputLegajo = CreateInput(content, "Ingresá tu legajo", true, 78f);

        AddSpacer(content, 10f);
        CreateServerStatus(content);

        Button manualButton = CreateTextButton(content, "Ingresar dirección manual", 34f);
        manualLinkLabel = manualButton.GetComponentInChildren<TMP_Text>();
        manualButton.onClick.AddListener(ToggleManualInput);
        SetLayout(manualButton.gameObject, 42f);

        manualInputGroup = CreateRect("Manual Server Address", content);
        SetLayout(manualInputGroup, 78f);
        controller.inputIP = CreateInput(manualInputGroup.transform,
            "Ej.: 192.168.1.20:8080", false, 78f);
        Stretch(controller.inputIP.GetComponent<RectTransform>());
        controller.inputIP.contentType = TMP_InputField.ContentType.Standard;
        manualInputGroup.SetActive(false);

        statusText = CreateText("Connection Message", content, string.Empty, 22f,
            FontStyles.Normal, SecondaryText, TextAlignmentOptions.Center);
        statusText.textWrappingMode = TextWrappingModes.Normal;
        SetLayout(statusText.gameObject, 48f, 1f);
        controller.textoError = statusText;

        connectButton = CreatePrimaryButton(content, out connectButtonLabel);
        connectButton.onClick.AddListener(controller.Ingresar);
        SetLayout(connectButton.gameObject, 88f);
    }

    private void CreateServerStatus(Transform parent)
    {
        GameObject row = CreateRect("Automatic Discovery Status", parent);
        SetLayout(row, 48f);
        HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 16f;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;

        GameObject dotObject = CreateRect("Status Dot", row.transform);
        statusDot = dotObject.AddComponent<Image>();
        statusDot.sprite = GetCircularSprite();
        statusDot.color = Cyan;
        statusDot.raycastTarget = false;
        LayoutElement dotLayout = dotObject.AddComponent<LayoutElement>();
        dotLayout.preferredWidth = 22f;
        dotLayout.preferredHeight = 22f;

        TMP_Text status = CreateText("Status Label", row.transform,
            "Servidor: detección automática", 25f, FontStyles.Normal,
            PrimaryText, TextAlignmentOptions.MidlineLeft);
        LayoutElement textLayout = status.gameObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.preferredHeight = 46f;
    }

    private void ToggleManualInput()
    {
        manualInputVisible = !manualInputVisible;
        manualInputGroup.SetActive(manualInputVisible);
        manualLinkLabel.text = manualInputVisible
            ? "Usar detección automática"
            : "Ingresar dirección manual";

        if (!manualInputVisible)
            controller.inputIP.text = string.Empty;

        Canvas.ForceUpdateCanvases();
    }

    public void SetStatus(string message, bool isError = false, bool isSuccess = false)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = isError ? Error : isSuccess ? Success : SecondaryText;
        }

        if (statusDot != null)
            statusDot.color = isError ? Error : isSuccess ? Success : Cyan;
    }

    public void SetBusy(bool busy)
    {
        if (connectButton == null)
            return;

        connectButton.interactable = !busy;
        connectButtonLabel.text = busy ? "CONECTANDO..." : "CONECTAR";
        controller.inputNombre.interactable = !busy;
        controller.inputLegajo.interactable = !busy;
        controller.inputIP.interactable = !busy;
    }

    private void Update()
    {
        ApplySafeAreaAndLayout(false);
    }

    private void ApplySafeAreaAndLayout(bool force)
    {
        Rect currentSafeArea = Screen.safeArea;
        Vector2Int currentScreen = new Vector2Int(Screen.width, Screen.height);
        if (!force && currentSafeArea == lastSafeArea && currentScreen == lastScreenSize)
            return;

        lastSafeArea = currentSafeArea;
        lastScreenSize = currentScreen;

        if (Screen.width > 0 && Screen.height > 0)
        {
            safeArea.anchorMin = new Vector2(
                currentSafeArea.xMin / Screen.width,
                currentSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(
                currentSafeArea.xMax / Screen.width,
                currentSafeArea.yMax / Screen.height);
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }

        float aspect = currentSafeArea.height > 0f
            ? currentSafeArea.width / currentSafeArea.height
            : 16f / 9f;

        if (aspect >= 1.65f)
        {
            SetAnchors(brandColumn, 0.045f, 0.10f, 0.49f, 0.91f, 0f, 0f, 0f, 0f);
            SetAnchors(card, 0.535f, 0.075f, 0.955f, 0.925f, 0f, 0f, 0f, 0f);
        }
        else
        {
            // Los formatos cercanos a 4:3 destinan mas ancho al formulario,
            // sin perder la identidad visual de la columna izquierda.
            SetAnchors(brandColumn, 0.035f, 0.12f, 0.415f, 0.90f, 0f, 0f, 0f, 0f);
            SetAnchors(card, 0.435f, 0.065f, 0.97f, 0.935f, 0f, 0f, 0f, 0f);
        }
    }

    private static TMP_InputField CreateInput(Transform parent, string placeholderText,
        bool numeric, float height)
    {
        GameObject borderObject = CreateRect("Input", parent);
        SetLayout(borderObject, height);
        Image border = borderObject.AddComponent<Image>();
        border.sprite = GetRoundedSprite();
        border.type = Image.Type.Sliced;
        border.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f);

        GameObject surfaceObject = CreateRect("Input Surface", borderObject.transform);
        RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
        Stretch(surfaceRect, 2f, 2f, 2f, 2f);
        Image surface = surfaceObject.AddComponent<Image>();
        surface.sprite = GetRoundedSprite();
        surface.type = Image.Type.Sliced;
        surface.color = FieldColor;

        GameObject viewportObject = CreateRect("Text Area", surfaceObject.transform);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport, 24f, 24f, 9f, 9f);
        viewportObject.AddComponent<RectMask2D>();

        TMP_Text placeholder = CreateText("Placeholder", viewport,
            placeholderText, 25f, FontStyles.Normal,
            new Color(SecondaryText.r, SecondaryText.g, SecondaryText.b, 0.82f),
            TextAlignmentOptions.MidlineLeft);
        Stretch(placeholder.rectTransform);

        TMP_Text text = CreateText("Text", viewport, string.Empty, 27f,
            FontStyles.Normal, PrimaryText, TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform);

        TMP_InputField input = borderObject.AddComponent<TMP_InputField>();
        input.targetGraphic = surface;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = numeric
            ? TMP_InputField.ContentType.IntegerNumber
            : TMP_InputField.ContentType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.caretColor = Cyan;
        input.selectionColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f);
        return input;
    }

    private static void CreateLabel(Transform parent, string value)
    {
        TMP_Text label = CreateText(value + " Label", parent, value, 25f,
            FontStyles.Normal, SecondaryText, TextAlignmentOptions.BottomLeft);
        SetLayout(label.gameObject, 34f);
    }

    private static Button CreateTextButton(Transform parent, string label, float fontSize)
    {
        GameObject buttonObject = CreateRect(label, parent);
        Image transparentTarget = buttonObject.AddComponent<Image>();
        transparentTarget.color = Color.clear;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = transparentTarget;
        TMP_Text text = CreateText("Label", buttonObject.transform, label, fontSize,
            FontStyles.Underline, Cyan, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private static Button CreatePrimaryButton(Transform parent, out TMP_Text label)
    {
        GameObject buttonObject = CreateRect("Connect Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = Turquoise;

        Shadow shadow = buttonObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f);
        shadow.effectDistance = new Vector2(0f, -6f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Turquoise;
        colors.highlightedColor = Cyan;
        colors.pressedColor = new Color(0.09f, 0.64f, 0.68f, 1f);
        colors.disabledColor = new Color(0.25f, 0.42f, 0.47f, 0.75f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        label = CreateText("Button Label", buttonObject.transform, "CONECTAR", 31f,
            FontStyles.Bold, PrimaryText, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value,
        float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateRect(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(14f, fontSize * 0.68f);
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = 5;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void AddSpacer(Transform parent, float height)
    {
        GameObject spacer = CreateRect("Spacer", parent);
        SetLayout(spacer, height);
    }

    private static void SetLayout(GameObject target, float preferredHeight, float flexibleHeight = 0f)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.flexibleHeight = flexibleHeight;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f,
        float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetAnchors(RectTransform rect, float minX, float minY,
        float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite == null)
            roundedSprite = CreateRoundedSprite(64, 14f, 18f);
        return roundedSprite;
    }

    private static Sprite GetCircularSprite()
    {
        if (circularSprite == null)
            circularSprite = CreateRoundedSprite(48, 23f, 12f);
        return circularSprite;
    }

    private static Sprite CreateRoundedSprite(int size, float radius, float border)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Rounded UI",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, 0f, x - (size - 1f - radius));
                float dy = Mathf.Max(radius - y, 0f, y - (size - 1f - radius));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01(distance - radius + 1f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    private static Color Html(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }
}

/// <summary>Fondo vertical de dos colores sin depender de una textura.</summary>
public sealed class MenuGradientGraphic : MaskableGraphic
{
    public Color BottomColor { get; set; } = Color.black;
    public Color TopColor { get; set; } = Color.blue;

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect area = rectTransform.rect;
        helper.AddVert(new Vector3(area.xMin, area.yMin), BottomColor, Vector2.zero);
        helper.AddVert(new Vector3(area.xMin, area.yMax), TopColor, Vector2.up);
        helper.AddVert(new Vector3(area.xMax, area.yMax), TopColor, Vector2.one);
        helper.AddVert(new Vector3(area.xMax, area.yMin), BottomColor, Vector2.right);
        helper.AddTriangle(0, 1, 2);
        helper.AddTriangle(0, 2, 3);
    }
}

/// <summary>Malla de ondas decorativa, liviana y escalable.</summary>
public sealed class MenuWaveGraphic : MaskableGraphic
{
    private const int Segments = 54;
    private float phase;

    private void Update()
    {
        phase += Time.unscaledDeltaTime * 0.22f;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect area = rectTransform.rect;
        float lineWidth = Mathf.Max(1.2f, area.height * 0.0045f);

        for (int curve = 0; curve < 9; curve++)
        {
            float depth = curve / 8f;
            float baseY = Mathf.Lerp(area.yMin + area.height * 0.10f,
                area.yMin + area.height * 0.42f, depth);
            float amplitude = area.height * Mathf.Lerp(0.22f, 0.10f, depth);
            Color curveColor = color;
            curveColor.a = Mathf.Lerp(0.58f, 0.12f, depth);

            Vector2 previous = EvaluatePoint(area, 0f, baseY, amplitude, curve);
            for (int segment = 1; segment <= Segments; segment++)
            {
                float t = segment / (float)Segments;
                Vector2 current = EvaluatePoint(area, t, baseY, amplitude, curve);
                AddLine(helper, previous, current, lineWidth, curveColor);
                previous = current;
            }
        }
    }

    private Vector2 EvaluatePoint(Rect area, float t, float baseY, float amplitude, int curve)
    {
        float envelope = 0.56f + 0.44f * Mathf.Sin(t * Mathf.PI);
        float wave = Mathf.Sin(t * Mathf.PI * 3.15f + phase + curve * 0.31f);
        float secondary = Mathf.Sin(t * Mathf.PI * 6.1f - phase * 0.7f) * 0.18f;
        return new Vector2(
            Mathf.Lerp(area.xMin, area.xMax, t),
            baseY + (wave + secondary) * amplitude * envelope);
    }

    private static void AddLine(VertexHelper helper, Vector2 start, Vector2 end,
        float width, Color lineColor)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int index = helper.currentVertCount;
        helper.AddVert(start - normal, lineColor, Vector2.zero);
        helper.AddVert(start + normal, lineColor, Vector2.up);
        helper.AddVert(end + normal, lineColor, Vector2.one);
        helper.AddVert(end - normal, lineColor, Vector2.right);
        helper.AddTriangle(index, index + 1, index + 2);
        helper.AddTriangle(index, index + 2, index + 3);
    }
}

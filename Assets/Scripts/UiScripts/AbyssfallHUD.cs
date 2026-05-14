using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Attach to any persistent GameObject ("HUDManager").
/// Right-click component header → "Rebuild Canvas" to regenerate UI.
[ExecuteAlways]
public class AbyssfallHUD : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerStats stats;

    [Header("Assets")]
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Sprite hpFillSprite;
    [SerializeField] private Sprite manaFillSprite;
    [SerializeField] private Sprite staminaFillSprite;

    // ── Live refs ─────────────────────────────────────────────────
    [SerializeField, HideInInspector] private Image hpFill;
    [SerializeField, HideInInspector] private Image manaFill;
    [SerializeField, HideInInspector] private Image staminaFill;
    [SerializeField, HideInInspector] private Text hpText;
    [SerializeField, HideInInspector] private Text manaText;
    [SerializeField, HideInInspector] private Text staminaText;

    private Canvas hudCanvas;

    // =========================================================
    // Lifecycle
    // =========================================================

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (stats == null)
            stats = FindFirstObjectByType<PlayerStats>();

        LoadAssets();

        if (transform.Find("HUDCanvas") == null)
            BuildCanvas();
        else
            hudCanvas = GetComponentInChildren<Canvas>();
    }

    void LoadAssets()
    {
#if UNITY_EDITOR
        if (frameSprite == null) frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Sprites/HUD_Bar_Frame.png");
        if (hpFillSprite == null) hpFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Sprites/HUD_Bar_Fill_Red.png");
        if (manaFillSprite == null) manaFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Sprites/HUD_Bar_Fill_Blue.png");
        if (staminaFillSprite == null) staminaFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Sprites/HUD_Bar_Fill_Stamina.png");
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (hudCanvas == null) Initialize();
            return;
        }
#endif
        if (stats == null || hudCanvas == null) return;
        Tick();
    }

    // =========================================================
    // Tick
    // =========================================================

    void Tick()
    {
        UpdateBar(hpFill, hpText, stats.currentHp, stats.maxHp);
        UpdateBar(manaFill, manaText, stats.currentMana, stats.maxMana);
        UpdateBar(staminaFill, staminaText, stats.CurrentStamina, stats.MaxStamina);
    }

    void UpdateBar(Image fill, Text text, float current, float max)
    {
        if (fill != null && max > 0f)
            fill.fillAmount = Mathf.Clamp01(current / max);
        
        if (text != null)
            text.text = $"{(int)current} / {(int)max}";
    }

    // =========================================================
    // Builder
    // =========================================================

    [ContextMenu("Rebuild Canvas")]
    public void BuildCanvas()
    {
        LoadAssets();

        var old = transform.Find("HUDCanvas");
        if (old != null) DestroyImmediate(old.gameObject);

        var cgo = new GameObject("HUDCanvas");
        cgo.transform.SetParent(transform, false);

        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 100;
        hudCanvas = cv;

        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;

        cgo.AddComponent<GraphicRaycaster>();

        // Container for bars - positioned top-left with padding
        var container = GO("Bars_Container", cgo.transform);
        SA(container, 0, 1, 0, 1, 50, -250, 450, -50);

        hpFill = CreateBar("HP_Bar", container.transform, hpFillSprite, new Vector2(0, 0), out hpText);
        manaFill = CreateBar("Mana_Bar", container.transform, manaFillSprite, new Vector2(0, -70), out manaText);
        staminaFill = CreateBar("Stamina_Bar", container.transform, staminaFillSprite, new Vector2(0, -140), out staminaText);
    }

    Image CreateBar(string name, Transform parent, Sprite fillSprite, Vector2 pos, out Text label)
    {
        var barRoot = GO(name, parent);
        var rt = barRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 60);

        // Frame
        var frame = GO("Frame", barRoot.transform).AddComponent<Image>();
        frame.sprite = frameSprite;
        frame.raycastTarget = false;
        Stretch(frame.gameObject);

        // Fill Area (masking not strictly needed if fill sprite matches well, but we use offsets)
        // Offset the fill slightly to sit inside the frame borders
        var fillArea = GO("FillArea", barRoot.transform);
        SA(fillArea, 0, 0, 1, 1, 40, 15, -40, -15);

        var fill = GO("Fill", fillArea.transform).AddComponent<Image>();
        fill.sprite = fillSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.raycastTarget = false;
        Stretch(fill.gameObject);

        // Text in the center
        label = GO("ValueText", barRoot.transform).AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 22;
        label.color = Color.white;
        label.raycastTarget = false;
        
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(1, -1);
        shadow.effectColor = new Color(0, 0, 0, 0.75f);
        
        Stretch(label.gameObject);

        return fill;
    }

    // ── Primitive factories ───────────────────────────────────────

    static GameObject GO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void SA(GameObject go, float x0, float y0, float x1, float y1, float l, float b, float r, float t)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(r, t);
    }

    static void Stretch(GameObject go) => SA(go, 0, 0, 1, 1, 0, 0, 0, 0);
}

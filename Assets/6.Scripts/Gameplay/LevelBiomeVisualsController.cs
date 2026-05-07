using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-80)]
public sealed class LevelBiomeVisualsController : MonoBehaviour
{
    public static LevelBiomeVisualsController Instance { get; private set; }

    private const string FloorVisualsName = "FloorVisuals";
    private const string StripCanvasName = "StripUICanvas";
    private const string OverlayName = "BiomeCaveOverlay";

    private static readonly Color CaveOverlayColor = new Color(0f, 0f, 0f, 0.2f);

    [Header("Floor Tint By Biome")]
    [SerializeField] private Color noneFloorTint = Color.white;
    [SerializeField] private Color forestFloorTint = new Color(0.36f, 0.52f, 0.34f, 1f);
    [SerializeField] private Color plainsFloorTint = new Color(0.62f, 0.69f, 0.44f, 1f);
    [SerializeField] private Color desertFloorTint = new Color(0.78f, 0.66f, 0.38f, 1f);
    [SerializeField] private Color oceanFloorTint = new Color(0.30f, 0.50f, 0.60f, 1f);
    [SerializeField] private Color mountainFloorTint = new Color(0.50f, 0.50f, 0.52f, 1f);
    [SerializeField] private Color caveFloorTint = new Color(0.26f, 0.26f, 0.28f, 1f);
    [SerializeField] private Color swampFloorTint = new Color(0.34f, 0.44f, 0.30f, 1f);
    [SerializeField] private Color urbanFloorTint = new Color(0.48f, 0.48f, 0.50f, 1f);
    [SerializeField] private Color dungeonInteriorFloorTint = new Color(0.33f, 0.31f, 0.34f, 1f);
    [SerializeField] private Color tundraFloorTint = new Color(0.70f, 0.76f, 0.80f, 1f);
    [SerializeField] private Color customFloorTint = new Color(0.56f, 0.46f, 0.60f, 1f);

    [Header("Cave Overlay")]
    [SerializeField, Min(0f)] private float caveOverlayBaseAlpha = 0.2f;
    [SerializeField, Min(0.02f)] private float caveFlickerFlashMinDuration = 0.08f;
    [SerializeField, Min(0.02f)] private float caveFlickerFlashMaxDuration = 0.18f;
    [SerializeField, Min(0.02f)] private float caveFlickerRecoverMinDuration = 0.20f;
    [SerializeField, Min(0.02f)] private float caveFlickerRecoverMaxDuration = 0.45f;
    [SerializeField, Min(0.02f)] private float caveFlickerPauseMinDuration = 2.2f;
    [SerializeField, Min(0.02f)] private float caveFlickerPauseMaxDuration = 4.2f;
    [SerializeField, Range(0f, 1f)] private float caveFlickerFlashToNormalChance = 0.45f;

    private GameplayLevelBootstrapper _bootstrapper;
    private LevelBiome _activeBiome = LevelBiome.None;
    private Image _caveOverlayImage;
    private CanvasGroup _caveOverlayGroup;
    private float _caveFlickerPhaseTimer;
    private float _caveFlickerPhaseDuration;
    private float _caveFlickerFromAlpha;
    private float _caveFlickerToAlpha;
    private bool _caveFlickerReturningToBase = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterAutoAttach()
    {
        SceneManager.sceneLoaded += (_, _) => AutoAttachToBootstrapper();
        AutoAttachToBootstrapper();
    }

    private static void AutoAttachToBootstrapper()
    {
        // Respect any manually placed controller in the scene.
        LevelBiomeVisualsController existing =
            Object.FindFirstObjectByType<LevelBiomeVisualsController>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameplayLevelBootstrapper bootstrapper =
            Object.FindFirstObjectByType<GameplayLevelBootstrapper>(FindObjectsInactive.Include);
        if (!bootstrapper)
            return;

        if (!bootstrapper.GetComponent<LevelBiomeVisualsController>())
            bootstrapper.gameObject.AddComponent<LevelBiomeVisualsController>();
    }

    private void Awake()
    {
        _bootstrapper = GetComponent<GameplayLevelBootstrapper>();
        if (_bootstrapper == null)
            _bootstrapper = Object.FindFirstObjectByType<GameplayLevelBootstrapper>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        // If a non-bootstrapper controller exists (e.g. manually placed on WorldManager), let it win.
        if (_bootstrapper != null)
        {
            LevelBiomeVisualsController[] all =
                Object.FindObjectsByType<LevelBiomeVisualsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                LevelBiomeVisualsController other = all[i];
                if (other == null || other == this)
                    continue;
                if (other.GetComponent<GameplayLevelBootstrapper>() == null)
                {
                    enabled = false;
                    return;
                }
            }
        }

        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        Instance = this;

        if (_bootstrapper != null)
            _bootstrapper.OnLevelStarted += HandleLevelStarted;
        ToggleSettingsStore.Changed += HandleToggleSettingsChanged;

        ApplyBiomeVisuals(ResolveActiveDefinition());
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        if (_bootstrapper != null)
            _bootstrapper.OnLevelStarted -= HandleLevelStarted;
        ToggleSettingsStore.Changed -= HandleToggleSettingsChanged;
    }

    private void Update()
    {
        if (_activeBiome != LevelBiome.Cave || _caveOverlayImage == null)
            return;
        TickCaveFlicker();
    }

    private void HandleLevelStarted(MapNodeDefinition def)
    {
        ApplyBiomeVisuals(def);
    }

    private void HandleToggleSettingsChanged(ToggleSettingId id, bool _)
    {
        if (id != ToggleSettingId.DisableScreenOverlayVisuals)
            return;

        EnsureCaveOverlayState(_activeBiome == LevelBiome.Cave);
    }

    private MapNodeDefinition ResolveActiveDefinition()
    {
        if (_bootstrapper != null && _bootstrapper.ActiveDefinition != null)
            return _bootstrapper.ActiveDefinition;
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition;
        return ActiveLevelContext.Current;
    }

    private void ApplyBiomeVisuals(MapNodeDefinition def)
    {
        _activeBiome = def != null ? def.biome : LevelBiome.None;
        Color tint = ResolveFloorTint(_activeBiome);
        ApplyFloorTintToScene(tint);
        EnsureCaveOverlayState(_activeBiome == LevelBiome.Cave);
    }

    private Color ResolveFloorTint(LevelBiome biome)
    {
        return biome switch
        {
            LevelBiome.Forest => forestFloorTint,
            LevelBiome.Plains => plainsFloorTint,
            LevelBiome.Desert => desertFloorTint,
            LevelBiome.Ocean => oceanFloorTint,
            LevelBiome.Mountain => mountainFloorTint,
            LevelBiome.Cave => caveFloorTint,
            LevelBiome.Swamp => swampFloorTint,
            LevelBiome.Urban => urbanFloorTint,
            LevelBiome.DungeonInterior => dungeonInteriorFloorTint,
            LevelBiome.Tundra => tundraFloorTint,
            LevelBiome.Custom => customFloorTint,
            _ => noneFloorTint
        };
    }

    private static void ApplyFloorTintToScene(Color tint)
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (!string.Equals(t.name, FloorVisualsName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            SpriteRenderer[] renderers = t.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                SpriteRenderer sr = renderers[r];
                if (!sr)
                    continue;
                sr.color = tint;
            }
        }
    }

    private void EnsureCaveOverlayState(bool enabled)
    {
        if (ToggleSettingsStore.Get(ToggleSettingId.DisableScreenOverlayVisuals))
            enabled = false;

        if (!enabled)
        {
            if (_caveOverlayGroup != null)
                _caveOverlayGroup.alpha = 0f;
            if (_caveOverlayImage != null)
                _caveOverlayImage.enabled = false;
            return;
        }

        EnsureCaveOverlayCreated();
        if (_caveOverlayImage == null || _caveOverlayGroup == null)
            return;

        _caveOverlayImage.enabled = true;
        _caveOverlayGroup.alpha = 1f;
        _caveOverlayGroup.blocksRaycasts = false;
        _caveOverlayGroup.interactable = false;

        Color c = CaveOverlayColor;
        c.a = caveOverlayBaseAlpha;
        _caveOverlayImage.color = c;
        ResetCaveFlickerState();
    }

    private void EnsureCaveOverlayCreated()
    {
        if (_caveOverlayImage != null && _caveOverlayGroup != null)
            return;

        Canvas stripCanvas = ResolveStripCanvas();
        if (stripCanvas == null)
            return;

        Transform existing = stripCanvas.transform.Find(OverlayName);
        GameObject go = existing ? existing.gameObject : new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        if (!existing)
            go.transform.SetParent(stripCanvas.transform, false);

        RectTransform rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.SetAsLastSibling();
        }

        _caveOverlayGroup = go.GetComponent<CanvasGroup>();
        _caveOverlayImage = go.GetComponent<Image>();
        if (_caveOverlayGroup == null)
            _caveOverlayGroup = go.AddComponent<CanvasGroup>();
        if (_caveOverlayImage == null)
            _caveOverlayImage = go.AddComponent<Image>();

        _caveOverlayImage.raycastTarget = false;
        _caveOverlayGroup.blocksRaycasts = false;
        _caveOverlayGroup.interactable = false;
    }

    private void ResetCaveFlickerState()
    {
        _caveFlickerPhaseTimer = 0f;
        _caveFlickerPhaseDuration = Mathf.Max(0.001f, caveFlickerPauseMinDuration);
        _caveFlickerFromAlpha = caveOverlayBaseAlpha;
        _caveFlickerToAlpha = caveOverlayBaseAlpha;
        _caveFlickerReturningToBase = true;
    }

    private void TickCaveFlicker()
    {
        if (_caveOverlayImage == null)
            return;

        _caveFlickerPhaseTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_caveFlickerPhaseTimer / Mathf.Max(0.001f, _caveFlickerPhaseDuration));
        float alpha = Mathf.Lerp(_caveFlickerFromAlpha, _caveFlickerToAlpha, t);
        Color c = CaveOverlayColor;
        c.a = Mathf.Clamp01(alpha);
        _caveOverlayImage.color = c;

        if (_caveFlickerPhaseTimer < _caveFlickerPhaseDuration)
            return;

        _caveFlickerPhaseTimer = 0f;

        if (_caveFlickerReturningToBase)
        {
            _caveFlickerFromAlpha = caveOverlayBaseAlpha;
            bool flashToNormal = Random.value <= Mathf.Clamp01(caveFlickerFlashToNormalChance);
            _caveFlickerToAlpha = flashToNormal ? 0f : Random.Range(0.02f, caveOverlayBaseAlpha * 0.45f);
            _caveFlickerPhaseDuration = Random.Range(
                Mathf.Max(0.01f, caveFlickerFlashMinDuration),
                Mathf.Max(caveFlickerFlashMinDuration, caveFlickerFlashMaxDuration));
            _caveFlickerReturningToBase = false;
        }
        else
        {
            _caveFlickerFromAlpha = _caveOverlayImage.color.a;
            _caveFlickerToAlpha = caveOverlayBaseAlpha;
            float recover = Random.Range(
                Mathf.Max(0.01f, caveFlickerRecoverMinDuration),
                Mathf.Max(caveFlickerRecoverMinDuration, caveFlickerRecoverMaxDuration));
            float pause = Random.Range(
                Mathf.Max(0.01f, caveFlickerPauseMinDuration),
                Mathf.Max(caveFlickerPauseMinDuration, caveFlickerPauseMaxDuration));
            _caveFlickerPhaseDuration = recover + pause;
            _caveFlickerReturningToBase = true;
        }
    }

    private static Canvas ResolveStripCanvas()
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (!string.Equals(t.name, StripCanvasName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            Canvas c = t.GetComponent<Canvas>();
            if (c != null)
                return c;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("UICanvas");
        if (tagged != null)
            return tagged.GetComponent<Canvas>();

        return null;
    }
}

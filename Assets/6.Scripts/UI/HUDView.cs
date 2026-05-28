using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HUDView : MonoBehaviour
{
    [Header("HUD Left Auto Fade")]
    [SerializeField] private bool fadeWhenPlayerOverlaps = true;
    [Tooltip("Also lower alpha when a live enemy overlaps the HUD (same test as the player).")]
    [SerializeField] private bool includeEnemiesInOverlapFade = true;
    [Tooltip("Seconds between enemy list refreshes when enemy overlap fade is enabled.")]
    [SerializeField, Min(0.05f)] private float enemyOverlapScanInterval = 0.25f;
    [SerializeField, Range(0.1f, 1f)] private float overlapAlpha = 0.5f;
    [SerializeField] private Camera overlapCamera;
    [SerializeField] private string overlapCameraName = "StripCamera";

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dpsText;
    [SerializeField] private TMP_Text actionText;

    [Header("HP")]
    [SerializeField] private Image hpFill;
    [Tooltip("Optional overlay; fill is current guard / max(natural cap, current guard).")]
    [SerializeField] private Image guardFill;
    [Tooltip("Optional. Shows current guard / natural cap (e.g. 40/40).")]
    [SerializeField] private TMP_Text guardValueText;
    [SerializeField] private TMP_Text hpValueText;

    [Header("Energy")]
    [SerializeField] private Image energyFill;
    [SerializeField] private TMP_Text energyValueText;

    [Header("Mana")]
    [SerializeField] private Image manaFill;
    [SerializeField] private TMP_Text manaValueText;

    [Header("Attack Delay")]
    [SerializeField] private Image attackDelayFill;
    [SerializeField] private TMP_Text attackDelayValueText;

    [Header("Gather Debuff")]
    [SerializeField] private GameObject gatherDebuffRoot;
    [SerializeField] private TMP_Text gatherDebuffText;

    private RectTransform _selfRect;
    private CanvasGroup _selfCanvasGroup;
    private PlayerController _player;
    private SpriteRenderer[] _playerRenderers = System.Array.Empty<SpriteRenderer>();
    private Canvas _parentCanvas;
    private Camera _hudRectEventCamera;
    private Camera _resolvedOverlapCamera;

    private EnemyBaseController[] _cachedEnemies = System.Array.Empty<EnemyBaseController>();
    private float _nextEnemyScanTime;

    private void Awake()
    {
        _selfRect = transform as RectTransform;
        _parentCanvas = GetComponentInParent<Canvas>();
        _hudRectEventCamera = ResolveHudRectEventCamera();
        _selfCanvasGroup = GetComponent<CanvasGroup>();
        if (!_selfCanvasGroup)
            _selfCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        _resolvedOverlapCamera = ResolveOverlapCamera();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCachePlayer();
        _nextEnemyScanTime = 0f;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _player = null;
        _playerRenderers = System.Array.Empty<SpriteRenderer>();
        _cachedEnemies = System.Array.Empty<EnemyBaseController>();
        _nextEnemyScanTime = 0f;
        _resolvedOverlapCamera = ResolveOverlapCamera();
        TryCachePlayer();
    }

    private void TryCachePlayer()
    {
        if (_player != null)
            return;

        _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        _playerRenderers = System.Array.Empty<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (!fadeWhenPlayerOverlaps || _selfRect == null || _selfCanvasGroup == null)
            return;

        if (_player == null)
            TryCachePlayer();

        RefreshPlayerRenderersIfNeeded();
        bool overlaps = IsPlayerSpriteOverHudRect();
        if (!overlaps && includeEnemiesInOverlapFade)
            overlaps = IsAnyLiveEnemyOverlappingHud();

        _selfCanvasGroup.alpha = overlaps ? overlapAlpha : 1f;
    }

    private bool IsPlayerSpriteOverHudRect()
    {
        if (_selfRect == null || _player == null || _playerRenderers == null || _playerRenderers.Length == 0)
            return false;

        Camera cam = ResolveOverlapCamera();
        if (cam == null)
            return false;

        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            SpriteRenderer sr = _playerRenderers[i];
            if (sr == null || !sr.enabled || sr.sprite == null || !sr.gameObject.activeInHierarchy)
                continue;

            if (IsSpriteRendererOverHudRect(sr, cam))
                return true;
        }

        return false;
    }

    private void RefreshEnemyCacheIfDue()
    {
        if (!includeEnemiesInOverlapFade)
            return;

        if (Time.unscaledTime < _nextEnemyScanTime)
            return;

        _nextEnemyScanTime = Time.unscaledTime + enemyOverlapScanInterval;
        _cachedEnemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private bool IsAnyLiveEnemyOverlappingHud()
    {
        if (_selfRect == null)
            return false;

        Camera cam = ResolveOverlapCamera();
        if (cam == null)
            return false;

        RefreshEnemyCacheIfDue();

        for (int i = 0; i < _cachedEnemies.Length; i++)
        {
            EnemyBaseController e = _cachedEnemies[i];
            if (e == null || e.IsDead || !e.gameObject.activeInHierarchy)
                continue;

            if (IsEnemyVisualOverHudRect(e, cam))
                return true;
        }

        return false;
    }

    private bool IsEnemyVisualOverHudRect(EnemyBaseController enemy, Camera cam)
    {
        Collider2D col = enemy.GetComponentInChildren<Collider2D>(true);
        if (col != null && col.enabled)
        {
            if (IsWorldBoundsOverHudRect(col.bounds, cam))
                return true;
        }

        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null && sr.enabled && sr.sprite != null && sr.gameObject.activeInHierarchy)
            return IsSpriteRendererOverHudRect(sr, cam);

        return false;
    }

    private void RefreshPlayerRenderersIfNeeded()
    {
        if (_player == null)
            return;

        bool needsRefresh = _playerRenderers == null || _playerRenderers.Length == 0;
        if (!needsRefresh)
        {
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                if (_playerRenderers[i] == null)
                {
                    needsRefresh = true;
                    break;
                }
            }
        }

        if (needsRefresh)
            _playerRenderers = _player.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private Camera ResolveOverlapCamera()
    {
        if (overlapCamera != null)
            return overlapCamera;

        if (_resolvedOverlapCamera != null)
            return _resolvedOverlapCamera;

        if (_parentCanvas != null && _parentCanvas.worldCamera != null)
        {
            _resolvedOverlapCamera = _parentCanvas.worldCamera;
            return _resolvedOverlapCamera;
        }

        if (!string.IsNullOrWhiteSpace(overlapCameraName))
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string name = overlapCameraName.Trim();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera c = cameras[i];
                if (c && string.Equals(c.gameObject.name, name, System.StringComparison.Ordinal))
                {
                    _resolvedOverlapCamera = c;
                    return _resolvedOverlapCamera;
                }
            }
        }

        _resolvedOverlapCamera = Camera.main;
        return _resolvedOverlapCamera;
    }

    private Camera ResolveHudRectEventCamera()
    {
        if (_parentCanvas == null)
            return null;

        if (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (_parentCanvas.worldCamera != null)
            return _parentCanvas.worldCamera;

        return Camera.main;
    }

    private bool IsSpriteRendererOverHudRect(SpriteRenderer sr, Camera cam)
    {
        return IsWorldBoundsOverHudRect(sr.bounds, cam);
    }

    private bool IsWorldBoundsOverHudRect(Bounds b, Camera cam)
    {
        Vector3 c = b.center;
        Vector3 e = b.extents;

        Vector3[] points =
        {
            c,
            c + new Vector3(-e.x, -e.y, 0f),
            c + new Vector3(-e.x,  e.y, 0f),
            c + new Vector3( e.x, -e.y, 0f),
            c + new Vector3( e.x,  e.y, 0f)
        };

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 screen = cam.WorldToScreenPoint(points[i]);
            if (screen.z <= 0f)
                continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(_selfRect, screen, _hudRectEventCamera))
                return true;
        }

        return false;
    }

    public void SetNameAndCombatPower(string displayName, float combatPower)
    {
        if (nameText)
            nameText.text = $"{displayName}  <size=65%>CP:{Mathf.RoundToInt(combatPower)}</size>";
    }

    public void SetAction(string action)
    {
        if (actionText) actionText.text = action ?? "";
    }

    public void SetDps(float dps)
    {
        if (!dpsText)
            return;

        if (dps <= 0f)
        {
            dpsText.text = "0 DPS";
            return;
        }

        dpsText.text = $"{dps:0.#} DPS";
    }

    public void SetHP(float current, float max)
    {
        float displayMax = Mathf.Max(1f, max);
        float fill = Mathf.Clamp01(Mathf.Min(current, max) / displayMax);
        if (hpFill) hpFill.fillAmount = fill;
        if (hpValueText) hpValueText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
    }

    /// <summary>
    /// Guard uses the same bar rect as HP; fill only occupies (naturalCap / maxHp) of the width at full guard,
    /// so a small flat cap does not read as a full-length bar.
    /// </summary>
    public void SetGuard(float current, float naturalCap, float maxHp)
    {
        if (guardFill)
        {
            bool showGuard = current > 0.0001f;
            guardFill.gameObject.SetActive(showGuard);
            guardFill.fillAmount = showGuard
                ? ComputeGuardFillAmount(current, naturalCap, maxHp)
                : 0f;
        }

        if (guardValueText)
        {
            if (current <= 0.0001f)
                guardValueText.gameObject.SetActive(false);
            else
            {
                guardValueText.gameObject.SetActive(true);
                guardValueText.text = $"{Mathf.RoundToInt(current)}";
            }
        }
    }

    private static float ComputeGuardFillAmount(float current, float naturalCap, float maxHp)
    {
        if (current <= 0.0001f)
            return 0f;

        float displayCap = naturalCap > 0.0001f ? naturalCap : current;
        if (current > naturalCap)
            displayCap = current;

        float hpD = Mathf.Max(1f, maxHp);
        float guardZone01 = Mathf.Clamp01(displayCap / hpD);
        float guardFill01 = Mathf.Clamp01(current / displayCap);
        return Mathf.Clamp01(guardFill01 * guardZone01);
    }

    public void SetEnergy(float current, float max)
    {
        if (energyFill) energyFill.fillAmount = (max <= 0f) ? 0f : Mathf.Clamp01(current / max);
        if (energyValueText) energyValueText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
    }

    public void SetMana(float current, float max)
    {
        if (manaFill) manaFill.fillAmount = (max <= 0f) ? 0f : Mathf.Clamp01(current / max);
        if (manaValueText) manaValueText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
    }

    public void SetAttackDelay(float normalizedCycle, float attacksPerSecond)
    {
        if (attackDelayFill)
            attackDelayFill.fillAmount = Mathf.Clamp01(normalizedCycle);

        if (attackDelayValueText)
            attackDelayValueText.text = attacksPerSecond > 0f ? $"{attacksPerSecond:0.##} APS" : "0 APS";
    }

    public void SetHealthBarVisible(bool visible)
    {
        if (hpFill) hpFill.gameObject.SetActive(visible);
        if (hpValueText) hpValueText.gameObject.SetActive(visible);
        if (guardFill) guardFill.gameObject.SetActive(visible);
        if (guardValueText) guardValueText.gameObject.SetActive(visible);
    }

    public void SetGatherDebuff(bool active, float speedMultiplier)
    {
        if (!gatherDebuffRoot) return;

        gatherDebuffRoot.SetActive(active);

        if (!active) return;

        if (gatherDebuffText)
        {
            int percent = Mathf.RoundToInt((1f - speedMultiplier) * 100f);
            gatherDebuffText.text = $"-{percent}% gather speed";
        }
    }
}

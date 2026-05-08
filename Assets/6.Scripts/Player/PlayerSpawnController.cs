using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerSpawnController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D col;

    [Header("Spawn Point")]
    [Tooltip("Name of the spawn point GameObject in each scene.")]
    [SerializeField] private string spawnPointName = "SpawnPoint_Player";
    [Tooltip("Optional spawn point name used only in Bootstrap scene for character preview.")]
    [SerializeField] private string bootstrapSpawnPointName = "SpawnPoint_BootstrapPlayerPreview";

    [Tooltip("Extra frames to wait after scene load before moving player (helps setup order).")]
    [SerializeField] private int waitFramesAfterLoad = 1;

    [Header("Optional Ground Snap")]
    [SerializeField] private bool snapToGround = false;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float castDistance = 5f;

    [Tooltip("Tiny separation so we don't end up penetrating the ground collider.")]
    [SerializeField] private float groundSkin = 0.001f;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float levelLoadScreenFadeSeconds = 1f;
    [SerializeField, Min(0f)] private float levelLoadBlackHoldSeconds = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugSnap = false;

    private SpriteRenderer[] renderers;
    private Coroutine _running;

    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[16];
    private const string GameplaySceneName = "GamePlay";
    private const string LevelLoadFaderName = "LevelLoadFader";
    private static readonly string[] PreferredGroundNameTokens = { "floor", "signpost" };

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!col) col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(SpawnAfterLoad(scene));
    }

    private static bool IsBootstrapScene(Scene scene) =>
        scene.IsValid() && scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase);

    private static bool SavedWorldPositionMatchesActiveMap(SaveData saveData)
    {
        if (saveData == null || string.IsNullOrWhiteSpace(saveData.activeMapNodeId))
            return true;

        MapNodeDefinition cur = ActiveLevelContext.Current;
        if (cur == null || string.IsNullOrWhiteSpace(cur.nodeId))
            return true;

        return string.Equals(
            saveData.activeMapNodeId.Trim(),
            cur.nodeId.Trim(),
            StringComparison.Ordinal);
    }

    private IEnumerator SpawnAfterLoad(Scene loadedScene)
    {
        var playerController = GetComponent<PlayerController>();
        bool isBootstrap = IsBootstrapScene(loadedScene);
        bool useScreenFade = !isBootstrap &&
            loadedScene.IsValid() &&
            loadedScene.name.Equals(GameplaySceneName, StringComparison.OrdinalIgnoreCase) &&
            levelLoadScreenFadeSeconds > 0.001f;

        CanvasGroup loadFader = useScreenFade ? GetOrCreateLevelLoadFader() : null;

        try
        {
            if (playerController != null)
            {
                playerController.SetTeleportDamageImmune(true);
                playerController.SetMovementLocked(true);
            }

            if (loadFader != null)
                loadFader.alpha = 1f;

            var combat = GetComponent<PlayerCombatController>();
            if (combat != null)
                combat.SetIdleCombatEnabled(false);

            var levelTransition = GetComponent<PlayerLevelTransition>();
            bool hideUntilScaleRestore = levelTransition != null && levelTransition.PendingScaleRestore;

            // When using full-screen load fade, avoid stacking the sprite fade on top
            // (it can make the black phase feel much longer than configured).
            bool doFade = !useScreenFade && fadeDuration > 0.001f;

            // Hide until fade-in, or until teleport scale is restored (avoids a visible tiny player when fade is off).
            if (doFade || hideUntilScaleRestore)
                SetAlpha(0f);
            else
                SetAlpha(1f);

            // Freeze physics so teleport + snap is clean
            if (rb) rb.simulated = false;

            // IMPORTANT: keep collider enabled so Cast snapping works
            if (col) col.enabled = true;

            // Wait for scene objects (spawn point/colliders) to exist
            for (int i = 0; i < waitFramesAfterLoad; i++)
                yield return null;

            string targetSpawnName = isBootstrap && !string.IsNullOrWhiteSpace(bootstrapSpawnPointName)
                ? bootstrapSpawnPointName
                : spawnPointName;

            GameObject spawn = GameObject.Find(targetSpawnName);
            if (spawn == null && isBootstrap && !string.Equals(targetSpawnName, spawnPointName, StringComparison.Ordinal))
                spawn = GameObject.Find(spawnPointName);

            bool restoredFromSavedWorldPosition = false;

            if (!isBootstrap &&
                loadedScene.IsValid() &&
                loadedScene.name.Equals(GameplaySceneName, StringComparison.OrdinalIgnoreCase))
            {
                SaveSlotManager.GameplaySpawnDisposition disposition =
                    SaveSlotManager.ConsumePendingGameplaySpawnDisposition();
                bool requestedSavedRestore =
                    disposition == SaveSlotManager.GameplaySpawnDisposition.RestoreSavedWorldPositionIfAvailable;

                if (requestedSavedRestore &&
                    SaveManager.Instance != null &&
                    SaveManager.Instance.TryGetLastLoadedData(out SaveData saveData) &&
                    saveData.hasSavedPlayerWorldPosition &&
                    SavedWorldPositionMatchesActiveMap(saveData))
                {
                    // Restore saved X only. Keep Y/Z from spawn/current flow and let ground snap place player on floor.
                    Vector3 basePos = spawn != null ? spawn.transform.position : transform.position;
                    // Always anchor to scene spawn first so stale previous-scene transform cannot leak through.
                    transform.position = basePos;
                    float savedX = saveData.playerWorldPosX;
                    bool savedXValidForMap = true;
                    if (WorldBounds.Instance != null)
                    {
                        float left = WorldBounds.Instance.Left;
                        float right = WorldBounds.Instance.Right;
                        // If save contains an impossible X for this map (e.g. bootstrap/UI-space value),
                        // ignore it instead of clamping to an edge spawn.
                        if (savedX < left - 1f || savedX > right + 1f)
                            savedXValidForMap = false;
                    }

                    if (savedXValidForMap)
                    {
                        float x = savedX;
                        if (WorldBounds.Instance != null)
                            x = Mathf.Clamp(x, WorldBounds.Instance.Left, WorldBounds.Instance.Right);
                        transform.position = new Vector3(x, basePos.y, basePos.z);
                        restoredFromSavedWorldPosition = true;
                    }

                    if (debugSnap)
                    {
                        string curId = ActiveLevelContext.Current != null ? (ActiveLevelContext.Current.nodeId ?? "") : "";
                        string saveId = saveData.activeMapNodeId ?? "";
                        Debug.Log($"[SpawnDebug] APPLY_SAVED_X curMap='{curId}' saveMap='{saveId}' savedX={saveData.playerWorldPosX:F3} valid={savedXValidForMap} baseY={basePos.y:F3} finalX={transform.position.x:F3}");
                    }
                }
                else if (requestedSavedRestore &&
                         SaveManager.Instance != null &&
                         SaveManager.Instance.TryGetLastLoadedData(out SaveData dbgData))
                {
                    if (spawn != null)
                        transform.position = spawn.transform.position;

                    if (debugSnap)
                    {
                        string curId = ActiveLevelContext.Current != null ? (ActiveLevelContext.Current.nodeId ?? "") : "";
                        string saveId = dbgData != null ? (dbgData.activeMapNodeId ?? "") : "";
                        bool hasPos = dbgData != null && dbgData.hasSavedPlayerWorldPosition;
                        bool mapOk = dbgData != null && SavedWorldPositionMatchesActiveMap(dbgData);
                        Debug.Log($"[SpawnDebug] SKIP_SAVED_X curMap='{curId}' saveMap='{saveId}' hasPos={hasPos} mapOk={mapOk}");
                    }
                }
                else if (spawn != null)
                {
                    transform.position = spawn.transform.position;
                }
                else
                {
                    Debug.LogWarning($"[PlayerSpawnController] Missing spawn point '{targetSpawnName}' in scene. Player stays where it is.");
                }
            }
            else if (spawn != null)
            {
                transform.position = spawn.transform.position;
            }
            else if (!isBootstrap)
            {
                Debug.LogWarning($"[PlayerSpawnController] Missing spawn point '{targetSpawnName}' in scene. Player stays where it is.");
            }

            // Let transforms + physics settle
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();

            // Restore full scale BEFORE ground snap. Snapping at teleport scale (~0.01) uses wrong collider bounds
            // and places the root incorrectly relative to the ground.
            levelTransition?.RestoreScaleAfterLevelChange();
            Physics2D.SyncTransforms();

            bool shouldSnapToGround = snapToGround ||
                                      restoredFromSavedWorldPosition ||
                                      (loadedScene.IsValid() &&
                                       loadedScene.name.Equals(GameplaySceneName, StringComparison.OrdinalIgnoreCase));
            if (shouldSnapToGround)
                SnapToGround_ColliderCast(!isBootstrap);

            if (rb)
                rb.position = transform.position;
            Physics2D.SyncTransforms();

            if (debugSnap)
                Debug.Log($"[SpawnDebug] AFTER_SNAP pos=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) restoredX={restoredFromSavedWorldPosition}");

            // Clear motion + re-enable physics
            if (rb)
            {
                rb.linearVelocity = Vector2.zero;
                rb.simulated = true;
            }

            if (debugSnap)
                Debug.Log($"[SpawnDebug] FINAL pos=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3})");

            // Fade in (optional)
            if (doFade)
                yield return FadeIn();
            else
                SetAlpha(1f);

            if (loadFader != null)
            {
                if (levelLoadBlackHoldSeconds > 0f)
                    yield return new WaitForSecondsRealtime(levelLoadBlackHoldSeconds);
                yield return FadeCanvasGroup(loadFader, 1f, 0f, levelLoadScreenFadeSeconds);
            }
        }
        finally
        {
            if (playerController != null)
            {
                playerController.SetTeleportDamageImmune(false);
                playerController.SetMovementLocked(false);
            }

            if (loadFader != null)
                loadFader.alpha = 0f;

            _running = null;
        }
    }

    /// <summary>
    /// Reliable snap: casts the player's collider downward against groundMask and moves
    /// the player so the collider bottom rests on the ground.
    /// Works with pivot offsets and thin/edge colliders better than a single Raycast.
    /// </summary>
    private void SnapToGround_ColliderCast(bool logFailureWarning)
    {
        if (!col)
        {
            if (debugSnap) Debug.LogWarning("[Snap] No collider assigned.");
            return;
        }

        // Ensure collider is enabled for Cast
        bool wasEnabled = col.enabled;
        if (!wasEnabled) col.enabled = true;

        // Try a few times in case composite/tile colliders build a frame late
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundMask,
                useTriggers = false
            };

            int hitCount = col.Cast(Vector2.down, filter, _castHits, Mathf.Max(0.25f, castDistance));

            if (hitCount > 0)
            {
                // Pick closest preferred ground first (Floor / SignPost), then closest fallback.
                RaycastHit2D best = default;
                bool haveBest = false;
                bool bestPreferred = false;
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit2D hit = _castHits[i];
                    if (hit.collider == null)
                        continue;

                    bool preferred = IsPreferredGroundCollider(hit.collider);
                    if (!haveBest)
                    {
                        best = hit;
                        haveBest = true;
                        bestPreferred = preferred;
                        continue;
                    }

                    if (preferred && !bestPreferred)
                    {
                        best = hit;
                        bestPreferred = true;
                        continue;
                    }

                    if (preferred == bestPreferred && hit.distance < best.distance)
                        best = hit;
                }

                if (!haveBest)
                    continue;

                // Unity reports free space along the cast until contact. We want the collider to end up
                // `groundSkin` above the surface. Single form: deltaY = groundSkin - distance.
                // Important: when distance is 0 (already touching), older logic used Max(0, -groundSkin)=0,
                // so groundSkin had no effect — nudging it in the inspector did nothing.
                float deltaY = groundSkin - best.distance;

                if (debugSnap)
                {
                    Debug.Log($"[Snap] Hit '{best.collider.name}' layer={LayerMask.LayerToName(best.collider.gameObject.layer)} " +
                              $"dist={best.distance:F4} deltaY={deltaY:F4} pointY={best.point.y:F3}");
                }

                transform.position += new Vector3(0f, deltaY, 0f);

                // restore enabled state if needed
                if (!wasEnabled) col.enabled = false;
                return;
            }

            if (debugSnap)
            {
                Debug.Log($"[Snap] No hit (attempt {attempt + 1}). groundMask={groundMask.value} " +
                          $"colBottomY={col.bounds.min.y:F3} posY={transform.position.y:F3}");
            }
        }

        if (!wasEnabled) col.enabled = false;

        if (logFailureWarning)
            Debug.LogWarning("[PlayerSpawnController] SnapToGround failed (no ground hit). Check groundMask + colliders.");
    }

    private static bool IsPreferredGroundCollider(Collider2D c)
    {
        if (c == null)
            return false;

        string n = c.name ?? "";
        if (ContainsPreferredGroundToken(n))
            return true;

        Transform t = c.transform;
        while (t != null)
        {
            if (ContainsPreferredGroundToken(t.name))
                return true;
            t = t.parent;
        }

        return false;
    }

    private static bool ContainsPreferredGroundToken(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return false;

        string lower = raw.ToLowerInvariant();
        for (int i = 0; i < PreferredGroundNameTokens.Length; i++)
        {
            if (lower.Contains(PreferredGroundNameTokens[i]))
                return true;
        }
        return false;
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            SetAlpha(a);
            yield return null;
        }
        SetAlpha(1f);
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null)
            yield break;

        cg.alpha = from;
        float dur = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            cg.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }
        cg.alpha = to;
    }

    private static CanvasGroup GetOrCreateLevelLoadFader()
    {
        GameObject go = GameObject.Find(LevelLoadFaderName);
        if (go == null)
            go = new GameObject(LevelLoadFaderName, typeof(Canvas), typeof(CanvasGroup), typeof(Image));

        Canvas canvas = go.GetComponent<Canvas>();
        if (!canvas) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Image img = go.GetComponent<Image>();
        if (!img) img = go.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        RectTransform rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        return cg;
    }


    private void SetAlpha(float alpha)
    {
        if (renderers == null) return;

        foreach (var r in renderers)
        {
            if (!r) continue;
            Color c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }
}

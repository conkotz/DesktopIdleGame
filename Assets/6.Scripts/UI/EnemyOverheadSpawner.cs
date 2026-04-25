using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EnemyOverheadUISpawner : MonoBehaviour
{
    private static readonly string[] LeftHudNameCandidates = { "LeftHud", "HUD_Left" };
    [Header("Refs")]
    [SerializeField] private UnitOverheadUI overheadPrefab;
    [SerializeField] private Transform overheadAnchor;
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private EnemyBaseController enemy;
    [SerializeField] private AilmentController ailments;
    [Tooltip("Optional. If unset we resolve gameplay HUD canvas by tag UICanvas only (avoids Bootstrap menu canvas).")]
    [SerializeField] private Canvas stripCanvas;
    [Tooltip("Optional. If unset we use this canvas's worldCamera, or Camera.main.")]
    [SerializeField] private Camera stripCamera;
    [Tooltip("Hide name, combat profile, and debuffs — only the HP bar (e.g. player overhead).")]
    [SerializeField] private bool hpBarOnly;

    private UnitOverheadUI overheadInstance;

    private void Awake()
    {
        if (!characterStats) characterStats = GetComponent<CharacterStats>();
        if (!enemy) enemy = GetComponent<EnemyBaseController>();
        if (!ailments) ailments = GetComponent<AilmentController>();
        if (!overheadAnchor) overheadAnchor = transform;

        ResolveStripContext();
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
        TrySpawnAfterSceneChange();
    }

    private void Start()
    {
        if (overheadPrefab == null)
            return;

        TrySpawnAfterSceneChange();
        if (!overheadInstance)
            StartCoroutine(SpawnOverheadWhenReady());
    }

    private void Update()
    {
        // Keep player-style overhead bars (hpBarOnly) visually tied to teleport shrink.
        if (!hpBarOnly || overheadInstance == null || overheadAnchor == null)
            return;

        Vector3 ls = overheadAnchor.lossyScale;
        float uniform = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
        overheadInstance.SetExternalScale(Mathf.Max(0.01f, uniform));
    }

    /// <summary>
    /// If our overhead lived under a canvas that was unloaded (e.g. player spawned in Bootstrap, then GamePlay loads),
    /// the instance reference is gone — spawn again on the gameplay UICanvas.
    /// </summary>
    private void TrySpawnAfterSceneChange()
    {
        if (!overheadPrefab)
            return;

        if (overheadInstance)
            return;

        ResolveStripContext();
        if (stripCanvas && stripCamera)
            SpawnAndBind();
    }

    private IEnumerator SpawnOverheadWhenReady()
    {
        while (!overheadInstance && overheadPrefab)
        {
            if (!isActiveAndEnabled)
                yield break;

            yield return null;
            ResolveStripContext();
            if (stripCanvas && stripCamera)
                SpawnAndBind();
        }
    }

    private void ResolveStripContext()
    {
        if (!stripCanvas)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("UICanvas");
            if (tagged)
                stripCanvas = tagged.GetComponent<Canvas>();
        }

        if (!stripCanvas)
            stripCanvas = FindSceneCanvasByTagOrName("UICanvas", "StripUICanvas");

        if (stripCanvas && !stripCamera)
        {
            if (stripCanvas.renderMode == RenderMode.ScreenSpaceCamera && stripCanvas.worldCamera)
                stripCamera = stripCanvas.worldCamera;
            else
                stripCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
        }
    }

    private static Canvas FindSceneCanvasByTagOrName(string tagName, string objectName)
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.hideFlags != HideFlags.None || !canvas.gameObject.scene.IsValid())
                continue;

            if (canvas.CompareTag(tagName) || canvas.name == objectName)
                return canvas;
        }

        return null;
    }

    private void SpawnAndBind()
    {
        if (overheadInstance)
            return;

        overheadInstance = Instantiate(overheadPrefab, stripCanvas.transform);
        overheadInstance.transform.localScale = Vector3.one;

        overheadInstance.Bind(
            characterStats,
            enemy,
            ailments,
            overheadAnchor,
            stripCanvas,
            stripCamera,
            hpBarOnly
        );

        if (hpBarOnly)
            PlaceUnderLeftHud();
    }

    private void PlaceUnderLeftHud()
    {
        if (overheadInstance == null || stripCanvas == null)
            return;

        Transform overheadTransform = overheadInstance.transform;
        Transform leftHud = FindLeftHudTransform();
        if (leftHud == null || leftHud.parent != stripCanvas.transform)
            return;

        int leftHudIndex = leftHud.GetSiblingIndex();
        int targetIndex = Mathf.Max(0, leftHudIndex);
        overheadTransform.SetSiblingIndex(targetIndex);
    }

    private Transform FindLeftHudTransform()
    {
        for (int i = 0; i < LeftHudNameCandidates.Length; i++)
        {
            Transform t = stripCanvas.transform.Find(LeftHudNameCandidates[i]);
            if (t != null)
                return t;
        }

        for (int i = 0; i < stripCanvas.transform.childCount; i++)
        {
            Transform child = stripCanvas.transform.GetChild(i);
            if (child == null)
                continue;

            string n = child.name;
            if (string.Equals(n, "LeftHud", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, "HUD_Left", StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (overheadInstance)
            Destroy(overheadInstance.gameObject);
    }
}
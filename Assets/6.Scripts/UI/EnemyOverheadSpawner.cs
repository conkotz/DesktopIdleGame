using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EnemyOverheadUISpawner : MonoBehaviour
{
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
        int frames = 0;
        while (!overheadInstance && overheadPrefab)
        {
            if (!isActiveAndEnabled)
                yield break;

            yield return null;
            ResolveStripContext();
            if (stripCanvas && stripCamera)
                SpawnAndBind();

            frames++;
            if (frames % 600 == 0)
            {
                Debug.LogWarning(
                    "[EnemyOverheadUISpawner] Still waiting for UICanvas / camera for overhead on '" + name +
                    "'. Assign Strip Canvas on the prefab or tag gameplay canvas 'UICanvas'.",
                    this);
            }
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

        if (stripCanvas && !stripCamera)
        {
            if (stripCanvas.renderMode == RenderMode.ScreenSpaceCamera && stripCanvas.worldCamera)
                stripCamera = stripCanvas.worldCamera;
            else
                stripCamera = Camera.main;
        }
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
    }

    private void OnDestroy()
    {
        if (overheadInstance)
            Destroy(overheadInstance.gameObject);
    }
}
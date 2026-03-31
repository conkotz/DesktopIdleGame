using UnityEngine;

[DisallowMultipleComponent]
public class EnemyOverheadUISpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private UnitOverheadUI overheadPrefab;
    [SerializeField] private Transform overheadAnchor;
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private EnemyBaseController enemy;
    [SerializeField] private AilmentController ailments;
    [SerializeField] private Canvas stripCanvas;
    [SerializeField] private Camera stripCamera;

    private UnitOverheadUI overheadInstance;

    private void Awake()
    {
        if (!characterStats) characterStats = GetComponent<CharacterStats>();
        if (!enemy) enemy = GetComponent<EnemyBaseController>();
        if (!ailments) ailments = GetComponent<AilmentController>();
        if (!overheadAnchor) overheadAnchor = transform;

        if (!stripCanvas)
            stripCanvas = FindFirstObjectByType<Canvas>();

        if (!stripCamera)
            stripCamera = FindFirstObjectByType<Camera>();
    }

    private void Start()
    {
        if (overheadPrefab == null || stripCanvas == null || stripCamera == null)
            return;

        overheadInstance = Instantiate(overheadPrefab, stripCanvas.transform);
        overheadInstance.transform.localScale = Vector3.one;

        overheadInstance.Bind(
            characterStats,
            enemy,
            ailments,
            overheadAnchor,
            stripCanvas,
            stripCamera
        );
    }

    private void OnDestroy()
    {
        if (overheadInstance != null)
            Destroy(overheadInstance.gameObject);
    }
}
using UnityEngine;

public class DamagePopupSystem : MonoBehaviour
{
    public static DamagePopupSystem Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Camera uiCamera;
    [SerializeField] private FloatingDamageTextUI popupPrefab;

    [Header("Spawn Offset")]
    [SerializeField] private float popupYOffsetStep = 16f;
    [SerializeField] private float popupXJitter = 8f;
    [SerializeField] private int popupYOffsetCycle = 4;

    private int _popupSpawnIndex = 0;
    private void Awake()
    {
        Instance = this;

        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!canvasRect && canvas) canvasRect = canvas.GetComponent<RectTransform>();

        // For Screen Space - Camera, use the canvas worldCamera
        if (!uiCamera && canvas) uiCamera = canvas.worldCamera;
    }

    public void Spawn(
        Vector3 worldPos,
        int amount,
        FloatingDamageTextUI.PopupDamageKind kind,
        bool isCrit,
        bool isDot,
        Vector3 direction,
        bool blocked = false)
    {
        if (!popupPrefab || !canvasRect || !uiCamera) return;

        Vector2 screenPos = uiCamera.WorldToScreenPoint(worldPos);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, uiCamera, out Vector2 localPoint))
            return;

        float xJitter = Random.Range(-popupXJitter, popupXJitter);
        float yOffset = (_popupSpawnIndex % Mathf.Max(1, popupYOffsetCycle)) * popupYOffsetStep;
        _popupSpawnIndex++;

        var popup = Instantiate(popupPrefab, canvasRect);
        popup.GetComponent<RectTransform>().anchoredPosition = localPoint + new Vector2(xJitter, yOffset);

        if (blocked || kind == FloatingDamageTextUI.PopupDamageKind.Blocked)
            popup.InitBlocked(direction);
        else
            popup.Init(amount, kind, isCrit, isDot, direction);
    }

    // Backward-compatible overload for older calls still using the old signature.
    public void Spawn(Vector3 worldPos, int amount, bool isCrit, Vector3 direction, bool blocked)
    {
        Spawn(
            worldPos,
            amount,
            FloatingDamageTextUI.PopupDamageKind.Physical,
            isCrit,
            false,
            direction,
            blocked
        );
    }

    public static Vector3 GetDamagePopupPos(Transform victim, Transform attacker, float xOffset = 0.35f, float yOffset = 1.2f)
    {
        float dir = 0f;

        if (attacker && victim)
            dir = Mathf.Sign(victim.position.x - attacker.position.x);

        if (dir == 0f) dir = 1f;

        return victim.position + new Vector3(dir * xOffset, yOffset, 0f);
    }
}
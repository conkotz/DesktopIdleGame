using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasScaler))]
public sealed class RuntimeCanvasScaleController : MonoBehaviour
{
    private const string PlayerPrefsKey = "ui.scaleMultiplier";

    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private Vector2 baseReferenceResolution = new Vector2(2560f, 1440f);
    [SerializeField, Range(0.75f, 1.75f)] private float defaultScaleMultiplier = 1f;
    [SerializeField, Range(0.75f, 1.75f)] private float minScaleMultiplier = 0.85f;
    [SerializeField, Range(0.75f, 1.75f)] private float maxScaleMultiplier = 1.4f;
    [SerializeField] private float hotkeyStep = 0.05f;
    [SerializeField] private bool enableHotkeys = true;

    private float _scaleMultiplier;

    private void Awake()
    {
        if (!canvasScaler)
            canvasScaler = GetComponent<CanvasScaler>();

        if (baseReferenceResolution.x <= 0f || baseReferenceResolution.y <= 0f)
            baseReferenceResolution = canvasScaler ? canvasScaler.referenceResolution : new Vector2(2560f, 1440f);

        _scaleMultiplier = PlayerPrefs.GetFloat(PlayerPrefsKey, defaultScaleMultiplier);
        ApplyScale();
    }

    private void Update()
    {
        if (!enableHotkeys)
            return;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrl)
            return;

        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            SetScaleMultiplier(_scaleMultiplier + hotkeyStep);
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            SetScaleMultiplier(_scaleMultiplier - hotkeyStep);
        else if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            SetScaleMultiplier(defaultScaleMultiplier);
    }

    public void SetScaleMultiplier(float value)
    {
        _scaleMultiplier = Mathf.Clamp(value, minScaleMultiplier, maxScaleMultiplier);
        PlayerPrefs.SetFloat(PlayerPrefsKey, _scaleMultiplier);
        PlayerPrefs.Save();
        ApplyScale();
    }

    private void ApplyScale()
    {
        if (!canvasScaler)
            return;

        float scale = Mathf.Max(0.01f, _scaleMultiplier);
        canvasScaler.referenceResolution = baseReferenceResolution / scale;
    }
}

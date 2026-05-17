using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Saves and loads the bootstrap save-slot scene. Other UI code sometimes clears <see cref="Button.onClick"/> after
/// scene load, so we re-attach listeners from <see cref="OnEnable"/>, a short deferred coroutine, and
/// <see cref="SaveManager"/> when GamePlay finishes loading (<see cref="RewireAll"/>).
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class ReturnToLoginMenuButton : MonoBehaviour
{
    [Tooltip("Scene name in Build Settings; must match your boot / save select scene.")]
    [SerializeField] private string bootstrapSceneName = "Bootstrap";

    private Button _button;
    private Coroutine _deferredWireRoutine;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        EnsureWired();
        if (isActiveAndEnabled)
        {
            if (_deferredWireRoutine != null)
                StopCoroutine(_deferredWireRoutine);
            _deferredWireRoutine = StartCoroutine(CoEnsureWiredAfterLayout());
        }
    }

    private void OnDisable()
    {
        if (_deferredWireRoutine != null)
        {
            StopCoroutine(_deferredWireRoutine);
            _deferredWireRoutine = null;
        }

        if (_button)
            _button.onClick.RemoveListener(HandleClicked);
    }

    private IEnumerator CoEnsureWiredAfterLayout()
    {
        yield return null;
        yield return null;
        EnsureWired();
        _deferredWireRoutine = null;
    }

    /// <summary>Re-attach <see cref="Button.onClick"/>; safe to call from <see cref="SaveManager"/> after GamePlay loads.</summary>
    public void EnsureWired()
    {
        if (!_button)
            _button = GetComponent<Button>();
        if (!_button)
            return;

        _button.onClick.RemoveListener(HandleClicked);
        _button.onClick.AddListener(HandleClicked);
    }

    /// <summary>Re-wires every instance (e.g. after loading GamePlay from Bootstrap).</summary>
    public static void RewireAll()
    {
        ReturnToLoginMenuButton[] arr =
            FindObjectsByType<ReturnToLoginMenuButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i])
                arr[i].EnsureWired();
        }
    }

    private void HandleClicked()
    {
        QuickMenuPanelToggleUI.HideIfOpen();

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[ReturnToLoginMenuButton] SaveManager.Instance is null; cannot save or return to login.");
            return;
        }

        string scene = string.IsNullOrWhiteSpace(bootstrapSceneName) ? "Bootstrap" : bootstrapSceneName.Trim();
        SaveManager.Instance.ReturnToSaveSlotSelectAfterSaving(scene);
    }
}

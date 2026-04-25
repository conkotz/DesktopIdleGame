using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple level-change effect: shrink the player root uniformly, then load the next scene (RuneScape-style).
/// Add to the player root next to <see cref="PlayerController"/>. Call <see cref="LoadSceneWithEffectOrImmediate"/>.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLevelTransition : MonoBehaviour
{
    [Header("Shrink")]
    [SerializeField, Min(0.1f)] private float shrinkDurationSeconds = 2f;
    [SerializeField, Min(0.001f)] private float shrinkToScale = 0.01f;

    private Vector3 _savedRootScale = Vector3.one;
    private bool _pendingRestore;
    private Coroutine _running;

    /// <summary>True after shrink-until-load until <see cref="RestoreScaleAfterLevelChange"/> runs.</summary>
    public bool PendingScaleRestore => _pendingRestore;

    private bool _shrinking;
    private float _elapsed;
    private float _dur;
    private float _startUniform;
    private float _endScale;
    private string _pendingScene;
    private float _teleportLockWorldX;

    private Animator[] _animators;
    private bool[] _animatorWasEnabled;

    public static void LoadSceneWithEffectOrImmediate(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[PlayerLevelTransition] Scene name is empty.");
            return;
        }

        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        PlayerLevelTransition t = pc != null ? pc.GetComponent<PlayerLevelTransition>() : null;
        MainMenuWindowUI.CaptureOpenStateForSceneChange();

        if (t != null)
            t.BeginShrinkThenLoad(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    /// <summary>Called from <see cref="PlayerSpawnController"/> after a DDOL scene change.</summary>
    public void RestoreScaleAfterLevelChange()
    {
        if (!_pendingRestore)
            return;

        transform.localScale = _savedRootScale;
        _pendingRestore = false;

        RestoreAnimators();

        var pc = GetComponent<PlayerController>();
        if (pc != null)
            pc.SetTeleportOutVisualsActive(false);
    }

    public void BeginShrinkThenLoad(string sceneName)
    {
        if (_running != null)
            return;

        _running = StartCoroutine(ShrinkThenLoadRoutine(sceneName));
    }

    private IEnumerator ShrinkThenLoadRoutine(string sceneName)
    {
        _savedRootScale = transform.localScale;
        _pendingRestore = true;

        DisableAnimators();

        var pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.SetTeleportOutVisualsActive(true);
            pc.SetMovementLocked(true);
        }

        var rb = GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        _teleportLockWorldX = transform.position.x;

        Vector3 sv = _savedRootScale;
        _startUniform = (sv.x + sv.y + sv.z) / 3f;
        if (_startUniform < 1e-6f)
            _startUniform = 1f;
        _endScale = Mathf.Max(0.001f, shrinkToScale);
        _dur = Mathf.Max(0.1f, shrinkDurationSeconds);
        _elapsed = 0f;
        _pendingScene = sceneName;
        _shrinking = true;

        while (_elapsed < _dur)
            yield return null;

        _shrinking = false;
        transform.localScale = new Vector3(_endScale, _endScale, _endScale);

        // Avoid one visible frame at tiny scale before the scene swap; spawn flow fades in from alpha 0.
        HideAllSpriteAlphas();

        SceneManager.LoadScene(_pendingScene);
        _running = null;
    }

    private void LateUpdate()
    {
        if (!_shrinking)
            return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        _elapsed += dt;
        float u = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _dur));
        float u2 = u * u * (3f - 2f * u);
        float s = Mathf.Lerp(_startUniform, _endScale, u2);
        transform.localScale = new Vector3(s, s, s);

        Vector3 p = transform.position;
        p.x = _teleportLockWorldX;
        transform.position = p;
    }

    private void OnDisable()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }

        _shrinking = false;
        RestoreAnimators();

        var pc = GetComponent<PlayerController>();
        if (pc != null)
            pc.SetTeleportOutVisualsActive(false);
    }

    private void DisableAnimators()
    {
        RestoreAnimators();
        _animators = GetComponentsInChildren<Animator>(true);
        if (_animators == null || _animators.Length == 0)
            return;

        _animatorWasEnabled = new bool[_animators.Length];
        for (int i = 0; i < _animators.Length; i++)
        {
            if (_animators[i] == null)
                continue;
            _animatorWasEnabled[i] = _animators[i].enabled;
            _animators[i].enabled = false;
        }
    }

    private void RestoreAnimators()
    {
        if (_animators == null || _animatorWasEnabled == null)
            return;

        for (int i = 0; i < _animators.Length; i++)
        {
            if (_animators[i] != null && _animatorWasEnabled[i])
                _animators[i].enabled = true;
        }

        _animators = null;
        _animatorWasEnabled = null;
    }

    private void HideAllSpriteAlphas()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.a = 0f;
            sr.color = c;
        }
    }
}

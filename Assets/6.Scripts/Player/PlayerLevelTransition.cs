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
    public const float StandardShrinkDurationSeconds = 1.5f;

    [Header("Shrink")]
    [SerializeField, Min(0.001f)] private float shrinkToScale = 0.01f;

    private Vector3 _savedRootScale = Vector3.one;
    private bool _pendingRestore;
    private Coroutine _running;

    /// <summary>True after shrink-until-load until <see cref="RestoreScaleAfterLevelChange"/> runs.</summary>
    public bool PendingScaleRestore => _pendingRestore;

    public bool IsTransitionRunning => _running != null || _shrinking;

    private bool _shrinking;
    private float _elapsed;
    private float _dur;
    private float _startUniform;
    private float _endScale;
    private string _pendingScene;
    private float _teleportLockWorldX;

    private Animator[] _animators;
    private bool[] _animatorWasEnabled;

    /// <returns>False when a transition is already running or the scene name is invalid.</returns>
    public static bool LoadSceneWithEffectOrImmediate(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[PlayerLevelTransition] Scene name is empty.");
            return false;
        }

        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        PlayerLevelTransition t = pc != null ? pc.GetComponent<PlayerLevelTransition>() : null;
        MainMenuWindowUI.CaptureOpenStateForSceneChange();

        if (t != null)
        {
            if (t.IsTransitionRunning)
                return false;

            t.BeginShrinkThenLoad(sceneName);
            return true;
        }

        PlayerController.NotifyGameplayMapSpawnStarted();
        MapTravelSession.ApplyPendingSpawnDispositionBeforeSceneLoad();
        SaveManager.Instance?.SaveBeforeSceneTransition();
        SceneManager.LoadScene(sceneName);
        PlayerController.NotifyReturnToTownTravelFinished();
        return true;
    }

    /// <summary>
    /// Same-scene teleport effect: shrink out, warp to X, then grow back in.
    /// Uses the same standardized timing as scene travel transitions.
    /// </summary>
    public static bool TryShrinkTeleportWithinScene(PlayerController player, float targetWorldX)
    {
        if (player == null || player.IsDead)
            return false;

        PlayerLevelTransition transition = player.GetComponent<PlayerLevelTransition>();
        if (transition == null || transition.IsTransitionRunning)
            return false;

        transition.BeginShrinkTeleportWithinScene(targetWorldX);
        return true;
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

        PlayerController.NotifyReturnToTownTravelFinished();
    }

    public void BeginShrinkThenLoad(string sceneName)
    {
        if (_running != null)
            return;

        _running = StartCoroutine(ShrinkThenLoadRoutine(sceneName));
    }

    public void BeginShrinkTeleportWithinScene(float targetWorldX)
    {
        if (_running != null)
            return;

        _running = StartCoroutine(ShrinkTeleportWithinSceneRoutine(targetWorldX));
    }

    private IEnumerator ShrinkThenLoadRoutine(string sceneName)
    {
        PlayerController.NotifyGameplayMapSpawnStarted();

        _savedRootScale = transform.localScale;
        _pendingRestore = true;

        DisableAnimators();

        var pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.SetTeleportOutVisualsActive(true);
            pc.SetTeleportDamageImmune(true);
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
        _dur = ResolveShrinkDurationSeconds();
        _elapsed = 0f;
        _pendingScene = sceneName;
        _shrinking = true;

        while (_elapsed < _dur)
            yield return null;

        _shrinking = false;
        transform.localScale = new Vector3(_endScale, _endScale, _endScale);

        // Avoid one visible frame at tiny scale before the scene swap; spawn flow fades in from alpha 0.
        HideAllSpriteAlphas();

        MapTravelSession.ApplyPendingSpawnDispositionBeforeSceneLoad();
        SaveManager.Instance?.SaveBeforeSceneTransition();
        SceneManager.LoadScene(_pendingScene);
        _running = null;
    }

    private float ResolveShrinkDurationSeconds()
    {
        // Standardized globally for all transition types (map teleport, in-world entrance, return-to-town, etc).
        return StandardShrinkDurationSeconds;
    }

    private IEnumerator ShrinkTeleportWithinSceneRoutine(float targetWorldX)
    {
        _savedRootScale = transform.localScale;
        _pendingRestore = false;

        DisableAnimators();

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.SetTeleportOutVisualsActive(true);
            pc.SetTeleportDamageImmune(true);
            pc.SetMovementLocked(true);
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        Vector3 sv = _savedRootScale;
        _startUniform = (sv.x + sv.y + sv.z) / 3f;
        if (_startUniform < 1e-6f)
            _startUniform = 1f;

        _endScale = Mathf.Max(0.001f, shrinkToScale);
        _dur = Mathf.Max(0.1f, ResolveShrinkDurationSeconds()) * 0.5f;
        _elapsed = 0f;
        _teleportLockWorldX = transform.position.x;
        _shrinking = true;

        while (_elapsed < _dur)
            yield return null;

        _shrinking = false;
        transform.localScale = new Vector3(_endScale, _endScale, _endScale);

        if (pc != null)
            pc.WarpToWorldX(targetWorldX);
        else
            transform.position = new Vector3(targetWorldX, transform.position.y, transform.position.z);

        _elapsed = 0f;
        _dur = Mathf.Max(0.1f, ResolveShrinkDurationSeconds()) * 0.5f;
        _teleportLockWorldX = transform.position.x;
        while (_elapsed < _dur)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _elapsed += dt;
            float u = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _dur));
            float u2 = u * u * (3f - 2f * u);
            float s = Mathf.Lerp(_endScale, _startUniform, u2);
            transform.localScale = new Vector3(s, s, s);

            Vector3 p = transform.position;
            p.x = _teleportLockWorldX;
            transform.position = p;
            yield return null;
        }

        transform.localScale = _savedRootScale;
        if (pc != null)
            pc.SnapToActiveLaneAtCurrentX();
        _shrinking = false;

        if (rb)
            rb.simulated = true;

        RestoreAnimators();

        if (pc != null)
        {
            pc.SetTeleportOutVisualsActive(false);
            pc.SetTeleportDamageImmune(false);
            pc.SetMovementLocked(false);
        }

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
        bool abortedCoroutine = _running != null;

        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }

        _shrinking = false;
        RestoreAnimators();

        var pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.SetTeleportOutVisualsActive(false);

            // Aborted mid-shrink without scene load — release damage immunity so the player isn't stuck immune.
            if (abortedCoroutine)
            {
                pc.SetTeleportDamageImmune(false);
                PlayerController.NotifyReturnToTownTravelFinished();
            }
        }
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

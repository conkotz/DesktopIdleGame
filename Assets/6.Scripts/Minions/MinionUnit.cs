using UnityEngine;

/// <summary>
/// Shared combat puppet: Soldier rig, animator locomotion, facing, and X movement.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class MinionUnit : MonoBehaviour
{
    [Header("Visual rig")]
    [Tooltip("Player-style Soldier prefab (Assets/2.Prefabs/Characters/Soldier.prefab).")]
    [SerializeField] private GameObject soldierVisualPrefab;

    [SerializeField] private Transform visualsRoot;
    [SerializeField] private Vector3 visualsRootLocalPosition = new Vector3(-0.13f, -0.32f, 0f);
    [SerializeField] private Vector3 soldierLocalPosition = new Vector3(-0.14f, 0.28f, 0f);

    [Header("Animator")]
    [SerializeField] private string idleStateName = "idle";
    [SerializeField] private string walkStateName = "walk";
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string hurtTriggerName = "Hurt";
    [SerializeField] private string dieTriggerName = "Die";
    [SerializeField] private string dieStateName = "die";

    private Rigidbody2D _rb;
    private Transform _visualFlipRoot;
    private Animator _animator;
    private bool _isMoving;
    private bool _attackLocked;
    private float _attackUnlockTime;
    private bool _isDeadVisual;
    private bool _wantsMove;
    private float _moveTargetX;
    private float _moveSpeed;
    private float _visualFlipScaleX = 1f;
    private Vector3 _soldierBaseLocalPosition;
    private float _colliderBottomBelowRoot;
    private bool _hasColliderBottomBelowRoot;

    public Animator Animator => _animator;
    public Transform VisualFlipRoot => _visualFlipRoot;
    /// <summary>+1 when facing right, -1 when facing left.</summary>
    public float FacingSignX =>
        Mathf.Approximately(_visualFlipScaleX, 0f) ? 1f : -Mathf.Sign(_visualFlipScaleX);
    public bool IsAliveVisual => !_isDeadVisual;
    public bool IsMoving => _wantsMove;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb)
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (!visualsRoot)
        {
            Transform existing = transform.Find("Visuals");
            visualsRoot = existing ? existing : transform;
        }

        if (visualsRoot)
            visualsRoot.localPosition = visualsRootLocalPosition;
    }

    private void OnEnable()
    {
        WorldFloorFollowerRegistry.Register(transform, WorldFloorFollowerRegistry.Category.Actor);
    }

    private void OnDisable()
    {
        WorldFloorFollowerRegistry.Unregister(transform);
    }

    public bool SetupFromOwner(Transform ownerRoot, Color spectralTint, float uniformScale)
    {
        if (!soldierVisualPrefab || !ownerRoot)
            return false;

        EnsureVisualHierarchy();
        ClearSoldierChildren();

        _soldierBaseLocalPosition = soldierLocalPosition;

        GameObject soldierGo = Instantiate(soldierVisualPrefab, _visualFlipRoot);
        soldierGo.name = "Soldier";
        soldierGo.transform.localPosition = soldierLocalPosition;
        soldierGo.transform.localRotation = Quaternion.identity;
        soldierGo.transform.localScale = Vector3.one;

        _animator = soldierGo.GetComponent<Animator>();
        if (_animator)
        {
            _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            _animator.updateMode = AnimatorUpdateMode.Normal;
        }

        MinionOwnerVisualSnapshot.CopySoldierAppearance(ownerRoot, soldierGo.transform, spectralTint);

        float u = Mathf.Max(0.05f, uniformScale);
        visualsRoot.localScale = new Vector3(u, u, u);
        _visualFlipRoot.localScale = Vector3.one;

        AlignToLaneFloor();
        CacheColliderBottomBelowRoot();
        AlignToLaneFloor();
        PlayIdle();
        return _animator != null;
    }

    public void ApplyExtraVisualLocalOffset(Vector3 offset)
    {
        if (!visualsRoot || offset.sqrMagnitude <= 1e-8f)
            return;

        visualsRoot.localPosition += offset;
    }

    public void AlignFloorToOwnerSoldier(Transform ownerRoot)
    {
        AlignToLaneFloor();
    }

    /// <summary>
    /// Keeps the minion on the canonical lane floor (same line as player, merchants, and signposts).
    /// Uses a cached feet offset — no per-frame <see cref="Physics2D.SyncTransforms"/>.
    /// </summary>
    public void AlignToLaneFloor(float feetYOffset = 0f)
    {
        if (!ShouldPreserveLaneHierarchy())
            LaneGroundEffectPlacement.AttachUnitToLane(transform);

        float floorTop = LaneGroundEffectPlacement.GetLaneFloorTopWorldY() + feetYOffset;
        float targetRootY = _hasColliderBottomBelowRoot
            ? floorTop + _colliderBottomBelowRoot
            : floorTop;

        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.y - targetRootY) <= 1e-5f)
            return;

        pos.y = targetRootY;
        transform.position = pos;

        if (_rb)
            _rb.position = new Vector2(pos.x, pos.y);
    }

    private void CacheColliderBottomBelowRoot()
    {
        Collider2D minionCol = GetSoldierCollider();
        if (!minionCol)
        {
            _hasColliderBottomBelowRoot = false;
            return;
        }

        Physics2D.SyncTransforms();
        _colliderBottomBelowRoot = transform.position.y - minionCol.bounds.min.y;
        _hasColliderBottomBelowRoot = true;
    }

    private bool ShouldPreserveLaneHierarchy() =>
        gameObject.scene.name == "DontDestroyOnLoad";

    public void SyncGroundY(float worldY)
    {
        if (!_rb)
            return;

        Vector2 pos = _rb.position;
        if (Mathf.Abs(pos.y - worldY) <= 1e-4f)
            return;

        _rb.MovePosition(new Vector2(pos.x, worldY));
    }

    public bool TryGetVisualFlipRoot(out Transform flipRoot)
    {
        flipRoot = _visualFlipRoot;
        return flipRoot != null;
    }

    public void FaceTargetX(float targetWorldX)
    {
        if (!_visualFlipRoot)
            return;

        float myX = transform.position.x;
        bool faceLeft = targetWorldX < myX;
        ApplyVisualFlipScale(faceLeft ? 1f : -1f);
    }

    public void SetMoveTargetX(float worldX, float speed)
    {
        speed = Mathf.Max(0.01f, speed);
        const float retargetEpsilon = 0.04f;
        const float arriveEpsilon = 0.04f;

        if (Mathf.Abs(transform.position.x - worldX) <= arriveEpsilon)
        {
            StopMovement();
            return;
        }

        if (_wantsMove &&
            Mathf.Abs(_moveTargetX - worldX) <= retargetEpsilon &&
            Mathf.Approximately(_moveSpeed, speed))
        {
            return;
        }

        _wantsMove = true;
        _moveTargetX = worldX;
        _moveSpeed = speed;
        SetLocomotionMoving(true);
    }

    public void StopMovement()
    {
        _wantsMove = false;
        SetLocomotionMoving(false);
        if (_rb)
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    public void TriggerMeleeAttack()
    {
        if (!_animator || _isDeadVisual)
            return;

        float duration = Mathf.Clamp(GetAttackClipLength(), 0.05f, 2f);
        _attackLocked = true;
        _attackUnlockTime = Time.time + duration;
        ResetTriggerSafe(attackTriggerName);
        _animator.SetTrigger(attackTriggerName);
    }

    public void TriggerHurt()
    {
        if (!_animator || _isDeadVisual)
            return;

        ResetTriggerSafe(hurtTriggerName);
        _animator.SetTrigger(hurtTriggerName);
    }

    public void PlayDie()
    {
        if (_isDeadVisual)
            return;

        _isDeadVisual = true;
        StopMovement();

        if (!_animator)
            return;

        ResetTriggerSafe(dieTriggerName);
        _animator.SetTrigger(dieTriggerName);
        if (!string.IsNullOrWhiteSpace(dieStateName))
            _animator.Play(dieStateName, 0, 0f);
    }

    public float GetDieClipLength()
    {
        if (!_animator || string.IsNullOrWhiteSpace(dieStateName))
            return 0.8f;

        var clips = _animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip c = clips[i];
            if (!c)
                continue;
            if (c.name.Equals(dieStateName, System.StringComparison.OrdinalIgnoreCase) ||
                c.name.IndexOf(dieStateName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return c.length;
        }

        return 0.8f;
    }

    public float GetAttackClipLength()
    {
        if (!_animator)
            return 0.35f;

        var clips = _animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip c = clips[i];
            if (!c)
                continue;
            if (c.name.Equals("attack", System.StringComparison.OrdinalIgnoreCase) ||
                c.name.IndexOf("attack", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return c.length;
        }

        return 0.35f;
    }

    private void Update()
    {
        if (_attackLocked && Time.time >= _attackUnlockTime)
            _attackLocked = false;

        if (_isDeadVisual)
            return;

        if (_wantsMove && !_attackLocked)
            TickHorizontalMovement();
        else if (!_attackLocked)
            SetLocomotionMoving(false);

        if (_attackLocked)
            return;

        if (_wantsMove)
            ReassertLocomotion(walkStateName);
    }

    private void TickHorizontalMovement()
    {
        Vector3 pos = transform.position;
        float remaining = Mathf.Abs(_moveTargetX - pos.x);
        float step = _moveSpeed * Time.deltaTime;

        if (remaining <= Mathf.Max(0.02f, step))
        {
            if (remaining > 1e-5f)
            {
                pos.x = _moveTargetX;
                transform.position = pos;
                if (_rb)
                    _rb.position = new Vector2(pos.x, pos.y);
            }

            StopMovement();
            return;
        }

        float newX = Mathf.MoveTowards(pos.x, _moveTargetX, step);
        if (Mathf.Abs(newX - pos.x) > 1e-6f)
            UpdateFacingFromMovement(newX - pos.x);

        pos.x = newX;
        transform.position = pos;
        if (_rb)
            _rb.position = new Vector2(pos.x, pos.y);
    }

    private void UpdateFacingFromMovement(float deltaX)
    {
        if (!_visualFlipRoot || Mathf.Abs(deltaX) <= 1e-5f)
            return;

        bool movingRight = deltaX > 0f;
        ApplyVisualFlipScale(movingRight ? -1f : 1f);
    }

    private void ApplyVisualFlipScale(float newScaleX)
    {
        if (!_visualFlipRoot)
            return;

        if (Mathf.Approximately(newScaleX, 0f))
            newScaleX = 1f;

        if (Mathf.Approximately(newScaleX, _visualFlipScaleX))
            return;

        Transform soldier = _visualFlipRoot.Find("Soldier");
        if (soldier)
        {
            Vector3 lp = soldier.localPosition;
            lp.x = -lp.x;
            soldier.localPosition = lp;
        }

        _visualFlipScaleX = newScaleX;
        _visualFlipRoot.localScale = new Vector3(newScaleX, 1f, 1f);
    }

    private void SetLocomotionMoving(bool moving)
    {
        if (_isDeadVisual || !_animator || _attackLocked)
            return;

        if (_isMoving == moving)
            return;

        _isMoving = moving;
        PlayState(moving ? walkStateName : idleStateName);
    }

    private void PlayIdle() => PlayState(idleStateName);

    private void ReassertLocomotion(string expectedStateName)
    {
        if (!_animator || string.IsNullOrWhiteSpace(expectedStateName))
            return;

        AnimatorStateInfo st = _animator.GetCurrentAnimatorStateInfo(0);
        if (!st.IsName(expectedStateName))
            PlayState(expectedStateName);
        else if (!_isMoving)
            _isMoving = true;
    }

    private Collider2D GetSoldierCollider()
    {
        if (!_visualFlipRoot)
            return null;

        Transform soldier = _visualFlipRoot.Find("Soldier");
        return soldier ? soldier.GetComponent<Collider2D>() : null;
    }

    private void PlayState(string stateName)
    {
        if (!_animator || string.IsNullOrWhiteSpace(stateName))
            return;

        AnimatorStateInfo st = _animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName(stateName))
            return;

        _animator.Play(stateName, 0, 0f);
    }

    private void EnsureVisualHierarchy()
    {
        if (!visualsRoot)
            visualsRoot = transform;

        Transform offset = visualsRoot.Find("VisualOffset");
        if (!offset)
        {
            var offsetGo = new GameObject("VisualOffset");
            offset = offsetGo.transform;
            offset.SetParent(visualsRoot, false);
            offset.localPosition = Vector3.zero;
            offset.localRotation = Quaternion.identity;
            offset.localScale = Vector3.one;
        }

        _visualFlipRoot = offset;
    }

    private void ClearSoldierChildren()
    {
        if (!_visualFlipRoot)
            return;

        for (int i = _visualFlipRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _visualFlipRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void ResetTriggerSafe(string triggerName)
    {
        if (!_animator || string.IsNullOrWhiteSpace(triggerName))
            return;

        if (!HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
            return;

        _animator.ResetTrigger(triggerName);
    }

    private bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType expectedType)
    {
        if (!_animator)
            return false;

        var ps = _animator.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].type == expectedType && ps[i].name == paramName)
                return true;
        }

        return false;
    }
}

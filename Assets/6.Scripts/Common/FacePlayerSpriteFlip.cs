using UnityEngine;

public enum FacePlayerMode
{
    [Tooltip("Face the player while they are within Face Player Range X.")]
    WhenInRange = 0,
    [Tooltip("Face only while dialogue or this merchant's shop is open (not while walk-to-click is pending).")]
    WhenEngaged = 1,
}

/// <summary>
/// Faces a 2D character toward the player using the same scale-x flip as <see cref="EnemyBaseController.FaceTargetX"/>.
/// While the player is within <see cref="facePlayerRangeX"/>, facing updates only when they move to the opposite side
/// (outside <see cref="flipDeadZoneWorld"/>); scale is not reset when the player walks away.
/// Assign <see cref="flipTarget"/> to <c>Visuals</c> so sibling name labels / world dialogue are not mirrored.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Character/Face Player Sprite Flip")]
[DisallowMultipleComponent]
public sealed class FacePlayerSpriteFlip : MonoBehaviour
{
    [SerializeField] private FacePlayerMode faceMode = FacePlayerMode.WhenInRange;

    [Tooltip("Sprites to mirror (enemy visualsRoot analogue). Leave empty to use direct child named Visuals, else this transform.")]
    [SerializeField] private Transform flipTarget;

    [Tooltip("Only update facing while horizontal separation to the player is at most this many world units.")]
    [SerializeField] private float facePlayerRangeX = 5f;

    [Tooltip("Match EnemyBaseController: invert which scale-x sign corresponds to facing left.")]
    [SerializeField] private bool invertFlip;

    [Tooltip("If separation is smaller than this (world units), keep last facing.")]
    [SerializeField] private float flipDeadZoneWorld = 0.02f;

    [Tooltip("Default tag used by EnemyBaseController to find the player transform.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("If set, used instead of tag / Find.")]
    [SerializeField] private PlayerController playerOverride;

    [Tooltip("Forced positive localScale.x after flip (enemy UIRoot / name label behaviour). Optional if UI is sibling of flipTarget.")]
    [SerializeField] private Transform[] unmirrorUiRoots;

    private Transform _playerTf;

    /// <summary>False until we have applied at least one in-range facing (handles first approach).</summary>
    private bool _hasLatchedSide;

    /// <summary>Committed side: player left of this transform in X (same sign as dx = px - myX).</summary>
    private bool _latchedPlayerOnLeft;

    private bool _wasEngaged;

    private void Awake()
    {
        if (!flipTarget)
        {
            Transform v = transform.Find("Visuals");
            flipTarget = v ? v : transform;
        }

        ResolvePlayerTransform();
    }

    private void LateUpdate()
    {
        ResolvePlayerTransform();

        if (!flipTarget)
            return;

        if (!_playerTf)
            return;

        if (!ShouldUpdateFacing())
        {
            if (faceMode == FacePlayerMode.WhenEngaged)
                _wasEngaged = false;
            UnmirrorUiRoots();
            return;
        }

        if (faceMode == FacePlayerMode.WhenEngaged && !_wasEngaged)
        {
            _wasEngaged = true;
            _hasLatchedSide = false;
        }

        float myX = transform.position.x;
        float px = _playerTf.position.x;
        float dx = px - myX;

        if (flipDeadZoneWorld > 0f && Mathf.Abs(dx) < flipDeadZoneWorld)
        {
            UnmirrorUiRoots();
            return;
        }

        bool playerOnLeft = dx < 0f;
        if (!_hasLatchedSide || playerOnLeft != _latchedPlayerOnLeft)
        {
            _latchedPlayerOnLeft = playerOnLeft;
            _hasLatchedSide = true;
            ApplyFacePlayerOnLeft(_latchedPlayerOnLeft);
        }

        UnmirrorUiRoots();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        facePlayerRangeX = Mathf.Max(0f, facePlayerRangeX);
        flipDeadZoneWorld = Mathf.Max(0f, flipDeadZoneWorld);
    }
#endif

    private bool ShouldUpdateFacing()
    {
        if (faceMode == FacePlayerMode.WhenEngaged)
            return IsEngagedWithPlayer();

        return DistanceToPlayerX() <= facePlayerRangeX;
    }

    private bool IsEngagedWithPlayer()
    {
        NPCInteractionSettings settings = GetComponentInParent<NPCInteractionSettings>();
        if (settings != null)
            return settings.IsEngagedWithPlayer();

        MerchantClick merchant = GetComponentInParent<MerchantClick>();
        return merchant != null && merchant.IsShopEngagedWithPlayer();
    }

    private float DistanceToPlayerX()
    {
        if (!_playerTf)
            return float.MaxValue;
        return Mathf.Abs(_playerTf.position.x - transform.position.x);
    }

    private void ResolvePlayerTransform()
    {
        if (playerOverride)
        {
            _playerTf = playerOverride.transform;
            return;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
        if (tagged)
        {
            _playerTf = tagged.transform;
            return;
        }

        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        _playerTf = pc ? pc.transform : null;
    }

    /// <remarks>
    /// Same structure as <see cref="EnemyBaseController.FaceTargetX"/>, but the boolean is negated — many NPC rigs
    /// are mirrored vs enemy sprites so facing toward the player needs the opposite scale-x sign.
    /// Toggle <see cref="invertFlip"/> if a specific prefab still faces the wrong way.
    /// </remarks>
    private void ApplyFacePlayerOnLeft(bool playerIsOnLeft)
    {
        bool faceLeft = playerIsOnLeft;

        bool flip = !faceLeft;
        if (invertFlip)
            flip = !flip;

        Vector3 s = flipTarget.localScale;
        float abs = Mathf.Abs(s.x);
        if (abs < 1e-5f)
            abs = 1f;
        s.x = flip ? -abs : abs;
        flipTarget.localScale = s;
    }

    private void UnmirrorUiRoots()
    {
        if (unmirrorUiRoots == null)
            return;

        for (int i = 0; i < unmirrorUiRoots.Length; i++)
        {
            Transform t = unmirrorUiRoots[i];
            if (!t)
                continue;

            Vector3 ls = t.localScale;
            ls.x = Mathf.Abs(ls.x);
            t.localScale = ls;
        }
    }
}

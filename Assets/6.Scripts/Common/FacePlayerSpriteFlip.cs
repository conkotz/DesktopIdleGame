using UnityEngine;

/// <summary>
/// Faces a 2D character toward the player using the same scale-x flip as <see cref="EnemyBaseController.FaceTargetX"/>.
/// Assign <see cref="flipTarget"/> to <c>Visuals</c> so sibling name labels / world dialogue are not mirrored.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Character/Face Player Sprite Flip")]
[DisallowMultipleComponent]
public sealed class FacePlayerSpriteFlip : MonoBehaviour
{
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

    /// <summary>Captured in Awake; restored when the player moves beyond <see cref="facePlayerRangeX"/>.</summary>
    private Vector3 _defaultFlipTargetLocalScale = Vector3.one;

    private Transform _playerTf;

    private void Awake()
    {
        if (!flipTarget)
        {
            Transform v = transform.Find("Visuals");
            flipTarget = v ? v : transform;
        }

        if (flipTarget)
            _defaultFlipTargetLocalScale = flipTarget.localScale;

        ResolvePlayerTransform();
    }

    private void LateUpdate()
    {
        ResolvePlayerTransform();

        if (!flipTarget)
            return;

        if (!_playerTf)
            return;

        float distX = DistanceToPlayerX();
        if (distX > facePlayerRangeX)
        {
            flipTarget.localScale = _defaultFlipTargetLocalScale;
            UnmirrorUiRoots();
            return;
        }

        float myX = transform.position.x;
        float px = _playerTf.position.x;
        if (flipDeadZoneWorld > 0f && Mathf.Abs(px - myX) < flipDeadZoneWorld)
        {
            UnmirrorUiRoots();
            return;
        }

        ApplyFaceTargetX(px);
        UnmirrorUiRoots();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        facePlayerRangeX = Mathf.Max(0f, facePlayerRangeX);
        flipDeadZoneWorld = Mathf.Max(0f, flipDeadZoneWorld);
    }
#endif

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
    private void ApplyFaceTargetX(float targetX)
    {
        float myX = transform.position.x;
        bool faceLeft = targetX < myX;

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

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Same-map teleporter linked by <see cref="teleporterLinkId"/>.
/// All instances sharing an id form a group: using one sends the player to another member (two-way).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class InMapTeleporter : MonoBehaviour
{
    [Tooltip("Shared link id — every teleporter with the same id can send the player to another member.")]
    [SerializeField] private string teleporterLinkId = "";

    [SerializeField] private TMP_Text nameLabel;

    [SerializeField, Min(0f)] private float cooldownSeconds = 0.75f;

    [SerializeField, Min(0.01f)] private float arriveDistanceX = 0.08f;

    [SerializeField, Min(0f)] private float arrivalOffsetX = 0.35f;

    private static readonly Dictionary<string, List<InMapTeleporter>> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    private float _nextAllowedTime;
    private Collider2D _col;
    private PlayerController _pendingPlayer;
    private float _pendingArrivalX;
    private bool _pendingTeleport;

    public string TeleporterLinkId => teleporterLinkId?.Trim() ?? string.Empty;

#if UNITY_EDITOR
    private void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c)
            c.isTrigger = true;

        if (!nameLabel)
            nameLabel = GetComponentInChildren<TMP_Text>(true);
        RefreshNameLabel();
    }

    private void OnValidate() => RefreshNameLabel();
#endif

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        RefreshNameLabel();
    }

    private void OnEnable()
    {
        Register();
        WorldFloorFollowerRegistry.Register(transform, WorldFloorFollowerRegistry.Category.Actor);
    }

    private void OnDisable()
    {
        Unregister();
        WorldFloorFollowerRegistry.Unregister(transform);
        _pendingTeleport = false;
        _pendingPlayer = null;
    }

    private void Update()
    {
        if (!_pendingTeleport || _pendingPlayer == null || _pendingPlayer.IsDead)
        {
            _pendingTeleport = false;
            _pendingPlayer = null;
            return;
        }

        if (Time.time < _nextAllowedTime)
            return;

        float dx = Mathf.Abs(_pendingPlayer.transform.position.x - _pendingArrivalX);
        if (dx > Mathf.Max(0.01f, arriveDistanceX))
            return;

        _pendingTeleport = false;
        ExecuteTeleport(_pendingPlayer);
        _pendingPlayer = null;
    }

    public void OnClickedByPlayer(PlayerController player)
    {
        if (player == null || player.IsDead || string.IsNullOrWhiteSpace(TeleporterLinkId))
            return;

        if (_col == null)
            _col = GetComponent<Collider2D>();
        if (_col == null)
            return;

        if (!TryResolveDestination(out InMapTeleporter destination))
            return;

        float px = player.transform.position.x;
        Bounds b = _col.bounds;
        float arrivalX = Mathf.Clamp(px, b.min.x, b.max.x);

        _pendingPlayer = player;
        _pendingArrivalX = arrivalX;
        _pendingTeleport = true;

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        combat?.ClearTarget();
        combat?.NotifyPlayerInitiatedMovement();

        player.MoveToPointX(arrivalX);
    }

    public static void CancelPendingApproachForPlayer(PlayerController player)
    {
        if (player == null)
            return;

        foreach (KeyValuePair<string, List<InMapTeleporter>> kv in Registry)
        {
            List<InMapTeleporter> list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                InMapTeleporter teleporter = list[i];
                if (teleporter != null && teleporter._pendingTeleport && teleporter._pendingPlayer == player)
                {
                    teleporter._pendingTeleport = false;
                    teleporter._pendingPlayer = null;
                }
            }
        }
    }

    public void SetTeleporterLinkId(string linkId)
    {
        Unregister();
        teleporterLinkId = string.IsNullOrWhiteSpace(linkId) ? string.Empty : linkId.Trim();
        Register();
        RefreshNameLabel();
    }

    public float GetArrivalWorldX(PlayerController player)
    {
        if (_col == null)
            _col = GetComponent<Collider2D>();

        float centerX = _col != null ? _col.bounds.center.x : transform.position.x;
        if (player == null)
            return centerX;

        float sign = player.transform.position.x <= centerX ? -1f : 1f;
        return centerX + sign * arrivalOffsetX;
    }

    private void ExecuteTeleport(PlayerController player)
    {
        if (!TryResolveDestination(out InMapTeleporter destination) || destination == null)
            return;

        _nextAllowedTime = Time.time + Mathf.Max(0f, cooldownSeconds);

        InMapTeleporter.CancelPendingApproachForPlayer(player);
        MapNodePortalTeleporter.CancelPendingApproachForPlayer(player);

        float destX = destination.GetArrivalWorldX(player);
        player.WarpToWorldX(destX);

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        combat?.ClearTarget();
        combat?.NotifyPlayerInitiatedMovement();
    }

    private bool TryResolveDestination(out InMapTeleporter destination)
    {
        destination = null;
        string key = TeleporterLinkId;
        if (string.IsNullOrEmpty(key))
            return false;

        if (!Registry.TryGetValue(key, out List<InMapTeleporter> members) || members == null || members.Count < 2)
            return false;

        InMapTeleporter best = null;
        float bestDist = float.PositiveInfinity;
        Vector3 origin = transform.position;

        for (int i = 0; i < members.Count; i++)
        {
            InMapTeleporter candidate = members[i];
            if (!candidate || candidate == this || !candidate.isActiveAndEnabled)
                continue;

            float dist = Vector3.SqrMagnitude(candidate.transform.position - origin);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        destination = best;
        return destination != null;
    }

    private void Register()
    {
        string key = TeleporterLinkId;
        if (string.IsNullOrEmpty(key))
            return;

        if (!Registry.TryGetValue(key, out List<InMapTeleporter> list) || list == null)
        {
            list = new List<InMapTeleporter>(4);
            Registry[key] = list;
        }

        if (!list.Contains(this))
            list.Add(this);
    }

    private void Unregister()
    {
        string key = TeleporterLinkId;
        if (string.IsNullOrEmpty(key))
            return;

        if (!Registry.TryGetValue(key, out List<InMapTeleporter> list) || list == null)
            return;

        list.Remove(this);
        if (list.Count == 0)
            Registry.Remove(key);
    }

    private void RefreshNameLabel()
    {
        if (!nameLabel)
            return;

        nameLabel.text = string.IsNullOrWhiteSpace(TeleporterLinkId) ? "Teleporter" : "Teleporter";
    }
}

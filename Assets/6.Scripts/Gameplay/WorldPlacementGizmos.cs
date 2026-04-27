using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class WorldPlacementGizmos : MonoBehaviour
{
    [Header("Scene Markers")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform laneRoot;
    [SerializeField] private BoxCollider2D floorCollider;

    [Header("Display")]
    [SerializeField] private bool showLabels = true;
    [SerializeField] private bool showSpawnGroups = true;
    [SerializeField] private float markerRadius = 0.18f;

    private static readonly Color PlayerSpawnColor = new Color(0f, 0.35f, 1f, 1f);
    private static readonly Color LaneColor = new Color(0f, 0.65f, 0.1f, 1f);
    private static readonly Color EnemySpawnColor = new Color(0.85f, 0.1f, 0f, 1f);
    private static readonly Color LabelTextColor = Color.white;
    private static readonly Color LabelBackgroundColor = new Color(0f, 0f, 0f, 0.75f);

    private void OnValidate()
    {
        AutoAssignMissingReferences();
    }

    private void OnDrawGizmos()
    {
        AutoAssignMissingReferences();

        DrawLane();
        DrawPlayerSpawn();

        if (showSpawnGroups)
            DrawSpawnGroups();
    }

    private void AutoAssignMissingReferences()
    {
        if (!playerSpawnPoint)
            playerSpawnPoint = transform.Find("SpawnPoint_Player");

        if (!laneRoot)
            laneRoot = transform.Find("Lane");

        if (!floorCollider && laneRoot)
            floorCollider = laneRoot.GetComponentInChildren<BoxCollider2D>();
    }

    private void DrawLane()
    {
        if (!floorCollider)
            return;

        Bounds bounds = floorCollider.bounds;
        Vector3 left = new Vector3(bounds.min.x, bounds.center.y, 0f);
        Vector3 right = new Vector3(bounds.max.x, bounds.center.y, 0f);

        Gizmos.color = LaneColor;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        DrawLabel(bounds.center + Vector3.up * 0.35f, "Lane / Floor", LaneColor);
    }

    private void DrawPlayerSpawn()
    {
        if (!playerSpawnPoint)
            return;

        Gizmos.color = PlayerSpawnColor;
        Gizmos.DrawSphere(playerSpawnPoint.position, markerRadius);
        Gizmos.DrawWireSphere(playerSpawnPoint.position, markerRadius * 2f);

        DrawLabel(playerSpawnPoint.position + Vector3.up * 0.35f, "Player Spawn", PlayerSpawnColor);
    }

    private void DrawSpawnGroups()
    {
        SpawnPointGroup[] groups = GetComponentsInChildren<SpawnPointGroup>(true);
        Gizmos.color = EnemySpawnColor;

        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            SpawnPointGroup group = groups[groupIndex];
            if (!group)
                continue;

            IReadOnlyList<Transform> points = group.Points;
            for (int i = 0; i < points.Count; i++)
            {
                Transform point = points[i];
                if (!point)
                    continue;

                Gizmos.DrawSphere(point.position, markerRadius * 0.45f);
                Gizmos.DrawWireSphere(point.position, markerRadius);
                DrawLabel(point.position + Vector3.up * 0.25f, point.name, EnemySpawnColor);
            }
        }
    }

    private void DrawLabel(Vector3 position, string text, Color color)
    {
        if (!showLabels)
            return;

#if UNITY_EDITOR
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            normal =
            {
                textColor = LabelTextColor,
                background = Texture2D.whiteTexture
            },
            padding = new RectOffset(4, 4, 2, 2)
        };

        Color oldColor = GUI.color;
        GUI.color = LabelBackgroundColor;
        Handles.Label(position, text, style);
        GUI.color = oldColor;
#endif
    }
}

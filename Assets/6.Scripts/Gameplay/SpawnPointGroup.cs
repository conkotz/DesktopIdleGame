using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on an empty GameObject in the GamePlay scene to define a named set of spawn points.
/// Child transforms are treated as spawn locations (or you can provide an explicit list).
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay/Spawn Point Group")]
[DisallowMultipleComponent]
public class SpawnPointGroup : MonoBehaviour
{
    [Tooltip("Must match MapNodeDefinition.spawnGroupPlans[*].groupId.")]
    public string groupId = "Default";

    [Tooltip("Optional explicit list; if empty, child transforms are used.")]
    [SerializeField] private List<Transform> points = new();

    public IReadOnlyList<Transform> Points
    {
        get
        {
            if (points != null && points.Count > 0)
                return points;

            var list = new List<Transform>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
                list.Add(transform.GetChild(i));
            return list;
        }
    }
}


using System;
using UnityEngine;

/// <summary>
/// Per-minion visual tweaks for the Soulforged Warrior Soldier rig and overhead strip.
/// </summary>
[Serializable]
public struct SoulforgedWarriorPositionAdjustment
{
    [Tooltip("Extra local offset applied to the Visuals root after rig setup (negative Y moves the soldier down).")]
    public Vector3 soldierVisualLocalOffset;

    [Tooltip("Extra local offset applied to the OverheadAnchor (negative X = left, positive Y = up).")]
    public Vector3 overheadAnchorLocalOffset;

    [Tooltip("Multiplies the shared minion presentation visual scale for this warrior only.")]
    [Min(0.05f)]
    public float visualScaleMultiplier;

    public static SoulforgedWarriorPositionAdjustment Default => new SoulforgedWarriorPositionAdjustment
    {
        soldierVisualLocalOffset = new Vector3(0f, -0.22f, 0f),
        overheadAnchorLocalOffset = new Vector3(0.2f, 0.42f, 0f),
        visualScaleMultiplier = 1.12f
    };
}

using System;
using UnityEngine;

[Serializable]
public class SkillProgress
{
    [Min(1)] public int level = 1;
    [Min(0)] public int xp = 0;

    public SkillProgress(int startLevel = 1, int startXp = 0)
    {
        level = Mathf.Max(1, startLevel);
        xp = Mathf.Max(0, startXp);
    }
}
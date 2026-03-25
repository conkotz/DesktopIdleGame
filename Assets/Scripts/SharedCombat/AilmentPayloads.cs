using UnityEngine;

[System.Serializable]
public struct BleedPayload
{
    public float totalDamage;
    public float duration;
    public int ticks;
    public Transform source;

    public BleedPayload(float totalDamage, float duration, int ticks, Transform source)
    {
        this.totalDamage = totalDamage;
        this.duration = duration;
        this.ticks = ticks;
        this.source = source;
    }
}

[System.Serializable]
public struct PoisonPayload
{
    public float totalDamage;
    public float duration;
    public int ticks;
    public int maxStacks;
    public Transform source;

    public PoisonPayload(float totalDamage, float duration, int ticks, int maxStacks, Transform source)
    {
        this.totalDamage = totalDamage;
        this.duration = duration;
        this.ticks = ticks;
        this.maxStacks = maxStacks;
        this.source = source;
    }
}
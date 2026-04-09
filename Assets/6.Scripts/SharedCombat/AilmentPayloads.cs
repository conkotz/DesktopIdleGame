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

[System.Serializable]
public struct ChillPayload
{
    public float duration;
    public int maxStacks;
    public float slowPerStack;
    public Transform source;

    public ChillPayload(float duration, int maxStacks, float slowPerStack, Transform source)
    {
        this.duration = duration;
        this.maxStacks = maxStacks;
        this.slowPerStack = slowPerStack;
        this.source = source;
    }
}

[System.Serializable]
public struct BurnPayload
{
    public float sourceDamage;
    public float burnDamageMultiplier;
    public Transform source;

    public BurnPayload(float sourceDamage, float burnDamageMultiplier, Transform source)
    {
        this.sourceDamage = sourceDamage;
        this.burnDamageMultiplier = burnDamageMultiplier;
        this.source = source;
    }
}

[System.Serializable]
public struct ShockPayload
{
    public float duration;
    public float damageTakenMultiplier;
    public Transform source;

    public ShockPayload(float duration, float damageTakenMultiplier, Transform source)
    {
        this.duration = duration;
        this.damageTakenMultiplier = damageTakenMultiplier;
        this.source = source;
    }
}
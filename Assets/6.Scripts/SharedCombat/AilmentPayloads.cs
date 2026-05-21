using UnityEngine;

[System.Serializable]
public struct BleedPayload
{
    public float totalDamage;
    public float duration;
    public int ticks;
    public Transform source;
    public string outgoingDpsSourceLabel;
    public bool outgoingAttributeToMinion;

    public BleedPayload(
        float totalDamage,
        float duration,
        int ticks,
        Transform source,
        string outgoingDpsSourceLabel = null,
        bool outgoingAttributeToMinion = false)
    {
        this.totalDamage = totalDamage;
        this.duration = duration;
        this.ticks = ticks;
        this.source = source;
        this.outgoingDpsSourceLabel = outgoingDpsSourceLabel;
        this.outgoingAttributeToMinion = outgoingAttributeToMinion;
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
    [Tooltip("Player transform for Master of Venoms rules (defaults to source when null).")]
    public Transform poisonMasteryOwner;
    public string outgoingDpsSourceLabel;
    public bool outgoingAttributeToMinion;

    public PoisonPayload(
        float totalDamage,
        float duration,
        int ticks,
        int maxStacks,
        Transform source,
        string outgoingDpsSourceLabel = null,
        bool outgoingAttributeToMinion = false,
        Transform poisonMasteryOwner = null)
    {
        this.totalDamage = totalDamage;
        this.duration = duration;
        this.ticks = ticks;
        this.maxStacks = maxStacks;
        this.source = source;
        this.poisonMasteryOwner = poisonMasteryOwner ? poisonMasteryOwner : source;
        this.outgoingDpsSourceLabel = outgoingDpsSourceLabel;
        this.outgoingAttributeToMinion = outgoingAttributeToMinion;
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
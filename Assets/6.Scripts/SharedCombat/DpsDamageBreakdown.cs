public enum DpsDamageBucket
{
    Physical,
    Magic,
    Corruption,
    Minion,
    Bleed,
    Poison,
    Burn
}

public struct DpsDamageBreakdown
{
    public float Physical;
    public float Magic;
    public float Corruption;
    public float Minion;
    public float Bleed;
    public float Poison;
    public float Burn;

    public float Total => Physical + Magic + Corruption + Minion + Bleed + Poison + Burn;

    public void Add(DpsDamageBucket bucket, float amount)
    {
        if (amount <= 0f)
            return;

        switch (bucket)
        {
            case DpsDamageBucket.Physical:
                Physical += amount;
                break;
            case DpsDamageBucket.Magic:
                Magic += amount;
                break;
            case DpsDamageBucket.Corruption:
                Corruption += amount;
                break;
            case DpsDamageBucket.Minion:
                Minion += amount;
                break;
            case DpsDamageBucket.Bleed:
                Bleed += amount;
                break;
            case DpsDamageBucket.Poison:
                Poison += amount;
                break;
            case DpsDamageBucket.Burn:
                Burn += amount;
                break;
        }
    }

    public DpsDamageBreakdown PerSecond(float duration)
    {
        if (duration <= 0f)
            return default;

        float inv = 1f / duration;
        return new DpsDamageBreakdown
        {
            Physical = Physical * inv,
            Magic = Magic * inv,
            Corruption = Corruption * inv,
            Minion = Minion * inv,
            Bleed = Bleed * inv,
            Poison = Poison * inv,
            Burn = Burn * inv
        };
    }
}

public struct DpsMitigationBreakdown
{
    public float Armour;
    public float MagicResist;
    public float CorruptionResist;
    public float Blocked;
    public float Parry;

    public float Total => Armour + MagicResist + CorruptionResist + Blocked + Parry;

    public void Add(in DpsMitigationBreakdown other)
    {
        Armour += other.Armour;
        MagicResist += other.MagicResist;
        CorruptionResist += other.CorruptionResist;
        Blocked += other.Blocked;
        Parry += other.Parry;
    }

    public DpsMitigationBreakdown PerSecond(float duration)
    {
        if (duration <= 0f)
            return default;

        float inv = 1f / duration;
        return new DpsMitigationBreakdown
        {
            Armour = Armour * inv,
            MagicResist = MagicResist * inv,
            CorruptionResist = CorruptionResist * inv,
            Blocked = Blocked * inv,
            Parry = Parry * inv
        };
    }
}

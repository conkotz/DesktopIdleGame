using System;

[Serializable]
public struct Drop
{
    public string itemId;
    public int amount;

    public Drop(string itemId, int amount)
    {
        this.itemId = itemId;
        this.amount = amount;
    }
}
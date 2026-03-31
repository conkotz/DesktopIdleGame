using UnityEngine;

public enum ActionBarAssignmentKind
{
    None,
    Ability,
    Item
}

[System.Serializable]
public class ActionBarAssignment
{
    [Header("Type")]
    public ActionBarAssignmentKind kind = ActionBarAssignmentKind.None;

    [Header("Shared")]
    public string id;
    public string displayName;
    public Sprite icon;

    [TextArea]
    public string description;

    public bool IsAssigned =>
        kind != ActionBarAssignmentKind.None &&
        !string.IsNullOrWhiteSpace(id);

    public bool IsAbility => kind == ActionBarAssignmentKind.Ability && !string.IsNullOrWhiteSpace(id);
    public bool IsItem => kind == ActionBarAssignmentKind.Item && !string.IsNullOrWhiteSpace(id);


    public static ActionBarAssignment CreateAbility(string id, string displayName, Sprite icon, string description = "")
    {
        return new ActionBarAssignment
        {
            kind = ActionBarAssignmentKind.Ability,
            id = id,
            displayName = displayName,
            icon = icon,
            description = description
        };
    }

    public static ActionBarAssignment CreateItem(ItemDefinition itemDef)
    {
        if (itemDef == null)
            return null;

        return new ActionBarAssignment
        {
            kind = ActionBarAssignmentKind.Item,
            id = itemDef.itemId,
            displayName = itemDef.displayName,
            icon = itemDef.icon,
            description = itemDef.description
        };
    }

    public void Clear()
    {
        kind = ActionBarAssignmentKind.None;
        id = string.Empty;
        displayName = string.Empty;
        icon = null;
        description = string.Empty;
    }
}
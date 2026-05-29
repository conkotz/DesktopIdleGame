using TMPro;
using UnityEngine;

/// <summary>Resolves display labels for world context menu headers.</summary>
public static class WorldContextMenuLabelResolver
{
    public static string Resolve(Collider2D col)
    {
        if (!col)
            return null;

        ResourceNode node = col.GetComponentInParent<ResourceNode>();
        if (node != null && !string.IsNullOrWhiteSpace(node.DisplayName))
            return node.DisplayName.Trim();

        EnemyClick enemyClick = col.GetComponentInParent<EnemyClick>();
        if (enemyClick != null)
        {
            EnemyBaseController enemy = enemyClick.GetEnemy();
            if (enemy != null && !string.IsNullOrWhiteSpace(enemy.DisplayName))
                return enemy.DisplayName.Trim();
        }

        MapNodePortalTeleporter portal = col.GetComponentInParent<MapNodePortalTeleporter>();
        if (portal != null)
            return ResolvePortalLabel(portal, col);

        StorageClick storage = col.GetComponentInParent<StorageClick>();
        if (storage != null)
            return ResolveObjectLabel(storage.gameObject, "Storage");

        if (WorldInteractRouter.IsNoticeBoardCollider(col))
            return ResolveObjectLabel(col.gameObject, "Notice Board");

        MerchantClick merchantClick = col.GetComponentInParent<MerchantClick>();
        Merchant merchant = null;
        if (merchantClick != null)
            merchant = merchantClick.GetComponent<Merchant>() ?? merchantClick.GetComponentInParent<Merchant>();
        string merchantLabel = ResolveCharacterLabel(col.transform, merchant);
        if (!string.IsNullOrWhiteSpace(merchantLabel))
            return merchantLabel;

        NPCInteractionSettings npc = col.GetComponentInParent<NPCInteractionSettings>();
        if (npc != null)
        {
            string npcLabel = ResolveCharacterLabel(npc.transform, null);
            if (!string.IsNullOrWhiteSpace(npcLabel))
                return npcLabel;
        }

        QuestGiver questGiver = col.GetComponentInParent<QuestGiver>();
        if (questGiver != null)
            return ResolveObjectLabel(questGiver.gameObject, "Quest Giver");

        return ResolveObjectLabel(col.gameObject, null);
    }

    private static string ResolvePortalLabel(MapNodePortalTeleporter portal, Collider2D col)
    {
        TMP_Text label = portal.GetComponentInChildren<TMP_Text>(true);
        if (label != null && !string.IsNullOrWhiteSpace(label.text))
            return label.text.Trim();

        return ResolveObjectLabel(col.gameObject, "Entrance");
    }

    private static string ResolveCharacterLabel(Transform anchor, Merchant merchantForPersonName)
    {
        if (!anchor)
            return null;

        if (merchantForPersonName != null)
        {
            string person = merchantForPersonName.CharacterDisplayName;
            if (!string.IsNullOrWhiteSpace(person))
                return person.Trim();

            string role = merchantForPersonName.MerchantName;
            if (!string.IsNullOrWhiteSpace(role))
                return role.Trim();
        }

        NpcIdentity npc = anchor.GetComponentInParent<NpcIdentity>(true);
        if (!npc)
            npc = anchor.GetComponentInChildren<NpcIdentity>(true);
        if (npc != null)
        {
            string person = npc.CharacterDisplayName;
            if (!string.IsNullOrWhiteSpace(person))
                return person.Trim();

            string role = npc.RoleDisplayLabel;
            if (!string.IsNullOrWhiteSpace(role))
                return role.Trim();
        }

        CharacterStats stats = anchor.GetComponentInParent<CharacterStats>(true);
        if (!stats)
            stats = anchor.GetComponentInChildren<CharacterStats>(true);
        if (stats != null && !string.IsNullOrWhiteSpace(stats.UnitDisplayName))
            return stats.UnitDisplayName.Trim();

        return null;
    }

    private static string ResolveObjectLabel(GameObject go, string fallback)
    {
        if (!go)
            return fallback;

        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null && !string.IsNullOrWhiteSpace(label.text))
        {
            string text = label.text.Trim();
            if (!text.StartsWith("DEPLETED", System.StringComparison.OrdinalIgnoreCase))
                return text;
        }

        string humanized = HumanizeUnityObjectName(go.name);
        return string.IsNullOrWhiteSpace(humanized) ? fallback : humanized;
    }

    private static string HumanizeUnityObjectName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string s = raw.Trim();
        if (s.EndsWith("(Clone)", System.StringComparison.Ordinal))
            s = s.Substring(0, s.Length - "(Clone)".Length).Trim();

        s = s.Replace('_', ' ').Trim();
        if (string.IsNullOrWhiteSpace(s))
            return null;

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Copies the owner's live Soldier sprite appearance onto a minion Soldier rig (animator-driven).
/// </summary>
public static class MinionOwnerVisualSnapshot
{
    public static Transform FindSoldierRoot(Transform ownerRoot)
    {
        if (!ownerRoot)
            return null;

        Transform t = ownerRoot.Find("Visuals/VisualOffset/Soldier");
        if (t) return t;
        t = ownerRoot.Find("Visuals/Soldier");
        if (t) return t;

        var all = ownerRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] && all[i].name.Equals("Soldier", System.StringComparison.OrdinalIgnoreCase))
                return all[i];
        }

        return null;
    }

    public static void CopySoldierAppearance(Transform ownerRoot, Transform minionSoldierRoot, Color spectralTint)
    {
        if (!ownerRoot || !minionSoldierRoot)
            return;

        Transform ownerSoldier = FindSoldierRoot(ownerRoot);
        if (!ownerSoldier)
            return;

        var ownerRenderers = ownerSoldier.GetComponentsInChildren<SpriteRenderer>(true);
        var minionByName = new Dictionary<string, SpriteRenderer>();
        var minionRenderers = minionSoldierRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < minionRenderers.Length; i++)
        {
            SpriteRenderer sr = minionRenderers[i];
            if (!sr || sr.GetComponentInParent<Canvas>(true))
                continue;
            minionByName[sr.gameObject.name] = sr;
        }

        for (int i = 0; i < ownerRenderers.Length; i++)
        {
            SpriteRenderer src = ownerRenderers[i];
            if (!src || !src.sprite || src.GetComponentInParent<Canvas>(true))
                continue;
            if (!minionByName.TryGetValue(src.gameObject.name, out SpriteRenderer dst))
                continue;

            dst.sprite = src.sprite;
            dst.flipX = src.flipX;
            dst.flipY = src.flipY;
            dst.drawMode = src.drawMode;
            dst.size = src.size;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingLayerName = src.sortingLayerName;
            dst.sortingOrder = src.sortingOrder;

            Color c = src.color;
            c.r *= spectralTint.r;
            c.g *= spectralTint.g;
            c.b *= spectralTint.b;
            c.a *= spectralTint.a;
            dst.color = c;
        }

        HideEquipmentParts(minionSoldierRoot, "Helmet");
    }

    public static void HideEquipmentParts(Transform soldierRoot, params string[] partNames)
    {
        if (!soldierRoot || partNames == null || partNames.Length == 0)
            return;

        var renderers = soldierRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int p = 0; p < partNames.Length; p++)
        {
            string partName = partNames[p];
            if (string.IsNullOrWhiteSpace(partName))
                continue;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (!sr || !sr.gameObject.name.Equals(partName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                sr.enabled = false;
            }
        }
    }
}

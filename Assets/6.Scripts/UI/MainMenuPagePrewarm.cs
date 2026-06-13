using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared prewarm helpers for heavy main-menu page content (inventory grids, stats, equipment slots).
/// </summary>
public static class MainMenuPagePrewarm
{
    private const int EquipmentSlotBatchSize = 4;

    public static IEnumerator CoPrewarmPageContents(GameObject page)
    {
        if (!page)
            yield break;

        EquipmentSlotUI.PrewarmSharedReferences();

        EquipmentStatsPanelUI[] statsPanels = page.GetComponentsInChildren<EquipmentStatsPanelUI>(true);
        for (int i = 0; i < statsPanels.Length; i++)
        {
            EquipmentStatsPanelUI panel = statsPanels[i];
            if (!panel)
                continue;

            panel.PrewarmForDisplay();
            yield return null;
        }

        EquipmentSlotUI[] equipmentSlots = page.GetComponentsInChildren<EquipmentSlotUI>(true);
        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            EquipmentSlotUI slot = equipmentSlots[i];
            if (slot)
                slot.PrewarmBinding();

            if (i > 0 && i % EquipmentSlotBatchSize == 0)
                yield return null;
        }

        WeaponSetPreviewSlotUI[] previewSlots = page.GetComponentsInChildren<WeaponSetPreviewSlotUI>(true);
        for (int i = 0; i < previewSlots.Length; i++)
        {
            WeaponSetPreviewSlotUI slot = previewSlots[i];
            if (slot)
                slot.PrewarmBinding();
        }

        InventoryGridUI[] inventoryGrids = page.GetComponentsInChildren<InventoryGridUI>(true);
        for (int i = 0; i < inventoryGrids.Length; i++)
        {
            InventoryGridUI grid = inventoryGrids[i];
            if (!grid || !grid.isActiveAndEnabled || grid.IsDisplayPrewarmed)
                continue;

            if (grid.GetComponent<UpgradeInventoryGridUI>() != null)
                continue;

            yield return grid.CoPrewarmPool();
        }

        UpgradeInventoryGridUI[] upgradeGrids = page.GetComponentsInChildren<UpgradeInventoryGridUI>(true);
        for (int i = 0; i < upgradeGrids.Length; i++)
        {
            UpgradeInventoryGridUI grid = upgradeGrids[i];
            if (!grid || grid.IsDisplayPrewarmed)
                continue;

            yield return grid.CoPrewarmPool();
        }

        UpgradePageUI[] upgradePages = page.GetComponentsInChildren<UpgradePageUI>(true);
        for (int i = 0; i < upgradePages.Length; i++)
        {
            UpgradePageUI upgradePage = upgradePages[i];
            if (!upgradePage)
                continue;

            yield return upgradePage.CoPrewarmForDisplay();
        }

        yield return CoFinalizePageLayout(page);
    }

    private static IEnumerator CoFinalizePageLayout(GameObject page)
    {
        if (!page)
            yield break;

        Canvas.ForceUpdateCanvases();

        RectTransform pageRect = page.transform as RectTransform;
        if (pageRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(pageRect);

        yield return null;
        Canvas.ForceUpdateCanvases();

        TMP_Text[] texts = page.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (!text || !text.gameObject.activeInHierarchy)
                continue;

            text.ForceMeshUpdate(true);

            if (i > 0 && i % 24 == 0)
                yield return null;
        }

        yield return null;
    }
}

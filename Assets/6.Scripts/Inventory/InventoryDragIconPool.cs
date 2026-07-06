using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One shared drag ghost for inventory + storage (only one drag runs at a time). Avoids Instantiate/Destroy spikes.
/// </summary>
public static class InventoryDragIconPool
{
    private static GameObject s_go;
    private static RectTransform s_rt;
    private static Image s_img;
    private static Canvas s_overlayCanvas;
    private static int s_baseSortOrder;

    public static void Show(Canvas rootCanvas, Sprite sprite)
    {
        if (rootCanvas == null)
            return;

        Transform canvasTransform = rootCanvas.transform;
        if (s_go == null)
        {
            s_go = new GameObject("PooledInventoryDragIcon");
            s_go.transform.SetParent(canvasTransform, false);
            s_rt = s_go.AddComponent<RectTransform>();
            s_img = s_go.AddComponent<Image>();
            s_img.raycastTarget = false;
            s_img.preserveAspect = true;
            s_rt.sizeDelta = new Vector2(48f, 48f);
        }

        s_go.transform.SetParent(canvasTransform, false);
        s_go.transform.SetAsLastSibling();
        s_img.sprite = sprite;

        s_overlayCanvas = s_go.GetComponent<Canvas>();
        if (s_overlayCanvas == null)
            GameplayScreenOverlayLayout.EnsureNestedOverlayCanvas(s_go, 0);
        s_overlayCanvas = s_go.GetComponent<Canvas>();

        s_baseSortOrder = rootCanvas.sortingOrder + 1;
        s_overlayCanvas.sortingOrder = s_baseSortOrder;
        s_go.SetActive(true);
    }

    public static GameObject GameObject => s_go;
    public static RectTransform RectTransform => s_rt;
    public static Image Image => s_img;

    /// <summary>Elevate the drag ghost above the furnace panel while the pointer is over it.</summary>
    public static void UpdateDragSortOverlay(Vector2 screenPoint, Camera eventCamera)
    {
        if (s_overlayCanvas == null || s_go == null || !s_go.activeSelf)
            return;

        if (FurnaceUI.IsOpen && FurnaceUI.ContainsScreenPoint(screenPoint, eventCamera))
            s_overlayCanvas.sortingOrder = FurnaceUI.CanvasSortingOrder + 1;
        else if (CookingUI.IsOpen && CookingUI.ContainsScreenPoint(screenPoint, eventCamera))
            s_overlayCanvas.sortingOrder = CookingUI.CanvasSortingOrder + 1;
        else if (BlacksmithingUI.IsOpen && BlacksmithingUI.ContainsScreenPoint(screenPoint, eventCamera))
            s_overlayCanvas.sortingOrder = BlacksmithingUI.CanvasSortingOrder + 1;
        else
            s_overlayCanvas.sortingOrder = s_baseSortOrder;
    }

    public static void Hide()
    {
        if (s_go != null)
            s_go.SetActive(false);
    }
}

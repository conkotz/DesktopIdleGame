using UnityEngine;

/// <summary>
/// Places a secondary UI window beside <see cref="MainMenuWindowUI"/> (Character shell), defaulting to the
/// right edge. If that would go past the canvas, mirrors to the left side instead. Finishes with a canvas clamp.
/// </summary>
public static class UIPinNextToMenuWindow
{
    public enum PinSide
    {
        Right,
        Left
    }

    public static void PositionNextToMainMenu(
        RectTransform secondary,
        RectTransform canvasRect,
        MainMenuWindowUI menu,
        float gap = 8f,
        float canvasEdgeMargin = 4f,
        PinSide preferredSide = PinSide.Right)
    {
        if (!secondary || !canvasRect)
            return;

        if (menu == null || !menu.TryGetMenuWindowRect(out RectTransform mainRect) || !mainRect)
            return;

        Canvas.ForceUpdateCanvases();

        Vector3[] mainC = new Vector3[4];
        Vector3[] secC = new Vector3[4];
        Vector3[] canvasC = new Vector3[4];
        mainRect.GetWorldCorners(mainC);
        secondary.GetWorldCorners(secC);
        canvasRect.GetWorldCorners(canvasC);

        float canvasLeft = canvasC[0].x;
        float canvasRight = canvasC[2].x;

        float mainLeft = mainC[0].x;
        float mainRight = mainC[2].x;
        float mainMidY = (mainC[0].y + mainC[2].y) * 0.5f;

        Vector3 secCenter = (secC[0] + secC[2]) * 0.5f;
        float secHalfW = (secC[2].x - secC[0].x) * 0.5f;

        float centerIfRight = mainRight + gap + secHalfW;
        float centerIfLeft = mainLeft - gap - secHalfW;

        float targetCenterX;
        if (preferredSide == PinSide.Left)
        {
            float leftEdge = centerIfLeft - secHalfW;
            if (leftEdge >= canvasLeft + canvasEdgeMargin)
                targetCenterX = centerIfLeft;
            else
                targetCenterX = centerIfRight;
        }
        else
        {
            float rightEdge = centerIfRight + secHalfW;
            if (rightEdge <= canvasRight - canvasEdgeMargin)
                targetCenterX = centerIfRight;
            else
            {
                float leftEdge = centerIfLeft - secHalfW;
                targetCenterX = leftEdge >= canvasLeft + canvasEdgeMargin ? centerIfLeft : centerIfRight;
            }
        }

        secondary.position += new Vector3(
            targetCenterX - secCenter.x,
            mainMidY - secCenter.y,
            0f);

        ClampToCanvas(secondary, canvasRect);
    }

    /// <summary>Places <paramref name="secondary"/> beside <paramref name="anchor"/> (prefers right, mirrors left if needed).</summary>
    public static void PositionBeside(
        RectTransform secondary,
        RectTransform anchor,
        RectTransform canvasRect,
        float gap = 8f,
        float canvasEdgeMargin = 4f)
    {
        if (!secondary || !anchor || !canvasRect)
            return;

        Canvas.ForceUpdateCanvases();

        Vector3[] anchorC = new Vector3[4];
        Vector3[] secC = new Vector3[4];
        Vector3[] canvasC = new Vector3[4];
        anchor.GetWorldCorners(anchorC);
        secondary.GetWorldCorners(secC);
        canvasRect.GetWorldCorners(canvasC);

        float canvasLeft = canvasC[0].x;
        float canvasRight = canvasC[2].x;

        float anchorLeft = anchorC[0].x;
        float anchorRight = anchorC[2].x;
        float anchorMidY = (anchorC[0].y + anchorC[2].y) * 0.5f;

        Vector3 secCenter = (secC[0] + secC[2]) * 0.5f;
        float secHalfW = (secC[2].x - secC[0].x) * 0.5f;

        float centerIfRight = anchorRight + gap + secHalfW;
        float rightEdge = centerIfRight + secHalfW;

        float targetCenterX;
        if (rightEdge <= canvasRight - canvasEdgeMargin)
            targetCenterX = centerIfRight;
        else
        {
            float centerIfLeft = anchorLeft - gap - secHalfW;
            float leftEdge = centerIfLeft - secHalfW;
            targetCenterX = leftEdge >= canvasLeft + canvasEdgeMargin ? centerIfLeft : centerIfRight;
        }

        secondary.position += new Vector3(
            targetCenterX - secCenter.x,
            anchorMidY - secCenter.y,
            0f);

        ClampToCanvas(secondary, canvasRect);
    }

    public static void AlignCenterTo(RectTransform target, RectTransform reference)
    {
        if (!target || !reference)
            return;

        Canvas.ForceUpdateCanvases();

        Vector3[] referenceCorners = new Vector3[4];
        Vector3[] targetCorners = new Vector3[4];
        reference.GetWorldCorners(referenceCorners);
        target.GetWorldCorners(targetCorners);

        Vector3 referenceCenter = (referenceCorners[0] + referenceCorners[2]) * 0.5f;
        Vector3 targetCenter = (targetCorners[0] + targetCorners[2]) * 0.5f;
        target.position += referenceCenter - targetCenter;
    }

    public static void ClampToCanvas(RectTransform rect, RectTransform canvas)
    {
        if (!rect || !canvas) return;

        Canvas.ForceUpdateCanvases();

        if (!TryGetRectWorldBounds(rect, out float rectLeft, out float rectBottom, out float rectRight, out float rectTop))
            return;

        Vector3 offset = ComputeCanvasClampOffset(
            rectLeft, rectBottom, rectRight, rectTop, canvas, out _);

        rect.position += offset;
    }

    /// <summary>
    /// Shifts <paramref name="root"/> so every active child <see cref="RectTransform"/> fits inside the canvas.
    /// </summary>
    public static void ClampSubtreeToCanvas(RectTransform root, RectTransform canvas)
    {
        if (!root || !canvas)
            return;

        Canvas.ForceUpdateCanvases();

        if (!TryGetSubtreeWorldBounds(root, out float boundsLeft, out float boundsBottom, out float boundsRight, out float boundsTop))
        {
            ClampToCanvas(root, canvas);
            return;
        }

        Vector3 offset = ComputeCanvasClampOffset(
            boundsLeft, boundsBottom, boundsRight, boundsTop, canvas, out _);

        root.position += offset;
    }

    private static Vector3 ComputeCanvasClampOffset(
        float boundsLeft,
        float boundsBottom,
        float boundsRight,
        float boundsTop,
        RectTransform canvas,
        out bool needsClamp)
    {
        needsClamp = false;

        Vector3[] canvasCorners = new Vector3[4];
        canvas.GetWorldCorners(canvasCorners);

        float canvasLeft = canvasCorners[0].x;
        float canvasBottom = canvasCorners[0].y;
        float canvasRight = canvasCorners[2].x;
        float canvasTop = canvasCorners[2].y;

        Vector3 offset = Vector3.zero;

        if (boundsLeft < canvasLeft)
        {
            offset.x += canvasLeft - boundsLeft;
            needsClamp = true;
        }

        if (boundsRight > canvasRight)
        {
            offset.x -= boundsRight - canvasRight;
            needsClamp = true;
        }

        if (boundsBottom < canvasBottom)
        {
            offset.y += canvasBottom - boundsBottom;
            needsClamp = true;
        }

        if (boundsTop > canvasTop)
        {
            offset.y -= boundsTop - canvasTop;
            needsClamp = true;
        }

        return offset;
    }

    private static bool TryGetRectWorldBounds(
        RectTransform rect,
        out float minX,
        out float minY,
        out float maxX,
        out float maxY)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        minX = corners[0].x;
        minY = corners[0].y;
        maxX = corners[2].x;
        maxY = corners[2].y;
        return true;
    }

    private static bool TryGetSubtreeWorldBounds(
        RectTransform root,
        out float minX,
        out float minY,
        out float maxX,
        out float maxY)
    {
        minX = float.PositiveInfinity;
        minY = float.PositiveInfinity;
        maxX = float.NegativeInfinity;
        maxY = float.NegativeInfinity;

        bool any = false;
        Vector3[] corners = new Vector3[4];
        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);

        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rt = rects[i];
            if (!rt || !rt.gameObject.activeInHierarchy)
                continue;

            rt.GetWorldCorners(corners);
            for (int c = 0; c < 4; c++)
            {
                any = true;
                if (corners[c].x < minX) minX = corners[c].x;
                if (corners[c].y < minY) minY = corners[c].y;
                if (corners[c].x > maxX) maxX = corners[c].x;
                if (corners[c].y > maxY) maxY = corners[c].y;
            }
        }

        return any;
    }
}

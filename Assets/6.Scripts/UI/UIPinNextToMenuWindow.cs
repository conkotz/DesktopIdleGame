using UnityEngine;

/// <summary>
/// Places a secondary UI window beside <see cref="MainMenuWindowUI"/> (Character shell), defaulting to the
/// right edge. If that would go past the canvas, mirrors to the left side instead. Finishes with a canvas clamp.
/// </summary>
public static class UIPinNextToMenuWindow
{
    public static void PositionNextToMainMenu(
        RectTransform secondary,
        RectTransform canvasRect,
        MainMenuWindowUI menu,
        float gap = 8f,
        float canvasEdgeMargin = 4f)
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
        float rightEdge = centerIfRight + secHalfW;

        float targetCenterX;
        if (rightEdge <= canvasRight - canvasEdgeMargin)
        {
            targetCenterX = centerIfRight;
        }
        else
        {
            float centerIfLeft = mainLeft - gap - secHalfW;
            float leftEdge = centerIfLeft - secHalfW;
            if (leftEdge >= canvasLeft + canvasEdgeMargin)
                targetCenterX = centerIfLeft;
            else
                targetCenterX = centerIfRight;
        }

        float targetCenterY = mainMidY;

        secondary.position += new Vector3(
            targetCenterX - secCenter.x,
            targetCenterY - secCenter.y,
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

    public static void ClampToCanvas(RectTransform rect, RectTransform canvas)
    {
        if (!rect || !canvas) return;

        Canvas.ForceUpdateCanvases();

        Vector3[] rectCorners = new Vector3[4];
        Vector3[] canvasCorners = new Vector3[4];

        rect.GetWorldCorners(rectCorners);
        canvas.GetWorldCorners(canvasCorners);

        Vector3 offset = Vector3.zero;

        float rectLeft = rectCorners[0].x;
        float rectBottom = rectCorners[0].y;
        float rectRight = rectCorners[2].x;
        float rectTop = rectCorners[2].y;

        float canvasLeft = canvasCorners[0].x;
        float canvasBottom = canvasCorners[0].y;
        float canvasRight = canvasCorners[2].x;
        float canvasTop = canvasCorners[2].y;

        if (rectLeft < canvasLeft)
            offset.x += canvasLeft - rectLeft;

        if (rectRight > canvasRight)
            offset.x -= rectRight - canvasRight;

        if (rectBottom < canvasBottom)
            offset.y += canvasBottom - rectBottom;

        if (rectTop > canvasTop)
            offset.y -= rectTop - canvasTop;

        rect.position += offset;
    }
}

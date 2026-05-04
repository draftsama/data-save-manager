#nullable enable

using System;
using UnityEngine;

[Serializable]
public struct DSMRectData
{
    public Vector2 AnchoredPosition;
    public Vector2 SizeDelta;
    public Vector2 AnchorMin;
    public Vector2 AnchorMax;
    public Vector2 Pivot;

    public static readonly DSMRectData Identity = new()
    {
        AnchoredPosition = Vector2.zero,
        SizeDelta = Vector2.zero,
        AnchorMin = new Vector2(0.5f, 0.5f),
        AnchorMax = new Vector2(0.5f, 0.5f),
        Pivot = new Vector2(0.5f, 0.5f),
    };
}

public static class DSMRectTransformExtensions
{
    public static DSMRectData Capture(this RectTransform rectTransform) => new()
    {
        AnchoredPosition = rectTransform.anchoredPosition,
        SizeDelta = rectTransform.sizeDelta,
        AnchorMin = rectTransform.anchorMin,
        AnchorMax = rectTransform.anchorMax,
        Pivot = rectTransform.pivot,
    };

    public static void Restore(this RectTransform rectTransform, DSMRectData data)
    {
        rectTransform.anchoredPosition = data.AnchoredPosition;
        rectTransform.sizeDelta = data.SizeDelta;
        rectTransform.anchorMin = data.AnchorMin;
        rectTransform.anchorMax = data.AnchorMax;
        rectTransform.pivot = data.Pivot;
    }
}

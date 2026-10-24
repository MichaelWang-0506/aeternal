using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Raycast filter for a full-screen blocker: clicks inside any hole pass through to what
/// is underneath, so the tutorial can allow its targets and block the rest.
/// </summary>
public class TutorialClickHole : MonoBehaviour, ICanvasRaycastFilter
{
    /// <summary>Open areas, in this object's local space. Empty blocks every click.</summary>
    public readonly List<Rect> holes = new List<Rect>();

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (holes.Count == 0) return true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPoint, eventCamera, out var local);
        foreach (var hole in holes)
        {
            if (hole.Contains(local)) return false;
        }
        return true;
    }
}

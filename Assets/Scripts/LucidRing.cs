using UnityEngine;
using UnityEngine.UI;

// A cheap UI annulus: no textures, allocations or arrow-shaped glyphs.
[RequireComponent(typeof(CanvasRenderer))]
public class LucidRing : MaskableGraphic
{
    protected LucidRing() { useLegacyMeshGeneration = false; }
    float amount = 1f;
    public float thickness = 3f;
    public float startDegrees = 90f;
    public float Fill { get => amount; set { value = Mathf.Clamp01(value); if (Mathf.Abs(value - amount) < .002f) return; amount = value; SetVerticesDirty(); } }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var r = rectTransform.rect; Vector2 c = r.center;
        float outer = Mathf.Min(r.width, r.height) * .5f, inner = Mathf.Max(0f, outer - thickness);
        int segments = Mathf.Max(1, Mathf.CeilToInt(64 * amount));
        for (int i = 0; i <= segments; i++)
        {
            float a = (startDegrees - 360f * amount * i / segments) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            vh.AddVert(c + dir * inner, color, Vector2.zero); vh.AddVert(c + dir * outer, color, Vector2.zero);
            if (i > 0) { int j = i * 2; vh.AddTriangle(j - 2, j - 1, j); vh.AddTriangle(j, j - 1, j + 1); }
        }
    }
}

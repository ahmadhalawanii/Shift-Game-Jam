using UnityEngine;
using UnityEngine.UI;

// Letter spacing for legacy UI Text: pushes each glyph quad sideways, line by line, honouring alignment.
[RequireComponent(typeof(Text))]
public class LucidTracking : BaseMeshEffect
{
    public float spacing = 2f;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Mathf.Approximately(spacing, 0f)) return;
        int quads = vh.currentVertCount / 4;
        if (quads < 2) return;
        var text = GetComponent<Text>();
        float align = 0f;
        switch (text.alignment)
        {
            case TextAnchor.UpperCenter: case TextAnchor.MiddleCenter: case TextAnchor.LowerCenter: align = .5f; break;
            case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: align = 1f; break;
        }
        // Spaces make no quad, so tracking alone would squeeze words together. When quads map one to one
        // onto visible characters, each space also widens by the tracking amount.
        string s = text.text ?? "";
        var gaps = new int[quads];
        int visible = 0, spaces = 0;
        foreach (char ch in s)
        {
            if (ch == '\n') { spaces = 0; continue; }
            if (char.IsWhiteSpace(ch)) { spaces++; continue; }
            if (visible < quads) gaps[visible] = spaces;
            visible++;
        }
        bool mapped = visible == quads;
        var v = new UIVertex();
        int lineStart = 0;
        float lastX = float.NegativeInfinity;
        for (int q = 0; q <= quads; q++)
        {
            float x = 0f;
            if (q < quads) { vh.PopulateUIVertex(ref v, q * 4); x = v.position.x; }
            // A glyph left of its predecessor starts a new line.
            if (q == quads || x < lastX - 1f)
            {
                int count = q - lineStart;
                int baseGap = mapped ? gaps[lineStart] : 0;
                int lineGaps = mapped ? gaps[q - 1] - baseGap : 0;
                float shift = -((count - 1) + lineGaps) * spacing * align;
                for (int k = 0; k < count; k++)
                    for (int j = 0; j < 4; j++)
                    {
                        int index = (lineStart + k) * 4 + j;
                        vh.PopulateUIVertex(ref v, index);
                        int extra = mapped ? gaps[lineStart + k] - baseGap : 0;
                        v.position.x += (k + extra) * spacing + shift;
                        vh.SetUIVertex(v, index);
                    }
                lineStart = q;
            }
            lastX = x;
        }
    }
}

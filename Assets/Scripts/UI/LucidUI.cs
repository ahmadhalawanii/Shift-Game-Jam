using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shared look for every Lucid screen. Colours are the ones the game already uses (HUD cream, the Dream
// cyan of the arena glow, the pink of the seeds, the Nightmare ember, the health red), nothing new.
// Text uses storybook Windows fonts: Constantia for titles, Candara for everything you read in play.
public static class LucidUI
{
    public static readonly Color Cream = new Color(.94f, .88f, 1f);          // LucidCombatHUD text
    public static readonly Color Dream = new Color(.57f, .92f, .89f);        // Dream arena glow, menu accent
    public static readonly Color Pink = new Color(1f, .478f, .784f);         // DreamCombat.Pink, healing seeds
    public static readonly Color Ember = new Color(1f, .49f, .31f);          // Nightmare tint
    public static readonly Color HealthRed = new Color(.9f, .16f, .25f);     // health fill
    public static readonly Color Chip = new Color(1f, .78f, .85f);           // recent damage
    public static readonly Color Panel = new Color(.035f, .035f, .07f, .78f); // HUD plates
    public static readonly Color Night = new Color(.025f, .018f, .055f);     // screen fade colour

    static Font ui, display;
    static Sprite soft;

    // A font dropped into Resources/Fonts wins; otherwise Windows fonts, then Unity's built-in one.
    public static Font Font => ui != null ? ui : ui = Load("Fonts/LucidUI", "Candara", "Corbel", "Segoe UI");
    public static Font Display => display != null ? display : display = Load("Fonts/LucidDisplay", "Constantia", "Georgia", "Candara");

    static Font Load(string resource, params string[] os)
    {
        var f = Resources.Load<Font>(resource);
        if (f != null) return f;
#if !UNITY_WEBGL || UNITY_EDITOR
        try
        {
            var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
            foreach (var name in os)
                if (installed.Contains(name))
                {
                    f = Font.CreateDynamicFontFromOSFont(name, 32);
                    if (f != null) return f;
                }
        }
        catch { }
#endif
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // Tight dark shadow for crispness plus a wider, softer drop for depth.
    public static void Style(UnityEngine.UI.Text t, float tracking = 0f)
    {
        if (tracking != 0f) t.gameObject.AddComponent<LucidTracking>().spacing = tracking;
        var far = t.gameObject.AddComponent<Shadow>();
        far.effectColor = new Color(0f, 0f, 0f, .45f);
        far.effectDistance = new Vector2(0f, -3f);
        var near = t.gameObject.AddComponent<Shadow>();
        near.effectColor = new Color(0f, 0f, 0f, .85f);
        near.effectDistance = new Vector2(1f, -1.5f);
    }

    public static UnityEngine.UI.Text Text(Transform parent, string value, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, float tracking = 0f, bool displayFace = false)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<UnityEngine.UI.Text>();
        t.font = displayFace ? Display : Font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.text = value;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Style(t, tracking);
        return t;
    }

    // 64 px square, opaque in the middle, smooth falloff to the edges. Sliced, it is a soft drop shadow of any size.
    public static Sprite Soft
    {
        get
        {
            if (soft != null) return soft;
            const int n = 64; const float r = 24f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + .5f - n / 2f) - (n / 2f - r));
                    float dy = Mathf.Max(0f, Mathf.Abs(y + .5f - n / 2f) - (n / 2f - r));
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / r);
                    float a = 1f - Mathf.SmoothStep(0f, 1f, d);
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            soft = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(28, 28, 28, 28));
            return soft;
        }
    }

    // Soft shadow placed behind target in the same parent, grown by spread and pushed down by drop.
    public static Image ShadowFor(RectTransform target, float alpha = .55f, float spread = 22f, float drop = 8f)
    {
        var go = new GameObject("Shadow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var r = (RectTransform)go.transform;
        r.SetParent(target.parent, false);
        r.SetSiblingIndex(target.GetSiblingIndex());
        r.anchorMin = target.anchorMin; r.anchorMax = target.anchorMax; r.pivot = target.pivot;
        r.localRotation = target.localRotation;
        if (target.anchorMin == target.anchorMax)
        {
            r.sizeDelta = target.sizeDelta + Vector2.one * spread * 2f;
            r.anchoredPosition = target.anchoredPosition + new Vector2((2f * target.pivot.x - 1f) * spread, (2f * target.pivot.y - 1f) * spread - drop);
        }
        else
        {
            r.offsetMin = target.offsetMin + new Vector2(-spread, -spread - drop);
            r.offsetMax = target.offsetMax + new Vector2(spread, spread - drop);
        }
        var img = go.GetComponent<Image>();
        img.sprite = Soft; img.type = Image.Type.Sliced;
        img.color = new Color(0f, 0f, 0f, alpha);
        img.raycastTarget = false;
        return img;
    }

    public static Image Stretch(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.offsetMin = offsetMin; r.offsetMax = offsetMax;
        var img = go.GetComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return img;
    }

    public static Color Alpha(Color c, float a) { c.a = a; return c; }
}

using UnityEditor;
using UnityEngine;

// Helpers shared by the editor builders (CombatBuilder, MemoryBuilder, the World builders).
public static class BuildKit
{
    // Creates the folder if it is missing. The parent folder must already exist.
    public static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    public static Color H(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }

    // Loads the material at dir/name.mat, or creates it with the given shader.
    public static Material GetOrCreate(string dir, string name, string shader)
    {
        string path = dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var s = Shader.Find(shader);
        if (s == null) throw new System.InvalidOperationException("Shader not found: " + shader + ". Wait for Unity to finish compiling, then run the builder again.");
        m = new Material(s);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // URP Lit material with a colour, optional HDR emission and smoothness.
    public static Material Lit(string dir, string name, string hex, float emission, float smooth)
    {
        var m = GetOrCreate(dir, name, "Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", H(hex));
        m.SetFloat("_Smoothness", smooth);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            m.SetColor("_EmissionColor", H(hex) * emission);
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

// Review tool: renders the main camera from a given pose to an opaque PNG under Temp/LucidShots,
// in either world, without entering Play mode. Used to check the look of the level while building it.
public static class WorldShots
{
    public const string Folder = "Temp/LucidShots";

    public static string Capture(string name, Vector3 position, Vector3 euler, bool nightmare, int width = 1280, int height = 720)
    {
        var cam = Camera.main;
        if (cam == null) return "No main camera";
        SetPreview(nightmare);

        Vector3 keepPos = cam.transform.position;
        Quaternion keepRot = cam.transform.rotation;
        var keepTarget = cam.targetTexture;
        cam.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));

        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        cam.Render();
        cam.Render(); // second pass so depth and opaque textures belong to this view
        var keepActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = keepActive;
        cam.targetTexture = keepTarget;
        RenderTexture.ReleaseTemporary(rt);
        cam.transform.SetPositionAndRotation(keepPos, keepRot);

        Directory.CreateDirectory(Folder);
        string path = Folder + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return Path.GetFullPath(path);
    }

    // Edit-mode preview of one world for every Lucid/World shader, the fog and the volumes.
    public static void SetPreview(bool nightmare)
    {
        var shift = Object.FindFirstObjectByType<WorldShift>();
        if (shift != null) { shift.previewNightmare = nightmare; shift.enabled = false; shift.enabled = true; }
        var look = Object.FindFirstObjectByType<LucidWorldLook>();
        if (look != null) { look.previewNightmare = nightmare; look.enabled = false; look.enabled = true; }
    }

    [MenuItem("Lucid/Preview/Dream")]
    static void PreviewDream() { SetPreview(false); SceneView.RepaintAll(); }

    [MenuItem("Lucid/Preview/Nightmare")]
    static void PreviewNightmare() { SetPreview(true); SceneView.RepaintAll(); }
}

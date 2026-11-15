using UnityEditor;

// Gameplay clip atlases for the How to Play window: full size, no mipmaps, no power-of-two stretching.
class TutorialClipImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Tutorial/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.mipmapEnabled = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.maxTextureSize = 4096;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        ti.alphaSource = TextureImporterAlphaSource.None;
    }
}

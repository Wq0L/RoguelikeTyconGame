using UnityEditor;
using UnityEngine;

// Tuş ikonu atlası (Resources/UI/KeyPrompts, KeyPrompts.cs): sıkıştırmasız (harf kenarları blok blok bozulmasın),
// mipmap'li (küçük yazıda keskin), kenarda tekrar yok, boyut olduğu gibi (2'nin kuvvetine çekilmez).
public sealed class KeyPromptsImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/UI/KeyPrompts/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}

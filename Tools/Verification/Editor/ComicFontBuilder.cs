using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Bungee 400 (SIL OFL 1.1, Docs/ThirdParty-Licenses.md) için TMP font varlığı ve konturlu malzeme (6 Kasım 2026). Yalnız izole
// kopyada çalışır; üretilen dosyalar ana projeye elle kopyalanır (Assets/Art/UI/ComicToon/Fonts/Bungee-Regular SDF.asset ve
// Assets/Art/UI/ComicToon/Bungee Outline.mat, .meta'larıyla). Ayarlar diğer başlık fontlarıyla aynı: 90 pt örnekleme, 12 dolgu,
// SDFAA, 1024² çok sayfalı dinamik atlas; build'de BuildFontAtlasPreparer atlası doldurur ve temizletmez.
// Konturlu malzeme "Heading Outline.mat"ın değerleriyle (siyah kontur 0.18, genişletme 0.25). Yedek font: LilitaOne SDF (onun
// yedekleri: Harvest Comic TR, Barlow) — Bungee'de olmayan bir harf (CJK vb.) oradan gelir.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method ComicFontBuilder.BuildBatch
public static class ComicFontBuilder
{
    const string Root = "Assets/Art/UI/ComicToon/";
    const string FontPath = Root + "Fonts/Bungee-Regular SDF.asset", OutlinePath = Root + "Bungee Outline.mat";

    public static void BuildBatch()
    {
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            var log = new List<string>();
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/Bungee-Regular.ttf");
            if (ttf == null) throw new Exception("Bungee-Regular.ttf not imported");
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null) AssetDatabase.DeleteAsset(FontPath);
            var font = TMP_FontAsset.CreateFontAsset(ttf, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Bungee-Regular SDF";
            AssetDatabase.CreateAsset(font, FontPath);
            foreach (Texture2D atlas in font.atlasTextures) { atlas.name = "Bungee-Regular Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }
            font.material.name = "Bungee-Regular Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            var lilita = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "Fonts/LilitaOne-Regular SDF.asset");
            font.fallbackFontAssetTable = lilita != null ? new List<TMP_FontAsset> { lilita } : new List<TMP_FontAsset>();
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            // Harfler kayıtlı varlığa, BuildFontAtlasPreparer'ın yoluyla eklenir: build'de temizlenmez, font ve atlas dokuları kirli
            // işaretlenip kaydedilir (yalnız font kirli işaretlenince atlas ve tablolar boş kaydediliyordu).
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var serialized = new SerializedObject(font);
            SerializedProperty clearOnBuild = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearOnBuild != null) { clearOnBuild.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            string characters = BuildFontAtlasPreparer.Characters + "ÂâÎîÛû€";
            bool all = font.TryAddCharacters(characters, out string missing);
            EditorUtility.SetDirty(font);
            foreach (Texture2D atlas in font.atlasTextures) if (atlas != null) EditorUtility.SetDirty(atlas);

            if (AssetDatabase.LoadAssetAtPath<Material>(OutlinePath) != null) AssetDatabase.DeleteAsset(OutlinePath);
            var heading = AssetDatabase.LoadAssetAtPath<Material>(Root + "Heading Outline.mat");
            var outline = new Material(font.material) { name = "Bungee Outline" };
            if (heading != null)
                foreach (string p in new[] { "_OutlineColor", "_FaceColor" }) if (heading.HasProperty(p)) outline.SetColor(p, heading.GetColor(p));
            if (heading != null)
                foreach (string p in new[] { "_OutlineWidth", "_FaceDilate", "_OutlineSoftness" }) if (heading.HasProperty(p)) outline.SetFloat(p, heading.GetFloat(p));
            outline.EnableKeyword("OUTLINE_ON");
            AssetDatabase.CreateAsset(outline, OutlinePath);
            AssetDatabase.SaveAssets();

            log.Add("font " + AssetDatabase.AssetPathToGUID(FontPath) + " glyphs " + font.characterTable.Count + " atlases " + font.atlasTextures.Length +
                    " all " + all + " missing '" + missing + "'");
            log.Add("outline " + AssetDatabase.AssetPathToGUID(OutlinePath));
            // Diskteki dosya: harf tablosu ve atlas boyutu gerçekten yazıldı mı.
            string text = File.ReadAllText(FontPath);
            int saved = text.Split(new[] { "m_Unicode:" }, StringSplitOptions.None).Length - 1;
            log.Add("on disk: characters " + saved + ", file " + text.Length + " bytes");
            if (saved < 100) throw new Exception("characters not saved to disk (" + saved + ")");
            foreach (char c in "iIİı") log.Add($"char {c} U+{(int)c:X4} glyph {(font.characterLookupTable.TryGetValue(c, out TMP_Character ch) ? ch.glyphIndex.ToString() : "-")}");
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/ComicFontBuilder.txt", new[] { "PASS" }.Concat(log));
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ComicFontBuilder.txt", "FAIL " + e);
            EditorApplication.Exit(1);
        }
    }
}

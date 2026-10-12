using System;
using UnityEditor;
using UnityEngine;
public static class CritArcArtBuilder
{
 public static void BuildAndRunBatch()
 {
  if(!Application.dataPath.Contains("VerificationProject"))throw new Exception("isolated only");
  AssetDatabase.Refresh();
  var art=AssetDatabase.LoadAssetAtPath<LateGameArtSO>("Assets/Resources/LateGameArt.asset");
  void Bind(LateGameSlot slot,string prefab){art.slots.RemoveAll(x=>x.slot==slot&&x.variant==LateGameVariant.CritArc);art.slots.Add(new LateGameArtSO.SlotBinding{slot=slot,variant=LateGameVariant.CritArc,lifetime=.12f,prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/LateGame/AntennaFamily/"+prefab+".prefab")});}
  Bind(LateGameSlot.ArcBolt,"AntennaSnap");Bind(LateGameSlot.AntennaContact,"ContactDouble");
  const string path="Assets/Art/LateGame/CritArc/CritArcClick.wav";
  var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
  var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
  art.sounds.RemoveAll(x=>x.sound==LateGameSound.CritArc&&x.variant==LateGameVariant.CritArc);
  art.sounds.Add(new LateGameArtSO.SoundBinding{sound=LateGameSound.CritArc,variant=LateGameVariant.CritArc,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path),volume=.32f,repeatSeconds=.07f,voices=2});
  EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();CritArcArtVerification.RunBatch();
 }
}

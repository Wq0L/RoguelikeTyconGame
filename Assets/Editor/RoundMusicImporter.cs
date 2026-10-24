using UnityEditor;
using UnityEngine;

// Keep long music out of decompressed RAM; retain stereo and the original rate.
public sealed class RoundMusicImporter : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Music/", System.StringComparison.Ordinal)) return;
        var importer = (AudioImporter)assetImporter;
        importer.forceToMono = false;
        var settings = importer.defaultSampleSettings;
        settings.preloadAudioData = false;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.85f;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
    }
}

using UnityEditor;
using UnityEngine;

namespace Garden.Editor
{
    public sealed class GardenAudio : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Garden/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            bool ambience = assetPath.EndsWith("/City.wav") || assetPath.EndsWith("/Wind.wav");
            settings.loadType = ambience ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = ambience ? 0.5f : 0.7f;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 24000;
            settings.preloadAudioData = !ambience;
            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings("Android", settings);
        }
    }
}

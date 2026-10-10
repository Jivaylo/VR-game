using UnityEditor;
using UnityEngine;

namespace RealityPlayground.Editor
{

    public sealed class StoryAlarmAudioImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/RealityPlayground/Resources/StoryAlarmAudio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = UnityEngine.AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .82f;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 32000;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}

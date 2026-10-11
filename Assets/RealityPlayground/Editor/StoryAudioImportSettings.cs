using System.IO;
using UnityEditor;
using UnityEngine;

namespace RealityPlayground.Editor
{

    public sealed class StoryAudioImportSettings : AssetPostprocessor
    {
        const string Folder = "Assets/RealityPlayground/StoryAudio/";
        static bool Authored(string path)
        {
            if (!path.StartsWith(Folder) || !path.EndsWith(".wav")) return false;
            string name = Path.GetFileName(path);
            return name.StartsWith("angel-divine-") || name.StartsWith("ambience-") || name.StartsWith("cue-") || name.StartsWith("loop-");
        }

        void OnPreprocessAudio()
        {
            if (Authored(assetPath)) Configure((AudioImporter)assetImporter);
        }

        static void Configure(AudioImporter importer)
        {
            var settings = importer.defaultSampleSettings;

            settings.loadType = Path.GetFileName(importer.assetPath).StartsWith("ambience-") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .78f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.ClearSampleSettingOverride("Android");
        }

        public static void Apply()
        {
            foreach (var path in Directory.GetFiles(Folder, "*.wav"))
            {
                string asset = path.Replace('\\', '/');
                if (!Authored(asset)) continue;
                var importer = AssetImporter.GetAtPath(asset) as AudioImporter;
                if (!importer) continue;
                Configure(importer); importer.SaveAndReimport();
            }
        }
    }
}

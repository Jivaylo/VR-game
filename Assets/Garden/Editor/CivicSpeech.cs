using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Garden.Editor
{
    public static class CivicSpeech
    {
        [Serializable] sealed class Entry { public string speaker, text, path; }
        [Serializable] sealed class Catalog { public Entry[] lines; }

        public static void Apply(GameObject root)
        {
            if (!root || EditorApplication.isPlaying) throw new InvalidOperationException("Open Garden in edit mode.");
            var loop = root.GetComponent<Loop>();
            if (!loop || !loop.talk) throw new InvalidOperationException("Garden loop and wrist receiver are required.");
            var briefing = root.GetComponent<Briefing>();
            if (!briefing) briefing = root.AddComponent<Briefing>();
            briefing.loop = loop;
            briefing.train = root.GetComponentInChildren<Train>(true);
            briefing.station = root.GetComponentInChildren<Assembly>(true);
            briefing.door = root.GetComponentsInChildren<Door>(true).FirstOrDefault(x => !x.localOnly);
            var threshold = root.GetComponentsInChildren<NavPad>(true).FirstOrDefault(x => x.action == "ReachOutside");
            loop.threshold = threshold ? threshold.destination ? threshold.destination : threshold.transform : null;
            loop.briefing = briefing;
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Garden/Audio/Voices.json"));
            if (catalog == null || catalog.lines == null) throw new InvalidOperationException("Voice catalog is missing.");
            var lines = catalog.lines.Select(x => new Talk.Line { speaker = x.speaker, text = x.text, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(x.path) }).ToArray();
            if (lines.Any(x => !x.clip)) throw new InvalidOperationException("A voice clip has not imported.");
            loop.talk.lines = lines;
            EditorUtility.SetDirty(briefing);
            EditorUtility.SetDirty(loop.talk);
            EditorUtility.SetDirty(loop);
            EditorUtility.SetDirty(root);
        }
    }
}

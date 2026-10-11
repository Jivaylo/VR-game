using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Garden.Editor
{
    public static class CivicStyle
    {
        [MenuItem("Garden/Apply Districts")]
        public static void ApplyOpen()
        {
            var root = Root();
            DistrictStyle.Apply(root);
            GuideBuilder.Apply(root);
            CivicSpeech.Apply(root);
            Save(root);
        }

        public static void Routes()
        {
            var root = Root();
            GuideBuilder.Apply(root);
            CivicSpeech.Apply(root);
            Save(root);
        }

        static GameObject Root()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Garden.unity") throw new InvalidOperationException("Open Garden first.");
            return scene.GetRootGameObjects().First(x => x.GetComponent<Loop>());
        }

        static void Save(GameObject root)
        {
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Garden/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name == "Garden" || name == "Rooster") continue;
                var module = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => ObjectNames.Matches(x.name, name) && x.GetComponent<Link>());
                if (module) PrefabUtility.SaveAsPrefabAsset(module.gameObject, path);
            }
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Garden/Prefabs/Garden.prefab");
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            AssetDatabase.SaveAssets();
        }
    }
}

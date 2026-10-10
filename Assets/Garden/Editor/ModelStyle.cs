using System;
using System.IO;
using System.Linq;
using Garden;
using RealityPlayground.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class ModelStyle
    {
        [MenuItem("Garden/Apply Models")]
        public static void ApplyOpen()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(x => x.GetComponent<Loop>());
            if (!root || scene.path != "Assets/Scenes/Garden.unity") throw new InvalidOperationException("Open Garden first.");
            Apply(root);
            PadStyle.Apply(root);
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Garden/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name == "Garden" || name == "Rooster") continue;
                var module = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => ObjectNames.Matches(x.name, name) && x.GetComponent<Link>());
                if (module) PrefabUtility.SaveAsPrefabAsset(module.gameObject, path);
            }
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Garden/Prefabs/Garden.prefab");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ApplyStory();
            EditorSceneManager.SetActiveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("Models installed: " + ModelWalls.Count + " wall panels.");
        }

        public static void Apply(GameObject root)
        {
            var old = ObjectNames.Find(root.transform, "Models");
            if (old) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Models").transform;
            group.SetParent(root.transform, false);
            ModelKit.Build();
            ModelWalls.Apply(root);
            ModelStairs.Apply(root);
            GardenStreets.Apply(root);
            ModelProps.Apply(root);
            Physics.SyncTransforms();
        }

        static void ApplyStory()
        {
            const string path = "Assets/RealityPlayground/StoryPrefabs/RealityStory.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PadStyle.Apply(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            string scenePath = "Assets/Scenes/RealityStory.unity";
            if (!File.Exists(scenePath)) return;
            var scene = SceneManager.GetSceneByPath(scenePath);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<StoryNavMarker>(true)) PadStyle.Apply(root);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SnakeGame.EditorTools
{
    /// <summary>
    /// 首次打开工程时自动创建 Assets/Scenes/Main.unity 并加入构建列表，
    /// 这样克隆下来 → 用 Unity 打开 → 直接按 Play 就能玩。
    /// 场景完全由 Unity API 生成，不依赖手写的 .unity 文本。
    /// </summary>
    [InitializeOnLoad]
    public static class SnakeProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static SnakeProjectSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            if (File.Exists(ScenePath))
            {
                EnsureBuildSettings();
                return;
            }

            CreateScene();
        }

        [MenuItem("Tools/Snake/Open Main Scene")]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else
                CreateScene();
        }

        [MenuItem("Tools/Snake/Rebuild Scene")]
        public static void RebuildScene()
        {
            if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
            CreateScene();
        }

        [MenuItem("Tools/Snake/Add Scene To Build Settings")]
        public static void AddSceneToBuildSettings()
        {
            EnsureBuildSettings();
            Debug.Log("[Snake] " + ScenePath + " 已加入 Build Settings。");
        }

        static void CreateScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("Snake Game");
            root.AddComponent<SnakeBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EnsureBuildSettings();

            Debug.Log("[Snake] 已生成场景 " + ScenePath + "，按 Play 即可开始。");
        }

        static void EnsureBuildSettings()
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene s in current)
                if (s.path == ScenePath) return;

            EditorBuildSettingsScene[] next = new EditorBuildSettingsScene[current.Length + 1];
            for (int i = 0; i < current.Length; i++) next[i] = current[i];
            next[next.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = next;
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TankBattle.EditorTools
{
    // 编辑器辅助：首次打开工程时自动生成 Assets/Scenes/Main.unity 并加入 Build Settings
    [InitializeOnLoad]
    public static class SceneSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string SceneFolder = "Assets/Scenes";

        static SceneSetup()
        {
            EditorApplication.delayCall += EnsureScene;
        }

        [MenuItem("Tank Battle/创建主场景 (Create Main Scene)")]
        public static void CreateMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);

            AddToBuildSettings();
            AssetDatabase.Refresh();

            // 若当前没有已保存的场景（Untitled），自动打开刚生成的主场景
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            Debug.Log("Tank Battle：已生成主场景 " + ScenePath + "（场景可为空，运行期由 Bootstrap 自动搭建）");
        }

        static void EnsureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath)) return;
            CreateMainScene();
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath) return;
            }

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}

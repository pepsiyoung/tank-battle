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
        public static void CreateMainScene() => CreateMainScene(true);

        // interactive=false 表示首次自动生成（不弹任何对话框）
        static void CreateMainScene(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                // 工程刚打开时活动场景是未保存的 Untitled，Unity 禁止此时以 Additive 模式新建场景
                if (interactive && active.isDirty &&
                    !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return; // 用户在保存对话框中取消
                }

                // Single 模式在 Untitled 场景下可用，生成完全空的主场景并保持打开
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else
            {
                // 已有已保存场景打开时沿用 Additive 流程，不打扰当前工作场景
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorSceneManager.CloseScene(scene, true);
            }

            AddToBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log("Tank Battle：已生成主场景 " + ScenePath + "（场景可为空，运行期由 Bootstrap 自动搭建）");
        }

        static void EnsureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath)) return;
            CreateMainScene(false);
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

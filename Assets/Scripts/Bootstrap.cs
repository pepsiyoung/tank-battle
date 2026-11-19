using UnityEngine;

namespace TankBattle
{
    // 无场景依赖的入口：进入 Play 模式后自动创建游戏根对象（场景保持为空即可）
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindObjectOfType<GameController>() != null) return;

            var go = new GameObject("TankBattle");
            go.AddComponent<GameController>();
        }
    }
}

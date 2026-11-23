using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    // 运行时模型装载：优先读取 Assets/Resources/Models/<name>.prefab（团结 AI 生成产物），
    // 找不到返回 null，由调用方走程序化几何体兜底；日后补入 AI prefab 无需改任何代码
    public static class ModelRegistry
    {
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        public static GameObject Prefab(string name)
        {
            GameObject p;
            if (!Cache.TryGetValue(name, out p))
            {
                p = Resources.Load<GameObject>("Models/" + name);
                if (p == null) Debug.Log("[ModelRegistry] 未找到模型 " + name + "，使用程序化几何体兜底");
                Cache[name] = p;
            }
            return p;
        }
    }
}

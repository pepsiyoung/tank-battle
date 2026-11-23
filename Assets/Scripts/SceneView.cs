using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    // 3D 场景渲染层：正交相机自上而下俯视 XZ 战场。
    // 结构物（砖墙/钢墙/坦克/基地/树冠）为真实高度模型——优先加载 Assets/Resources/Models 下的
    // 团结 AI 生成 prefab，缺失时用基础几何体程序化拼装；
    // 地面/水面/冰面/灼烧地/子弹/道具/粒子/死光延续像素贴片平放呈现（复用 SpriteFactory 纹理）。
    // 正俯视下透明物按 Y 高度自然排序，高度分层取代旧 SpriteRenderer sortingOrder：
    // 地面 0 < 水 .015 < 冰 .02 < 灼烧 .03 < 道具 .42 < 星 .52 < 护盾 .54 < 蓄力条 .56
    // < 子弹 .7 < 死光 .75~.83 < 粒子 .9；结构物（墙/坦克/树冠）以实体高度占据 0~0.7
    public class SceneView
    {
        public Camera Cam;

        /* ---------------- 高度分层 ---------------- */

        const float YWater = 0.015f;
        const float YIce = 0.02f;
        const float YBurn = 0.03f;
        const float YFrame = 0.06f;
        const float YPowerup = 0.42f;
        const float YStar = 0.52f;
        const float YShield = 0.54f;
        const float YBar = 0.56f;
        const float YBarAhead = 0.625f; // 蓄力条在坦克北侧的偏移（原 2D 版条在坦克上方 30px）
        const float YBullet = 0.7f;     // 子弹须越过树冠（对应旧 order 25 > 树 20）
        const float YBeamGlow = 0.75f;
        const float YBeamBody = 0.79f;
        const float YBeamCore = 0.83f;
        const float YParticle = 0.9f;
        const float CamHeight = 20f;

        // AI 坦克模型包围盒含炮管，足印比 40px 车体略放宽（仅 AI 路径生效）
        const float AiTankFootprint = 0.96f;

        static readonly Quaternion FlatRot = Quaternion.Euler(90f, 0f, 0f);

        Transform root;

        // 地形槽位 + 按类型对象池（tileCode 脏检查与 2D 版一致：type<<4|variant）
        readonly GameObject[,] tiles = new GameObject[Conf.Rows, Conf.Cols];
        readonly int[,] tileCode = new int[Conf.Rows, Conf.Cols];
        readonly Dictionary<int, Stack<GameObject>> tilePool = new Dictionary<int, Stack<GameObject>>();

        GameObject protoTankP, protoTankE, protoBrick, protoHard, protoSteel, protoBase, protoTree;
        GameObject decalProto;
        bool hardFromAi;

        Material matBrick, matHard, matSteel, matIce;
        readonly Material[] matWater = new Material[3];
        readonly Material[] matBurn = new Material[2];
        readonly Material[] matBaseTop = new Material[4];

        readonly Dictionary<Tank, TankView> tankViews = new Dictionary<Tank, TankView>();
        readonly List<SpriteRenderer> bulletPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> powerupPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> particlePool = new List<SpriteRenderer>();
        readonly List<BeamView> beamPool = new List<BeamView>();

        class TankView
        {
            public GameObject Root;
            public Transform Spin;
            public GameObject Model;
            public List<Material> TintMats;
            public SpriteRenderer Star;
            public SpriteRenderer Shield;
            public SpriteRenderer BarBg;
            public SpriteRenderer BarFill;
            public Color BodyColor;
        }

        class BeamView
        {
            public SpriteRenderer Glow;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
        }

        /* ---------------- 坐标与相机 ---------------- */

        public static Vector3 ToWorld(float px, float py)
        {
            return ToWorld(px, py, 0f);
        }

        // 逻辑像素坐标 → XZ 地平面世界坐标（py=0 的行在北侧 +Z，即屏幕上方；y 为离地高度）
        public static Vector3 ToWorld(float px, float py, float y)
        {
            return new Vector3((px - Conf.FieldW * 0.5f) / Conf.PpU, y, (Conf.FieldH * 0.5f - py) / Conf.PpU);
        }

        public void ApplyCameraMount(bool touch)
        {
            if (Cam == null) return;
            // 触屏时镜头中心南移，战场整体上移，给底部虚拟按键留出空间
            Cam.transform.position = new Vector3(0f, CamHeight, touch ? -1.1f : 0f);
        }

        /* ---------------- 搭建 ---------------- */

        public void Build()
        {
            var camGo = new GameObject("BattleCamera");
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = 7.2f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Conf.PageBg;
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 60f;
            Cam.transform.position = new Vector3(0f, CamHeight, 0f);
            Cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Cam.depth = 10f;
            camGo.AddComponent<AudioListener>();

            // 场景里若已存在相机（例如 Unity 默认 Main Camera）则隐藏，避免画面被覆盖
            foreach (Camera c in Object.FindObjectsOfType<Camera>())
            {
                if (c != Cam) c.gameObject.SetActive(false);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Conf.Hex("26304c");
            QualitySettings.shadowDistance = 40f;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.1f;
            sun.color = Conf.Hex("eaf0ff");
            sunGo.transform.rotation = Quaternion.Euler(58f, -32f, 0f);

            var field = new GameObject("Field");
            root = field.transform;

            BuildGround();
            BuildFrame();
            BuildMaterials();
            BuildPrototypes();

            for (int y = 0; y < Conf.Rows; y++)
                for (int x = 0; x < Conf.Cols; x++)
                    tileCode[y, x] = -1;

            for (int i = 0; i < 48; i++) bulletPool.Add(NewFlatSprite("Bullet" + i));
            for (int i = 0; i < 12; i++) powerupPool.Add(NewFlatSprite("Powerup" + i));
            for (int i = 0; i < 400; i++) particlePool.Add(NewFlatSprite("Particle" + i));
            for (int i = 0; i < 6; i++)
            {
                var v = new BeamView();
                v.Glow = NewFlatSprite("BeamGlow" + i);
                v.Body = NewFlatSprite("BeamBody" + i);
                v.Core = NewFlatSprite("BeamCore" + i);
                beamPool.Add(v);
            }
        }

        void BuildGround()
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(root, false);
            go.transform.rotation = FlatRot;
            go.transform.localScale = new Vector3(Conf.FieldW / Conf.PpU, Conf.FieldH / Conf.PpU, 1f);
            var mr = go.AddComponent<MeshRenderer>();
            var mf = go.AddComponent<MeshFilter>();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mf.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);

            var m = new Material(Shader.Find("Standard"));
            m.mainTexture = SpriteFactory.Ground();
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.06f);
            mr.sharedMaterial = m;
            mr.receiveShadows = true;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildFrame()
        {
            Color line = Conf.Rgba(120, 160, 255, 0.14f);
            float half = Conf.FieldW / 2f;
            float t = 3f;
            AddFrameQuad("FrameTop", ToWorld(half, 0f, YFrame), new Vector3(Conf.FieldW / Conf.PpU, t / Conf.PpU, 1f), line);
            AddFrameQuad("FrameBottom", ToWorld(half, Conf.FieldH, YFrame), new Vector3(Conf.FieldW / Conf.PpU, t / Conf.PpU, 1f), line);
            AddFrameQuad("FrameLeft", ToWorld(0f, half, YFrame), new Vector3(t / Conf.PpU, Conf.FieldH / Conf.PpU, 1f), line);
            AddFrameQuad("FrameRight", ToWorld(Conf.FieldW, half, YFrame), new Vector3(t / Conf.PpU, Conf.FieldH / Conf.PpU, 1f), line);
        }

        void AddFrameQuad(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = FlatRot;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Unit;
            sr.color = color;
        }

        /* ---------------- 材质与原型 ---------------- */

        void BuildMaterials()
        {
            matBrick = LitMat(SpriteFactory.Tile(TileType.Brick, 0));
            matHard = LitMat(SpriteFactory.Tile(TileType.HardBrick, 0));
            matSteel = LitMat(SpriteFactory.Tile(TileType.Steel, 0));
            matIce = UnlitMat(SpriteFactory.Tile(TileType.Ice, 0));
            for (int i = 0; i < 3; i++) matWater[i] = UnlitMat(SpriteFactory.Tile(TileType.Water, i));
            for (int i = 0; i < 2; i++) matBurn[i] = UnlitMat(SpriteFactory.Tile(TileType.Burn, i));
            for (int i = 0; i < 4; i++) matBaseTop[i] = UnlitAlphaMat(SpriteFactory.Tile(TileType.Base, i));
        }

        void BuildPrototypes()
        {
            protoTankP = MakeArchetype("tank_player", delegate { return BuildTankProto(false); }, AiTankFootprint, 0f);
            protoTankE = MakeArchetype("tank_enemy", delegate { return BuildTankProto(true); }, AiTankFootprint, 0f);
            hardFromAi = ModelRegistry.Prefab("wall_brick") != null;
            protoBrick = MakeArchetype("wall_brick", delegate { return BuildWallProto(matBrick); }, 0.98f, 0.45f);
            protoHard = MakeArchetype("wall_brick", delegate { return BuildWallProto(matHard); }, 0.98f, 0.45f);
            protoSteel = MakeArchetype("wall_steel", delegate { return BuildWallProto(matSteel); }, 0.98f, 0.45f);
            protoBase = MakeArchetype("base_hq", BuildBaseProto, 0.95f, 0f);
            protoTree = MakeArchetype("tree_canopy", BuildTreeProto, 1.14f, 0f);
            decalProto = BuildDecalProto();
        }

        static Material LitMat(Sprite s)
        {
            var m = new Material(Shader.Find("Standard"));
            m.mainTexture = s.texture;
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.08f);
            return m;
        }

        static Material UnlitMat(Sprite s)
        {
            var m = new Material(Shader.Find("Unlit/Texture"));
            m.mainTexture = s.texture;
            return m;
        }

        static Material UnlitAlphaMat(Sprite s)
        {
            var m = new Material(Shader.Find("Unlit/Transparent"));
            m.mainTexture = s.texture;
            return m;
        }

        static Material ProtoMat(Color c, float gloss)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        // 归一化 + 包装：AI 模型按包围盒缩放到目标足印/高度（程序化兜底已按目标尺寸构建），
        // 外包一层原点 = 包围盒 XZ 中心且底面贴地的 Root，保证旋转不跑偏
        GameObject MakeArchetype(string resName, System.Func<GameObject> fallback, float footprint, float height)
        {
            GameObject proto = ModelRegistry.Prefab(resName);
            GameObject inst;
            if (proto != null)
            {
                inst = Object.Instantiate(proto);
                inst.transform.position = Vector3.zero;
                Bounds b = BoundsOf(inst);
                float span = Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
                inst.transform.localScale *= footprint / span;
                if (height > 0f)
                {
                    b = BoundsOf(inst);
                    Vector3 ls = inst.transform.localScale;
                    inst.transform.localScale = new Vector3(ls.x, ls.y * (height / Mathf.Max(0.001f, b.size.y)), ls.z);
                }
            }
            else
            {
                inst = fallback();
                inst.transform.position = Vector3.zero;
            }

            foreach (Collider col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            // 包一层 Root：原点 = 包围盒 XZ 中心、底面贴地，旋转与摆放都不跑偏
            var wrap = new GameObject("Arch_" + resName);
            inst.transform.SetParent(wrap.transform, true);
            Bounds bb = BoundsOf(wrap);
            inst.transform.position = new Vector3(-bb.center.x, -bb.min.y, -bb.center.z);
            wrap.SetActive(false);
            return wrap;
        }

        static Bounds BoundsOf(GameObject go)
        {
            bool has = false;
            Bounds b = default(Bounds);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!has) b = new Bounds(go.transform.position, Vector3.one);
            return b;
        }

        static GameObject AddBox(Transform parent, string name, Vector3 size, Vector3 localPos, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static GameObject AddCyl(Transform parent, string name, float radius, float height, Vector3 localPos, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static GameObject AddSphere(Transform parent, string name, Vector3 size, Vector3 localPos, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        /* ---------------- 程序化兜底构建 ---------------- */

        // 坦克：车体/炮塔命名件可被运行时染色（Hull/Turret），炮口 +Z 前向
        GameObject BuildTankProto(bool enemy)
        {
            var rootGo = new GameObject(enemy ? "TankEnemy" : "TankPlayer");
            float hullW = enemy ? 0.6f : 0.54f;
            float hullH = enemy ? 0.24f : 0.22f;
            float hullD = enemy ? 0.52f : 0.46f;

            Material tintMat = ProtoMat(Color.white, 0.35f);
            AddBox(rootGo.transform, "Hull", new Vector3(hullW, hullH, hullD), new Vector3(0f, 0.1f + hullH * 0.5f, 0f), tintMat);

            Material trackMat = ProtoMat(Conf.Hex("1e2530"), 0.1f);
            Material stripeMat = ProtoMat(Conf.Hex("3a4657"), 0.1f);
            float trackX = hullW * 0.5f + 0.065f;
            float trackD = hullD + 0.14f;
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -trackX : trackX;
                string side = s == 0 ? "L" : "R";
                AddBox(rootGo.transform, "Track" + side, new Vector3(0.13f, 0.2f, trackD), new Vector3(sx, 0.1f, 0f), trackMat);
                for (int k = 0; k < 4; k++)
                {
                    float zz = -trackD * 0.5f + 0.08f + k * (trackD - 0.16f) / 3f;
                    AddBox(rootGo.transform, "Stripe" + side + k, new Vector3(0.14f, 0.16f, 0.05f), new Vector3(sx, 0.11f, zz), stripeMat);
                }
            }

            float turretY = 0.1f + hullH + 0.07f;
            if (enemy) AddBox(rootGo.transform, "Turret", new Vector3(0.34f, 0.14f, 0.36f), new Vector3(0f, turretY, -0.02f), tintMat);
            else AddCyl(rootGo.transform, "Turret", 0.2f, 0.15f, new Vector3(0f, turretY, 0f), tintMat);

            Material barrelMat = ProtoMat(Conf.Hex("7c8899"), 0.4f);
            float barrelLen = enemy ? 0.5f : 0.44f;
            float barrelR = enemy ? 0.05f : 0.045f;
            float startZ = enemy ? 0.16f : 0.14f;
            var barrel = AddCyl(rootGo.transform, "Barrel", barrelR, barrelLen, new Vector3(0f, turretY, startZ + barrelLen * 0.5f), barrelMat);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            return rootGo;
        }

        GameObject BuildWallProto(Material m)
        {
            var go = new GameObject("Wall");
            AddBox(go.transform, "Block", new Vector3(0.98f, 0.45f, 0.98f), new Vector3(0f, 0.225f, 0f), m);
            return go;
        }

        GameObject BuildBaseProto()
        {
            var go = new GameObject("BaseHQ");
            Material plinth = ProtoMat(Conf.Hex("3d4a63"), 0.15f);
            AddBox(go.transform, "Plinth", new Vector3(0.92f, 0.3f, 0.92f), new Vector3(0f, 0.15f, 0f), plinth);

            Material gold = ProtoMat(Conf.Gold, 0.5f);
            gold.EnableKeyword("_EMISSION");
            gold.SetColor("_EmissionColor", Conf.Gold * 0.35f);
            AddBox(go.transform, "Rim", new Vector3(0.98f, 0.03f, 0.98f), new Vector3(0f, 0.315f, 0f), gold);

            // 顶面徽章贴片（Unlit 透明，随基地耐久换材质）
            var top = new GameObject("Top");
            top.transform.SetParent(go.transform, false);
            top.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            top.transform.rotation = FlatRot;
            top.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
            var mr = top.AddComponent<MeshRenderer>();
            var mf = top.AddComponent<MeshFilter>();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mf.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            mr.sharedMaterial = matBaseTop[3];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        GameObject BuildTreeProto()
        {
            var go = new GameObject("Tree");
            AddSphere(go.transform, "Canopy1", new Vector3(0.95f, 0.75f, 0.95f), new Vector3(0f, 0.34f, 0f), ProtoMat(Conf.Hex("123a1c"), 0.2f));
            AddSphere(go.transform, "Canopy2", new Vector3(0.8f, 0.6f, 0.8f), new Vector3(-0.16f, 0.46f, 0.12f), ProtoMat(Conf.Hex("1f7a3d"), 0.2f));
            AddSphere(go.transform, "Canopy3", new Vector3(0.66f, 0.52f, 0.66f), new Vector3(0.15f, 0.5f, -0.1f), ProtoMat(Conf.Hex("2c9c50"), 0.2f));
            return go;
        }

        GameObject BuildDecalProto()
        {
            var go = new GameObject("Decal");
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Face";
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(go.transform, false);
            face.transform.localPosition = Vector3.zero;
            face.transform.rotation = FlatRot;
            var mr = face.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterial = matWater[0];
            go.SetActive(false);
            return go;
        }

        /* ---------------- 地形槽位同步 ---------------- */

        GameObject InstantiateProto(GameObject proto)
        {
            var go = Object.Instantiate(proto);
            go.transform.SetParent(root, false);
            go.SetActive(true);
            return go;
        }

        GameObject TakeTile(TileType t)
        {
            Stack<GameObject> st;
            if (!tilePool.TryGetValue((int)t, out st))
            {
                st = new Stack<GameObject>();
                tilePool[(int)t] = st;
            }
            if (st.Count > 0)
            {
                var go = st.Pop();
                go.SetActive(true);
                return go;
            }

            switch (t)
            {
                case TileType.Brick: return InstantiateProto(protoBrick);
                case TileType.HardBrick:
                {
                    var go = InstantiateProto(protoHard);
                    if (hardFromAi) DarkenMaterials(go, 0.62f); // AI 砖墙复用：硬砖整体压暗
                    return go;
                }
                case TileType.Steel: return InstantiateProto(protoSteel);
                case TileType.Base: return InstantiateProto(protoBase);
                case TileType.Tree: return InstantiateProto(protoTree);
                default: return InstantiateProto(decalProto); // 水/冰/灼烧贴片
            }
        }

        void RecycleTile(int type, GameObject go)
        {
            go.SetActive(false);
            Stack<GameObject> st;
            if (!tilePool.TryGetValue(type, out st))
            {
                st = new Stack<GameObject>();
                tilePool[type] = st;
            }
            st.Push(go);
        }

        static float TileY(TileType t)
        {
            if (t == TileType.Water) return YWater;
            if (t == TileType.Ice) return YIce;
            if (t == TileType.Burn) return YBurn;
            return 0f;
        }

        void SetTile(int x, int y, TileType type, int variant)
        {
            int code = ((int)type << 4) | (variant & 15);
            if (code == tileCode[y, x]) return;

            int oldType = tileCode[y, x] < 0 ? -1 : tileCode[y, x] >> 4;
            tileCode[y, x] = code;

            GameObject old = tiles[y, x];
            if (old != null && oldType != (int)type)
            {
                RecycleTile(oldType, old);
                tiles[y, x] = null;
            }

            if (type == TileType.Empty) return;

            GameObject go = tiles[y, x];
            if (go == null)
            {
                go = TakeTile(type);
                tiles[y, x] = go;
            }
            go.transform.position = ToWorld(x * Conf.Tile + Conf.Tile * 0.5f, y * Conf.Tile + Conf.Tile * 0.5f, TileY(type));
            go.transform.rotation = Quaternion.identity;
            ApplyVariant(go, type, variant);
        }

        void ApplyVariant(GameObject go, TileType type, int variant)
        {
            if (type == TileType.Water)
            {
                var mr = go.transform.Find("Face").GetComponent<MeshRenderer>();
                mr.sharedMaterial = matWater[((variant % 3) + 3) % 3];
            }
            else if (type == TileType.Ice)
            {
                go.transform.Find("Face").GetComponent<MeshRenderer>().sharedMaterial = matIce;
            }
            else if (type == TileType.Burn)
            {
                var mr = go.transform.Find("Face").GetComponent<MeshRenderer>();
                mr.sharedMaterial = matBurn[((variant % 2) + 2) % 2];
            }
            else if (type == TileType.Base)
            {
                int hp = Mathf.Clamp(variant, 0, 3);
                foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
                {
                    if (mr.gameObject.name == "Top")
                    {
                        mr.sharedMaterial = matBaseTop[hp];
                        break;
                    }
                }
            }
        }

        static void DarkenMaterials(GameObject go, float k)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i].color = new Color(k, k, k, 1f);
                r.materials = mats;
            }
        }

        /* ---------------- 平面贴片工具 ---------------- */

        SpriteRenderer NewFlatSprite(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.rotation = FlatRot;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Unit;
            sr.color = Color.white;
            go.SetActive(false);
            return sr;
        }

        static SpriteRenderer AttachFlat(Transform parent, string name, Sprite sprite, Color color, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = FlatRot;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            return sr;
        }

        /* ---------------- 主同步 ---------------- */

        public void Sync(Game g, float nowMs)
        {
            if (g == null || g.Grid == null) return;

            int waterFrame = Mathf.FloorToInt(nowMs / 110f) % 3;
            int burnFrame = Mathf.FloorToInt(nowMs / 130f) % 2;

            for (int y = 0; y < Conf.Rows; y++)
            {
                for (int x = 0; x < Conf.Cols; x++)
                {
                    TileType type = g.Grid[y, x];
                    int variant = 0;
                    if (type == TileType.Water) variant = waterFrame;
                    else if (type == TileType.Burn) variant = burnFrame;
                    else if (type == TileType.Base) variant = g.BaseHp;
                    SetTile(x, y, type, variant);
                }
            }

            SyncTanks(g, nowMs);
            SyncBullets(g);
            SyncPowerups(g, nowMs);
            SyncParticles(g);
            SyncBeams(g);
        }

        void SyncTanks(Game g, float nowMs)
        {
            var alive = new List<Tank>();
            foreach (Tank t in g.Tanks)
            {
                alive.Add(t);
                TankView v;
                if (!tankViews.TryGetValue(t, out v))
                {
                    v = CreateTankView(t);
                    tankViews[t] = v;
                }
                UpdateTankView(v, t, nowMs);
            }

            var stale = new List<Tank>();
            foreach (KeyValuePair<Tank, TankView> kv in tankViews)
            {
                if (!alive.Contains(kv.Key)) stale.Add(kv.Key);
            }
            foreach (Tank k in stale)
            {
                TankView v = tankViews[k];
                Object.Destroy(v.Root);
                if (v.TintMats != null)
                    foreach (Material m in v.TintMats) Object.Destroy(m);
                tankViews.Remove(k);
            }
        }

        TankView CreateTankView(Tank t)
        {
            var v = new TankView();
            v.BodyColor = t.Body;

            v.Root = new GameObject("Tank");
            v.Root.transform.SetParent(root, false);

            var spinGo = new GameObject("Spin");
            spinGo.transform.SetParent(v.Root.transform, false);
            v.Spin = spinGo.transform;

            v.Model = Object.Instantiate(t.IsEnemy ? protoTankE : protoTankP);
            v.Model.transform.SetParent(v.Spin, false);
            v.Model.transform.localPosition = Vector3.zero;
            v.Model.transform.localRotation = Quaternion.identity;
            v.Model.SetActive(true);

            // 染色：优先命中命名件（Hull/Turret，白色基材保证染色即所见）；
            // AI 模型无命名件时全体染色
            v.TintMats = new List<Material>();
            Renderer[] rs = v.Model.GetComponentsInChildren<Renderer>(true);
            bool named = false;
            foreach (Renderer r in rs)
            {
                if (r.name.Contains("Hull") || r.name.Contains("Turret")) { named = true; break; }
            }
            foreach (Renderer r in rs)
            {
                bool tint = named ? (r.name.Contains("Hull") || r.name.Contains("Turret")) : true;
                if (!tint) continue;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i].color = t.Body;
                    mats[i].EnableKeyword("_EMISSION");
                    mats[i].SetColor("_EmissionColor", Color.black);
                    v.TintMats.Add(mats[i]);
                }
                r.materials = mats;
            }

            v.Star = AttachFlat(v.Root.transform, "Star", SpriteFactory.Star8(), Conf.Gold, new Vector3(0f, YStar, 0f));
            v.Star.enabled = false;
            v.Shield = AttachFlat(v.Root.transform, "Shield", SpriteFactory.Ring(), Conf.Rgba(120, 220, 255, 0.8f), new Vector3(0f, YShield, 0f));
            v.Shield.enabled = false;
            v.BarBg = AttachFlat(v.Root.transform, "BarBg", SpriteFactory.Unit, Conf.Rgba(0, 0, 0, 0.55f), new Vector3(0f, YBar, YBarAhead));
            v.BarBg.transform.localScale = new Vector3(Conf.TankSize / Conf.PpU, 4f / Conf.PpU, 1f);
            v.BarBg.enabled = false;
            v.BarFill = AttachFlat(v.Root.transform, "BarFill", SpriteFactory.Unit, Color.white, new Vector3(0f, YBar, YBarAhead));
            v.BarFill.enabled = false;

            return v;
        }

        void UpdateTankView(TankView v, Tank t, float nowMs)
        {
            v.Root.transform.position = ToWorld(t.X + t.Size * 0.5f, t.Y + t.Size * 0.5f, 0f);
            v.Spin.localRotation = Quaternion.Euler(0f, 90f * t.Dir, 0f);

            if (v.BodyColor != t.Body)
            {
                v.BodyColor = t.Body;
                foreach (Material m in v.TintMats) m.color = t.Body;
            }

            bool spawnStar = t.IsEnemy && t.SpawnTimer > 0;
            v.Model.SetActive(!spawnStar);
            v.Star.enabled = spawnStar;
            if (spawnStar)
            {
                float phase = Mathf.Repeat(nowMs * 0.006f, 1f);
                float r = 10f + Mathf.Sin(phase * Mathf.PI) * 6f;
                v.Star.transform.localScale = new Vector3(r / 22f, r / 22f, 1f);
                v.Star.transform.rotation = Quaternion.Euler(90f, 0f, nowMs * 0.17f);
            }

            // 受击白闪 / 护盾微光：2D 用半透明遮罩，3D 用发光脉冲
            Color emis = Color.black;
            if (t.FlashTimer > 0)
            {
                float k = 0.4f + 0.4f * Mathf.Abs(Mathf.Sin(nowMs * 0.02f));
                emis = new Color(0.55f * k, 0.55f * k, 0.55f * k, 1f);
            }
            else if (t.Shield > 0)
            {
                float k = 0.04f + 0.05f * Mathf.Abs(Mathf.Sin(nowMs * 0.02f));
                emis = Conf.Gold * k;
            }
            for (int i = 0; i < v.TintMats.Count; i++) v.TintMats[i].SetColor("_EmissionColor", emis);

            if (t.Shield > 0)
            {
                v.Shield.enabled = true;
                float radius = t.Size * 0.5f + 4f + Mathf.Sin(nowMs * 0.01f) * 2f;
                float scale = radius / 28f;
                v.Shield.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                v.Shield.enabled = false;
            }

            bool showBar = t.Charging && t.ChargeT >= Conf.ChargeBarDelay;
            v.BarBg.enabled = showBar;
            v.BarFill.enabled = showBar;
            if (showBar)
            {
                float p = Mathf.Min(1f, t.ChargeT / (float)Conf.ChargeFrames);
                Color col = t.ChargeReady ? Conf.Danger : (t.PlayerIndex == 0 ? Conf.Gold : Conf.Green);
                v.BarFill.color = col;
                float w = Conf.TankSize * p;
                v.BarFill.transform.localScale = new Vector3(Mathf.Max(0.001f, w / Conf.PpU), 4f / Conf.PpU, 1f);
                v.BarFill.transform.localPosition = new Vector3(-(Conf.TankSize - w) * 0.5f / Conf.PpU, YBar, YBarAhead);
            }
        }

        void SyncBullets(Game g)
        {
            int i = 0;
            foreach (Bullet b in g.Bullets)
            {
                if (i >= bulletPool.Count) break;
                SpriteRenderer sr = bulletPool[i++];
                sr.gameObject.SetActive(true);
                sr.sprite = SpriteFactory.Bullet(b.FromPlayer);
                sr.transform.position = ToWorld(b.X + b.W * 0.5f, b.Y + b.H * 0.5f, YBullet);
                sr.transform.rotation = Quaternion.Euler(90f, 0f, -90f * b.Dir);
                sr.transform.localScale = new Vector3(18f / Conf.PpU, 18f / Conf.PpU, 1f);
            }
            for (; i < bulletPool.Count; i++) bulletPool[i].gameObject.SetActive(false);
        }

        static Color PowerupColor(string type)
        {
            switch (type)
            {
                case "star": return Conf.Gold;
                case "grenade": return Conf.Orange;
                case "tank": return Conf.Green;
                case "shovel": return Conf.SteelColor;
                case "shield": return Conf.Accent;
                default: return Conf.Accent2;
            }
        }

        void SyncPowerups(Game g, float nowMs)
        {
            int i = 0;
            foreach (Powerup p in g.Powerups)
            {
                if (i >= powerupPool.Count) break;
                SpriteRenderer sr = powerupPool[i++];
                sr.gameObject.SetActive(true);
                sr.sprite = SpriteFactory.Powerup(p.Type, PowerupColor(p.Type));
                sr.transform.position = ToWorld(p.X + 17f, p.Y + 17f, YPowerup);
                sr.transform.rotation = FlatRot;
                float pulse = 1f + Mathf.Sin(nowMs * 0.006f + p.T) * 0.08f;
                sr.transform.localScale = new Vector3(pulse, pulse, 1f);
            }
            for (; i < powerupPool.Count; i++) powerupPool[i].gameObject.SetActive(false);
        }

        void SyncParticles(Game g)
        {
            int i = 0;
            foreach (Particle p in g.Particles)
            {
                if (i >= particlePool.Count) break;
                SpriteRenderer sr = particlePool[i++];
                sr.gameObject.SetActive(true);
                sr.transform.position = ToWorld(p.X, p.Y, YParticle);
                sr.transform.rotation = FlatRot;

                float a = Mathf.Max(0f, p.Life / 60f);
                Color c = p.Color;

                if (p.Ring)
                {
                    float radius = (1f - a) * 30f + 4f;
                    sr.sprite = SpriteFactory.Circle();
                    sr.transform.localScale = new Vector3(radius * 2f / 64f, radius * 2f / 64f, 1f);
                    c.a = a;
                }
                else
                {
                    sr.sprite = SpriteFactory.Unit;
                    sr.transform.localScale = new Vector3(p.Size / Conf.PpU, p.Size / Conf.PpU, 1f);
                    c.a = Mathf.Min(1f, a * 1.4f);
                }
                sr.color = c;
            }
            for (; i < particlePool.Count; i++) particlePool[i].gameObject.SetActive(false);
        }

        void SyncBeams(Game g)
        {
            int i = 0;
            foreach (Beam b in g.Beams)
            {
                if (i >= beamPool.Count) break;
                BeamView v = beamPool[i++];

                float a = Mathf.Max(0f, b.Life / 22f);
                float x0 = Mathf.Min(b.X0, b.X1);
                float y0 = Mathf.Min(b.Y0, b.Y1);
                float w = Mathf.Abs(b.X1 - b.X0);
                float h = Mathf.Abs(b.Y1 - b.Y0);
                Color col = b.OwnerPi == 0 ? Conf.Gold : Conf.Green;

                v.Glow.gameObject.SetActive(true);
                v.Glow.transform.position = ToWorld(x0 + w * 0.5f, y0 + h * 0.5f, YBeamGlow);
                v.Glow.transform.rotation = FlatRot;
                v.Glow.transform.localScale = new Vector3((w + 10f) / Conf.PpU, (h + 10f) / Conf.PpU, 1f);
                col.a = 0.28f * a;
                v.Glow.color = col;

                v.Body.gameObject.SetActive(true);
                v.Body.transform.position = ToWorld(x0 + w * 0.5f, y0 + h * 0.5f, YBeamBody);
                v.Body.transform.rotation = FlatRot;
                v.Body.transform.localScale = new Vector3(w / Conf.PpU, h / Conf.PpU, 1f);
                col.a = 0.6f * a;
                v.Body.color = col;

                v.Core.gameObject.SetActive(true);
                v.Core.transform.position = ToWorld(x0 + w * 0.5f, y0 + h * 0.5f, YBeamCore);
                v.Core.transform.rotation = FlatRot;
                if (b.Dir == Conf.Up || b.Dir == Conf.Down)
                {
                    float cw = Mathf.Max(4f, w * 0.22f);
                    v.Core.transform.localScale = new Vector3(cw / Conf.PpU, h / Conf.PpU, 1f);
                }
                else
                {
                    float ch = Mathf.Max(4f, h * 0.22f);
                    v.Core.transform.localScale = new Vector3(w / Conf.PpU, ch / Conf.PpU, 1f);
                }
                Color white = Color.white;
                white.a = 0.9f * a;
                v.Core.color = white;
            }

            for (; i < beamPool.Count; i++)
            {
                beamPool[i].Glow.gameObject.SetActive(false);
                beamPool[i].Body.gameObject.SetActive(false);
                beamPool[i].Core.gameObject.SetActive(false);
            }
        }
    }
}

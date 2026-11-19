using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    // 场景渲染层：地形、坦克、子弹、道具、粒子、死光全部用 SpriteRenderer 呈现
    public class SceneView
    {
        public Camera Cam;

        Transform root;
        readonly SpriteRenderer[,] tiles = new SpriteRenderer[Conf.Rows, Conf.Cols];
        readonly int[,] tileCode = new int[Conf.Rows, Conf.Cols];

        readonly Dictionary<Tank, TankView> tankViews = new Dictionary<Tank, TankView>();
        readonly List<SpriteRenderer> bulletPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> powerupPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> particlePool = new List<SpriteRenderer>();
        readonly List<BeamView> beamPool = new List<BeamView>();

        class TankView
        {
            public GameObject Root;
            public Transform Turret;
            public SpriteRenderer Body;
            public SpriteRenderer Star;
            public SpriteRenderer Shield;
            public SpriteRenderer Flash;
            public SpriteRenderer BarBg;
            public SpriteRenderer BarFill;
            public Color BodyColor;
            public Color LightColor;
            public string Label;
        }

        class BeamView
        {
            public SpriteRenderer Glow;
            public SpriteRenderer Body;
            public SpriteRenderer Core;
        }

        public static Vector3 ToWorld(float px, float py)
        {
            return new Vector3((px - Conf.FieldW * 0.5f) / Conf.PpU, (Conf.FieldH * 0.5f - py) / Conf.PpU, 0f);
        }

        public void Build()
        {
            var camGo = new GameObject("BattleCamera");
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = 7.2f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Conf.PageBg;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 100f;
            Cam.transform.position = new Vector3(0f, 0f, -10f);
            Cam.depth = 10f;
            camGo.AddComponent<AudioListener>();

            // 场景里若已存在相机（例如 Unity 默认 Main Camera）则隐藏，避免画面被覆盖
            foreach (Camera c in Object.FindObjectsOfType<Camera>())
            {
                if (c != Cam) c.gameObject.SetActive(false);
            }

            var field = new GameObject("Field");
            root = field.transform;

            for (int y = 0; y < Conf.Rows; y++)
            {
                for (int x = 0; x < Conf.Cols; x++)
                {
                    var go = new GameObject("Tile_" + x + "_" + y);
                    go.transform.SetParent(root, false);
                    go.transform.position = ToWorld(x * Conf.Tile + Conf.Tile / 2f, y * Conf.Tile + Conf.Tile / 2f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = SpriteFactory.Tile(TileType.Empty, 0);
                    sr.sortingOrder = 0;
                    tiles[y, x] = sr;
                    tileCode[y, x] = -1;
                }
            }

            BuildFrame();

            for (int i = 0; i < 48; i++)
            {
                var go = new GameObject("Bullet" + i);
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Bullet(true);
                sr.sortingOrder = 25;
                sr.transform.localScale = new Vector3(4f / Conf.PpU, 7f / Conf.PpU, 1f);
                go.SetActive(false);
                bulletPool.Add(sr);
            }

            for (int i = 0; i < 12; i++)
            {
                var go = new GameObject("Powerup" + i);
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Powerup("star", Conf.Gold);
                sr.sortingOrder = 6;
                go.SetActive(false);
                powerupPool.Add(sr);
            }

            for (int i = 0; i < 400; i++)
            {
                var go = new GameObject("Particle" + i);
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Unit;
                sr.color = Color.white;
                sr.sortingOrder = 35;
                go.SetActive(false);
                particlePool.Add(sr);
            }

            for (int i = 0; i < 6; i++)
            {
                var v = new BeamView();
                v.Glow = NewQuad("BeamGlow" + i, 30, new Color(1f, 1f, 1f, 0f));
                v.Body = NewQuad("BeamBody" + i, 31, new Color(1f, 1f, 1f, 0f));
                v.Core = NewQuad("BeamCore" + i, 32, new Color(1f, 1f, 1f, 0f));
                beamPool.Add(v);
            }
        }

        void BuildFrame()
        {
            Color line = Conf.Rgba(120, 160, 255, 0.14f);
            float half = Conf.FieldW / 2f;
            float t = 3f;
            AddFrameQuad("FrameTop", ToWorld(half, 0f), new Vector3(Conf.FieldW / Conf.PpU, t / Conf.PpU, 1f), line, 1);
            AddFrameQuad("FrameBottom", ToWorld(half, Conf.FieldH), new Vector3(Conf.FieldW / Conf.PpU, t / Conf.PpU, 1f), line, 1);
            AddFrameQuad("FrameLeft", ToWorld(0f, half), new Vector3(t / Conf.PpU, Conf.FieldH / Conf.PpU, 1f), line, 1);
            AddFrameQuad("FrameRight", ToWorld(Conf.FieldW, half), new Vector3(t / Conf.PpU, Conf.FieldH / Conf.PpU, 1f), line, 1);
        }

        void AddFrameQuad(string name, Vector3 pos, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Unit;
            sr.color = color;
            sr.sortingOrder = order;
        }

        SpriteRenderer NewQuad(string name, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Unit;
            sr.color = color;
            sr.sortingOrder = order;
            go.SetActive(false);
            return sr;
        }

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

                    int code = ((int)type << 4) | (variant & 15);
                    if (code == tileCode[y, x]) continue;

                    tileCode[y, x] = code;
                    SpriteRenderer sr = tiles[y, x];
                    sr.sprite = SpriteFactory.Tile(type, variant);
                    sr.sortingOrder = type == TileType.Tree ? 20 : 0;
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
                Object.Destroy(tankViews[k].Root);
                tankViews.Remove(k);
            }
        }

        TankView CreateTankView(Tank t)
        {
            var v = new TankView();
            v.BodyColor = t.Body;
            v.LightColor = t.Light;
            v.Label = t.IsEnemy ? "" : (t.PlayerIndex + 1) + "P";

            v.Root = new GameObject("Tank");
            v.Root.transform.SetParent(root, false);

            var turret = new GameObject("Turret");
            turret.transform.SetParent(v.Root.transform, false);
            v.Turret = turret.transform;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(turret.transform, false);
            v.Body = bodyGo.AddComponent<SpriteRenderer>();
            v.Body.sprite = SpriteFactory.Tank(v.BodyColor, v.LightColor, v.Label);
            v.Body.sortingOrder = 10;

            var flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(v.Root.transform, false);
            flashGo.transform.localScale = new Vector3(Conf.TankSize / Conf.PpU, Conf.TankSize / Conf.PpU, 1f);
            v.Flash = flashGo.AddComponent<SpriteRenderer>();
            v.Flash.sprite = SpriteFactory.Unit;
            v.Flash.color = new Color(1f, 1f, 1f, 0.5f);
            v.Flash.sortingOrder = 12;
            v.Flash.enabled = false;

            var starGo = new GameObject("Star");
            starGo.transform.SetParent(v.Root.transform, false);
            v.Star = starGo.AddComponent<SpriteRenderer>();
            v.Star.sprite = SpriteFactory.Star8();
            v.Star.color = Conf.Gold;
            v.Star.sortingOrder = 11;
            v.Star.enabled = false;

            var shieldGo = new GameObject("Shield");
            shieldGo.transform.SetParent(v.Root.transform, false);
            v.Shield = shieldGo.AddComponent<SpriteRenderer>();
            v.Shield.sprite = SpriteFactory.Ring();
            v.Shield.color = Conf.Rgba(120, 220, 255, 0.8f);
            v.Shield.sortingOrder = 9;
            v.Shield.enabled = false;

            var bgGo = new GameObject("BarBg");
            bgGo.transform.SetParent(v.Root.transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 30f / Conf.PpU, 0f);
            bgGo.transform.localScale = new Vector3(Conf.TankSize / Conf.PpU, 4f / Conf.PpU, 1f);
            v.BarBg = bgGo.AddComponent<SpriteRenderer>();
            v.BarBg.sprite = SpriteFactory.Unit;
            v.BarBg.color = Conf.Rgba(0, 0, 0, 0.55f);
            v.BarBg.sortingOrder = 13;
            v.BarBg.enabled = false;

            var fillGo = new GameObject("BarFill");
            fillGo.transform.SetParent(v.Root.transform, false);
            fillGo.transform.localPosition = new Vector3(-1f, 30f / Conf.PpU, 0f);
            v.BarFill = fillGo.AddComponent<SpriteRenderer>();
            v.BarFill.sprite = SpriteFactory.Unit;
            v.BarFill.sortingOrder = 14;
            v.BarFill.enabled = false;

            return v;
        }

        void UpdateTankView(TankView v, Tank t, float nowMs)
        {
            v.Root.transform.position = ToWorld(t.X + t.Size / 2f, t.Y + t.Size / 2f);
            v.Turret.localRotation = Quaternion.Euler(0f, 0f, -90f * t.Dir);

            if (v.BodyColor != t.Body || v.LightColor != t.Light)
            {
                v.BodyColor = t.Body;
                v.LightColor = t.Light;
                v.Body.sprite = SpriteFactory.Tank(t.Body, t.Light, v.Label);
            }

            bool spawnStar = t.IsEnemy && t.SpawnTimer > 0;
            v.Body.enabled = !spawnStar;
            v.Star.enabled = spawnStar;

            if (spawnStar)
            {
                float phase = Mathf.Repeat(nowMs * 0.006f, 1f);
                float r = 10f + Mathf.Sin(phase * Mathf.PI) * 6f;
                v.Star.transform.localScale = new Vector3(r / 22f, r / 22f, 1f);
                v.Star.transform.localRotation = Quaternion.Euler(0f, 0f, nowMs * 0.17f);
            }

            float alpha = 1f;
            if (t.SpawnTimer > 0 || t.Shield > 0)
            {
                alpha = 0.55f + Mathf.Abs(Mathf.Sin(nowMs * 0.02f)) * 0.45f;
            }
            v.Body.color = new Color(1f, 1f, 1f, alpha);

            if (t.Shield > 0)
            {
                v.Shield.enabled = true;
                float radius = t.Size / 2f + 4f + Mathf.Sin(nowMs * 0.01f) * 2f;
                float scale = radius / 28f;
                v.Shield.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                v.Shield.enabled = false;
            }

            v.Flash.enabled = t.FlashTimer > 0;

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
                v.BarFill.transform.localPosition = new Vector3(-(Conf.TankSize - w) / 2f / Conf.PpU, 30f / Conf.PpU, 0f);
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
                sr.transform.position = ToWorld(b.X + b.W / 2f, b.Y + b.H / 2f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, -90f * b.Dir);
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
                sr.transform.position = ToWorld(p.X + 17f, p.Y + 17f);
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
                sr.transform.position = ToWorld(p.X, p.Y);

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
                Vector3 center = ToWorld(x0 + w / 2f, y0 + h / 2f);
                Color col = b.OwnerPi == 0 ? Conf.Gold : Conf.Green;

                v.Glow.gameObject.SetActive(true);
                v.Glow.transform.position = center;
                v.Glow.transform.localScale = new Vector3((w + 10f) / Conf.PpU, (h + 10f) / Conf.PpU, 1f);
                col.a = 0.28f * a;
                v.Glow.color = col;

                v.Body.gameObject.SetActive(true);
                v.Body.transform.position = center;
                v.Body.transform.localScale = new Vector3(w / Conf.PpU, h / Conf.PpU, 1f);
                col.a = 0.6f * a;
                v.Body.color = col;

                v.Core.gameObject.SetActive(true);
                v.Core.transform.position = center;
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

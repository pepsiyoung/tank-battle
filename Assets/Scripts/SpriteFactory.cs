using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    // 运行时程序化生成所有像素贴图（工程内不包含任何美术资源）
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite _unit;

        // 1×1 纯白精灵：世界尺寸 1 单位，用于缩放绘制色块（进度条、死光、粒子等）
        public static Sprite Unit
        {
            get
            {
                if (_unit == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.SetPixels32(new Color32[] { new Color32(255, 255, 255, 255) });
                    tex.Apply(false, false);
                    _unit = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                }
                return _unit;
            }
        }

        static Sprite Make(string key, int w, int h, System.Action<PixelPainter> draw, float ppu)
        {
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            var p = new PixelPainter(w, h);
            draw(p);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(p.Buf);
            tex.Apply(false, false);

            var sp = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            Cache[key] = sp;
            return sp;
        }

        static Sprite Make(string key, int w, int h, System.Action<PixelPainter> draw)
        {
            return Make(key, w, h, draw, Conf.PpU);
        }

        static string ColorKey(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        /* ---------------- 通用形状 ---------------- */

        public static Sprite Circle()
        {
            return Make("shape:circle", 64, 64, p =>
            {
                p.FillCircle(0f, 0f, 30f, Color.white);
            });
        }

        public static Sprite Ring()
        {
            return Make("shape:ring", 64, 64, p =>
            {
                p.StrokeCircle(0f, 0f, 28f, 2.4f, Color.white);
            });
        }

        public static Sprite Star8()
        {
            return Make("shape:star8", 48, 48, p =>
            {
                var pts = new Vector2[16];
                for (int i = 0; i < 16; i++)
                {
                    float ang = -Mathf.PI / 2f + i * Mathf.PI / 8f;
                    float r = (i % 2 == 0) ? 22f : 9f;
                    pts[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                }
                p.FillPoly(pts, Color.white);
            });
        }

        // UI 圆角矩形（九宫格切片）
        public static Sprite Rounded()
        {
            const string key = "ui:rounded";
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            const int size = 48;
            const int radius = 14;
            const int border = 16;
            var p = new PixelPainter(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float dx = px - cx;
                    float dy = py - cy;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        p.SetPixel(x, y, Color.white);
                    }
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(p.Buf);
            tex.Apply(false, false);

            var sp = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            Cache[key] = sp;
            return sp;
        }

        /* ---------------- 地形 ---------------- */

        public static Sprite Tile(TileType type, int variant)
        {
            switch (type)
            {
                case TileType.Empty:
                    return Make("tile:empty", 48, 48, p =>
                    {
                        p.FillRect(-24f, -24f, 48f, 48f, Conf.EmptyTile);
                        Color line = Conf.Rgba(120, 160, 255, 0.05f);
                        p.FillRect(-24f, -24f, 48f, 1f, line);
                        p.FillRect(-24f, -24f, 1f, 48f, line);
                    });

                case TileType.Brick:
                    return Make("tile:brick", 48, 48, p => DrawBrick(p, Conf.BrickColor, 0.16f, 0.20f));

                case TileType.HardBrick:
                    return Make("tile:hard", 48, 48, p =>
                    {
                        DrawBrick(p, Conf.HardBrickColor, 0.12f, 0.24f);
                        p.FillRect(-14f, -14f, 28f, 28f, Conf.Rgba(0, 0, 0, 0.14f));
                    });

                case TileType.Steel:
                    return Make("tile:steel", 48, 48, p =>
                    {
                        p.FillRect(-24f, -24f, 48f, 48f, Conf.SteelColor);
                        p.FillRect(-24f, -24f, 48f, 3f, Conf.SteelLight);
                        p.FillRect(-24f, -24f, 3f, 48f, Conf.SteelLight);
                        p.FillRect(-24f, 21f, 48f, 3f, Conf.SteelDark);
                        p.FillRect(21f, -24f, 3f, 48f, Conf.SteelDark);
                        p.FillRect(-12f, -12f, 24f, 24f, Conf.SteelInner);
                    });

                case TileType.Water:
                {
                    int frame = ((variant % 3) + 3) % 3;
                    return Make("tile:water" + frame, 48, 48, p =>
                    {
                        p.FillRect(-24f, -24f, 48f, 48f, Conf.WaterDark);
                        for (int i = 0; i < 3; i++)
                        {
                            float yy = ((i * 16 + frame * 14) % 48) - 24f;
                            p.FillRect(-24f, yy, 48f, 4f, Conf.WaterLine);
                        }
                        p.FillRect(-24f, -1f + (frame - 1) * 2f, 48f, 2f, Conf.Rgba(150, 210, 255, 0.55f));
                    });
                }

                case TileType.Ice:
                    return Make("tile:ice", 48, 48, p =>
                    {
                        p.FillRect(-24f, -24f, 48f, 48f, Conf.IceColor);
                        p.FillRect(-22f, -22f, 44f, 3f, Conf.Rgba(255, 255, 255, 0.55f));
                        p.Line(-19f, 19f, 19f, -19f, 2f, Conf.Rgba(255, 255, 255, 0.5f));
                    });

                case TileType.Tree:
                    return Make("tile:tree", 48, 48, p =>
                    {
                        p.FillCircle(0f, 5f, 22f, Conf.Hex("123a1c"));
                        p.FillCircle(-6f, -2f, 16f, Conf.Hex("1f7a3d"));
                        p.FillCircle(6f, -4f, 16f, Conf.Hex("2c9c50"));
                        p.FillCircle(0f, -8f, 16f, Conf.Hex("3fbf63"));
                        p.FillCircle(-3f, -11f, 4f, Conf.Rgba(255, 255, 255, 0.18f));
                    });

                case TileType.Burn:
                {
                    int frame = ((variant % 2) + 2) % 2;
                    return Make("tile:burn" + frame, 48, 48, p =>
                    {
                        p.FillRect(-24f, -24f, 48f, 48f, Conf.BurnBase);
                        p.FillRect(-24f, -24f, 48f, 48f, Conf.BurnBase2);
                        for (int i = 0; i < 5; i++)
                        {
                            int seed = i * 37 + 11;
                            float fx = ((seed * 17 + i * 11) % 48) - 24f;
                            float fy = ((seed * 29 + frame * 21) % 48) - 24f;
                            float glow = 0.45f + (frame == 0 ? 0.15f : 0.45f);
                            float r = 2f + (seed % 3);
                            p.FillCircle(fx, fy, r, Conf.Rgba(255, 140, 60, glow));
                        }
                    });
                }

                case TileType.Base:
                {
                    int hp = Mathf.Clamp(variant, 0, 3);
                    return Make("tile:base" + hp, 48, 48, p => DrawBase(p, hp));
                }
            }

            return Make("tile:empty", 48, 48, p =>
            {
                p.FillRect(-24f, -24f, 48f, 48f, Conf.EmptyTile);
            });
        }

        static void DrawBrick(PixelPainter p, Color baseColor, float lightA, float darkA)
        {
            p.FillRect(-24f, -24f, 48f, 48f, baseColor);
            p.FillRect(-24f, -24f, 48f, 3f, Conf.Rgba(255, 255, 255, lightA));
            p.FillRect(-24f, -24f, 3f, 48f, Conf.Rgba(255, 255, 255, lightA));
            p.FillRect(-24f, 21f, 48f, 3f, Conf.Rgba(0, 0, 0, darkA));
            p.FillRect(21f, -24f, 3f, 48f, Conf.Rgba(0, 0, 0, darkA));
        }

        static void DrawBase(PixelPainter p, int hp)
        {
            p.RadialGlow(0f, 0f, 34f, Conf.Rgba(255, 210, 74, 0.35f), Conf.Rgba(255, 210, 74, 0f));

            Color wing = Conf.Hex("6b4326");
            p.FillPoly(new Vector2[]
            {
                new Vector2(-8.6f, -2.4f), new Vector2(-20.2f, -14.4f), new Vector2(-13.4f, 2.4f)
            }, wing);
            p.FillPoly(new Vector2[]
            {
                new Vector2(9.6f, -1f), new Vector2(19.2f, -13.4f), new Vector2(20.2f, 4.8f)
            }, wing);

            p.FillEllipse(0f, 2.4f, 11.5f, 11.5f, Conf.Hex("7d4f28"));
            p.FillEllipse(12.5f, 1f, 4.8f, 3.8f, Conf.Hex("f2ede2"));
            p.FillCircle(-5.8f, -6.7f, 7.2f, Conf.Hex("f4f0e6"));
            p.FillPoly(new Vector2[]
            {
                new Vector2(-11.5f, -9.6f), new Vector2(-19.2f, -6.2f), new Vector2(-10.6f, -4.8f)
            }, Conf.Hex("f2c14e"));
            p.FillCircle(-6.7f, -7.7f, 1.3f, Conf.Hex("1a1a1a"));
            p.FillRect(-7.7f, 12.5f, 3.8f, 2.9f, Conf.Hex("f2c14e"));
            p.FillRect(1f, 12.5f, 3.8f, 2.9f, Conf.Hex("f2c14e"));

            // 基地生命值（小鸟上方 3 格）
            for (int i = 0; i < 3; i++)
            {
                float px = -16.5f + i * 12f;
                if (i < hp)
                {
                    p.FillRect(px, -22f, 9f, 7f, Conf.Gold);
                    p.FillRect(px, -22f, 9f, 2f, Conf.Rgba(255, 255, 255, 0.45f));
                }
                else
                {
                    p.FillRect(px, -22f, 9f, 7f, Conf.Rgba(255, 255, 255, 0.14f));
                }
            }

            // 破损裂纹
            Color crack = Conf.Rgba(20, 20, 20, 0.7f);
            if (hp <= 2)
            {
                p.Line(-9.6f, -17.3f, 0f, -4.8f, 2f, crack);
                p.Line(0f, -4.8f, -7.7f, 5.8f, 2f, crack);
            }
            if (hp <= 1)
            {
                p.Line(3.8f, -14.4f, 12.5f, -1.9f, 2f, crack);
                p.Line(12.5f, -1.9f, 2.9f, 7.7f, 2f, crack);
            }
        }

        // 3D 地面整张贴图：13×13 格 EmptyTile 底色 + 网格线（SceneView 地面用）
        static Texture2D _groundTex;

        public static Texture2D Ground()
        {
            if (_groundTex != null) return _groundTex;

            int s = (int)Conf.FieldW;
            var p = new PixelPainter(s, s);
            p.FillRect(-s * 0.5f, -s * 0.5f, s, s, Conf.EmptyTile);
            Color line = Conf.Rgba(120, 160, 255, 0.05f);
            for (int i = 0; i < Conf.Cols; i++)
            {
                float x = -s * 0.5f + i * Conf.Tile;
                p.FillRect(x, -s * 0.5f, 1f, s, line);
                float y = -s * 0.5f + i * Conf.Tile;
                p.FillRect(-s * 0.5f, y, s, 1f, line);
            }

            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(p.Buf);
            tex.Apply(false, false);
            _groundTex = tex;
            return tex;
        }

        /* ---------------- 坦克 ---------------- */

        public static Sprite Tank(Color body, Color light, string label)
        {
            string key = "tank:" + ColorKey(body) + ":" + ColorKey(light) + ":" + label;
            return Make(key, 40, 40, p =>
            {
                p.FillRect(-18f, -12f, 36f, 26f, Conf.Rgba(0, 0, 0, 0.35f));
                DrawTrack(p, -18f);
                DrawTrack(p, 9f);

                p.FillRect(-10f, -12f, 20f, 24f, body);
                p.FillRect(-10f, -12f, 20f, 5f, light);
                p.FillRect(-7f, -16f, 14f, 8f, body);
                p.FillRect(-7f, -16f, 14f, 3f, light);
                p.FillRect(-2f, -20f, 4f, 6f, Conf.Hex("7c8899"));

                if (!string.IsNullOrEmpty(label)) DrawLabel(p, label);
            });
        }

        static void DrawTrack(PixelPainter p, float x)
        {
            p.FillRect(x, -14f, 9f, 28f, Conf.Hex("1e2530"));
            for (int i = -12; i < 14; i += 6)
            {
                p.FillRect(x, i, 9f, 3f, Conf.Hex("3a4657"));
            }
        }

        static void DrawLabel(PixelPainter p, string label)
        {
            Color c = Conf.Rgba(255, 255, 255, 0.92f);
            float startX = label.Length == 2 ? -7f : -3.5f;
            for (int i = 0; i < label.Length; i++)
            {
                DrawGlyph(p, label[i], startX + i * 7.5f, 6f, 2f, c);
            }
        }

        static void DrawGlyph(PixelPainter p, char ch, float x, float y, float scale, Color c)
        {
            string[] rows;
            if (ch == '1') rows = new string[] { ".#.", "##.", ".#.", ".#.", "###" };
            else if (ch == '2') rows = new string[] { "##.", "..#", ".#.", "#..", "###" };
            else if (ch == 'P') rows = new string[] { "##.", "#.#", "##.", "#..", "#.." };
            else return;

            for (int ry = 0; ry < 5; ry++)
            {
                string row = rows[ry];
                for (int rx = 0; rx < 3; rx++)
                {
                    if (row[rx] == '#') p.FillRect(x + rx * scale, y + ry * scale, scale, scale, c);
                }
            }
        }

        /* ---------------- 子弹 ---------------- */

        public static Sprite Bullet(bool fromPlayer)
        {
            string key = fromPlayer ? "bullet:p" : "bullet:e";
            return Make(key, 18, 18, p =>
            {
                if (fromPlayer)
                {
                    p.RadialGlow(0f, 0f, 9f, Conf.Rgba(255, 255, 255, 0.9f), Conf.Rgba(255, 210, 74, 0f));
                    p.FillRect(-2f, -4f, 4f, 8f, Conf.Hex("fff6c9"));
                    p.FillRect(-1f, -4f, 2f, 8f, Conf.Gold);
                }
                else
                {
                    p.RadialGlow(0f, 0f, 9f, Conf.Rgba(255, 120, 120, 0.9f), Conf.Rgba(255, 60, 60, 0f));
                    p.FillRect(-2f, -4f, 4f, 8f, Conf.Hex("ff7b7b"));
                    p.FillRect(-1f, -4f, 2f, 8f, Conf.Hex("ff2f3f"));
                }
            });
        }

        /* ---------------- 道具 ---------------- */

        public static Sprite Powerup(string type, Color color)
        {
            string key = "pw:" + type;
            return Make(key, 34, 34, p =>
            {
                p.RadialGlow(0f, 0f, 20f, Conf.Alpha(color, 0.8f), Conf.Alpha(color, 0f));
                p.FillRect(-13f, -13f, 26f, 26f, Conf.Rgba(10, 14, 26, 0.85f));
                p.FillRect(-13f, -13f, 26f, 2f, color);
                p.FillRect(-13f, 11f, 26f, 2f, color);
                p.FillRect(-13f, -13f, 2f, 26f, color);
                p.FillRect(11f, -13f, 2f, 26f, color);

                switch (type)
                {
                    case "star":
                    {
                        var pts = new Vector2[10];
                        for (int i = 0; i < 10; i++)
                        {
                            float ang = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                            float r = (i % 2 == 0) ? 8.5f : 3.6f;
                            pts[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r + 1f);
                        }
                        p.FillPoly(pts, color);
                        break;
                    }
                    case "grenade":
                        p.FillCircle(0f, 1f, 6f, color);
                        p.FillRect(-0.8f, -8f, 1.6f, 4f, color);
                        p.Line(-8f, -5f, -5f, -2f, 1.6f, color);
                        p.Line(8f, -5f, 5f, -2f, 1.6f, color);
                        p.Line(-8f, 7f, -5f, 4f, 1.6f, color);
                        p.Line(8f, 7f, 5f, 4f, 1.6f, color);
                        break;
                    case "tank":
                        p.FillRect(-6f, -2f, 12f, 6f, color);
                        p.FillRect(-2f, -7f, 4f, 5f, color);
                        p.FillRect(-7f, -4f, 2f, 10f, color);
                        p.FillRect(5f, -4f, 2f, 10f, color);
                        break;
                    case "shovel":
                        p.FillRect(-7f, -5f, 14f, 10f, color);
                        p.FillRect(-7f, -1f, 14f, 1.5f, Conf.Rgba(10, 14, 26, 0.85f));
                        p.FillRect(2f, -5f, 1.5f, 10f, Conf.Rgba(10, 14, 26, 0.85f));
                        break;
                    case "shield":
                        p.FillPoly(new Vector2[]
                        {
                            new Vector2(-6f, -7f), new Vector2(6f, -7f), new Vector2(6f, 2f),
                            new Vector2(0f, 8f), new Vector2(-6f, 2f)
                        }, color);
                        p.FillPoly(new Vector2[]
                        {
                            new Vector2(-3.5f, -4.5f), new Vector2(3.5f, -4.5f), new Vector2(3.5f, 1.5f),
                            new Vector2(0f, 5f), new Vector2(-3.5f, 1.5f)
                        }, Conf.Rgba(10, 14, 26, 0.85f));
                        break;
                    case "clock":
                        p.StrokeCircle(0f, 1f, 7f, 2f, color);
                        p.Line(0f, 1f, 0f, -4f, 1.6f, color);
                        p.Line(0f, 1f, 4f, 3f, 1.6f, color);
                        break;
                }
            });
        }
    }
}

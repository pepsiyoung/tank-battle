using UnityEngine;

namespace TankBattle
{
    public enum TileType
    {
        Empty = 0,
        Brick = 1,
        Steel = 2,
        Water = 3,
        Tree = 4,
        Ice = 5,
        Base = 6,
        HardBrick = 7,
        Burn = 8
    }

    // 全局常量：数值与网页版 game.js 保持一致
    public static class Conf
    {
        public const int Cols = 13;
        public const int Rows = 13;
        public const float Tile = 48f;
        public const float TankSize = 40f;
        public const float FieldW = Cols * Tile;   // 624
        public const float FieldH = Rows * Tile;   // 624
        public const float PpU = 48f;              // 每单位像素数

        public const int ChargeFrames = 120;       // 蓄力 2 秒 @60fps
        public const int ChargeBarDelay = 30;      // 蓄力条延迟 0.5 秒
        public const int UltCharges = 3;           // 每辆坦克死光次数
        public const float BeamWidth = 3f * Tile;  // 死光宽度 3 格
        public const int BurnDuration = 45 * 60;   // 灼烧 45 秒

        public const int MaxOnField = 4;
        public const int TotalEnemies = 20;
        public const float PowerupDropChance = 0.3f;
        public static readonly int[] CarrierIndexes = { 3, 10, 17 };

        public const int Up = 0;
        public const int Right = 1;
        public const int Down = 2;
        public const int Left = 3;

        public static readonly int[] DirX = { 0, 1, 0, -1 };
        public static readonly int[] DirY = { -1, 0, 1, 0 };

        public static int Opposite(int d) { return (d + 2) % 4; }

        // 调色板（对应网页版 CSS 与 Canvas 用色）
        public static readonly Color PageBg = Hex("070b16");
        public static readonly Color FieldBg = Hex("0a0f1c");
        public static readonly Color EmptyTile = Hex("0d1424");
        public static readonly Color BrickColor = Hex("b5482c");
        public static readonly Color HardBrickColor = Hex("7a2e1a");
        public static readonly Color SteelColor = Hex("8f9bb0");
        public static readonly Color SteelLight = Hex("e6ecf6");
        public static readonly Color SteelDark = Hex("55627a");
        public static readonly Color SteelInner = Hex("b8c2d4");
        public static readonly Color WaterDark = Hex("123f8f");
        public static readonly Color WaterLine = Hex("2a6cd4");
        public static readonly Color IceColor = Hex("aee2ff");
        public static readonly Color BurnBase = Hex("241a10");
        public static readonly Color BurnBase2 = Hex("372a18");
        public static readonly Color Gold = Hex("ffd24a");
        public static readonly Color Green = Hex("3ee08c");
        public static readonly Color Danger = Hex("ff5d6c");
        public static readonly Color Orange = Hex("ff8a5c");
        public static readonly Color Accent = Hex("5b8cff");
        public static readonly Color Accent2 = Hex("c26bff");
        public static readonly Color TextMain = Hex("e9eefc");
        public static readonly Color TextMuted = Hex("8b97b8");
        public static readonly Color PanelFill = new Color(16f / 255f, 24f / 255f, 46f / 255f, 0.72f);
        public static readonly Color PanelBorder = new Color(120f / 255f, 160f / 255f, 255f / 255f, 0.18f);

        public static Color Hex(string hex)
        {
            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        public static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        // 与 JS Math.round 一致的四舍五入（Unity 的 Mathf.Round 是银行家舍入）
        public static float Round(float v)
        {
            return Mathf.Floor(v + 0.5f);
        }
    }
}

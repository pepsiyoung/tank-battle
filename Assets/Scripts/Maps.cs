using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    public class EnemyStat
    {
        public int Hp;
        public float Speed;
        public int Points;
        public float BulletSpeed;
        public float ShootInt;
        public Color Body;
        public Color Light;
    }

    public class ArmorPalette
    {
        public Color Body;
        public Color Light;
    }

    // 关卡地图、基地位置与敌军生成规则
    public static class Maps
    {
        public const int BaseX = 6;
        public const int BaseY = 12;

        public static readonly int[,] BaseWalls =
        {
            { 5, 11 }, { 6, 11 }, { 7, 11 }, { 5, 12 }, { 7, 12 }
        };

        // 每关 12 行地形，第 13 行（基地与保护墙）由 LoadMap 程序化生成
        static readonly string[][] StageRows =
        {
            new string[]
            {
                ".............",
                ".#.#.#.#.#.#.",
                ".#.#.#.#.#.#.",
                "....TTTT.....",
                "#.#.#.#.#.#.#",
                "#.#.#.#.#.#.#",
                ".....SSS.....",
                ".#.#.#.#.#.#.",
                ".#.#.#.#.#.#.",
                "....TTTT.....",
                "#.#.#.#.#.#.#",
                "#.#.#.#.#.#.#"
            },
            new string[]
            {
                ".............",
                ".#.#S#.#S#.#.",
                ".#.#S#.#S#.#.",
                ".....S.S.....",
                "#.#.#.#.#.#.#",
                "#.#.#.#.#.#.#",
                "..W...W...W..",
                "..W...W...W..",
                "#.#.#.#.#.#.#",
                "#.#.#.#.#.#.#",
                ".S.S.....S.S.",
                ".S.S.....S.S."
            },
            new string[]
            {
                ".............",
                "TTT.#.#.#.TTT",
                "TTT.#.#.#.TTT",
                ".............",
                "..I.I...I.I..",
                "..I.I...I.I..",
                "#.#.#.#.#.#.#",
                "#.#.#.#.#.#.#",
                ".....SSS.....",
                ".............",
                ".#.#.#.#.#.#.",
                ".#.#.#.#.#.#."
            },
            new string[]
            {
                ".............",
                ".S.#.#.#.#.S.",
                ".S.#.#.#.#.S.",
                ".............",
                "WW........WW.",
                "WW.#.#.#.#.WW",
                "...#.#.#.#...",
                "...I...I.....",
                "#.#.#.#.#.#.#",
                "#.#.#.#.#.#.#",
                ".TTT.....TTT.",
                ".TTT.....TTT."
            },
            new string[]
            {
                ".............",
                ".S.S.S.S.S.S.",
                ".S.S.S.S.S.S.",
                ".............",
                "##.#####.##..",
                "##.#####.##..",
                "...W...W.....",
                "...W...W.....",
                "SSS.....SSS..",
                ".............",
                ".T.T.T.T.T.T.",
                ".T.T.T.T.T.T."
            }
        };

        public static int StageCount { get { return StageRows.Length; } }

        // 四类敌军属性
        public static readonly Dictionary<string, EnemyStat> EnemyStats = new Dictionary<string, EnemyStat>
        {
            { "basic", new EnemyStat { Hp = 1, Speed = 1.5f, Points = 100, BulletSpeed = 4.5f, ShootInt = 1.8f, Body = Conf.Hex("b9c2d1"), Light = Conf.Hex("e6ecf6") } },
            { "fast",  new EnemyStat { Hp = 1, Speed = 2.6f, Points = 200, BulletSpeed = 5.0f, ShootInt = 1.6f, Body = Conf.Hex("4fb6ff"), Light = Conf.Hex("bfe6ff") } },
            { "power", new EnemyStat { Hp = 1, Speed = 1.3f, Points = 300, BulletSpeed = 6.5f, ShootInt = 1.0f, Body = Conf.Hex("ffb14d"), Light = Conf.Hex("ffe3b3") } },
            { "armor", new EnemyStat { Hp = 4, Speed = 0.9f, Points = 400, BulletSpeed = 4.5f, ShootInt = 2.0f, Body = Conf.Hex("7ed06a"), Light = Conf.Hex("c4f2b0") } }
        };

        // 装甲坦克按剩余血量换色
        public static readonly Dictionary<int, ArmorPalette> ArmorColors = new Dictionary<int, ArmorPalette>
        {
            { 4, new ArmorPalette { Body = Conf.Hex("7ed06a"), Light = Conf.Hex("c4f2b0") } },
            { 3, new ArmorPalette { Body = Conf.Hex("ffd24a"), Light = Conf.Hex("ffefb0") } },
            { 2, new ArmorPalette { Body = Conf.Hex("ff9a3d"), Light = Conf.Hex("ffd2a0") } },
            { 1, new ArmorPalette { Body = Conf.Hex("ff5d6c"), Light = Conf.Hex("ffc0c6") } }
        };

        public static TileType[,] LoadMap(int stage)
        {
            string[] rows = StageRows[(stage - 1) % StageRows.Length];
            var grid = new TileType[Conf.Rows, Conf.Cols];
            for (int y = 0; y < Conf.Rows; y++)
            {
                for (int x = 0; x < Conf.Cols; x++)
                {
                    grid[y, x] = TileType.Empty;
                }
            }

            int maxY = Mathf.Min(rows.Length, Conf.Rows - 1);
            for (int y = 0; y < maxY; y++)
            {
                string row = rows[y];
                for (int x = 0; x < Conf.Cols && x < row.Length; x++)
                {
                    switch (row[x])
                    {
                        case '#': grid[y, x] = TileType.Brick; break;
                        case 'S': grid[y, x] = TileType.Steel; break;
                        case 'W': grid[y, x] = TileType.Water; break;
                        case 'T': grid[y, x] = TileType.Tree; break;
                        case 'I': grid[y, x] = TileType.Ice; break;
                        default: grid[y, x] = TileType.Empty; break;
                    }
                }
            }

            grid[BaseY, BaseX] = TileType.Base;
            for (int i = 0; i < BaseWalls.GetLength(0); i++)
            {
                grid[BaseWalls[i, 1], BaseWalls[i, 0]] = TileType.HardBrick;
            }
            return grid;
        }

        // 按关卡加权随机生成 20 辆敌军
        public static List<string> BuildEnemyQueue(int stage)
        {
            var queue = new List<string>();
            float basicW = Mathf.Max(18f, 58f - stage * 5f);
            float fastW = 18f + stage * 2f;
            float powerW = 10f + stage * 2f;
            float armorW = 10f + stage * 2f;
            float total = basicW + fastW + powerW + armorW;
            for (int i = 0; i < Conf.TotalEnemies; i++)
            {
                float r = Random.value * total;
                string type;
                if (r < basicW) type = "basic";
                else if (r < basicW + fastW) type = "fast";
                else if (r < basicW + fastW + powerW) type = "power";
                else type = "armor";
                queue.Add(type);
            }
            return queue;
        }
    }
}

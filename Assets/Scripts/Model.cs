using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    public class Profile
    {
        public int Lives = 3;
        public int Score;
        public int Power;
    }

    public class RunStats
    {
        public int Kills;
        public int UltUses;
        public int Shots;
    }

    public class Tank
    {
        public float X;
        public float Y;
        public int Dir;
        public float Speed;
        public float Size = Conf.TankSize;

        public bool IsEnemy;
        public int PlayerIndex;
        public string Type;
        public bool Carrier;

        public Color Body;
        public Color Light;

        public bool Alive = true;
        public int Hp = 1;
        public int MaxHp = 1;
        public int Points;

        public float BulletSpeed;
        public float ShootInt;

        public int SpawnTimer;
        public float ShootCooldown;
        public float AiTimer;
        public int FlashTimer;
        public bool Moving;

        public int UltCharges = Conf.UltCharges;
        public bool Charging;
        public int ChargeT;
        public bool ChargeReady;
        public bool WasFiring;

        public int Shield;
    }

    public class Bullet
    {
        public int Dir;
        public float Speed;
        public string Owner;
        public bool FromPlayer;
        public int Power;
        public float X;
        public float Y;
        public float W;
        public float H;
        public bool Dead;
    }

    public class Powerup
    {
        public float X;
        public float Y;
        public string Type;
        public int T;
    }

    public class Particle
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public int Life;
        public Color Color;
        public float Size;
        public bool Ring;
    }

    public class Beam
    {
        public float X0;
        public float Y0;
        public float X1;
        public float Y1;
        public int Dir;
        public int Life;
        public int OwnerPi;
    }

    public class BurnTile
    {
        public int X;
        public int Y;
        public int T;
    }

    public class Respawn
    {
        public int Pi;
        public int T;
    }

    // 游戏状态单例（与 game.js 中的 G 一一对应）
    public class Game
    {
        public int Mode = 1;
        public string State = "menu";   // menu | playing | paused | ending | stageclear | gameover | victory
        public string EndKind = "clear";
        public string EndReason = "";
        public int Stage = 1;

        public TileType[,] Grid;

        public Profile[] Profiles =
        {
            new Profile { Lives = 3, Score = 0, Power = 0 },
            new Profile { Lives = 3, Score = 0, Power = 0 }
        };

        public readonly List<Tank> Tanks = new List<Tank>();
        public readonly List<Bullet> Bullets = new List<Bullet>();
        public readonly List<Powerup> Powerups = new List<Powerup>();
        public readonly List<Particle> Particles = new List<Particle>();
        public readonly List<Beam> Beams = new List<Beam>();
        public readonly List<BurnTile> BurnTiles = new List<BurnTile>();
        public readonly List<Respawn> Respawns = new List<Respawn>();
        public readonly List<string> EnemyQueue = new List<string>();

        public int EnemiesLeft = Conf.TotalEnemies;
        public int EnemiesOnField;
        public int SpawnCount;
        public float SpawnTimer = 60f;

        public bool BaseDown;
        public int BaseHp = 3;
        public int BaseFlash;
        public int BaseWallTimer;
        public int FreezeTimer;
        public int EndTimer;

        public float Acc;
        public float NowMs;

        public RunStats Stats = new RunStats();

        public Game()
        {
            Grid = new TileType[Conf.Rows, Conf.Cols];
            for (int y = 0; y < Conf.Rows; y++)
            {
                for (int x = 0; x < Conf.Cols; x++)
                {
                    Grid[y, x] = TileType.Empty;
                }
            }
        }
    }
}

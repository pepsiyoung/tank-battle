using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    // 主控制器：游戏循环、输入、状态切换
    public partial class GameController : MonoBehaviour
    {
        const float StepMs = 1000f / 60f;

        public Game G;
        public SceneView View;
        public GameUI Ui;

        readonly HashSet<KeyCode> vkeys = new HashSet<KeyCode>();
        bool vFire;

        void Awake()
        {
            G = new Game();

            View = new SceneView();
            View.Build();

            var uiGo = new GameObject("TankBattleUI");
            Ui = uiGo.AddComponent<GameUI>();
            Ui.Build(this);

            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0.42f;
            AudioSynth.Init(src);

            Ui.ShowStart();
        }

        void Update()
        {
            HandleGlobalKeys();

            float dt = Mathf.Min(60f, Time.deltaTime * 1000f);
            G.NowMs += dt;

            if (G.State == "playing")
            {
                G.Acc += dt;
                int guard = 0;
                while (G.Acc >= StepMs && guard++ < 4)
                {
                    Step();
                    G.Acc -= StepMs;
                }
                if (guard >= 4) G.Acc = 0f;
            }
            else if (G.State == "ending")
            {
                G.Acc += dt;
                while (G.Acc >= StepMs)
                {
                    UpdateParticles();
                    UpdateBeams();
                    if (--G.EndTimer <= 0) FinalizeEnd();
                    G.Acc -= StepMs;
                }
            }

            View.Sync(G, G.NowMs);
            Ui.UpdateHud(G);
        }

        void HandleGlobalKeys()
        {
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (G.State == "playing" || G.State == "paused") TogglePause();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (G.State == "menu") StartGame(1);
                else if (G.State == "stageclear" || G.State == "gameover" || G.State == "victory") NextOrRestart();
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                ToggleSound();
            }
        }

        /* ---------------- 输入辅助 ---------------- */

        public void PressVirtual(KeyCode k) { vkeys.Add(k); }
        public void ReleaseVirtual(KeyCode k) { vkeys.Remove(k); }
        public void PressVirtualFire() { vFire = true; }
        public void ReleaseVirtualFire() { vFire = false; }

        bool Down(KeyCode k)
        {
            return Input.GetKey(k) || vkeys.Contains(k);
        }

        int GetInputDir(int pi)
        {
            bool alt = G.Mode == 1 && pi == 0; // 单人模式：P1 兼容方向键

            if (pi == 0)
            {
                if (Down(KeyCode.W)) return Conf.Up;
                if (Down(KeyCode.S)) return Conf.Down;
                if (Down(KeyCode.A)) return Conf.Left;
                if (Down(KeyCode.D)) return Conf.Right;
                if (alt)
                {
                    if (Down(KeyCode.UpArrow)) return Conf.Up;
                    if (Down(KeyCode.DownArrow)) return Conf.Down;
                    if (Down(KeyCode.LeftArrow)) return Conf.Left;
                    if (Down(KeyCode.RightArrow)) return Conf.Right;
                }
                return -1;
            }

            if (Down(KeyCode.UpArrow)) return Conf.Up;
            if (Down(KeyCode.DownArrow)) return Conf.Down;
            if (Down(KeyCode.LeftArrow)) return Conf.Left;
            if (Down(KeyCode.RightArrow)) return Conf.Right;
            return -1;
        }

        bool PressedFire(int pi)
        {
            if (pi == 0)
            {
                if (Down(KeyCode.Space)) return true;
                if (G.Mode == 1 && (Down(KeyCode.Return) || Down(KeyCode.KeypadEnter))) return true;
                return vFire;
            }
            return Down(KeyCode.Return) || Down(KeyCode.KeypadEnter) || vFire;
        }

        /* ---------------- 流程控制 ---------------- */

        public void StartGame(int mode)
        {
            G.Mode = mode;
            G.Profiles = new Profile[]
            {
                new Profile { Lives = 3, Score = 0, Power = 0 },
                new Profile { Lives = 3, Score = 0, Power = 0 }
            };
            G.Stats = new RunStats();
            G.Stage = 1;
            StartStage();
            Ui.HideOverlays();
            Ui.SetP2Off(mode == 1);
            G.State = "playing";
        }

        public void StartStage()
        {
            G.Grid = Maps.LoadMap(G.Stage);
            G.EnemiesLeft = Conf.TotalEnemies;
            G.EnemyQueue.Clear();
            G.EnemyQueue.AddRange(Maps.BuildEnemyQueue(G.Stage));
            G.EnemiesOnField = 0;
            G.SpawnCount = 0;
            G.SpawnTimer = 60f;
            G.Bullets.Clear();
            G.Powerups.Clear();
            G.Particles.Clear();
            G.Beams.Clear();
            G.BurnTiles.Clear();
            G.Respawns.Clear();
            G.Tanks.Clear();
            G.BaseDown = false;
            G.BaseHp = 3;
            G.BaseFlash = 0;
            G.BaseWallTimer = 0;
            G.FreezeTimer = 0;
            G.EndTimer = 0;
            G.Acc = 0f;
            G.Profiles[0].Power = 0;
            G.Profiles[1].Power = 0;
            G.Profiles[0].Lives = 3;
            G.Profiles[1].Lives = 3;

            for (int i = 0; i < G.Mode; i++)
            {
                if (G.Profiles[i].Lives > 0) G.Tanks.Add(CreatePlayerTank(i));
            }

            if (Ui != null) Ui.UpdateHud(G);
        }

        public void NextOrRestart()
        {
            if (G.EndKind == "clear")
            {
                G.Stage++;
                StartStage();
                Ui.HideOverlays();
                G.State = "playing";
            }
            else
            {
                StartGame(G.Mode);
            }
        }

        public void TogglePause()
        {
            if (G.State == "playing")
            {
                G.State = "paused";
                Ui.ShowPause();
                AudioSynth.Pause();
            }
            else if (G.State == "paused")
            {
                G.State = "playing";
                Ui.HideOverlays();
            }
        }

        public void ToggleSound()
        {
            AudioSynth.Enabled = !AudioSynth.Enabled;
            Ui.SetSoundLabel(AudioSynth.Enabled);
        }

        public void GoHome()
        {
            G.State = "menu";
            G.Tanks.Clear();
            G.Bullets.Clear();
            G.Powerups.Clear();
            G.Particles.Clear();
            G.Beams.Clear();
            G.BurnTiles.Clear();
            G.Respawns.Clear();
            G.EnemyQueue.Clear();
            G.BaseDown = false;
            G.BaseHp = 3;
            G.Grid = new TileType[Conf.Rows, Conf.Cols];
            Ui.HideOverlays();
            Ui.ShowStart();
        }
    }
}

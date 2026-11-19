using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    // 玩法规则：与 game.js 的 step()/update*/kill*/fire* 一一对应
    public partial class GameController
    {
        static readonly string[] PowerupTypes =
        {
            "star", "star", "grenade", "tank", "shovel", "shield", "clock"
        };

        void Step()
        {
            // 基地保护墙 / 受击闪烁 / 冰冻计时
            if (G.BaseWallTimer > 0)
            {
                G.BaseWallTimer--;
                if (G.BaseWallTimer == 0) BuildBaseWall(TileType.HardBrick);
            }
            if (G.BaseFlash > 0) G.BaseFlash--;
            if (G.FreezeTimer > 0) G.FreezeTimer--;

            // 敌军生成
            if (G.SpawnTimer > 0f) G.SpawnTimer -= 1f;
            else if (G.EnemyQueue.Count > 0 && G.EnemiesOnField < Conf.MaxOnField) TrySpawnEnemy();

            UpdateRespawns();
            UpdatePlayers();
            UpdateEnemies();
            UpdateBullets();
            UpdatePowerups();
            UpdateParticles();
            UpdateBeams();
            UpdateBurnTiles();
            CheckBurnOverlap();
            Ui.UpdateHud(G);
        }

        void UpdateRespawns()
        {
            for (int i = G.Respawns.Count - 1; i >= 0; i--)
            {
                Respawn r = G.Respawns[i];
                if (--r.T > 0) continue;
                G.Respawns.RemoveAt(i);
                if (G.Profiles[r.Pi].Lives <= 0) continue;

                Tank t = CreatePlayerTank(r.Pi);

                // 出生点若有敌方坦克则直接消灭（腾出位置）
                var blockers = new List<Tank>();
                foreach (Tank o in G.Tanks)
                {
                    if (o.Alive && o.IsEnemy &&
                        RectHits(t.X, t.Y, Conf.TankSize, Conf.TankSize, o.X, o.Y, o.Size, o.Size))
                    {
                        blockers.Add(o);
                    }
                }
                foreach (Tank b in blockers) KillEnemy(b, "p" + r.Pi);

                bool free = true;
                foreach (Tank o in G.Tanks)
                {
                    if (o.Alive &&
                        RectHits(t.X, t.Y, Conf.TankSize, Conf.TankSize, o.X, o.Y, o.Size, o.Size))
                    {
                        free = false;
                        break;
                    }
                }

                if (free)
                {
                    t.Shield = 120;
                    G.Tanks.Add(t);
                }
                else
                {
                    G.Respawns.Add(new Respawn { Pi = r.Pi, T = 60 });
                }
            }
        }

        void TrySpawnEnemy()
        {
            int[] cols = { 0, 6, 12 };
            var picks = new List<int>();
            foreach (int c in cols)
            {
                float px = c * Conf.Tile + (Conf.Tile - Conf.TankSize) / 2f;
                float py = (Conf.Tile - Conf.TankSize) / 2f;
                bool blocked = false;
                foreach (Tank t in G.Tanks)
                {
                    if (t.Alive && RectHits(px, py, Conf.TankSize, Conf.TankSize, t.X, t.Y, t.Size, t.Size))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked) picks.Add(c);
            }

            if (picks.Count == 0) return;

            int pick = picks[Random.Range(0, picks.Count)];
            string type = G.EnemyQueue[0];
            G.EnemyQueue.RemoveAt(0);
            bool carrier = System.Array.IndexOf(Conf.CarrierIndexes, G.SpawnCount) >= 0;
            float x = pick * Conf.Tile + (Conf.Tile - Conf.TankSize) / 2f;
            float y = (Conf.Tile - Conf.TankSize) / 2f;

            G.Tanks.Add(CreateEnemy(type, x, y, carrier));
            G.EnemiesOnField++;
            G.SpawnCount++;
            G.SpawnTimer = 50f + Random.Range(0f, 40f);
            AudioSynth.Spawn();
        }

        void UpdatePlayers()
        {
            Tank[] snapshot = G.Tanks.ToArray();
            foreach (Tank t in snapshot)
            {
                if (t.IsEnemy || !t.Alive) continue;

                if (t.SpawnTimer > 0) t.SpawnTimer--;
                if (t.Shield > 0) t.Shield--;
                if (t.FlashTimer > 0) t.FlashTimer--;
                t.ShootCooldown -= 1f;

                int dir = GetInputDir(t.PlayerIndex);
                if (dir >= 0)
                {
                    MoveTank(t, dir);
                }
                else
                {
                    t.Moving = false;
                    if (TileAtCenter(t) == TileType.Ice) MoveTank(t, t.Dir);
                }

                UpdatePlayerFire(t);
            }
        }

        void UpdatePlayerFire(Tank t)
        {
            bool firing = PressedFire(t.PlayerIndex);

            // 绝招耗尽：回到经典连射（按住即射击）
            if (t.UltCharges <= 0)
            {
                if (firing && t.ShootCooldown <= 0f) Fire(t);
                t.WasFiring = firing;
                return;
            }

            // 按下发射键：开始蓄力
            if (firing && !t.WasFiring)
            {
                t.Charging = true;
                t.ChargeT = 0;
                t.ChargeReady = false;
            }

            if (t.Charging)
            {
                if (firing)
                {
                    t.ChargeT++;
                    if (t.ChargeT >= Conf.ChargeFrames && !t.ChargeReady)
                    {
                        t.ChargeReady = true;
                        AudioSynth.ChargeFull();
                    }
                    else if (t.ChargeT < Conf.ChargeFrames && t.ChargeT % 30 == 0)
                    {
                        AudioSynth.ChargeTick();
                    }
                    if (t.ChargeT >= Conf.ChargeBarDelay && t.ChargeT % 5 == 0) SpawnChargeParticle(t);
                }
                else
                {
                    // 松开：满蓄力 → 破坏死光；未满 → 普通射击
                    if (t.ChargeReady && t.UltCharges > 0)
                    {
                        t.UltCharges--;
                        G.Stats.UltUses++;
                        FireBeam(t);
                        AudioSynth.Beam();
                    }
                    else if (t.ShootCooldown <= 0f)
                    {
                        Fire(t);
                    }
                    t.Charging = false;
                    t.ChargeT = 0;
                    t.ChargeReady = false;
                }
            }

            t.WasFiring = firing;
        }

        void FireBeam(Tank t)
        {
            float cx = t.X + t.Size / 2f;
            float cy = t.Y + t.Size / 2f;
            float half = Conf.BeamWidth / 2f;
            float x0 = 0f, y0 = 0f, x1 = 0f, y1 = 0f;

            if (t.Dir == Conf.Up) { x0 = cx - half; x1 = cx + half; y0 = 0f; y1 = t.Y; }
            else if (t.Dir == Conf.Down) { x0 = cx - half; x1 = cx + half; y0 = t.Y + t.Size; y1 = Conf.FieldH; }
            else if (t.Dir == Conf.Left) { y0 = cy - half; y1 = cy + half; x0 = 0f; x1 = t.X; }
            else { y0 = cy - half; y1 = cy + half; x0 = t.X + t.Size; x1 = Conf.FieldW; }

            // 消灭路径上的所有地形
            int tx0 = Mathf.FloorToInt(x0 / Conf.Tile);
            int tx1 = Mathf.CeilToInt(x1 / Conf.Tile) - 1;
            int ty0 = Mathf.FloorToInt(y0 / Conf.Tile);
            int ty1 = Mathf.CeilToInt(y1 / Conf.Tile) - 1;
            bool baseHit = false;

            for (int ty = ty0; ty <= ty1; ty++)
            {
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    if (tx < 0 || ty < 0 || tx >= Conf.Cols || ty >= Conf.Rows) continue;
                    if (!RectHits(x0, y0, x1 - x0, y1 - y0, tx * Conf.Tile, ty * Conf.Tile, Conf.Tile, Conf.Tile)) continue;

                    TileType tile = G.Grid[ty, tx];
                    if (tile == TileType.Empty) continue;
                    if (tile == TileType.Water) continue; // 河流不被清除

                    float fx = tx * Conf.Tile + Conf.Tile / 2f;
                    float fy = ty * Conf.Tile + Conf.Tile / 2f;

                    if (tile == TileType.Tree)
                    {
                        // 草地 → 灼烧（45 秒）
                        G.Grid[ty, tx] = TileType.Burn;
                        AddBurnTile(tx, ty);
                        SpawnTileFlash(fx, fy, Conf.Hex("ff8a3c"));
                        continue;
                    }
                    if (tile == TileType.Burn)
                    {
                        AddBurnTile(tx, ty);
                        continue;
                    }
                    if (tile == TileType.Base) baseHit = true;
                    G.Grid[ty, tx] = TileType.Empty;
                    SpawnTileFlash(fx, fy, tile == TileType.Base ? Conf.Danger : Conf.Gold);
                }
            }

            if (baseHit) DestroyBase();

            // 消灭路径上的所有坦克
            foreach (Tank other in G.Tanks.ToArray())
            {
                if (!other.Alive || other == t) continue;
                if (!RectHits(x0, y0, x1 - x0, y1 - y0, other.X, other.Y, other.Size, other.Size)) continue;
                if (other.IsEnemy) KillEnemy(other, "p" + t.PlayerIndex);
                else KillPlayer(other);
            }

            G.Beams.Add(new Beam
            {
                X0 = x0, Y0 = y0, X1 = x1, Y1 = y1,
                Dir = t.Dir, Life = 22, OwnerPi = t.PlayerIndex
            });
            SpawnBeamParticles(x0, y0, x1, y1);
        }

        void SpawnTileFlash(float x, float y, Color color)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float sp = 1f + Random.value * 3f;
                G.Particles.Add(new Particle
                {
                    X = x, Y = y,
                    Vx = Mathf.Cos(a) * sp, Vy = Mathf.Sin(a) * sp,
                    Life = 16 + Random.Range(0, 12),
                    Color = color,
                    Size = 2f + Random.value * 4f,
                    Ring = false
                });
            }
        }

        void SpawnBeamParticles(float x0, float y0, float x1, float y1)
        {
            float w = x1 - x0;
            float h = y1 - y0;
            for (int i = 0; i < 46; i++)
            {
                G.Particles.Add(new Particle
                {
                    X = x0 + Random.value * w,
                    Y = y0 + Random.value * h,
                    Vx = (Random.value - 0.5f) * 3f,
                    Vy = (Random.value - 0.5f) * 3f,
                    Life = 18 + Random.Range(0, 16),
                    Color = Random.value < 0.5f ? Conf.Gold : Conf.Orange,
                    Size = 3f + Random.value * 5f,
                    Ring = false
                });
            }
        }

        void SpawnChargeParticle(Tank t)
        {
            float cx = t.X + t.Size / 2f;
            float cy = t.Y + t.Size / 2f;
            float a = Random.value * Mathf.PI * 2f;
            float r = t.Size / 2f + 6f;
            Color col = t.PlayerIndex == 0 ? Conf.Gold : Conf.Green;
            G.Particles.Add(new Particle
            {
                X = cx + Mathf.Cos(a) * r,
                Y = cy + Mathf.Sin(a) * r,
                Vx = -Mathf.Cos(a) * 1.6f,
                Vy = -Mathf.Sin(a) * 1.6f,
                Life = 14,
                Color = col,
                Size = 3f,
                Ring = false
            });
        }

        void AddBurnTile(int tx, int ty)
        {
            foreach (BurnTile b in G.BurnTiles)
            {
                if (b.X == tx && b.Y == ty)
                {
                    b.T = Conf.BurnDuration;
                    return;
                }
            }
            G.BurnTiles.Add(new BurnTile { X = tx, Y = ty, T = Conf.BurnDuration });
        }

        void UpdateBurnTiles()
        {
            for (int i = G.BurnTiles.Count - 1; i >= 0; i--)
            {
                BurnTile b = G.BurnTiles[i];
                if (--b.T <= 0)
                {
                    if (G.Grid[b.Y, b.X] == TileType.Burn) G.Grid[b.Y, b.X] = TileType.Empty;
                    G.BurnTiles.RemoveAt(i);
                }
            }
        }

        void CheckBurnOverlap()
        {
            foreach (Tank t in G.Tanks.ToArray())
            {
                if (!t.Alive) continue;
                int x0 = Mathf.FloorToInt(t.X / Conf.Tile);
                int x1 = Mathf.FloorToInt((t.X + t.Size - 1f) / Conf.Tile);
                int y0 = Mathf.FloorToInt(t.Y / Conf.Tile);
                int y1 = Mathf.FloorToInt((t.Y + t.Size - 1f) / Conf.Tile);
                bool hit = false;
                for (int ty = y0; ty <= y1 && !hit; ty++)
                {
                    for (int tx = x0; tx <= x1; tx++)
                    {
                        if (tx < 0 || ty < 0 || tx >= Conf.Cols || ty >= Conf.Rows) continue;
                        if (G.Grid[ty, tx] == TileType.Burn)
                        {
                            hit = true;
                            break;
                        }
                    }
                }
                if (hit)
                {
                    if (t.IsEnemy) KillEnemy(t, null);
                    else KillPlayer(t);
                }
            }
        }

        void UpdateBeams()
        {
            for (int i = G.Beams.Count - 1; i >= 0; i--)
            {
                if (--G.Beams[i].Life <= 0) G.Beams.RemoveAt(i);
            }
        }

        void UpdateEnemies()
        {
            Tank[] snapshot = G.Tanks.ToArray();
            foreach (Tank t in snapshot)
            {
                if (!t.IsEnemy || !t.Alive) continue;
                if (t.FlashTimer > 0) t.FlashTimer--;
                if (t.SpawnTimer > 0)
                {
                    t.SpawnTimer--;
                    continue;
                }
                if (G.FreezeTimer > 0)
                {
                    t.Moving = false;
                    continue;
                }

                t.ShootCooldown -= 1f;
                t.AiTimer -= 1f;

                if (t.AiTimer <= 0f)
                {
                    t.AiTimer = 40f + Random.Range(0f, 80f);
                    float r = Random.value;
                    if (r < 0.35f)
                    {
                        t.Dir = SeekDirToBase(t);
                    }
                    else
                    {
                        var choices = new List<int>();
                        int opp = Conf.Opposite(t.Dir);
                        for (int d = 0; d < 4; d++)
                        {
                            if (d != opp) choices.Add(d);
                        }
                        t.Dir = choices[Random.Range(0, choices.Count)];
                    }
                }

                MoveTank(t, t.Dir);
                if (t.ShootCooldown <= 0f) Fire(t);
            }
        }

        int SeekDirToBase(Tank t)
        {
            float ex = (t.X + t.Size / 2f) / Conf.Tile;
            float ey = (t.Y + t.Size / 2f) / Conf.Tile;
            float bx = Maps.BaseX + 0.5f;
            float by = Maps.BaseY + 0.5f;
            if (Mathf.Abs(bx - ex) > Mathf.Abs(by - ey)) return bx > ex ? Conf.Right : Conf.Left;
            return by > ey ? Conf.Down : Conf.Up;
        }

        void UpdateBullets()
        {
            foreach (Bullet b in G.Bullets)
            {
                int dx = Conf.DirX[b.Dir];
                int dy = Conf.DirY[b.Dir];
                b.X += dx * b.Speed;
                b.Y += dy * b.Speed;

                if (b.X < -20f || b.Y < -20f || b.X > Conf.FieldW + 20f || b.Y > Conf.FieldH + 20f)
                {
                    b.Dead = true;
                    continue;
                }

                // 前方地块
                float fx, fy;
                if (b.Dir == Conf.Up) { fx = b.X + b.W / 2f; fy = b.Y; }
                else if (b.Dir == Conf.Down) { fx = b.X + b.W / 2f; fy = b.Y + b.H; }
                else if (b.Dir == Conf.Left) { fx = b.X; fy = b.Y + b.H / 2f; }
                else { fx = b.X + b.W; fy = b.Y + b.H / 2f; }

                int tx = Mathf.FloorToInt(fx / Conf.Tile);
                int ty = Mathf.FloorToInt(fy / Conf.Tile);
                if (tx < 0 || ty < 0 || tx >= Conf.Cols || ty >= Conf.Rows)
                {
                    b.Dead = true;
                    continue;
                }

                TileType tile = G.Grid[ty, tx];
                if (tile == TileType.Brick)
                {
                    G.Grid[ty, tx] = TileType.Empty;
                    AudioSynth.HitBrick();
                    SpawnExplosion(tx * Conf.Tile + Conf.Tile / 2f, ty * Conf.Tile + Conf.Tile / 2f, Conf.Hex("d85a34"));
                    b.Dead = true;
                    continue;
                }
                else if (tile == TileType.HardBrick)
                {
                    G.Grid[ty, tx] = TileType.Brick;
                    AudioSynth.HitBrick();
                    SpawnExplosion(tx * Conf.Tile + Conf.Tile / 2f, ty * Conf.Tile + Conf.Tile / 2f, Conf.Hex("8a3520"));
                    b.Dead = true;
                    continue;
                }
                else if (tile == TileType.Steel)
                {
                    if (b.FromPlayer && b.Power >= 3)
                    {
                        G.Grid[ty, tx] = TileType.Empty;
                        AudioSynth.HitSteel();
                        SpawnExplosion(tx * Conf.Tile + Conf.Tile / 2f, ty * Conf.Tile + Conf.Tile / 2f, Conf.Hex("cfd6e4"));
                    }
                    else
                    {
                        AudioSynth.HitSteel();
                    }
                    b.Dead = true;
                    continue;
                }
                else if (tile == TileType.Base)
                {
                    HitBase();
                    b.Dead = true;
                    continue;
                }

                // 坦克碰撞
                foreach (Tank t in G.Tanks.ToArray())
                {
                    if (!t.Alive) continue;
                    if (b.FromPlayer && !t.IsEnemy) continue;
                    if (!b.FromPlayer && t.IsEnemy) continue;
                    if (RectHits(b.X, b.Y, b.W, b.H, t.X, t.Y, t.Size, t.Size))
                    {
                        if (HitTank(t, b))
                        {
                            b.Dead = true;
                            break;
                        }
                    }
                }
            }

            // 子弹对消
            for (int i = 0; i < G.Bullets.Count; i++)
            {
                Bullet a = G.Bullets[i];
                if (a.Dead) continue;
                for (int j = i + 1; j < G.Bullets.Count; j++)
                {
                    Bullet c = G.Bullets[j];
                    if (c.Dead || a.FromPlayer == c.FromPlayer) continue;
                    float ax = a.X + a.W / 2f, ay = a.Y + a.H / 2f;
                    float cx = c.X + c.W / 2f, cy = c.Y + c.H / 2f;
                    float d = Mathf.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy));
                    if (d < 8f)
                    {
                        a.Dead = true;
                        c.Dead = true;
                        SpawnExplosion((a.X + c.X) / 2f, (a.Y + c.Y) / 2f, Color.white);
                        break;
                    }
                }
            }

            G.Bullets.RemoveAll(x => x.Dead);
        }

        void UpdatePowerups()
        {
            for (int i = G.Powerups.Count - 1; i >= 0; i--)
            {
                Powerup p = G.Powerups[i];
                p.T++;
                foreach (Tank t in G.Tanks.ToArray())
                {
                    if (t.IsEnemy || !t.Alive) continue;
                    if (RectHits(p.X, p.Y, 34f, 34f, t.X, t.Y, t.Size, t.Size))
                    {
                        ApplyPowerup(t.PlayerIndex, p);
                        G.Powerups.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        void UpdateParticles()
        {
            for (int i = G.Particles.Count - 1; i >= 0; i--)
            {
                Particle p = G.Particles[i];
                p.Life--;
                p.X += p.Vx;
                p.Y += p.Vy;
                p.Vx *= 0.94f;
                p.Vy *= 0.94f;
                if (p.Life <= 0) G.Particles.RemoveAt(i);
            }
        }

        /* ---------------- 阶段 / 结束判定 ---------------- */

        void CheckStageClear()
        {
            if (G.State != "playing") return;

            bool anyEnemy = false;
            foreach (Tank t in G.Tanks)
            {
                if (t.IsEnemy) { anyEnemy = true; break; }
            }

            if (G.EnemiesLeft <= 0 && G.EnemyQueue.Count == 0 && !anyEnemy)
            {
                G.EndKind = G.Stage >= Maps.StageCount ? "victory" : "clear";
                G.EndTimer = 110;
                G.State = "ending";
                AudioSynth.StageClear();
            }
        }

        void CheckGameOver()
        {
            if (G.BaseDown) return;

            if (G.Mode == 1)
            {
                bool anyPlayer = false;
                foreach (Tank t in G.Tanks)
                {
                    if (!t.IsEnemy && t.PlayerIndex == 0) { anyPlayer = true; break; }
                }
                if (G.Profiles[0].Lives <= 0 && !anyPlayer)
                {
                    G.EndKind = "over";
                    G.EndReason = "玩家 1 生命耗尽";
                    G.EndTimer = 110;
                    G.State = "ending";
                    AudioSynth.GameOver();
                }
            }
            else
            {
                bool anyPlayer = false;
                foreach (Tank t in G.Tanks)
                {
                    if (!t.IsEnemy) { anyPlayer = true; break; }
                }
                if (G.Profiles[0].Lives <= 0 && G.Profiles[1].Lives <= 0 && !anyPlayer)
                {
                    G.EndKind = "over";
                    G.EndReason = "所有玩家生命耗尽";
                    G.EndTimer = 110;
                    G.State = "ending";
                    AudioSynth.GameOver();
                }
            }
        }

        void FinalizeEnd()
        {
            if (G.EndKind == "clear")
            {
                G.State = "stageclear";
                Ui.ShowEnd(G);
            }
            else if (G.EndKind == "victory")
            {
                G.State = "victory";
                Ui.ShowEnd(G);
            }
            else
            {
                G.State = "gameover";
                Ui.ShowEnd(G);
            }
        }

        /* ---------------- 实体创建 ---------------- */

        Tank CreatePlayerTank(int pi)
        {
            int col = pi == 0 ? 4 : 8;
            Color body = pi == 0 ? Conf.Hex("e2a912") : Conf.Hex("22b573");
            Color light = pi == 0 ? Conf.Hex("ffe9a0") : Conf.Hex("b0f5d3");

            return new Tank
            {
                X = col * Conf.Tile + (Conf.Tile - Conf.TankSize) / 2f,
                Y = 12 * Conf.Tile + (Conf.Tile - Conf.TankSize) / 2f,
                Dir = Conf.Up,
                Speed = 2.4f,
                Size = Conf.TankSize,
                IsEnemy = false,
                PlayerIndex = pi,
                Body = body,
                Light = light,
                Alive = true,
                Hp = 1,
                Shield = 0,
                SpawnTimer = 90,
                ShootCooldown = 0f,
                FlashTimer = 0,
                Moving = false,
                UltCharges = Conf.UltCharges,
                Charging = false,
                ChargeT = 0,
                ChargeReady = false,
                WasFiring = false
            };
        }

        Tank CreateEnemy(string type, float px, float py, bool carrier)
        {
            EnemyStat s = Maps.EnemyStats[type];
            Color body = s.Body;
            Color light = s.Light;
            if (type == "armor")
            {
                ArmorPalette pal = Maps.ArmorColors[4];
                body = pal.Body;
                light = pal.Light;
            }

            return new Tank
            {
                X = px,
                Y = py,
                Dir = Conf.Down,
                Speed = s.Speed,
                Size = Conf.TankSize,
                IsEnemy = true,
                Type = type,
                Carrier = carrier,
                Alive = true,
                Hp = s.Hp,
                MaxHp = s.Hp,
                Points = s.Points,
                BulletSpeed = s.BulletSpeed,
                ShootInt = s.ShootInt,
                Body = body,
                Light = light,
                SpawnTimer = 60,
                ShootCooldown = 30f + Random.value * 60f,
                AiTimer = 60f + Random.value * 60f,
                FlashTimer = 0,
                Moving = false
            };
        }

        void SpawnBullet(Tank t, bool fromPlayer, string owner, int power)
        {
            float cx = t.X + t.Size / 2f;
            float cy = t.Y + t.Size / 2f;
            float off = t.Size / 2f + 2f;
            float speed = fromPlayer ? (power >= 1 ? 6.5f : 5f) : t.BulletSpeed;

            var b = new Bullet
            {
                Dir = t.Dir,
                Speed = speed,
                Owner = owner,
                FromPlayer = fromPlayer,
                Power = power,
                W = 4f,
                H = 7f,
                Dead = false
            };

            if (t.Dir == Conf.Up) { b.X = cx - 2f; b.Y = cy - off - 3f; }
            else if (t.Dir == Conf.Down) { b.X = cx - 2f; b.Y = cy + off - 4f; }
            else if (t.Dir == Conf.Left) { b.X = cx - off - 3f; b.Y = cy - 2f; b.W = 7f; b.H = 4f; }
            else { b.X = cx + off - 4f; b.Y = cy - 2f; b.W = 7f; b.H = 4f; }

            G.Bullets.Add(b);
        }

        /* ---------------- 碰撞工具 ---------------- */

        static bool RectHits(float x, float y, float w, float h, float x2, float y2, float w2, float h2)
        {
            return x < x2 + w2 && x + w > x2 && y < y2 + h2 && y + h > y2;
        }

        bool TileSolid(int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= Conf.Cols || ty >= Conf.Rows) return true;
            TileType t = G.Grid[ty, tx];
            return t == TileType.Brick || t == TileType.HardBrick || t == TileType.Steel ||
                   t == TileType.Water || t == TileType.Base;
        }

        bool CanMoveTo(Tank tank, float nx, float ny)
        {
            if (nx < 0f || ny < 0f || nx + Conf.TankSize > Conf.FieldW || ny + Conf.TankSize > Conf.FieldH) return false;

            int x0 = Mathf.FloorToInt(nx / Conf.Tile);
            int x1 = Mathf.FloorToInt((nx + Conf.TankSize - 1f) / Conf.Tile);
            int y0 = Mathf.FloorToInt(ny / Conf.Tile);
            int y1 = Mathf.FloorToInt((ny + Conf.TankSize - 1f) / Conf.Tile);

            for (int ty = y0; ty <= y1; ty++)
            {
                for (int tx = x0; tx <= x1; tx++)
                {
                    if (TileSolid(tx, ty)) return false;
                }
            }

            foreach (Tank t in G.Tanks)
            {
                if (t == tank || !t.Alive) continue;
                // 敌方坦克出生动画期间碰撞体积同样生效；玩家坦克出生无敌期间忽略碰撞
                if (t.SpawnTimer > 0 && !t.IsEnemy) continue;
                if (RectHits(nx, ny, Conf.TankSize, Conf.TankSize, t.X, t.Y, t.Size, t.Size)) return false;
            }

            return true;
        }

        static float AlignCoord(float v)
        {
            return Conf.Round((v - 4f) / Conf.Tile) * Conf.Tile + 4f;
        }

        TileType TileAtCenter(Tank t)
        {
            int tx = Mathf.FloorToInt((t.X + t.Size / 2f) / Conf.Tile);
            int ty = Mathf.FloorToInt((t.Y + t.Size / 2f) / Conf.Tile);
            if (tx < 0 || ty < 0 || tx >= Conf.Cols || ty >= Conf.Rows) return TileType.Empty;
            return G.Grid[ty, tx];
        }

        void MoveTank(Tank t, int dir)
        {
            t.Dir = dir;
            int vx = Conf.DirX[dir];
            int vy = Conf.DirY[dir];

            if (vx != 0)
            {
                float ty = Mathf.Abs(AlignCoord(t.Y) - t.Y) <= Conf.Tile / 2f ? AlignCoord(t.Y) : t.Y;
                float dx = vx * t.Speed;
                if (CanMoveTo(t, t.X + dx, ty)) { t.X += dx; t.Y = ty; t.Moving = true; }
                else if (CanMoveTo(t, t.X + dx, t.Y)) { t.X += dx; t.Moving = true; }
                else t.Moving = false;
            }
            else
            {
                float tx = Mathf.Abs(AlignCoord(t.X) - t.X) <= Conf.Tile / 2f ? AlignCoord(t.X) : t.X;
                float dy = vy * t.Speed;
                if (CanMoveTo(t, tx, t.Y + dy)) { t.X = tx; t.Y += dy; t.Moving = true; }
                else if (CanMoveTo(t, t.X, t.Y + dy)) { t.Y += dy; t.Moving = true; }
                else t.Moving = false;
            }
        }

        /* ---------------- 射击 / 命中 ---------------- */

        void Fire(Tank t)
        {
            if (!t.IsEnemy)
            {
                int pi = t.PlayerIndex;
                Profile prof = G.Profiles[pi];
                int maxBullets = prof.Power >= 2 ? 2 : 1;
                string owner = "p" + pi;

                int mine = 0;
                foreach (Bullet b in G.Bullets)
                {
                    if (b.FromPlayer && b.Owner == owner) mine++;
                }
                if (mine >= maxBullets) return;

                t.ShootCooldown = 12f;
                SpawnBullet(t, true, owner, prof.Power);
                G.Stats.Shots++;
                AudioSynth.Shoot();
            }
            else
            {
                if (t.SpawnTimer > 0) return;
                t.ShootCooldown = Conf.Round(t.ShootInt * 60f * (0.7f + Random.value * 0.6f));
                SpawnBullet(t, false, "enemy", 0);
                AudioSynth.EnemyShoot();
            }
        }

        bool HitTank(Tank t, Bullet b)
        {
            if (t.SpawnTimer > 0) return true; // 出生无敌

            if (b.FromPlayer && t.IsEnemy)
            {
                t.Hp -= 1;
                if (t.Hp <= 0)
                {
                    KillEnemy(t, b.Owner);
                }
                else
                {
                    t.FlashTimer = 8;
                    if (t.Type == "armor")
                    {
                        ArmorPalette p = Maps.ArmorColors[t.Hp];
                        t.Body = p.Body;
                        t.Light = p.Light;
                    }
                    AudioSynth.HitSteel();
                }
                return true;
            }

            if (!b.FromPlayer && !t.IsEnemy)
            {
                if (t.Shield > 0)
                {
                    AudioSynth.HitSteel();
                    return true;
                }
                KillPlayer(t);
                return true;
            }

            return false;
        }

        void KillEnemy(Tank t, string owner)
        {
            t.Alive = false;
            G.EnemiesOnField--;
            G.EnemiesLeft--;
            G.Stats.Kills++;

            if (owner != null && owner.Length > 1 && owner[0] == 'p')
            {
                int pi;
                if (int.TryParse(owner.Substring(1), out pi) && pi >= 0 && pi < G.Profiles.Length)
                {
                    G.Profiles[pi].Score += t.Points;
                }
            }

            AudioSynth.TankExplode();
            SpawnExplosion(t.X + t.Size / 2f, t.Y + t.Size / 2f, Conf.Hex("ffb14d"));

            if (t.Carrier || Random.value < Conf.PowerupDropChance)
            {
                SpawnPowerup(t.X + t.Size / 2f, t.Y + t.Size / 2f);
            }

            G.Tanks.Remove(t);
            if (G.SpawnTimer > 40f) G.SpawnTimer = 40f;
            CheckStageClear();
            Ui.UpdateHud(G);
        }

        void KillPlayer(Tank t)
        {
            Profile prof = G.Profiles[t.PlayerIndex];
            prof.Lives--;
            prof.Power = 0;

            AudioSynth.TankExplode();
            SpawnExplosion(t.X + t.Size / 2f, t.Y + t.Size / 2f,
                t.PlayerIndex == 0 ? Conf.Gold : Conf.Green);

            G.Tanks.Remove(t);
            CheckGameOver();

            if (prof.Lives > 0 && !G.BaseDown && G.State == "playing")
            {
                G.Respawns.Add(new Respawn { Pi = t.PlayerIndex, T = 90 });
            }
            Ui.UpdateHud(G);
        }

        void HitBase()
        {
            if (G.BaseDown || G.State != "playing") return;

            G.BaseHp--;
            G.BaseFlash = 20;

            float cx = Maps.BaseX * Conf.Tile + Conf.Tile / 2f;
            float cy = Maps.BaseY * Conf.Tile + Conf.Tile / 2f;
            SpawnExplosion(cx, cy, Conf.Gold);
            AudioSynth.HitSteel();

            if (G.BaseHp <= 0)
            {
                DestroyBase();
            }
            else
            {
                // 每次被打击都重建基地保护墙：钢墙持续 8 秒，之后恢复为强化砖
                if (G.BaseWallTimer < 480) G.BaseWallTimer = 480;
                BuildBaseWall(TileType.Steel);
            }
            Ui.UpdateHud(G);
        }

        void DestroyBase()
        {
            if (G.BaseDown) return;
            G.BaseDown = true;
            G.Grid[Maps.BaseY, Maps.BaseX] = TileType.Empty;
            AudioSynth.BaseDown();

            float cx = Maps.BaseX * Conf.Tile + Conf.Tile / 2f;
            float cy = Maps.BaseY * Conf.Tile + Conf.Tile / 2f;
            SpawnExplosion(cx, cy, Conf.Danger);

            for (int i = 0; i < 40; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float sp = 1f + Random.value * 5f;
                G.Particles.Add(new Particle
                {
                    X = cx, Y = cy,
                    Vx = Mathf.Cos(a) * sp, Vy = Mathf.Sin(a) * sp,
                    Life = 40 + Random.Range(0, 30),
                    Color = Conf.Orange,
                    Size = 3f + Random.value * 5f,
                    Ring = false
                });
            }

            G.EndKind = "over";
            G.EndReason = "基地被摧毁";
            G.EndTimer = 90;
            G.State = "ending";
        }

        /* ---------------- 道具 ---------------- */

        void SpawnPowerup(float cx, float cy)
        {
            string type = PowerupTypes[Random.Range(0, PowerupTypes.Length)];
            int tx = Mathf.Clamp(Mathf.FloorToInt(cx / Conf.Tile), 0, Conf.Cols - 1);
            int ty = Mathf.Clamp(Mathf.FloorToInt(cy / Conf.Tile), 0, Conf.Rows - 1);
            float x = tx * Conf.Tile + (Conf.Tile - 34f) / 2f;
            float y = ty * Conf.Tile + (Conf.Tile - 34f) / 2f;
            G.Powerups.Add(new Powerup { X = x, Y = y, Type = type, T = 0 });
        }

        void ApplyPowerup(int pi, Powerup p)
        {
            Profile prof = G.Profiles[pi];
            switch (p.Type)
            {
                case "star":
                    prof.Power = Mathf.Min(3, prof.Power + 1);
                    break;
                case "grenade":
                {
                    foreach (Tank t in G.Tanks.ToArray())
                    {
                        if (t.IsEnemy && t.SpawnTimer <= 0) KillEnemy(t, "p" + pi);
                    }
                    break;
                }
                case "tank":
                    prof.Lives = Mathf.Min(9, prof.Lives + 1);
                    break;
                case "shovel":
                    if (G.BaseWallTimer < 720) G.BaseWallTimer = 720;
                    BuildBaseWall(TileType.Steel);
                    break;
                case "shield":
                {
                    foreach (Tank t in G.Tanks)
                    {
                        if (!t.IsEnemy && t.PlayerIndex == pi)
                        {
                            t.Shield = 600;
                            break;
                        }
                    }
                    break;
                }
                case "clock":
                    G.FreezeTimer = 480;
                    break;
            }
            AudioSynth.Pickup();
            Ui.UpdateHud(G);
        }

        void BuildBaseWall(TileType type)
        {
            var placed = new List<int[]>();
            for (int i = 0; i < Maps.BaseWalls.GetLength(0); i++)
            {
                int x = Maps.BaseWalls[i, 0];
                int y = Maps.BaseWalls[i, 1];
                if (x >= 0 && y >= 0 && x < Conf.Cols && y < Conf.Rows && G.Grid[y, x] != TileType.Base)
                {
                    G.Grid[y, x] = type;
                    placed.Add(new int[] { x, y });
                }
            }

            // 基地墙重建时：压死与新建墙体重叠的坦克
            foreach (Tank t in G.Tanks.ToArray())
            {
                if (!t.Alive) continue;
                foreach (int[] cell in placed)
                {
                    if (RectHits(t.X, t.Y, t.Size, t.Size, cell[0] * Conf.Tile, cell[1] * Conf.Tile, Conf.Tile, Conf.Tile))
                    {
                        if (t.IsEnemy) KillEnemy(t, null);
                        else KillPlayer(t);
                        break;
                    }
                }
            }
        }

        /* ---------------- 粒子 ---------------- */

        void SpawnExplosion(float x, float y, Color color)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float sp = 1f + Random.value * 4f;
                G.Particles.Add(new Particle
                {
                    X = x, Y = y,
                    Vx = Mathf.Cos(a) * sp, Vy = Mathf.Sin(a) * sp,
                    Life = 26 + Random.Range(0, 18),
                    Color = color,
                    Size = 2f + Random.value * 5f,
                    Ring = false
                });
            }
            G.Particles.Add(new Particle
            {
                X = x, Y = y, Vx = 0f, Vy = 0f,
                Life = 12, Color = Color.white, Size = 26f, Ring = true
            });
        }
    }
}

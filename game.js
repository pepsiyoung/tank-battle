/* =========================================================================
   TANK BATTLE — FC 经典坦克大战 重制版
   双人模式 · 现代 UI · Canvas 渲染
   ========================================================================= */
(() => {
"use strict";

/* ---------------- Canvas ---------------- */
const canvas = document.getElementById("game");
const ctx = canvas.getContext("2d");
const W = canvas.width;    // 624
const H = canvas.height;   // 624

/* ---------------- 常量 ---------------- */
const COLS = 13, ROWS = 13;
const TILE = W / COLS;     // 48
const TANK = 40;
const CHARGE_FRAMES = 120;         // 绝招蓄力时长（2 秒 @60fps）
const CHARGE_BAR_DELAY = 30;       // 蓄力条出现延迟（0.5 秒）
const ULT_CHARGES = 3;             // 每辆坦克可用的绝招次数
const BEAM_WIDTH = 3 * TILE;       // 破坏死光宽度：3 格
const BURN_DURATION = 45 * 60;     // 灼烧持续时长（45 秒）

const EMPTY = 0, BRICK = 1, STEEL = 2, WATER = 3, TREE = 4, ICE = 5, BASE = 6, HARDBRICK = 7, BURN = 8;

const UP = 0, RIGHT = 1, DOWN = 2, LEFT = 3;
const DIR = {
  [UP]:    { x: 0, y: -1 },
  [RIGHT]: { x: 1, y: 0 },
  [DOWN]:  { x: 0, y: 1 },
  [LEFT]:  { x: -1, y: 0 },
};
const opposite = d => (d + 2) % 4;

/* ---------------- 音效（Web Audio 合成复古音） ---------------- */
const Sound = (() => {
  let ac = null, master = null;
  let enabled = true;

  function ensure() {
    if (!ac) {
      try { ac = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { ac = null; }
      if (ac) {
        master = ac.createGain();
        master.gain.value = 0.42;
        master.connect(ac.destination);
      }
    }
    if (ac && ac.state === "suspended") ac.resume();
  }
  function tone(freq, dur, type = "square", vol = 0.14, slideTo = null) {
    if (!enabled) return;
    ensure();
    if (!ac) return;
    const t = ac.currentTime;
    const o = ac.createOscillator();
    const g = ac.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t + dur);
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g); g.connect(master);
    o.start(t); o.stop(t + dur);
  }
  function noise(dur, vol = 0.2, freq = 800) {
    if (!enabled) return;
    ensure();
    if (!ac) return;
    const t = ac.currentTime;
    const len = Math.floor(ac.sampleRate * dur);
    const buf = ac.createBuffer(1, len, ac.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / len);
    const src = ac.createBufferSource();
    src.buffer = buf;
    const f = ac.createBiquadFilter();
    f.type = "lowpass"; f.frequency.value = freq;
    const g = ac.createGain();
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(f); f.connect(g); g.connect(master);
    src.start(t);
  }
  return {
    get enabled() { return enabled; },
    set enabled(v) { enabled = v; },
    resume() { ensure(); },
    shoot() { tone(880, 0.07, "square", 0.09, 300); },
    enemyShoot() { tone(500, 0.08, "square", 0.06, 220); },
    hitBrick() { noise(0.07, 0.12, 1400); },
    hitSteel() { tone(190, 0.06, "square", 0.09); },
    explode() { noise(0.25, 0.22, 650); tone(130, 0.25, "sawtooth", 0.1, 40); },
    tankExplode() { noise(0.38, 0.3, 520); tone(110, 0.35, "sawtooth", 0.14, 35); },
    pickup() { tone(660, 0.09, "square", 0.11, 1320); },
    spawn() { tone(300, 0.1, "triangle", 0.09, 620); },
    stageClear() { [523, 659, 784, 1046].forEach((f, i) => setTimeout(() => tone(f, 0.16, "square", 0.11), i * 130)); },
    gameOver() { [420, 330, 240, 150].forEach((f, i) => setTimeout(() => tone(f, 0.28, "sawtooth", 0.1), i * 200)); },
    baseDown() { noise(0.6, 0.4, 420); tone(80, 0.6, "sawtooth", 0.18, 30); },
    chargeTick() { tone(300 + Math.random() * 120, 0.05, "square", 0.05, 520); },
    chargeFull() { tone(760, 0.22, "square", 0.1, 1520); },
    beam() { noise(0.55, 0.32, 1100); tone(110, 0.5, "sawtooth", 0.16, 880); },
    pause() { tone(700, 0.07, "square", 0.07); },
  };
})();

/* ---------------- 输入 ---------------- */
const keys = new Set();
const vkeys = new Set(); // 虚拟按键（触屏）

window.addEventListener("keydown", (e) => {
  if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Space"].includes(e.code)) e.preventDefault();
  keys.add(e.code);
  Sound.resume();
  handleKey(e.code);
});
window.addEventListener("keyup", (e) => keys.delete(e.code));
window.addEventListener("blur", () => keys.clear());

function pressVKey(k) { vkeys.add(k); }
function releaseVKey(k) { vkeys.delete(k); }

const isDown = c => keys.has(c) || vkeys.has(c);

function getInputDir(pi) {
  const alt = G.mode === 1 && pi === 0; // 单人模式：P1 兼容方向键
  if (pi === 0) {
    if (isDown("KeyW")) return UP;
    if (isDown("KeyS")) return DOWN;
    if (isDown("KeyA")) return LEFT;
    if (isDown("KeyD")) return RIGHT;
    if (alt) {
      if (isDown("ArrowUp")) return UP;
      if (isDown("ArrowDown")) return DOWN;
      if (isDown("ArrowLeft")) return LEFT;
      if (isDown("ArrowRight")) return RIGHT;
    }
    return null;
  }
  if (isDown("ArrowUp")) return UP;
  if (isDown("ArrowDown")) return DOWN;
  if (isDown("ArrowLeft")) return LEFT;
  if (isDown("ArrowRight")) return RIGHT;
  return null;
}
function pressedFire(pi) {
  if (pi === 0) {
    if (isDown("Space")) return true;
    if (G.mode === 1 && (isDown("Enter") || isDown("NumpadEnter"))) return true;
    return isDown("vFire");
  }
  return isDown("Enter") || isDown("NumpadEnter") || isDown("vFire");
}

function handleKey(code) {
  if (code === "KeyP" || code === "Escape") {
    if (G.state === "playing" || G.state === "paused") togglePause();
  } else if (code === "Enter" || code === "NumpadEnter") {
    if (G.state === "menu") startGame(1);
    else if (G.state === "stageclear" || G.state === "gameover" || G.state === "victory") nextOrRestart();
  } else if (code === "KeyM") {
    toggleSound();
  }
}

/* ---------------- 关卡地图 ---------------- */
// 12 行地形（第 13 行 row12 由程序管理：基地 + 保护墙 + 玩家出生点）
const MAPS = [
  [ // Stage 1
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
    "#.#.#.#.#.#.#",
    ".............",
  ],
  [ // Stage 2
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
    ".S.S.....S.S.",
    ".............",
  ],
  [ // Stage 3
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
    ".#.#.#.#.#.#.",
    ".............",
  ],
  [ // Stage 4
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
    ".TTT.....TTT.",
    ".............",
  ],
  [ // Stage 5
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
    ".T.T.T.T.T.T.",
    ".............",
  ],
];

const CHAR_MAP = { ".": EMPTY, "#": BRICK, "S": STEEL, "W": WATER, "T": TREE, "I": ICE };
const BASE_POS = { x: 6, y: 12 };
const BASE_WALLS = [[5, 11], [6, 11], [7, 11], [5, 12], [7, 12]];

function loadMap(stage) {
  const rows = MAPS[(stage - 1) % MAPS.length];
  const grid = [];
  for (let y = 0; y < ROWS; y++) grid.push(new Array(COLS).fill(EMPTY));
  for (let y = 0; y < Math.min(rows.length, ROWS - 1); y++) {
    const row = rows[y];
    for (let x = 0; x < COLS; x++) {
      const ch = row[x];
      grid[y][x] = CHAR_MAP[ch] !== undefined ? CHAR_MAP[ch] : EMPTY;
    }
  }
  // 基地 + 保护墙
  grid[BASE_POS.y][BASE_POS.x] = BASE;
  for (const [x, y] of BASE_WALLS) grid[y][x] = HARDBRICK;
  return grid;
}

/* ---------------- 敌军数据 ---------------- */
const ENEMY_STATS = {
  basic: { hp: 1, speed: 1.5, points: 100, bulletSpeed: 4.5, shootInt: 1.8, body: "#b9c2d1", light: "#e6ecf6" },
  fast:  { hp: 1, speed: 2.6, points: 200, bulletSpeed: 5.0, shootInt: 1.6, body: "#4fb6ff", light: "#bfe6ff" },
  power: { hp: 1, speed: 1.3, points: 300, bulletSpeed: 6.5, shootInt: 1.0, body: "#ffb14d", light: "#ffe3b3" },
  armor: { hp: 4, speed: 0.9, points: 400, bulletSpeed: 4.5, shootInt: 2.0, body: "#7ed06a", light: "#c4f2b0" },
};
const ARMOR_COLORS = {
  4: { body: "#7ed06a", light: "#c4f2b0" },
  3: { body: "#ffd24a", light: "#ffefb0" },
  2: { body: "#ff9a3d", light: "#ffd2a0" },
  1: { body: "#ff5d6c", light: "#ffc0c6" },
};
const MAX_ON_FIELD = 4;
const TOTAL_ENEMIES = 20;
const CARRIER_INDEXES = [3, 10, 17]; // 携带道具的敌军序号（0-based）
const POWERUP_DROP_CHANCE = 0.3;      // 击杀敌军掉落道具的基础概率

function buildEnemyQueue(stage) {
  const q = [];
  const basicW = Math.max(18, 58 - stage * 5);
  const fastW = 18 + stage * 2;
  const powerW = 10 + stage * 2;
  const armorW = 10 + stage * 2;
  for (let i = 0; i < TOTAL_ENEMIES; i++) {
    const r = Math.random() * (basicW + fastW + powerW + armorW);
    let t;
    if (r < basicW) t = "basic";
    else if (r < basicW + fastW) t = "fast";
    else if (r < basicW + fastW + powerW) t = "power";
    else t = "armor";
    q.push(t);
  }
  return q;
}

/* ---------------- 游戏状态 ---------------- */
const G = {
  mode: 1,
  state: "menu", // menu | playing | paused | ending | stageclear | gameover
  endKind: "clear",
  endReason: "",
  stage: 1,
  grid: null,
  profiles: [{ lives: 3, score: 0, power: 0 }, { lives: 3, score: 0, power: 0 }],
  tanks: [],
  bullets: [],
  powerups: [],
  particles: [],
  beams: [],
  burnTiles: [],
  respawns: [],
  enemyQueue: [],
  enemiesLeft: TOTAL_ENEMIES,
  enemiesOnField: 0,
  spawnCount: 0,
  spawnTimer: 60,
  baseDown: false,
  baseHp: 3,
  baseFlash: 0,
  baseWallTimer: 0,
  freezeTimer: 0,
  endTimer: 0,
  acc: 0,
  nowMs: 0,
  stats: { kills: 0, ultUses: 0, shots: 0 },
};

/* ---------------- 实体创建 ---------------- */
function createPlayerTank(pi) {
  const col = pi === 0 ? 4 : 8;
  const pal = pi === 0
    ? { body: "#e2a912", light: "#ffe9a0" }   // P1 金色
    : { body: "#22b573", light: "#b0f5d3" };  // P2 绿色
  return {
    x: col * TILE + (TILE - TANK) / 2,
    y: 12 * TILE + (TILE - TANK) / 2,
    dir: UP, speed: 2.4, size: TANK,
    isEnemy: false, playerIndex: pi,
    body: pal.body, light: pal.light,
    alive: true, hp: 1,
    shield: 0, spawnTimer: 90, shootCooldown: 0, flashTimer: 0, moving: false,
    ultCharges: ULT_CHARGES, charging: false, chargeT: 0, chargeReady: false, wasFiring: false,
  };
}
function createEnemy(type, px, py, carrier) {
  const s = ENEMY_STATS[type];
  const pal = type === "armor" ? ARMOR_COLORS[4] : { body: s.body, light: s.light };
  return {
    x: px, y: py, dir: DOWN, speed: s.speed, size: TANK,
    isEnemy: true, type, carrier,
    alive: true, hp: s.hp, maxHp: s.hp,
    points: s.points, bulletSpeed: s.bulletSpeed, shootInt: s.shootInt,
    body: pal.body, light: pal.light,
    spawnTimer: 60, shootCooldown: 30 + Math.random() * 60, aiTimer: 60 + Math.random() * 60,
    flashTimer: 0, moving: false,
  };
}
function spawnBullet(t, fromPlayer, owner, power) {
  const c = { x: t.x + t.size / 2, y: t.y + t.size / 2 };
  const off = t.size / 2 + 2;
  const speed = fromPlayer ? (power >= 1 ? 6.5 : 5) : t.bulletSpeed;
  const b = { dir: t.dir, speed, owner, fromPlayer, power, w: 4, h: 7, dead: false };
  if (t.dir === UP) { b.x = c.x - 2; b.y = c.y - off - 3; }
  else if (t.dir === DOWN) { b.x = c.x - 2; b.y = c.y + off - 4; }
  else if (t.dir === LEFT) { b.x = c.x - off - 3; b.y = c.y - 2; b.w = 7; b.h = 4; }
  else { b.x = c.x + off - 4; b.y = c.y - 2; b.w = 7; b.h = 4; }
  G.bullets.push(b);
}

/* ---------------- 碰撞工具 ---------------- */
const rectHits = (x, y, w, h, x2, y2, w2, h2) =>
  x < x2 + w2 && x + w > x2 && y < y2 + h2 && y + h > y2;

function tileSolid(tx, ty) {
  if (tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS) return true;
  const t = G.grid[ty][tx];
  return t === BRICK || t === HARDBRICK || t === STEEL || t === WATER || t === BASE;
}
function bulletSolid(tx, ty) {
  if (tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS) return true;
  const t = G.grid[ty][tx];
  return t === BRICK || t === HARDBRICK || t === STEEL || t === BASE;
}
function canMoveTo(tank, nx, ny) {
  if (nx < 0 || ny < 0 || nx + TANK > W || ny + TANK > H) return false;
  const x0 = Math.floor(nx / TILE), x1 = Math.floor((nx + TANK - 1) / TILE);
  const y0 = Math.floor(ny / TILE), y1 = Math.floor((ny + TANK - 1) / TILE);
  for (let ty = y0; ty <= y1; ty++)
    for (let tx = x0; tx <= x1; tx++)
      if (tileSolid(tx, ty)) return false;
  for (const t of G.tanks) {
    if (t === tank || !t.alive) continue;
    // 敌方坦克出生动画期间碰撞体积同样生效；玩家坦克出生短暂无敌期间忽略碰撞
    if (t.spawnTimer > 0 && !t.isEnemy) continue;
    if (rectHits(nx, ny, TANK, TANK, t.x, t.y, t.size, t.size)) return false;
  }
  return true;
}
const alignCoord = v => Math.round((v - 4) / TILE) * TILE + 4;
const tileAtCenter = t => G.grid[Math.floor((t.y + t.size / 2) / TILE)][Math.floor((t.x + t.size / 2) / TILE)];

function moveTank(t, dir) {
  const v = DIR[dir];
  t.dir = dir;
  if (v.x !== 0) {
    const ty = Math.abs(alignCoord(t.y) - t.y) <= TILE / 2 ? alignCoord(t.y) : t.y;
    const dx = v.x * t.speed;
    if (canMoveTo(t, t.x + dx, ty)) { t.x += dx; t.y = ty; t.moving = true; }
    else if (canMoveTo(t, t.x + dx, t.y)) { t.x += dx; t.moving = true; }
    else t.moving = false;
  } else {
    const tx = Math.abs(alignCoord(t.x) - t.x) <= TILE / 2 ? alignCoord(t.x) : t.x;
    const dy = v.y * t.speed;
    if (canMoveTo(t, tx, t.y + dy)) { t.x = tx; t.y += dy; t.moving = true; }
    else if (canMoveTo(t, t.x, t.y + dy)) { t.y += dy; t.moving = true; }
    else t.moving = false;
  }
}

/* ---------------- 射击 ---------------- */
function fire(t) {
  if (!t.isEnemy) {
    const pi = t.playerIndex;
    const prof = G.profiles[pi];
    const maxBullets = prof.power >= 2 ? 2 : 1;
    const mine = G.bullets.filter(b => b.fromPlayer && b.owner === "p" + pi);
    if (mine.length >= maxBullets) return;
    t.shootCooldown = 12;
    spawnBullet(t, true, "p" + pi, prof.power);
    G.stats.shots++;
    Sound.shoot();
  } else {
    if (t.spawnTimer > 0) return;
    t.shootCooldown = Math.round(t.shootInt * 60 * (0.7 + Math.random() * 0.6));
    spawnBullet(t, false, "enemy", 0);
    Sound.enemyShoot();
  }
}

/* ---------------- 命中 / 击杀 ---------------- */
function hitTank(t, b) {
  if (t.spawnTimer > 0) return true; // 出生无敌
  if (b.fromPlayer && t.isEnemy) {
    t.hp -= 1;
    if (t.hp <= 0) killEnemy(t, b.owner);
    else {
      t.flashTimer = 8;
      if (t.type === "armor") { const p = ARMOR_COLORS[t.hp]; t.body = p.body; t.light = p.light; }
      Sound.hitSteel();
    }
    return true;
  }
  if (!b.fromPlayer && !t.isEnemy) {
    if (t.shield > 0) { Sound.hitSteel(); return true; }
    killPlayer(t);
    return true;
  }
  return false;
}

function killEnemy(t, owner) {
  t.alive = false;
  G.enemiesOnField--;
  G.enemiesLeft--;
  G.stats.kills++;
  const pi = owner ? parseInt(owner.slice(1), 10) : NaN;
  if (!isNaN(pi)) G.profiles[pi].score += t.points;
  Sound.tankExplode();
  spawnExplosion(t.x + t.size / 2, t.y + t.size / 2, "#ffb14d");
  if (t.carrier || Math.random() < POWERUP_DROP_CHANCE) spawnPowerup(t.x + t.size / 2, t.y + t.size / 2);
  G.tanks = G.tanks.filter(x => x !== t);
  G.spawnTimer = Math.min(G.spawnTimer, 40);
  checkStageClear();
  updateHUD();
}

function killPlayer(t) {
  const prof = G.profiles[t.playerIndex];
  prof.lives--;
  prof.power = 0;
  Sound.tankExplode();
  spawnExplosion(t.x + t.size / 2, t.y + t.size / 2, t.playerIndex === 0 ? "#ffd24a" : "#3ee08c");
  G.tanks = G.tanks.filter(x => x !== t);
  checkGameOver();
  if (prof.lives > 0 && !G.baseDown && G.state === "playing") {
    G.respawns.push({ pi: t.playerIndex, t: 90 });
  }
  updateHUD();
}

function hitBase() {
  if (G.baseDown || G.state !== "playing") return;
  G.baseHp--;
  G.baseFlash = 20;
  const cx = BASE_POS.x * TILE + TILE / 2, cy = BASE_POS.y * TILE + TILE / 2;
  spawnExplosion(cx, cy, "#ffd24a");
  Sound.hitSteel();
  if (G.baseHp <= 0) {
    destroyBase();
  } else {
    // 每次被打击都重建一次基地保护墙：钢墙持续 8 秒，之后恢复为强化砖
    G.baseWallTimer = Math.max(G.baseWallTimer, 480);
    buildBaseWall(STEEL);
  }
  updateHUD();
}

function destroyBase() {
  if (G.baseDown) return;
  G.baseDown = true;
  G.grid[BASE_POS.y][BASE_POS.x] = EMPTY;
  Sound.baseDown();
  const cx = BASE_POS.x * TILE + TILE / 2, cy = BASE_POS.y * TILE + TILE / 2;
  spawnExplosion(cx, cy, "#ff5d6c");
  for (let i = 0; i < 40; i++) {
    const a = Math.random() * Math.PI * 2;
    const sp = 1 + Math.random() * 5;
    G.particles.push({ x: cx, y: cy, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 40 + Math.random() * 30, color: "#ff8a5c", size: 3 + Math.random() * 5 });
  }
  G.endKind = "over";
  G.endReason = "基地被摧毁";
  G.endTimer = 90;
  G.state = "ending";
}

/* ---------------- 道具 ---------------- */
const POWERUP_TYPES = ["star", "star", "grenade", "tank", "shovel", "shield", "clock"];
function spawnPowerup(cx, cy) {
  const type = POWERUP_TYPES[(Math.random() * POWERUP_TYPES.length) | 0];
  const tx = Math.max(0, Math.min(COLS - 1, Math.floor(cx / TILE)));
  const ty = Math.max(0, Math.min(ROWS - 1, Math.floor(cy / TILE)));
  const x = tx * TILE + (TILE - 34) / 2;
  const y = ty * TILE + (TILE - 34) / 2;
  G.powerups.push({ x, y, type, t: 0 });
}
function applyPowerup(pi, p) {
  const prof = G.profiles[pi];
  switch (p.type) {
    case "star":
      prof.power = Math.min(3, prof.power + 1);
      break;
    case "grenade": {
      const targets = G.tanks.filter(t => t.isEnemy && t.spawnTimer <= 0);
      for (const t of targets) killEnemy(t, "p" + pi);
      break;
    }
    case "tank":
      prof.lives = Math.min(9, prof.lives + 1);
      break;
    case "shovel":
      G.baseWallTimer = Math.max(G.baseWallTimer, 720);
      buildBaseWall(STEEL);
      break;
    case "shield": {
      const tank = G.tanks.find(t => !t.isEnemy && t.playerIndex === pi);
      if (tank) tank.shield = 600;
      break;
    }
    case "clock":
      G.freezeTimer = 480;
      break;
  }
  Sound.pickup();
  updateHUD();
}
function buildBaseWall(type) {
  const placed = [];
  for (const [x, y] of BASE_WALLS) {
    if (x >= 0 && y >= 0 && x < COLS && y < ROWS && G.grid[y][x] !== BASE) {
      G.grid[y][x] = type;
      placed.push([x, y]);
    }
  }
  // 基地墙重建时（钢墙 / 砖墙均触发）：压死与新建墙体重叠的坦克
  {
    for (const t of [...G.tanks]) {
      if (!t.alive) continue;
      for (const [x, y] of placed) {
        if (rectHits(t.x, t.y, t.size, t.size, x * TILE, y * TILE, TILE, TILE)) {
          if (t.isEnemy) killEnemy(t, null);
          else killPlayer(t);
          break;
        }
      }
    }
  }
}

/* ---------------- 粒子 ---------------- */
function spawnExplosion(x, y, color) {
  for (let i = 0; i < 24; i++) {
    const a = Math.random() * Math.PI * 2;
    const sp = 1 + Math.random() * 4;
    G.particles.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 26 + Math.random() * 18, color, size: 2 + Math.random() * 5 });
  }
  G.particles.push({ x, y, vx: 0, vy: 0, life: 12, color: "#ffffff", size: 26, ring: true });
}

/* ---------------- 主循环 ---------------- */
const STEP = 1000 / 60;
let last = performance.now();

function loop(now) {
  const dt = Math.min(60, now - last);
  last = now;
  G.nowMs = now;
  if (G.state === "playing") {
    G.acc += dt;
    let guard = 0;
    while (G.acc >= STEP && guard++ < 4) { step(); G.acc -= STEP; }
    if (guard >= 4) G.acc = 0;
  } else if (G.state === "ending") {
    G.acc += dt;
    while (G.acc >= STEP) { updateParticles(); updateBeams(); if (--G.endTimer <= 0) finalizeEnd(); G.acc -= STEP; }
  }
  render();
  requestAnimationFrame(loop);
}

function step() {
  // 基地保护墙 / 受击闪烁计时
  if (G.baseWallTimer > 0) { G.baseWallTimer--; if (G.baseWallTimer === 0) buildBaseWall(HARDBRICK); }
  if (G.baseFlash > 0) G.baseFlash--;
  if (G.freezeTimer > 0) G.freezeTimer--;

  // 敌军生成
  if (G.spawnTimer > 0) G.spawnTimer--;
  else if (G.enemyQueue.length > 0 && G.enemiesOnField < MAX_ON_FIELD) trySpawnEnemy();

  // 玩家重生
  for (let i = G.respawns.length - 1; i >= 0; i--) {
    const r = G.respawns[i];
    if (--r.t <= 0) {
      G.respawns.splice(i, 1);
      if (G.profiles[r.pi].lives > 0) {
        const t = createPlayerTank(r.pi);
        // 出生点若有敌方坦克则直接消灭（腾出位置）
        const blockers = G.tanks.filter(o => o.alive && o.isEnemy && rectHits(t.x, t.y, TANK, TANK, o.x, o.y, o.size, o.size));
        for (const b of blockers) killEnemy(b, "p" + r.pi);
        const free = !G.tanks.some(o => o.alive && rectHits(t.x, t.y, TANK, TANK, o.x, o.y, o.size, o.size));
        if (free) { t.shield = 120; G.tanks.push(t); }
        else G.respawns.push({ pi: r.pi, t: 60 });
      }
    }
  }

  updatePlayers();
  updateEnemies();
  updateBullets();
  updatePowerups();
  updateParticles();
  updateBeams();
  updateBurnTiles();
  checkBurnOverlap();
  updateHUD();
}

function trySpawnEnemy() {
  const cols = [0, 6, 12];
  const picks = cols.filter(c => {
    const px = c * TILE + (TILE - TANK) / 2, py = (TILE - TANK) / 2;
    return !G.tanks.some(t => t.alive && rectHits(px, py, TANK, TANK, t.x, t.y, t.size, t.size));
  });
  if (picks.length === 0) return;
  const c = picks[(Math.random() * picks.length) | 0];
  const type = G.enemyQueue.shift();
  const carrier = CARRIER_INDEXES.includes(G.spawnCount);
  const px = c * TILE + (TILE - TANK) / 2, py = (TILE - TANK) / 2;
  G.tanks.push(createEnemy(type, px, py, carrier));
  G.enemiesOnField++;
  G.spawnCount++;
  G.spawnTimer = 50 + Math.random() * 40;
  Sound.spawn();
}

function updatePlayers() {
  for (const t of G.tanks) {
    if (t.isEnemy) continue;
    if (t.spawnTimer > 0) t.spawnTimer--;
    if (t.shield > 0) t.shield--;
    if (t.flashTimer > 0) t.flashTimer--;
    t.shootCooldown--;
    const dir = getInputDir(t.playerIndex);
    if (dir !== null) moveTank(t, dir);
    else {
      t.moving = false;
      if (tileAtCenter(t) === ICE) moveTank(t, t.dir);
    }
    updatePlayerFire(t);
  }
}

function updatePlayerFire(t) {
  const firing = pressedFire(t.playerIndex);
  // 绝招耗尽：回到经典连射（按住即射击）
  if (t.ultCharges <= 0) {
    if (firing && t.shootCooldown <= 0) fire(t);
    t.wasFiring = firing;
    return;
  }
  // 按下发射键：开始蓄力
  if (firing && !t.wasFiring) {
    t.charging = true;
    t.chargeT = 0;
    t.chargeReady = false;
  }
  if (t.charging) {
    if (firing) {
      t.chargeT++;
      if (t.chargeT >= CHARGE_FRAMES && !t.chargeReady) {
        t.chargeReady = true;
        Sound.chargeFull();
      } else if (t.chargeT < CHARGE_FRAMES && t.chargeT % 30 === 0) {
        Sound.chargeTick();
      }
      if (t.chargeT >= CHARGE_BAR_DELAY && t.chargeT % 5 === 0) spawnChargeParticle(t);
    } else {
      // 松开：满蓄力 → 破坏死光；未满 → 普通射击
      if (t.chargeReady && t.ultCharges > 0) {
        t.ultCharges--;
        G.stats.ultUses++;
        fireBeam(t);
        Sound.beam();
      } else if (t.shootCooldown <= 0) {
        fire(t);
      }
      t.charging = false;
      t.chargeT = 0;
      t.chargeReady = false;
    }
  }
  t.wasFiring = firing;
}

function fireBeam(t) {
  const cx = t.x + t.size / 2, cy = t.y + t.size / 2;
  const half = BEAM_WIDTH / 2;
  let x0, y0, x1, y1;
  if (t.dir === UP)         { x0 = cx - half; x1 = cx + half; y0 = 0;            y1 = t.y; }
  else if (t.dir === DOWN)  { x0 = cx - half; x1 = cx + half; y0 = t.y + t.size; y1 = H; }
  else if (t.dir === LEFT)  { y0 = cy - half; y1 = cy + half; x0 = 0;            x1 = t.x; }
  else                      { y0 = cy - half; y1 = cy + half; x0 = t.x + t.size; x1 = W; }

  // 消灭路径上的所有地形
  const tx0 = Math.floor(x0 / TILE), tx1 = Math.ceil(x1 / TILE) - 1;
  const ty0 = Math.floor(y0 / TILE), ty1 = Math.ceil(y1 / TILE) - 1;
  let baseHit = false;
  // 施放者所在格除外：坦克足印比格子小 4px 且有偏移，光束矩形与脚下格子重叠，
  // 若脚下是草地会被误变成灼烧地（站在草丛放大招烧死自己）
  const selfX0 = Math.floor(t.x / TILE), selfX1 = Math.floor((t.x + t.size - 1) / TILE);
  const selfY0 = Math.floor(t.y / TILE), selfY1 = Math.floor((t.y + t.size - 1) / TILE);
  for (let ty = ty0; ty <= ty1; ty++) {
    for (let tx = tx0; tx <= tx1; tx++) {
      if (tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS) continue;
      if (tx >= selfX0 && tx <= selfX1 && ty >= selfY0 && ty <= selfY1) continue;
      if (!rectHits(x0, y0, x1 - x0, y1 - y0, tx * TILE, ty * TILE, TILE, TILE)) continue;
      const tile = G.grid[ty][tx];
      if (tile === EMPTY) continue;
      if (tile === WATER) continue;      // 河流不被清除
      if (tile === TREE) {               // 草地 → 灼烧（45 秒）
        G.grid[ty][tx] = BURN;
        addBurnTile(tx, ty);
        spawnTileFlash(tx * TILE + TILE / 2, ty * TILE + TILE / 2, "#ff8a3c");
        continue;
      }
      if (tile === BURN) {               // 已灼烧 → 刷新计时
        addBurnTile(tx, ty);
        continue;
      }
      if (tile === BASE) baseHit = true;
      G.grid[ty][tx] = EMPTY;
      spawnTileFlash(tx * TILE + TILE / 2, ty * TILE + TILE / 2, tile === BASE ? "#ff5d6c" : "#ffd24a");
    }
  }
  if (baseHit) destroyBase();

  // 消灭路径上的所有坦克
  for (const other of [...G.tanks]) {
    if (!other.alive || other === t) continue;
    if (!rectHits(x0, y0, x1 - x0, y1 - y0, other.x, other.y, other.size, other.size)) continue;
    if (other.isEnemy) killEnemy(other, "p" + t.playerIndex);
    else killPlayer(other);
  }

  G.beams.push({ x0, y0, x1, y1, dir: t.dir, life: 22, ownerPi: t.playerIndex });
  spawnBeamParticles(x0, y0, x1, y1);
}

function spawnTileFlash(x, y, color) {
  for (let i = 0; i < 8; i++) {
    const a = Math.random() * Math.PI * 2;
    const sp = 1 + Math.random() * 3;
    G.particles.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 16 + Math.random() * 12, color, size: 2 + Math.random() * 4 });
  }
}

function spawnBeamParticles(x0, y0, x1, y1) {
  const w = x1 - x0, h = y1 - y0;
  for (let i = 0; i < 46; i++) {
    const px = x0 + Math.random() * w;
    const py = y0 + Math.random() * h;
    G.particles.push({
      x: px, y: py,
      vx: (Math.random() - 0.5) * 3, vy: (Math.random() - 0.5) * 3,
      life: 18 + Math.random() * 16,
      color: Math.random() < 0.5 ? "#ffd24a" : "#ff8a5c",
      size: 3 + Math.random() * 5,
    });
  }
}

function spawnChargeParticle(t) {
  const cx = t.x + t.size / 2, cy = t.y + t.size / 2;
  const a = Math.random() * Math.PI * 2;
  const r = t.size / 2 + 6;
  const col = t.playerIndex === 0 ? "#ffd24a" : "#3ee08c";
  G.particles.push({ x: cx + Math.cos(a) * r, y: cy + Math.sin(a) * r, vx: -Math.cos(a) * 1.6, vy: -Math.sin(a) * 1.6, life: 14, color: col, size: 3 });
}

function addBurnTile(tx, ty) {
  const existing = G.burnTiles.find(b => b.x === tx && b.y === ty);
  if (existing) existing.t = BURN_DURATION;
  else G.burnTiles.push({ x: tx, y: ty, t: BURN_DURATION });
}

function updateBurnTiles() {
  for (let i = G.burnTiles.length - 1; i >= 0; i--) {
    const b = G.burnTiles[i];
    if (--b.t <= 0) {
      if (G.grid[b.y][b.x] === BURN) G.grid[b.y][b.x] = EMPTY; // 灼烧结束，变为空地
      G.burnTiles.splice(i, 1);
    }
  }
}

function checkBurnOverlap() {
  for (const t of [...G.tanks]) {
    if (!t.alive) continue;
    const x0 = Math.floor(t.x / TILE), x1 = Math.floor((t.x + t.size - 1) / TILE);
    const y0 = Math.floor(t.y / TILE), y1 = Math.floor((t.y + t.size - 1) / TILE);
    let hit = false;
    for (let ty = y0; ty <= y1 && !hit; ty++)
      for (let tx = x0; tx <= x1; tx++) {
        if (tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS) continue;
        if (G.grid[ty][tx] === BURN) { hit = true; break; }
      }
    if (hit) {
      if (t.isEnemy) killEnemy(t, null);
      else killPlayer(t);
    }
  }
}

function updateBeams() {
  for (let i = G.beams.length - 1; i >= 0; i--) {
    if (--G.beams[i].life <= 0) G.beams.splice(i, 1);
  }
}

function updateEnemies() {
  for (const t of G.tanks) {
    if (!t.isEnemy) continue;
    if (t.flashTimer > 0) t.flashTimer--;
    if (t.spawnTimer > 0) { t.spawnTimer--; continue; }
    if (G.freezeTimer > 0) { t.moving = false; continue; }
    t.shootCooldown--;
    t.aiTimer--;
    if (t.aiTimer <= 0) {
      t.aiTimer = 40 + Math.random() * 80;
      const r = Math.random();
      if (r < 0.35) t.dir = seekDirToBase(t);
      else {
        const choices = [UP, RIGHT, DOWN, LEFT].filter(d => d !== opposite(t.dir));
        t.dir = choices[(Math.random() * choices.length) | 0];
      }
    }
    moveTank(t, t.dir);
    if (t.shootCooldown <= 0) fire(t);
  }
}

function seekDirToBase(t) {
  const ex = (t.x + t.size / 2) / TILE, ey = (t.y + t.size / 2) / TILE;
  const bx = BASE_POS.x + 0.5, by = BASE_POS.y + 0.5;
  if (Math.abs(bx - ex) > Math.abs(by - ey)) return bx > ex ? RIGHT : LEFT;
  return by > ey ? DOWN : UP;
}

function updateBullets() {
  for (const b of G.bullets) {
    const v = DIR[b.dir];
    b.x += v.x * b.speed;
    b.y += v.y * b.speed;
    if (b.x < -20 || b.y < -20 || b.x > W + 20 || b.y > H + 20) { b.dead = true; continue; }

    // 前方地块
    let fx, fy;
    if (b.dir === UP) { fx = b.x + b.w / 2; fy = b.y; }
    else if (b.dir === DOWN) { fx = b.x + b.w / 2; fy = b.y + b.h; }
    else if (b.dir === LEFT) { fx = b.x; fy = b.y + b.h / 2; }
    else { fx = b.x + b.w; fy = b.y + b.h / 2; }
    const tx = Math.floor(fx / TILE), ty = Math.floor(fy / TILE);
    if (tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS) { b.dead = true; continue; }
    const tile = G.grid[ty][tx];
    if (tile === BRICK) {
      G.grid[ty][tx] = EMPTY;
      Sound.hitBrick();
      spawnExplosion(tx * TILE + TILE / 2, ty * TILE + TILE / 2, "#d85a34");
      b.dead = true; continue;
    } else if (tile === HARDBRICK) {
      G.grid[ty][tx] = BRICK;
      Sound.hitBrick();
      spawnExplosion(tx * TILE + TILE / 2, ty * TILE + TILE / 2, "#8a3520");
      b.dead = true; continue;
    } else if (tile === STEEL) {
      if (b.fromPlayer && b.power >= 3) {
        G.grid[ty][tx] = EMPTY;
        Sound.hitSteel();
        spawnExplosion(tx * TILE + TILE / 2, ty * TILE + TILE / 2, "#cfd6e4");
      } else Sound.hitSteel();
      b.dead = true; continue;
    } else if (tile === BASE) {
      hitBase();
      b.dead = true; continue;
    }

    // 坦克碰撞
    for (const t of G.tanks) {
      if (!t.alive) continue;
      if (b.fromPlayer && !t.isEnemy) continue;
      if (!b.fromPlayer && t.isEnemy) continue;
      if (rectHits(b.x, b.y, b.w, b.h, t.x, t.y, t.size, t.size)) {
        if (hitTank(t, b)) { b.dead = true; break; }
      }
    }
  }
  // 子弹对消
  for (let i = 0; i < G.bullets.length; i++) {
    const a = G.bullets[i];
    if (a.dead) continue;
    for (let j = i + 1; j < G.bullets.length; j++) {
      const c = G.bullets[j];
      if (c.dead || a.fromPlayer === c.fromPlayer) continue;
      const d = Math.hypot((a.x + a.w / 2) - (c.x + c.w / 2), (a.y + a.h / 2) - (c.y + c.h / 2));
      if (d < 8) {
        a.dead = c.dead = true;
        spawnExplosion((a.x + c.x) / 2, (a.y + c.y) / 2, "#ffffff");
        break;
      }
    }
  }
  G.bullets = G.bullets.filter(b => !b.dead);
}

function updatePowerups() {
  for (let i = G.powerups.length - 1; i >= 0; i--) {
    const p = G.powerups[i];
    p.t++;
    for (const t of G.tanks) {
      if (t.isEnemy) continue;
      if (rectHits(p.x, p.y, 34, 34, t.x, t.y, t.size, t.size)) {
        applyPowerup(t.playerIndex, p);
        G.powerups.splice(i, 1);
        break;
      }
    }
  }
}

function updateParticles() {
  for (let i = G.particles.length - 1; i >= 0; i--) {
    const p = G.particles[i];
    p.life--;
    p.x += p.vx; p.y += p.vy;
    p.vx *= 0.94; p.vy *= 0.94;
    if (p.life <= 0) G.particles.splice(i, 1);
  }
}

/* ---------------- 阶段 / 结束判定 ---------------- */
function checkStageClear() {
  if (G.state !== "playing") return;
  if (G.enemiesLeft <= 0 && G.enemyQueue.length === 0 && G.tanks.every(t => !t.isEnemy)) {
    G.endKind = G.stage >= MAPS.length ? "victory" : "clear";
    G.endTimer = 110;
    G.state = "ending";
    Sound.stageClear();
  }
}
function checkGameOver() {
  if (G.baseDown) return;
  if (G.mode === 1) {
    if (G.profiles[0].lives <= 0 && !G.tanks.some(t => !t.isEnemy && t.playerIndex === 0)) {
      G.endKind = "over";
      G.endReason = "玩家 1 生命耗尽";
      G.endTimer = 110;
      G.state = "ending";
      Sound.gameOver();
    }
  } else {
    if (G.profiles[0].lives <= 0 && G.profiles[1].lives <= 0 &&
        !G.tanks.some(t => !t.isEnemy)) {
      G.endKind = "over";
      G.endReason = "所有玩家生命耗尽";
      G.endTimer = 110;
      G.state = "ending";
      Sound.gameOver();
    }
  }
}

function finalizeEnd() {
  if (G.endKind === "clear") {
    G.state = "stageclear";
    showEnd("stageclear");
  } else if (G.endKind === "victory") {
    G.state = "victory";
    showEnd("victory");
  } else {
    G.state = "gameover";
    showEnd("gameover");
  }
}

/* ---------------- 渲染 ---------------- */
function render() {
  ctx.fillStyle = "#0a0f1c";
  ctx.fillRect(0, 0, W, H);

  // 地面 / 障碍
  for (let y = 0; y < ROWS; y++) {
    for (let x = 0; x < COLS; x++) {
      const t = G.grid ? G.grid[y][x] : EMPTY;
      if (t !== TREE) drawTile(t, x, y);
    }
  }

  // 道具
  for (const p of G.powerups) drawPowerup(p);

  // 坦克
  for (const t of G.tanks) drawTank(t);

  // 树（覆盖坦克）
  if (G.grid) {
    for (let y = 0; y < ROWS; y++)
      for (let x = 0; x < COLS; x++)
        if (G.grid[y][x] === TREE) drawTile(TREE, x, y);
  }

  // 子弹
  for (const b of G.bullets) drawBullet(b);

  // 破坏死光
  for (const b of G.beams) drawBeam(b);

  // 粒子
  for (const p of G.particles) {
    const a = Math.max(0, p.life / 60);
    ctx.globalAlpha = p.ring ? a : Math.min(1, a * 1.4);
    ctx.fillStyle = p.color;
    if (p.ring) {
      ctx.beginPath();
      ctx.arc(p.x, p.y, (1 - a) * 30 + 4, 0, Math.PI * 2);
      ctx.fill();
    } else {
      ctx.fillRect(p.x - p.size / 2, p.y - p.size / 2, p.size, p.size);
    }
  }
  ctx.globalAlpha = 1;

  // 冰冻提示
  if (G.freezeTimer > 0 && G.state === "playing") {
    ctx.fillStyle = "rgba(140,220,255,0.12)";
    ctx.fillRect(0, 0, W, H);
  }
}

function drawTile(t, x, y) {
  const px = x * TILE, py = y * TILE;
  if (t === EMPTY) {
    ctx.fillStyle = "#0d1424";
    ctx.fillRect(px, py, TILE, TILE);
    ctx.strokeStyle = "rgba(120,160,255,0.04)";
    ctx.lineWidth = 1;
    ctx.strokeRect(px + 0.5, py + 0.5, TILE, TILE);
  } else if (t === BRICK) drawBrick(px, py);
  else if (t === HARDBRICK) drawHardBrick(px, py);
  else if (t === STEEL) drawSteel(px, py);
  else if (t === WATER) drawWater(px, py);
  else if (t === ICE) drawIce(px, py);
  else if (t === TREE) drawTree(px, py);
  else if (t === BURN) drawBurn(px, py);
  else if (t === BASE) drawBase(px, py);
}

function drawBrick(x, y) {
  // 干净的完整方块，无杂边
  ctx.fillStyle = "#b5482c";
  ctx.fillRect(x, y, TILE, TILE);
  ctx.fillStyle = "rgba(255,255,255,0.16)";
  ctx.fillRect(x, y, TILE, 3);
  ctx.fillRect(x, y, 3, TILE);
  ctx.fillStyle = "rgba(0,0,0,0.20)";
  ctx.fillRect(x, y + TILE - 3, TILE, 3);
  ctx.fillRect(x + TILE - 3, y, 3, TILE);
}

function drawHardBrick(x, y) {
  // 强化砖块：比普通砖块更深，同样是无杂边的完整方块
  ctx.fillStyle = "#7a2e1a";
  ctx.fillRect(x, y, TILE, TILE);
  ctx.fillStyle = "rgba(255,255,255,0.12)";
  ctx.fillRect(x, y, TILE, 3);
  ctx.fillRect(x, y, 3, TILE);
  ctx.fillStyle = "rgba(0,0,0,0.24)";
  ctx.fillRect(x, y + TILE - 3, TILE, 3);
  ctx.fillRect(x + TILE - 3, y, 3, TILE);
  // 中心更深的标记，便于区分强化砖
  ctx.fillStyle = "rgba(0,0,0,0.14)";
  ctx.fillRect(x + 10, y + 10, TILE - 20, TILE - 20);
}

function drawSteel(x, y) {
  ctx.fillStyle = "#8f9bb0";
  ctx.fillRect(x, y, TILE, TILE);
  ctx.fillStyle = "#e6ecf6";
  ctx.fillRect(x, y, TILE, 3);
  ctx.fillRect(x, y, 3, TILE);
  ctx.fillStyle = "#55627a";
  ctx.fillRect(x, y + TILE - 3, TILE, 3);
  ctx.fillRect(x + TILE - 3, y, 3, TILE);
  ctx.fillStyle = "#b8c2d4";
  ctx.fillRect(x + 12, y + 12, TILE - 24, TILE - 24);
}

function drawWater(x, y) {
  ctx.fillStyle = "#123f8f";
  ctx.fillRect(x, y, TILE, TILE);
  const phase = (G.nowMs * 0.02) % TILE;
  ctx.fillStyle = "#2a6cd4";
  for (let i = 0; i < 3; i++) {
    const yy = y + ((i * TILE / 3 + phase) % TILE);
    ctx.fillRect(x, yy, TILE, 4);
  }
  ctx.fillStyle = "rgba(150,210,255,0.55)";
  const wave = Math.sin(G.nowMs * 0.005 + x * 0.6) * 5;
  ctx.fillRect(x, y + TILE / 2 + wave, TILE, 2);
}

function drawIce(x, y) {
  ctx.fillStyle = "#aee2ff";
  ctx.fillRect(x, y, TILE, TILE);
  ctx.fillStyle = "rgba(255,255,255,0.55)";
  ctx.fillRect(x + 2, y + 2, TILE - 4, 3);
  ctx.strokeStyle = "rgba(255,255,255,0.5)";
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(x + 5, y + TILE - 5);
  ctx.lineTo(x + TILE - 5, y + 5);
  ctx.stroke();
}

function drawTree(x, y) {
  const cx = x + TILE / 2, cy = y + TILE / 2;
  const sway = Math.sin(G.nowMs * 0.0012 + x * 0.1) * 1.5;
  ctx.fillStyle = "#123a1c";
  ctx.beginPath(); ctx.arc(cx, cy + 5, TILE / 2 - 2, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#1f7a3d";
  ctx.beginPath(); ctx.arc(cx - 6 + sway, cy - 2, TILE / 3, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#2c9c50";
  ctx.beginPath(); ctx.arc(cx + 6 - sway, cy - 4, TILE / 3, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#3fbf63";
  ctx.beginPath(); ctx.arc(cx + sway, cy - 8, TILE / 3, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "rgba(255,255,255,0.18)";
  ctx.beginPath(); ctx.arc(cx - 3 + sway, cy - 11, 4, 0, Math.PI * 2); ctx.fill();
}

function drawBurn(x, y) {
  // 灼烧地面：焦黑底 + 余烬火星
  ctx.fillStyle = "#241a10";
  ctx.fillRect(x, y, TILE, TILE);
  ctx.fillStyle = "#372a18";
  ctx.fillRect(x, y, TILE, TILE);
  for (let i = 0; i < 5; i++) {
    const seed = x * 13 + y * 7 + i * 31;
    const fx = x + ((seed * 17 + i * 11) % TILE);
    const fy = y + ((seed * 29 + Math.floor(G.nowMs * 0.02)) % TILE);
    const glow = 0.45 + Math.abs(Math.sin(G.nowMs * 0.03 + seed)) * 0.55;
    ctx.fillStyle = "rgba(255,140,60," + glow + ")";
    ctx.beginPath();
    ctx.arc(fx, fy, 2 + (seed % 3), 0, Math.PI * 2);
    ctx.fill();
  }
}

function drawBase(x, y) {
  const s = TILE;
  const cx = x + s / 2, cy = y + s / 2;
  // 底座光晕
  const glow = ctx.createRadialGradient(cx, cy, 2, cx, cy, s * 0.7);
  glow.addColorStop(0, "rgba(255,210,74,0.35)");
  glow.addColorStop(1, "rgba(255,210,74,0)");
  ctx.fillStyle = glow;
  ctx.fillRect(x, y, s, s);
  // 鹰（简化像素风）
  ctx.fillStyle = "#6b4326";
  ctx.beginPath(); ctx.moveTo(cx - s * 0.18, cy - s * 0.05); ctx.lineTo(cx - s * 0.42, cy - s * 0.30); ctx.lineTo(cx - s * 0.28, cy + s * 0.05); ctx.closePath(); ctx.fill();
  ctx.beginPath(); ctx.moveTo(cx + s * 0.20, cy - s * 0.02); ctx.lineTo(cx + s * 0.40, cy - s * 0.28); ctx.lineTo(cx + s * 0.42, cy + s * 0.10); ctx.closePath(); ctx.fill();
  ctx.fillStyle = "#7d4f28";
  ctx.beginPath(); ctx.ellipse(cx, cy + s * 0.05, s * 0.24, s * 0.24, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#f2ede2";
  ctx.beginPath(); ctx.ellipse(cx + s * 0.26, cy + s * 0.02, s * 0.10, s * 0.08, -0.4, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#f4f0e6";
  ctx.beginPath(); ctx.arc(cx - s * 0.12, cy - s * 0.14, s * 0.15, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#f2c14e";
  ctx.beginPath(); ctx.moveTo(cx - s * 0.24, cy - s * 0.20); ctx.lineTo(cx - s * 0.40, cy - s * 0.13); ctx.lineTo(cx - s * 0.22, cy - s * 0.10); ctx.closePath(); ctx.fill();
  ctx.fillStyle = "#1a1a1a";
  ctx.beginPath(); ctx.arc(cx - s * 0.14, cy - s * 0.16, s * 0.025, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#f2c14e";
  ctx.fillRect(cx - s * 0.16, cy + s * 0.26, s * 0.08, s * 0.06);
  ctx.fillRect(cx + s * 0.02, cy + s * 0.26, s * 0.08, s * 0.06);

  // 基地生命值显示（小鸟上方）
  {
    const pipW = 9, pipH = 7, gap = 3;
    const totalW = pipW * 3 + gap * 2;
    const sx = cx - totalW / 2, sy = y + 2;
    for (let i = 0; i < 3; i++) {
      const px = sx + i * (pipW + gap);
      if (i < G.baseHp) {
        ctx.fillStyle = "#ffd24a";
        ctx.fillRect(px, sy, pipW, pipH);
        ctx.fillStyle = "rgba(255,255,255,0.45)";
        ctx.fillRect(px, sy, pipW, 2);
      } else {
        ctx.fillStyle = "rgba(255,255,255,0.14)";
        ctx.fillRect(px, sy, pipW, pipH);
      }
    }
  }

  // 受击闪烁 / 破损裂纹
  if (G.baseFlash > 0) {
    ctx.fillStyle = "rgba(255,80,80," + (G.baseFlash / 20 * 0.5) + ")";
    ctx.fillRect(x, y, s, s);
  }
  if (G.baseHp <= 2) drawBaseCracks(x, y, s, G.baseHp);
}

function drawBaseCracks(x, y, s, hp) {
  ctx.strokeStyle = "rgba(20,20,20,0.7)";
  ctx.lineWidth = 2;
  ctx.lineCap = "round";
  ctx.beginPath();
  if (hp <= 2) {
    ctx.moveTo(x + s * 0.30, y + s * 0.14);
    ctx.lineTo(x + s * 0.50, y + s * 0.40);
    ctx.lineTo(x + s * 0.34, y + s * 0.62);
  }
  if (hp <= 1) {
    ctx.moveTo(x + s * 0.58, y + s * 0.20);
    ctx.lineTo(x + s * 0.76, y + s * 0.46);
    ctx.lineTo(x + s * 0.56, y + s * 0.66);
  }
  ctx.stroke();
}

function drawTrack(x) {
  ctx.fillStyle = "#1e2530";
  ctx.fillRect(x, -14, 9, 28);
  ctx.fillStyle = "#3a4657";
  for (let i = -12; i < 14; i += 6) ctx.fillRect(x, i, 9, 3);
}

function drawTank(t) {
  if (!t.alive) return;
  if (t.isEnemy && t.spawnTimer > 0) { drawSpawnStar(t); return; }
  const cx = t.x + t.size / 2, cy = t.y + t.size / 2;

  let alpha = 1;
  if (t.spawnTimer > 0 || t.shield > 0) alpha = 0.55 + Math.abs(Math.sin(G.nowMs * 0.02)) * 0.45;
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.translate(cx, cy);
  ctx.rotate(t.dir * Math.PI / 2);

  ctx.fillStyle = "rgba(0,0,0,0.35)";
  ctx.fillRect(-18, -12, 36, 26);
  drawTrack(-18); drawTrack(9);

  ctx.fillStyle = t.body;
  ctx.fillRect(-10, -12, 20, 24);
  ctx.fillStyle = t.light;
  ctx.fillRect(-10, -12, 20, 5);
  ctx.fillStyle = t.body;
  ctx.fillRect(-7, -16, 14, 8);
  ctx.fillStyle = t.light;
  ctx.fillRect(-7, -16, 14, 3);
  ctx.fillStyle = "#7c8899";
  ctx.fillRect(-2, -20, 4, 6);
  ctx.restore();

  if (t.flashTimer > 0) {
    ctx.save();
    ctx.globalAlpha = 0.5;
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(t.x, t.y, t.size, t.size);
    ctx.restore();
  }
  // 护盾光环
  if (t.shield > 0) {
    ctx.save();
    ctx.strokeStyle = "rgba(120,220,255,0.8)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(cx, cy, t.size / 2 + 4 + Math.sin(G.nowMs * 0.01) * 2, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  }

  // 玩家编号标签
  if (!t.isEnemy) {
    ctx.save();
    ctx.fillStyle = "rgba(255,255,255,0.92)";
    ctx.font = "bold 11px Orbitron, 'Noto Sans SC', sans-serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText((t.playerIndex + 1) + "P", cx, cy + 1);
    ctx.restore();
  }

  // 绝招蓄力效果（0.5 秒后才显示蓄力条）
  if (t.charging && t.chargeT >= CHARGE_BAR_DELAY) {
    const p = Math.min(1, t.chargeT / CHARGE_FRAMES);
    const col = t.chargeReady ? "#ff5d6c" : (t.playerIndex === 0 ? "#ffd24a" : "#3ee08c");
    ctx.save();
    // 光环
    ctx.globalAlpha = 0.4 + p * 0.5;
    ctx.strokeStyle = col;
    ctx.lineWidth = 2 + p * 2;
    ctx.beginPath();
    ctx.arc(cx, cy, t.size / 2 + 6 + p * 12 + Math.sin(G.nowMs * 0.05) * 2, 0, Math.PI * 2);
    ctx.stroke();
    ctx.globalAlpha = 1;
    // 蓄力进度条
    const bw = t.size, bh = 4;
    const bx = t.x, by = t.y - 12;
    ctx.fillStyle = "rgba(0,0,0,0.55)";
    ctx.fillRect(bx, by, bw, bh);
    ctx.fillStyle = col;
    ctx.fillRect(bx, by, bw * p, bh);
    ctx.strokeStyle = "rgba(255,255,255,0.35)";
    ctx.lineWidth = 1;
    ctx.strokeRect(bx + 0.5, by + 0.5, bw - 1, bh - 1);
    ctx.restore();
  }
}

function drawSpawnStar(t) {
  const cx = t.x + t.size / 2, cy = t.y + t.size / 2;
  const phase = (G.nowMs * 0.006) % 1;
  const r = 10 + Math.sin(phase * Math.PI) * 6;
  ctx.save();
  ctx.translate(cx, cy);
  ctx.rotate(G.nowMs * 0.003);
  ctx.fillStyle = "#ffd24a";
  ctx.beginPath();
  ctx.moveTo(0, -r); ctx.lineTo(r * 0.4, -r * 0.4); ctx.lineTo(r, 0);
  ctx.lineTo(r * 0.4, r * 0.4); ctx.lineTo(0, r); ctx.lineTo(-r * 0.4, r * 0.4);
  ctx.lineTo(-r, 0); ctx.lineTo(-r * 0.4, -r * 0.4);
  ctx.closePath();
  ctx.fill();
  ctx.restore();
}

function drawBullet(b) {
  const cx = b.x + b.w / 2, cy = b.y + b.h / 2;
  ctx.save();
  ctx.translate(cx, cy);
  ctx.rotate(b.dir * Math.PI / 2);
  const glow = ctx.createRadialGradient(0, 0, 0, 0, 0, 9);
  if (b.fromPlayer) { glow.addColorStop(0, "rgba(255,255,255,0.9)"); glow.addColorStop(1, "rgba(255,210,74,0)"); }
  else { glow.addColorStop(0, "rgba(255,120,120,0.9)"); glow.addColorStop(1, "rgba(255,60,60,0)"); }
  ctx.fillStyle = glow;
  ctx.fillRect(-9, -9, 18, 18);
  ctx.fillStyle = b.fromPlayer ? "#fff6c9" : "#ff7b7b";
  ctx.fillRect(-2, -4, 4, 8);
  ctx.fillStyle = b.fromPlayer ? "#ffd24a" : "#ff2f3f";
  ctx.fillRect(-1, -4, 2, 8);
  ctx.restore();
}

function drawBeam(b) {
  const a = Math.max(0, b.life / 22);
  if (a <= 0) return;
  const x0 = Math.min(b.x0, b.x1), y0 = Math.min(b.y0, b.y1);
  const w = Math.abs(b.x1 - b.x0), h = Math.abs(b.y1 - b.y0);
  const col = b.ownerPi === 0 ? "255,210,74" : "62,224,140";
  ctx.save();
  ctx.globalAlpha = a;
  // 外光晕
  ctx.fillStyle = "rgba(" + col + ",0.28)";
  ctx.fillRect(x0 - 5, y0 - 5, w + 10, h + 10);
  // 主体
  ctx.fillStyle = "rgba(" + col + ",0.6)";
  ctx.fillRect(x0, y0, w, h);
  // 明亮核心
  ctx.fillStyle = "rgba(255,255,255,0.9)";
  if (b.dir === UP || b.dir === DOWN) {
    const cw = Math.max(4, w * 0.22);
    ctx.fillRect(x0 + w / 2 - cw / 2, y0, cw, h);
  } else {
    const ch = Math.max(4, h * 0.22);
    ctx.fillRect(x0, y0 + h / 2 - ch / 2, w, ch);
  }
  ctx.restore();
}

const POWERUP_COLORS = {
  star: "#ffd24a", grenade: "#ff8a5c", tank: "#3ee08c",
  shovel: "#8f9bb0", shield: "#5b8cff", clock: "#c26bff",
};
const POWERUP_GLYPHS = {
  star: "★", grenade: "💥", tank: "🛡", shovel: "🧱", shield: "⛨", clock: "❄",
};
function drawPowerup(p) {
  const cx = p.x + 17, cy = p.y + 17;
  const pulse = 1 + Math.sin(G.nowMs * 0.006 + p.t) * 0.08;
  ctx.save();
  ctx.translate(cx, cy);
  ctx.scale(pulse, pulse);
  const glow = ctx.createRadialGradient(0, 0, 2, 0, 0, 20);
  glow.addColorStop(0, POWERUP_COLORS[p.type] + "cc");
  glow.addColorStop(1, POWERUP_COLORS[p.type] + "00");
  ctx.fillStyle = glow;
  ctx.fillRect(-20, -20, 40, 40);
  ctx.fillStyle = "rgba(10,14,26,0.85)";
  ctx.fillRect(-13, -13, 26, 26);
  ctx.strokeStyle = POWERUP_COLORS[p.type];
  ctx.lineWidth = 2;
  ctx.strokeRect(-13, -13, 26, 26);
  ctx.fillStyle = POWERUP_COLORS[p.type];
  ctx.font = "16px sans-serif";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(POWERUP_GLYPHS[p.type], 0, 1);
  ctx.restore();
}

/* ---------------- HUD ---------------- */
const $ = id => document.getElementById(id);
let lastEnemyCount = -1, lastP1Score = -1, lastP2Score = -1, lastLives = "", lastPower = "", lastUlt = "", lastStage = -1, lastBase = -1;

function updateHUD() {
  const p1 = G.profiles[0], p2 = G.profiles[1];
  if (G.stage !== lastStage) { $("hudStage").textContent = G.stage; lastStage = G.stage; }
  if (p1.score !== lastP1Score) { $("p1Score").textContent = p1.score; lastP1Score = p1.score; }
  if (p2.score !== lastP2Score) { $("p2Score").textContent = p2.score; lastP2Score = p2.score; }
  if (G.baseHp !== lastBase) { $("hudBase").innerHTML = baseIcons(G.baseHp); lastBase = G.baseHp; }
  if (G.enemiesLeft !== lastEnemyCount) {
    const el = $("hudEnemies");
    el.innerHTML = "";
    for (let i = 0; i < G.enemiesLeft; i++) { const s = document.createElement("i"); el.appendChild(s); }
    lastEnemyCount = G.enemiesLeft;
  }
  const livesKey = p1.lives + "-" + p2.lives + "-" + G.mode;
  if (livesKey !== lastLives) {
    $("p1Lives").innerHTML = livesIcons(p1.lives);
    $("p2Lives").innerHTML = livesIcons(p2.lives);
    lastLives = livesKey;
  }
  const powerKey = p1.power + "-" + p2.power;
  if (powerKey !== lastPower) {
    $("p1Power").innerHTML = powerIcons(p1.power);
    $("p2Power").innerHTML = powerIcons(p2.power);
    lastPower = powerKey;
  }
  const p1t = G.tanks.find(t => !t.isEnemy && t.playerIndex === 0);
  const p2t = G.tanks.find(t => !t.isEnemy && t.playerIndex === 1);
  const ultKey = (p1t ? p1t.ultCharges : 0) + "-" + (p2t ? p2t.ultCharges : 0);
  if (ultKey !== lastUlt) {
    $("p1Ult").innerHTML = ultIcons(p1t ? p1t.ultCharges : 0);
    $("p2Ult").innerHTML = ultIcons(p2t ? p2t.ultCharges : 0);
    lastUlt = ultKey;
  }
}

function baseIcons(n) {
  let s = "";
  for (let i = 0; i < 3; i++) s += "<i" + (i < n ? "" : " class='dead'") + "></i>";
  return s;
}
function livesIcons(n) {
  let s = "";
  const shown = Math.min(n, 8);
  for (let i = 0; i < shown; i++) s += "<i></i>";
  if (n > 8) s += "<span class='lives-more'>+" + (n - 8) + "</span>";
  return s;
}
function powerIcons(n) {
  let s = "";
  for (let i = 0; i < 3; i++) s += "<i" + (i < n ? " class='on'" : "") + "></i>";
  return s;
}
function ultIcons(n) {
  let s = "";
  for (let i = 0; i < 3; i++) s += "<i" + (i < n ? " class='on'" : "") + "></i>";
  return s;
}

/* ---------------- 界面切换 ---------------- */
function showOverlay(id) { $(id).classList.remove("hidden"); }
function hideOverlays() {
  ["overlayStart", "overlayPause", "overlayEnd"].forEach(id => $(id).classList.add("hidden"));
}
function showEnd(kind) {
  const isClear = kind === "stageclear";
  const isVictory = kind === "victory";
  $("endTitle").textContent = isVictory ? "🎉 通关！" : (isClear ? "阶段完成" : "游戏结束");
  $("endSub").textContent = isVictory ? "恭喜通关全部关卡" : (isClear ? `第 ${G.stage} 关通关` : G.endReason);
  $("endStage").textContent = G.stage;
  $("endP1").textContent = G.profiles[0].score;
  $("endP2").textContent = G.profiles[1].score;
  $("btnNext").textContent = isClear ? "下一关 ▶" : "再来一局 ↻";
  // 通关数据统计
  const vs = $("victoryStats");
  if (vs) {
    vs.classList.toggle("hidden", !isVictory);
    if (isVictory) {
      $("vKills").textContent = G.stats.kills;
      $("vUlts").textContent = G.stats.ultUses;
      $("vShots").textContent = G.stats.shots;
      $("vScore").textContent = G.profiles[0].score + G.profiles[1].score;
    }
  }
  showOverlay("overlayEnd");
}

function togglePause() {
  if (G.state === "playing") { G.state = "paused"; showOverlay("overlayPause"); Sound.pause(); }
  else if (G.state === "paused") { G.state = "playing"; hideOverlays(); }
}
function toggleSound() {
  Sound.enabled = !Sound.enabled;
  $("btnSound").textContent = Sound.enabled ? "🔊" : "🔇";
  if (Sound.enabled) Sound.resume();
}

function startGame(mode) {
  G.mode = mode;
  G.profiles = [{ lives: 3, score: 0, power: 0 }, { lives: 3, score: 0, power: 0 }];
  G.stats = { kills: 0, ultUses: 0, shots: 0 };
  G.stage = 1;
  startStage();
  hideOverlays();
  $("panelP2").classList.toggle("off", mode === 1);
  G.state = "playing";
  Sound.resume();
}
function startStage() {
  G.grid = loadMap(G.stage);
  G.enemiesLeft = TOTAL_ENEMIES;
  G.enemyQueue = buildEnemyQueue(G.stage);
  G.enemiesOnField = 0;
  G.spawnCount = 0;
  G.spawnTimer = 60;
  G.bullets = [];
  G.powerups = [];
  G.particles = [];
  G.beams = [];
  G.burnTiles = [];
  G.respawns = [];
  G.tanks = [];
  G.baseDown = false;
  G.baseHp = 3;
  G.baseFlash = 0;
  G.baseWallTimer = 0;
  G.freezeTimer = 0;
  G.profiles[0].power = 0;
  G.profiles[1].power = 0;
  G.profiles[0].lives = 3;
  G.profiles[1].lives = 3;
  for (let i = 0; i < G.mode; i++) {
    if (G.profiles[i].lives > 0) G.tanks.push(createPlayerTank(i));
  }
  updateHUD();
}
function nextOrRestart() {
  if (G.endKind === "clear") {
    G.stage++;
    startStage();
    hideOverlays();
    G.state = "playing";
  } else {
    startGame(G.mode);
  }
}

/* ---------------- 事件绑定 ---------------- */
document.querySelectorAll(".btn-mode").forEach(btn => {
  btn.addEventListener("click", () => startGame(parseInt(btn.dataset.mode, 10)));
});
$("btnResume").addEventListener("click", () => { if (G.state === "paused") togglePause(); });
$("btnRestartPause").addEventListener("click", () => startGame(G.mode));
$("btnNext").addEventListener("click", nextOrRestart);
$("btnHome").addEventListener("click", () => { G.state = "menu"; showOverlay("overlayStart"); hideOverlays(); });
$("btnPause").addEventListener("click", togglePause);
$("btnSound").addEventListener("click", toggleSound);

// 触屏虚拟按键
function bindTouch(btn, vkey) {
  const on = e => { e.preventDefault(); pressVKey(vkey); };
  const off = e => { e.preventDefault(); releaseVKey(vkey); };
  btn.addEventListener("pointerdown", on);
  btn.addEventListener("pointerup", off);
  btn.addEventListener("pointerleave", off);
  btn.addEventListener("pointercancel", off);
}
const touchDirs = { "up": "ArrowUp", "down": "ArrowDown", "left": "ArrowLeft", "right": "ArrowRight" };
document.querySelectorAll(".dpad button").forEach(b => bindTouch(b, touchDirs[b.dataset.dir]));
const fireBtn = document.getElementById("touchFire");
if (fireBtn) bindTouch(fireBtn, "vFire");

/* ---------------- 启动 ---------------- */
showOverlay("overlayStart");
requestAnimationFrame(loop);
})();

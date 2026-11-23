# CODELY.md — 项目指令上下文

> 本文件是 Codely CLI 的项目指令上下文，每次会话自动加载，供后续交互遵循。请随项目演进持续维护更新。

## 项目概览

**坦克大战（FC 经典重制）— 两个并存的实现**

| 实现 | 位置 | 说明 |
|------|------|------|
| 网页版 | `html5/` 目录（`index.html` / `style.css` / `game.js`） | 原生 HTML + CSS + Canvas 2D，零依赖零构建，2026-11-18 由 `/Users/protoss/Downloads/tank-battle` 原样复刻（逐字节一致） |
| Unity 版 | `Assets/` + `Packages/` + `ProjectSettings/` | 原生 Unity 2022.3 LTS 工程，C# 复刻同一套玩法，**3D 正交俯视渲染**：结构物为 3D 模型（团结 AI 生成，缺失时程序化几何体兜底），平面贴图/音效运行时程序化生成（2026-11-20 由 2D 像素版改造为 3D） |

> 两版玩法参数仍完全一致（见上）；视觉呈现不同：Unity 版为 3D 正交俯视，网页版保持 2D Canvas 像素风，视觉层互不要求同步。

两者玩法参数一致：13×13 网格、48px 格、40px 坦克、每关 20 辆敌军、场上最多 4 辆、基地 3 点耐久、按住发射 2 秒蓄力破坏死光（每辆 3 次，清 3 格宽路径并把草地变 45 秒灼烧区）。

### Unity 工程目录

| 路径 | 说明 |
|------|------|
| `Assets/Scripts/Conf.cs` | 全局常量、地形/方向枚举、调色板、`Conf.Round`（对齐 JS 的 Math.round） |
| `Assets/Scripts/Maps.cs` | 5 关地图字符串（每关 12 行，第 13 行程序生成基地与保护墙）、敌军权重表、装甲配色 |
| `Assets/Scripts/Model.cs` | 数据模型：`Game`（对应 JS 的 G）、`Tank`、`Bullet`、`Powerup`、`Particle`、`Beam`、`BurnTile`、`Respawn` |
| `Assets/Scripts/GameController.cs` | 入口 MonoBehaviour：主循环累加器、键鼠/触屏输入、暂停/静音/关卡流转 |
| `Assets/Scripts/GameRules.cs` | `partial GameController`：`Step()` 与全部规则（移动、射击、命中、AI、道具、死光、灼烧、结算） |
| `Assets/Scripts/PixelPainter.cs` | 画布坐标（中心原点、y 向下）像素画笔：矩形、圆、椭圆、线、扫描线多边形、径向辉光 |
| `Assets/Scripts/SpriteFactory.cs` | 运行时生成全部贴图：地形、坦克（含 1P/2P 像素字标）、子弹、道具图标、星形、护盾环 |
| `Assets/Scripts/SceneView.cs` | 3D 渲染层：正交俯视相机与方向光、XZ 战场、地形槽位对象池（`tileCode` 脏检查）、坦克视图（Y 轴朝向/出生星/护盾/蓄力条/受击发光）、子弹/道具/粒子/死光平面贴片对象池 |
| `Assets/Scripts/ModelRegistry.cs` | 运行时模型装载：`Resources.Load("Models/<name>")` 懒加载，找不到返回 null 走程序化兜底 |
| `Assets/Resources/Models/` | 3D 模型 prefab 落点（当前仅 `tree_canopy.prefab` + `tree_canopy_mat.mat`），后补 prefab 无需改代码 |
| `Assets/Scripts/GameUI.cs` | UGUI：顶栏 HUD、双玩家面板、开始/暂停/结算弹窗、触屏按键、相机取景 |
| `Assets/Scripts/AudioSynth.cs` | 采样合成音效（替代 Web Audio），带缓存 |
| `Assets/Scripts/Bootstrap.cs` | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 入口，场景可为空 |
| `Assets/Editor/SceneSetup.cs` | 首次打开工程自动生成 `Assets/Scenes/Main.unity` 并加入 Build Settings；菜单 `Tank Battle/创建主场景` |
| `Packages/manifest.json` | 依赖：`com.unity.ugui` 及各基础模块 |
| `ProjectSettings/ProjectVersion.txt` | `2022.3.62f3c1`（Unity 中国版 2022.3.62，与本机已装编辑器一致；装全球版或其它补丁会提示版本不一致） |

## 构建和运行

### 网页版

```bash
cd html5
python3 -m http.server 8765     # 浏览器打开 http://127.0.0.1:8765/
# 或直接：open index.html（在 html5/ 目录下）
node --check game.js            # JS 语法校验（在 html5/ 目录下）
```

### Unity 版

1. Unity Hub → `Add` → 选择本目录（工程根目录，不是 Assets）→ 用 **Unity 2022.3（本机为中国版 2022.3.62f3c1）** 打开
2. 首次打开时 `Assets/Editor/SceneSetup.cs` 会自动生成 `Assets/Scenes/Main.unity`（空场景）并加入 Build Settings；若未生成，执行菜单 **Tank Battle → 创建主场景**
3. 打开该场景后按 **Play**：`Bootstrap` 自动创建相机、战场与 UI
4. 打包：File → Build Settings，场景已在列表中，选择目标平台 Build
5. 独立版窗口模式运行：窗口默认为**显示器原生分辨率的 2/3**（保持宽高比、不铺满屏幕），`GameController.ApplyDisplayFit` 用 `Screen.SetResolution`（实测按物理像素生效）设置；窗口可拖拽缩放

> 场景保持为空是刻意设计：所有对象由 `Bootstrap` 在运行时搭建，因此没有 prefab / 场景引用 / GUID 依赖，工程可以纯文本管理。

### 操作

- P1：`WASD` 移动、`空格` 射击；单人模式兼容方向键
- P2：方向键移动、`Enter` 射击
- `P` / `Esc` 暂停，`M` 静音
- 按住发射键 2 秒蓄力 → 松开释放破坏死光（未满则普通射击）
- 触屏设备自动显示虚拟方向键与发射键

## 开发约定

- **Unity 侧**：C# 9（Unity 2022.3），无第三方包；逻辑坐标系沿用网页版像素值（624×624，1 单位 = 48px），仅在 `SceneView.ToWorld` 换算世界坐标
- **3D 模型资产**：结构物（坦克/墙/基地/树冠）优先走 `Assets/Resources/Models/` 下团结 AI 生成的 prefab（`ModelRegistry.Prefab` 加载）；缺失时由 `SceneView` 用基础几何体程序化拼装兜底。当前 AI 模型只有 `tree_canopy`，其余为程序化兜底（模型前向约定 +Z、足印按 40px 坦克/48px 格归一化，Y 轴 yaw = 90×Dir）
- **平面贴图**：地面/水面/冰/灼烧/子弹/道具/粒子/UI 贴图仍全部经 `SpriteFactory` + `PixelPainter` 运行时生成，不要为它们引入图片文件
- **独立版 Shader 包含**：运行期 `Shader.Find` 创建材质时 shader 必须已被打进包——本工程在 `Assets/Resources/ShaderKeep/` 下放了引用各 shader（Standard / Unlit/Texture / Unlit/Transparent / Sprites/Default）的保包材质（Resources 目录必定进包）；新增运行期使用的 shader 时要同步加一个保包材质，否则编辑器里正常、独立版会因 `new Material(null)` 启动崩溃
- **实体建模**：普通 C# 类 + 列表（对应 JS 的对象字面量 + 数组），不用 MonoBehaviour 承载游戏实体；渲染由 `SceneView` 按列表同步对象池
- **高度分层（取代 sortingOrder）**：正俯视下透明物按 Y 高度排序——地面 0、水 .015、冰 .02、灼烧 .03、描边 .06、道具 .42、出生星 .52、护盾 .54、蓄力条 .56、子弹 .7（须越过树冠）、死光 .75~.83、粒子 .9；墙/坦克/基地/树冠为实体高度（0~0.7）。新增平面层时按其旧 2D 层序映射 Y 值
- **音效**：所有音效经 `AudioSynth` 采样合成并缓存，不要放 AudioClip 资源；背景音乐同样由 `AudioSynth.BgmClip()` 程序化合成（红白机坦克大战风格，168 BPM、8 小节 ≈11.4s 循环），由 `GameController` 专用 AudioSource 播放——**仅在开局（StartStage）后响起、每关从头播**，暂停暂停、静音联动，结算（FinalizeEnd）与返回主菜单（GoHome）时停止；原曲受版权保护，若用户提供音频文件可直接替换（素材库/AI 生成需积分）
- **UI**：全部 UGUI 运行期构建，HUD 用脏值缓存（`lastStage/lastP1Score/...`）避免每帧写 Text
- **网页版**：整体 IIFE + `"use strict"`，中文分节注释，实体为普通对象，固定 60FPS 步进

### 校验

- 网页版：`node --check html5/game.js`，浏览器实际开局验证
- Unity 版：2026-11-20 已在 Unity 中国版 2022.3.62f3c1 中实际编译（零错误零警告）并通过 Play 模式试玩验证（相机/光照/地形池/坦克朝向与染色/基地耐久/贴片/死光）。改动后仍以 `unity_editor.refresh` 的 Console 错误为准；3D 改版录屏见 `screenshots/tank3d_pass2.mp4`
- Windows 独立版：`BuildPipeline.BuildPlayer`（StandaloneWindows64）打包到 `Builds/Windows/`（已在 .gitignore）；打包后启动 exe 检查 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\tank-battle\Player.log` 无异常

## 已知限制与待办

- 生成 AI 模型需要积分（Rodin 约 165 积分/个，当前账号余额不足）：玩家坦克、敌方坦克、砖墙、钢墙、基地目前为程序化几何体兜底；充值后把 AI prefab 放到 `Assets/Resources/Models/`（命名 `tank_player` / `tank_enemy` / `wall_brick` / `wall_steel` / `base_hq`）即可自动替换，无需改代码
- 树冠 AI 模型的 FBX 未内嵌贴图，已手动下载 diffuse/normal 并挂到 `Assets/Resources/Models/tree_canopy_mat.mat`
- 本机没有 dotnet/mono，无法在编辑器之外单独编译 C#
- Unity 版使用系统字体（PingFang SC / Microsoft YaHei 等）替代网页版的 Orbitron，数字与标题字形略有差异
- 网页版的玻璃模糊、背景网格/光晕等纯装饰效果未在 Unity 版复刻，其余玩法与界面结构保持一致
- 两套实现的调参常量需手动同步（`Conf.cs` ↔ `html5/game.js` 顶部常量）

## 后续交互指引

- 与用户交流一律使用简体中文（代码、命令、路径、标识符等保留原文）。
- 修改玩法参数：网页版改 `html5/game.js` 顶部常量，Unity 版改 `Assets/Scripts/Conf.cs`，两边保持一致。
- 新增地形需要同时更新：`TileType`、`Maps.LoadMap` 字符映射、`Conf` 配色、`SpriteFactory.Tile`、`GameRules.TileSolid`；网页版对应 `CHAR_MAP`、`tileSolid`、`drawTile`。
- 3D 渲染改动集中在 `SceneView.cs`：新增平面层须按高度分层表给 Y 值；新增结构物走 `ModelRegistry` + 程序化兜底双路径。
- 本文件与代码不符时以代码为准，并及时修订本文件。

## Codely Added Memories
- 当前项目默认使用中文与用户交流；除非用户明确要求其他语言，否则请用中文回答。

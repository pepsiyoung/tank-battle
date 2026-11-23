# 坦克大战（FC 经典重制）

FC 红白机《坦克大战》的现代重制版，包含**两个并存的实现**：零依赖的 2D 网页版，和 Unity 打造的 3D 正交俯视版。玩法参数完全一致。

- 13×13 战场网格，每关 20 辆敌军，场上同时最多 4 辆
- 守护基地（3 点耐久），消灭全部敌军过关，共 5 关
- 按住发射键 2 秒蓄力 → 松开释放**破坏死光**（每辆 3 次）：清出一条 3 格宽通路，并把草地变成 45 秒灼烧区
- 双人同屏合作；触屏设备自动显示虚拟按键

## 两个实现

| 实现 | 位置 | 技术 |
|---|---|---|
| 网页版 | `html5/` | 原生 HTML + CSS + Canvas 2D，零依赖零构建，打开即玩 |
| Unity 版 | `Assets/` 等（工程根目录） | Unity 2022.3 LTS（中国版 2022.3.62f3c1），C#，**3D 正交俯视渲染**：结构物为 3D 模型（团结 AI 生成 + 程序化兜底），平面贴图/音效运行时程序化生成 |

## 快速开始

### 网页版

```bash
cd html5
python3 -m http.server 8765     # 浏览器打开 http://127.0.0.1:8765/
```

也可以直接双击 `html5/index.html`（部分浏览器对本地文件的音频策略较严格，推荐起本地服务器）。

### Unity 版

1. Unity Hub → `Add` → 选择**工程根目录**（不是 Assets）→ 用 **Unity 2022.3 LTS** 打开
2. 首次打开会自动生成 `Assets/Scenes/Main.unity`（空场景是刻意设计，运行时由 `Bootstrap` 搭建全部对象）；若未生成，执行菜单 **Tank Battle → 创建主场景**
3. 按 **Play** 开玩

### Windows 独立版

File → Build Settings（场景已在列表中）→ 选择 Windows 平台 → Build。产物为一个文件夹（`tank-battle.exe` + `_Data` + `UnityPlayer.dll`），分发时请整包压缩。独立版窗口默认为显示器分辨率的 2/3，可拖拽缩放。

## 操作

| 按键 | 作用 |
|---|---|
| `W A S D` | P1 移动（单人模式兼容方向键） |
| `空格` | P1 射击 |
| `↑ ← ↓ →` | P2 移动 |
| `Enter` | P2 射击 |
| `P` / `Esc` | 暂停 |
| `M` | 静音 |

按住发射键 2 秒蓄力：进度条满后松开释放破坏死光；未满则普通射击。

## 场地与道具

- **砖墙**：可被子弹与死光摧毁；**钢墙**：仅死光可摧毁
- **草地**：坦克可以驶入藏身（俯视下遮挡视线）
- **水面**：坦克无法通行、子弹可以通过；**冰面**：坦克打滑
- **基地**：被击毁即失败；保护墙可被「铁锹」道具临时变成钢墙
- 道具：星（提升火力）、手雷（清屏）、坦克（加命）、铁锹（加固基地）、护盾（无敌）、时钟（冻结敌军）

## 目录结构

```
html5/            网页版（index.html / style.css / game.js）
Assets/           Unity 版代码、场景、运行时资源（含 AI 模型与保包材质）
Packages/         Unity 依赖清单
ProjectSettings/  Unity 工程设置
CODELY.md         AI 代理（Codely CLI）的项目上下文说明，非人类文档
LICENSE           开源许可证
```

## 开发说明

- 两套实现的玩法常量需手动同步：Unity 版改 `Assets/Scripts/Conf.cs`，网页版改 `html5/game.js` 顶部常量
- 网页版语法校验：`node --check html5/game.js`
- Unity 版依赖 `Packages/manifest.json` 中通过本地路径引用的 TJGenerators 扩展；在新机器克隆后如需 AI 资产生成功能，请通过 Codely CLI 重新安装该扩展
- 更详细的架构、约定与已知限制见 `CODELY.md`

## License

见 [LICENSE](LICENSE)。

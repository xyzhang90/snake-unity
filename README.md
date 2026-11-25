# Snake · 从 C 控制台到 Unity

大一下学期《程序设计项目实践（PBLF）》的小组作业是一个 **Windows 控制台贪吃蛇**（C 语言，链表存蛇身、字符画格子、4 档难度、障碍物、本地排行榜）。
这个仓库把它重做成两个可以真正跑起来的东西：

- **`docs/`** —— 网页版，点开就能玩（GitHub Pages 托管，手机也能玩）
- **`unity/`** —— Unity 工程，克隆下来用 Unity 打开、按 Play 即可

> 在线试玩：https://xyzhang90.github.io/snake-unity/
> 原始控制台版仓库：https://github.com/xyzhang90/25-9-snake

---

## 玩法

| 操作 | 键盘 | 触屏 |
| --- | --- | --- |
| 转向 | `WASD` / 方向键 | 滑动屏幕 / 右下角方向键 |
| 暂停 | `空格` | 点击屏幕 |
| 重开 | `R` | 面板按钮 |
| 回菜单 | `Esc` | 面板按钮 |
| 选难度 | `1` – `4` | 菜单里点选 |

**难度**（沿用原版 `iRefreshTimes[4]` 的思路，把毫秒改成更适合人玩的节奏）：

| 难度 | 一步耗时 | 每颗果子 | 障碍 |
| --- | --- | --- | --- |
| 简单 EASY | 0.30s | 1 分 | 无 |
| 普通 NORMAL | 0.18s | 2 分 | 无 |
| 困难 HARD | 0.11s | 3 分 | 4 个 2×2 方块 |
| 地狱 HELL | 0.07s | 4 分 | 4 个 2×2 方块 |

**增强（原版没有的）**

- **金色果子**：每吃 5 颗果子刷一次，限时 6 秒，分数 ×3；顶部 HUD 有倒计时
- **穿墙模式**：开关可选，撞墙从对面出来（原版只有撞死）
- **平滑移动**：格子逻辑不变，渲染用插值，不再一格一格跳
- **暂停**：原版虽然定义了 `PAUSE` 宏但没实现
- **音效 + 粒子**：全部运行时合成，仓库里没有音频/图片素材
- **最高分持久化**：网页用 `localStorage`，Unity 用 `PlayerPrefs`

---

## 目录结构

```
snake-unity/
├── docs/                  # 网页版（GitHub Pages 从这里发布）
│   ├── index.html
│   ├── style.css
│   ├── engine.js          # 纯逻辑引擎，不依赖 DOM
│   └── game.js            # Canvas 渲染 / 输入 / WebAudio 音效
├── unity/                 # Unity 工程
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── GameCore.cs        # 纯逻辑（与 docs/engine.js 规则一一对应）
│   │   │   ├── BoardView.cs       # 棋盘/蛇/食物/粒子，全部运行时生成
│   │   │   ├── Hud.cs             # uGUI 界面
│   │   │   ├── Sfx.cs             # 波形合成的音效
│   │   │   └── SnakeBootstrap.cs  # 状态机入口
│   │   └── Editor/
│   │       └── SnakeProjectSetup.cs  # 首次打开自动生成场景 + 加入构建列表
│   ├── Packages/manifest.json
│   └── ProjectSettings/ProjectVersion.txt
└── tests/engine.test.js   # 13 条引擎单测，node 直接跑
```

## 打开 Unity 工程

1. 用 [Unity Hub](https://unity.com/download) 安装 **Unity 2022.3 LTS**（或更新版本，允许它提示升级）
2. Hub → `Add project from disk` → 选择本仓库的 `unity/` 目录
3. 首次打开时，`Assets/Editor/SnakeProjectSetup.cs` 会自动创建 `Assets/Scenes/Main.unity` 并加入 Build Settings
4. 按 **Play** 就能玩

如果自动建场景失败（比如打开时正在编译），手动兜底：菜单 `Tools → Snake → Open Main Scene`，或新建空场景后放一个空物体挂上 `SnakeBootstrap`。

**Build 成可分发版本**

- Windows：`File → Build Settings → PC, Mac & Linux Standalone → Build`
- 网页：`File → Build Settings → WebGL → Build`，把产物放进仓库另开一个分支挂 Pages 即可

> 说明：工程没有附带 `ProjectSettings/ProjectSettings.asset`，Unity 首次打开会按 2022.3 默认值生成（默认就是旧版 Input Manager，脚本里用的 `Input.GetKeyDown` 依赖它）。如果你把它拖进了「新建 Unity 6 项目」并且启用了新版 Input System，请到 `Edit → Project Settings → Player → Active Input Handling` 改成 `Both` 或 `Input Manager (Old)`。

## 跑测试

```bash
node tests/engine.test.js
```

覆盖：移动/成长、撞墙、穿墙、障碍、撞自己（含"尾巴让位"的边界情况）、金果生成与超时、3000 步随机长局稳定性。

## 与控制台原版的对应关系

| 原版实现 | 这里的实现 |
| --- | --- |
| `Snake` 链表 + `malloc` 逐节分配 | `List<Vector2Int>` / JS 数组，头部插入、尾部弹出 |
| `cGameScene[20][40]` 字符缓冲逐格刷新 | 无字符缓冲，直接按状态渲染（逻辑与视图分离） |
| `Sleep(50/25/10/1)` 阻塞主循环 | 时间累加器 + 插值，帧率与逻辑步长解耦 |
| `_kbhit()` + `_getch()` 轮询 | Unity `Input` / 浏览器事件 |
| `scoreBoard.txt` 本地榜单 | `PlayerPrefs` / `localStorage` 单条最高分 |
| 障碍物固定在 4 个 2×2 位置 | 同一套坐标，只是在 Unity 里 y 轴翻转了 |

## Roadmap

- [ ] 排行榜（多玩家）而不是只存最高分
- [ ] 关卡推进 / 更多道具
- [ ] WebGL 构建产物直接挂 Pages，让网页版也跑在 Unity 里

## License

MIT

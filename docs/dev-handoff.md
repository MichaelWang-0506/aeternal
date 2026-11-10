# 开发交接文档（Dev Handoff）

最后更新：2026-11-10
用途：给后续开发（人或 AI）快速接手。记录当前进度、系统结构、约定和待办。
相关文档：`README.md`、`design/game-brief.md`（设计总纲、build order、对 GDD 的修订）、`docs/level1-zeusonia.md`（第一关）、`docs/level2-atlantis.md`（第二关）、`docs/art-requirements.md`（美术需求）。

---

## 1. 当前状态一览

| 项目 | 状态 |
|---|---|
| 引擎 | Unity 6（6000.2.6f2），C# |
| 仓库 | https://github.com/MichaelWang-0506/aeternal （private） |
| 分支 | `feat/level-2-atlantis`（尚未合并到 `main`） |
| 最后 commit | `bbb04a9` chore: Play starts from the main menu（2026-11-07） |
| 未提交 | 主角帧动画 + 打开项目时自动进入 MainMenu（见 §5 2026-11-08），**都已实测，尚未 commit** |
| 编译 | 最近一次 batchmode 构建成功，无 `error CS` |
| 实测 | 自动化 playtest 103/103 通过（2026-11-08，含帧动画）；第二关只经过自动化 playtest，还没有真人试玩 |

---

## 2. 整局流程

```
主菜单 New Game（Continue 读取自动存档）
 ├─ Play Tutorial  → TutorialScene → 序章
 ├─ Prologue       → 序章 Steampunk City（线性：哥布林 → 精英 → Thanatos 剧情杀）
 └─ Skip Prologue  → 直接进入 Zeusonia
序章结束（必败于 Thanatos → Chronus 时间循环）
 → 第一关 Zeusonia（5 层分支地图，Boss 前汇入唯一商人，Boss 宙斯）
 → 宙斯的祝福（宙斯卡组觉醒 → 解锁 Divine Shift）
 → 第二关 Atlantis（保留卡组/血量/金币/宙斯卡组；珊瑚墙地形；Boss 波塞冬）
 → Region Cleared → 主菜单
```

关卡之间通过 `MapData.nextMap` 串联；`keepsProgress` 决定是否保留进度。Divine Trial 测试关已不在主流程中（宙斯之后直接接 Atlantis）。

---

## 3. 系统与文件（`Assets/Scripts/`）

### 核心 / 局内状态
- `Core/RunManager.cs`：整局状态的唯一来源（牌组、血量、金币、Aether、已解锁卡组、祝福、地图节点、胜利后推进）。
- `Core/RunMap.cs`：分支地图生成。Boss 前一层固定为唯一商人，所有路径汇入；休息点在精英层。
- `Core/RunSave.cs`：战斗之间自动存档，主菜单 Continue 读取。

### 数据（ScriptableObject）
- `Encounters/MapData.cs`：区域配置（敌人池、Boss、楼层、`nextMap`、`keepsProgress`、`deckChoiceRewards`、经济数值）。
- `Encounters/EncounterData.cs`：单场战斗（精英、开场对话、剧情杀、蓄力攻击、`stormy` 天气、精灵着色/缩放）。
- 资源：`Assets/Scriptable Objects/Maps/`、`Zeusonia/`、`Atlantis/`（第二关全部为新拷贝，由 `tools/content/gen_atlantis.py` 生成）。
- `Resources/BattlefieldStyle.asset`：视觉调参总表（Hit effects、Ambience、Characters 下的 `playerFrames`）。
- `Scriptable Objects/Hero Frames.asset`：主角帧动画（`SpriteFrameSet`）。

### 战斗
- `Combat/CombatManager.cs`、`TurnManager.cs`：战斗主循环。`GuardLock` 在教程中锁住 Guard。
- `Combat/EncounterStoryDirector.cs`：剧情层（叙述、对话、剧情杀、蓄力攻击、祝福演出）。
- 第二关：珊瑚墙阻挡格子 2 回合（不会出现在玩家格或攻击的最后一个安全格）；波塞冬卡组两面墙合成水笼（5 + 每回合 2）。

### 地图与界面（运行时用代码搭建）
- `Map/RegionMapView.cs`、`Map/RunStarter.cs`、`UI/MainMenuController.cs`。
- `UI/RewardScreen.cs`：选一个卡组，从其池里 3 选 1，外加 2 选 1 封印卡（可跳过）。
- `UI/ShopPanel.cs`：商人 Hermus，移除或同稀有度替换选中卡组里的一张卡；Aether 30 金。
- `UI/CardGridPanel.cs`、`UI/CardView.cs`、`UI/RegionEventPanel.cs`、`UI/StoryOverlay.cs`、`UI/BattleUISkin`、`UI/UIIntro`。

### 表现（FX）与音频
- `FX/BattlefieldStyler`、`StatusVisuals`、`ProceduralSprites`、`SceneFader`。
- `FX/ImpactFX`：斩击、闪电、护盾环、治疗、流血、死亡爆散、hit-stop。
- `FX/AmbientLayer`：烟、火星、云和闪电、水下气泡。
- `FX/CharacterAnimator`：敌人用程序动画；主角有帧时改用 `SpriteFrameAnimator`（受击 → Hit，死亡 → 停在 Hit 最后一帧，移动 → Move，攻击卡 → Attack 并朝向敌人，其它卡 → Guard）。
- `FX/SpriteFrameSet.cs` / `SpriteFrameAnimator.cs`：帧序列数据 + 播放器（idle 5 fps，动作 18 fps，动作播完回 Idle）。
- `Audio/ChipSynth.cs` 等：代码合成的 8-bit 占位音乐和音效。

### 编辑器与工具
- `Editor/PlayFromMainMenu.cs`：Play 总是从 MainMenu 开始（菜单 Aeternal > Play From Main Menu 可关）；**每次打开项目时自动打开 MainMenu 场景一次**（SessionState，batchmode / 有未保存改动时跳过）。
- `Editor/PlaytestBuild` + `Debug/PlaytestDriver.cs`：自动化 playtest（见约定 11）。
- `tools/content/gen_atlantis.py`：生成第二关的着色精灵和资源。
- `tools/content/gen_hero_frames.py`：从 `Images/Characters/protagonist.png` 变形出 21 帧（idle 4、move 6、attack 5、hit 3、guard 3），写出 `protagonist_frames.png`（4032×2496，7 列）+ `.meta` 和 `Hero Frames.asset` + `.meta`。GUID 固定，重跑不会断引用。

---

## 4. 开发约定（踩过的坑）

1. **运行时自举**：新 UI/系统用 `[RuntimeInitializeOnLoadMethod]` + `SceneManager.sceneLoaded` 自动挂载，不改场景 YAML。
2. **命名遮蔽**：类里不要定义名为 `Button` / `Text` / `Rect` 的静态方法；辅助方法统一叫 `MakeButton`。局部变量不要叫 `name`。
3. **Fake-null**：Unity 对象不要用 `??`，写显式的 `== null`。
4. **Resources 命名冲突**：加载资源写 `UnityEngine.Resources.Load`。
5. **行尾**：RunManager、MapData、EncounterData、CombatEndController、RunStarter、CombatManager、CombatFXManager 用 LF；其余文件和新文件用 CRLF。不要改变原文件的行尾。
6. **战斗外复用卡牌 prefab**：必须走 `CardView.Spawn`（设置 `displayOnly`）。
7. **手写 ScriptableObject / meta YAML**：缺的字段取 C# 初始值；新资源配 `.meta`（固定 GUID，可用 uuid5）。Sprite sheet：`spriteMode 2`，自定义 pivot 用 `alignment 9`。
8. **编译检查**：编辑器开着时不能对同一项目跑 batchmode。做法是把 `Assets/` rsync 到一个项目副本，在副本上跑 batchmode，然后在日志里查 `error CS`。
9. **TMP 字体**：全局使用 Jersey 10 像素字体；`✓` 等特殊符号可能缺字形，优先用英文。`Jersey10 SDF.asset` 经常因为动态图集自动扩充出现大段 diff，属于噪音，不要随功能一起 commit（除非确实重建了字体）。
10. **协作规则**：写文件前先确认；没有用户指示不 commit/push。游戏内文字用英文。截图证据放 `production/qa/evidence/`。
11. **自动化 playtest**：
   1. `rsync -a --delete Assets/ <副本>/Assets/`，然后删掉副本里的 `Temp/UnityLockfile`。
   2. 构建：`Unity -batchmode -quit -projectPath <副本> -buildTarget OSXUniversal -executeMethod Aeternal.EditorTools.PlaytestBuild.Build -playtestOut "<out>/Aeternal Playtest.app" -logFile build.log`。
   3. 运行：`"Aeternal Playtest.app/Contents/MacOS/..." -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --playtest <输出目录>`。
   4. 输出编号截图和 `report.md`；保留的证据复制到 `production/qa/evidence/playtest-<日期>-runN/`。
   5. 存档与正式版分开；上一次运行留下的存档会让 Continue 检查失败，跑之前先清掉。
12. **第二关资源必须是拷贝**：改 Atlantis 不能影响序章/第一关的资源（例外只有 `Zeusonia.asset` 的 `nextMap` / `deckChoiceRewards` 和卡牌的 rarity 字段）。
13. **Python 工具**：需要 Pillow + NumPy（`pip install pillow numpy`）。运行方式：`python3 tools/content/gen_hero_frames.py . [预览图.png]`。

---

## 5. 进度记录

### 2026-10-25 之前
序章、第一关 Zeusonia、分支地图、金币与商人、牌组查看、胜利奖励、Divine Trial、Shift 升级、Guard、存档、暂停菜单（`03b6e03` 及之前）。

### 2026-11-04 ~ 11-05（已 commit）
- `e9b9e09` 自动化 playtest（PlaytestDriver + PlaytestBuild）。
- `8a08b38` / `ffe780c`：保证商人出现，所有路径在 Boss 前汇入一个商人。
- `1a743ed`：格子上的伤害标记，战斗 UI 改成主菜单风格，占位音乐和音效，精英外观。
- `4a2baea`：全局使用 Jersey 10 字体；卡面文字放在底板上，盖住卡图原有的印刷字（解决了旧的“卡面文字重叠”问题）。
- `48180f7`：教程中锁住 Guard。
- `c5bddf4` / `4e4d6b6`：删除一次性场景搭建菜单、旧场景对象和不再使用的文件。
- `4621977`：打击特效、卡牌动画、会动的背景、着色区分的敌人。

### 2026-11-07（已 commit）
- `1deade8` 第二关 Atlantis：珊瑚墙、水笼、波塞冬卡组（以 Tide Pearl 的形式收集）、两关通用的“选卡组”奖励、卡牌稀有度。
- `bbb04a9`：Play 从主菜单开始；解除 Unity Cloud 项目绑定。

### 2026-11-08 ~ 11-10（**未 commit**）
- **主角帧动画**：用现有立绘切出 21 帧，在游戏中替代主角的程序动画（敌人不变）。涉及文件：`FX/SpriteFrameSet.cs`、`FX/SpriteFrameAnimator.cs`（新）、`CharacterAnimator.cs`、`BattlefieldStyle.cs`、`BattlefieldStyler.cs`、`Resources/BattlefieldStyle.asset`、`Images/Characters/protagonist_frames.png`、`Scriptable Objects/Hero Frames.asset`、`tools/content/gen_hero_frames.py`。证据：`production/qa/evidence/hero-frames-2026-11-08/`（帧总览、攻击帧、游戏内跳跃/受击截图、playtest 103/103）。
- **打开项目即进入 MainMenu**：`Editor/PlayFromMainMenu.cs`，用户已确认有效。
- **课程作业（Unity Game Jam Quest FA26）交付物**，放在仓库外的 `~/Desktop/aeternal/`：
  - `Aeternal_Unity_Project.zip`（132 MB）：Assets、Packages、ProjectSettings、tools、docs、design、README，不含 Library / builds / .git / production。已验证解压后可以导入，没有编译错误。
  - `Aeternal - Unity GDD FA26.docx`：用户自己改过的版本。
  - `Aeternal - Unity GDD FA26 (polished).docx`：润色版，加了 “Notes on the Requirements” 一节，解释回合制下的移动、鼠标操作、地图碰撞、敌人碰撞和 Unity 6。
  - 作业主题解释：玩家注定输给 Thanatos，这次失败开启了时间循环（"Task Failed Successfully"）。
- 仓库根目录有 `Unity GDD FA26.docx`、`Unity Game Jam Quest FA26.md`、`playtest_for_windows_1(.zip)`，这些是作业材料和 Windows 构建，不属于项目源码，**不要 commit**（可以考虑加进 `.gitignore`）。

---

## 6. 已知问题

- **第二关没有真人试玩**：数值未经验证（72 HP 波塞冬 + 珊瑚墙 + Tidal Wave，对上可以 Shift 的玩家）。
- **波塞冬美术**：目前是宙斯着色（手里还是闪电），GDD 里的半章鱼形象需要正式美术。
- **波塞冬卡组**：要到第三关才能真正使用；现在只有自动化 playtest 覆盖。
- **Corrosion**（溺尸的腐蚀伤害）未实现，目前是普通物理伤害。
- **帧动画只做了主角**；敌人仍是程序动画。
- **没有 EditMode/PlayMode 单元测试**（`Assets/Tests/` 不存在），目前只有自动化 playtest。
- **作业相关**：作业要求 Unity 2022.3，本项目是 Unity 6，需要和老师确认；截止日 10/30 已过；GDD 里的 Project Link 仍是 `[Insert Link]`。

---

## 7. 建议的下一步

1. 按用户指示 commit 未提交内容，建议拆成两个 commit：
   - `feat: hero frame animation cut from the protagonist art`（帧动画 + 生成脚本 + 证据）；
   - `chore: open the main menu scene when the project opens`。
   `Jersey10 SDF.asset` 的 diff 先还原或单独处理。
2. 作业收尾（用户自己操作）：上传 zip，把链接填进 GDD，导出 PDF，提交 Gradescope，填 reflection form。
3. 真人试玩第二关并调数值；然后把 `feat/level-2-atlantis` 合并到 `main`。
4. 加自动化测试（`/test-setup`）：奖励、商人扣金币/Aether、地图连通性、Shift 加成、存档往返。
5. game-brief build order 剩余项：Shift 页面（拖动排序、卡组搭配加成）、Divine Focus 回复规则、Chronus 循环间加成表、正式音乐和美术。

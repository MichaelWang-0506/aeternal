# 开发交接文档（Dev Handoff）

最后更新：2026-10-25
用途：给后续开发（人或 AI）快速接手。记录当前进度、系统结构、约定和待办。
相关文档：`README.md`、`docs/level1-zeusonia.md`（第一关设计与数值）、`docs/art-requirements.md`（美术需求）。

---

## 1. 当前状态一览

| 项目 | 状态 |
|---|---|
| 引擎 | Unity 6（6000.2.6f2），C# |
| 仓库 | https://github.com/MichaelWang-0506/aeternal （private，`main`） |
| 最后推送 | `8ac945d`（README）。**之后的所有工作都还没有 commit** |
| 编译 | 最近一次 Unity 编译成功（无 `error CS`） |
| 实测 | 地图、战斗切换已实测；本批（商人 / 牌组查看 / 奖励动画 / 试炼关）**仅部分实测** |

未提交内容：第一关 Zeusonia + 风暴碎片、分支地图、金币与商人、牌组查看、胜利奖励界面、Divine Trial 测试关、主菜单 New Game 面板。

---

## 2. 整局流程

```
主菜单 New Game
 ├─ Play Tutorial  → TutorialScene → 序章
 ├─ Prologue       → 序章 Steampunk City（线性：哥布林 → 精英 → Thanatos 剧情杀）
 └─ Skip Prologue  → 直接进入 Zeusonia
序章结束 → 第一关 Zeusonia（分支地图，最终 Boss 宙斯）
 → 宙斯的祝福（风暴碎片 + 宙斯核心卡组 → 解锁 Divine Shift）
 → Divine Trial（测试关，保留进度，验证 Shift 可用）→ Region Cleared → 主菜单
```

关卡之间通过 `MapData.nextMap` 串联；`keepsProgress` 决定进入下一关时是否保留卡组/血量/金币/祝福。

---

## 3. 系统与文件（`Assets/Scripts/`）

### 核心 / 局内状态
- `Core/RunManager.cs`：整局状态的唯一来源。牌组、血量（`PlayerMaxHealth`）、金币（`Gold` / `AddGold` / `SpendGold`）、风暴碎片（`StormShards`）、已解锁卡组（`UnlockedDecks` / `ShiftDecks`）、祝福（`ReceiveBlessing`）、地图（`Map`、`CurrentMapNodeId`、`EnterMapNode`）、胜利后推进（`AdvanceAfterVictory` → `ContinueToNextRegion`）。
- `Core/RunMap.cs`：杀戮尖塔式分支地图生成。按 `MapData.floorPlan` 逐层生成 2–3 个节点 + Boss 层，连线不交叉、所有节点可达（2000 次模拟验证）。节点类型：Enemy / Elite / Event / Rest / Treasure / Boss / Shop。

### 数据（ScriptableObject）
- `Encounters/MapData.cs`：一个区域的全部配置，包括敌人池、Boss、楼层规则、`nextMap`、`keepsProgress`、祝福卡组、碎片池、区域事件、休息回血，以及经济数值（`goldReward` 14–22、`eliteGoldReward` 30–40、`cardPrice` 40、`shardPrice` 65、`priceSpread` 6、`removalPrice` 50、`shopCardCount` 4）。
- `Encounters/EncounterData.cs`：单场战斗。精英标记、开场对话、剧情杀（Thanatos 第 3 回合狂暴）、蓄力攻击（宙斯每 3 回合 ×2）。
- 资源位置：`Assets/Scriptable Objects/Maps/`（Steampunk City、Zeusonia、Divine Trial）、`Assets/Scriptable Objects/Zeusonia/`（敌人、Remnant Deck、Zeus Core Deck、试炼宙斯）。
- `Resources/BattlefieldStyle.asset`：视觉调参总表，含 `cardPrefab`（界面外显示卡面用）。

### 战斗
- `Combat/CombatManager.cs`、`TurnManager.cs`：战斗主循环。
- `Combat/EncounterStoryDirector.cs`：剧情层，负责开场叙述、对话、剧情杀与狂暴、蓄力攻击、宙斯祝福演出。
- `UI/CombatEndController.cs`：战斗结束。Boss 战显示 regionClearPanel；普通战斗发金币并打开 `RewardScreen`。

### 地图与界面（都在运行时用代码搭建，不依赖场景连线）
- `Map/RegionMapView.cs`：地图界面。左上角显示血量/金币/牌组/碎片，有 View Deck 按钮，节点点击后分发到战斗、事件、休息、宝箱或商人。
- `Map/RunStarter.cs`：开局入口，`StartPoint { Tutorial, Prologue, FirstRegion }`。
- `UI/MainMenuController.cs`：主菜单（New Game / Continue（禁用）/ Settings / Credits / Quit），New Game 面板提供三种开局。
- `UI/RewardScreen.cs`：约 1.1 秒胜利动画（VICTORY 砸入、星星爆开、金币上浮），之后显示卡面三选一或 Skip。
- `UI/ShopPanel.cs`：商人 Hermus。卡牌、风暴碎片、每次光顾可移除一张卡。
- `UI/CardGridPanel.cs`：可滚动卡面网格，可只看也可选择（用于牌组查看和移除卡牌）。
- `UI/CardView.cs`：在战斗外显示卡面的统一入口，复用手牌 prefab，并设置 `CardUI.displayOnly = true`。
- `UI/RegionEventPanel.cs`、`UI/StoryOverlay.cs`：事件面板和剧情叠层。

### 表现（FX）
`FX/BattlefieldStyler`（战场风格化、AP/DF 图标、Shift 封印显示）、`CharacterAnimator`（待机/攻击/眩晕动画）、`StatusVisuals`（流血/眩晕层数）、`ProceduralSprites`（代码生成的圆、水滴、星星等）、`SceneFader`。

---

## 4. 开发约定（踩过的坑）

1. **运行时自举**：新 UI/系统用 `[RuntimeInitializeOnLoadMethod]` + `SceneManager.sceneLoaded` 自动挂载，不改场景 YAML。
2. **命名遮蔽**：类里不要定义名为 `Button` / `Text` / `Rect` 的静态方法，否则会遮蔽同名 Unity 类型（`GetComponent<Button>()` 会出错）。辅助方法统一叫 `MakeButton`。局部变量也不要叫 `name`。
3. **Fake-null**：Unity 对象不要用 `??`，例如 `GetComponent<X>() ?? AddComponent<X>()`，要写显式的 `== null` 判断。
4. **Resources 命名冲突**：项目里有 `Aeternal.Resources` 命名空间，加载资源要写 `UnityEngine.Resources.Load`。
5. **行尾**：RunManager、MapData、EncounterData、CombatEndController、RunStarter、CombatManager、CombatFXManager 用 LF；其余文件和新文件用 CRLF。编辑后不要改变原文件的行尾。
6. **在战斗外复用卡牌 prefab**：必须走 `CardView.Spawn`。`CardUI` 每帧检查 `IsGameOver`，如果不设 `displayOnly`，战斗结束后卡会被置灰且无法点击（本日修复的 bug）。
7. **ScriptableObject 手写 YAML**：YAML 里缺的字段取 C# 初始值；新资源要配 `.meta`（自定 GUID）。
8. **编译检查**：Unity 编辑器开着时无法用 batchmode 编译。去 `~/Library/Logs/Unity/Editor.log` 查 `error CS` 和 `Tundra build success`，并确认新脚本生成了 `.meta`。
9. **TMP 字体缺字形**：`✓` 等特殊符号可能显示不出来，优先用英文文字。
10. **协作规则**：写文件前先确认；没有用户指示不 commit/push。游戏内文字用英文。

---

## 5. 今日（2026-10-25）完成

- **跳过序章**：New Game 弹出面板，可选 Play Tutorial / Prologue / Skip Prologue。设置里原来的“新游戏时播放教程”开关已删除。
- **金币**：普通战 14–22，精英 30–40；地图左上角显示。
- **商人 Hermus**（`$` 节点，第 2 层起出现）：4 张卡约 40 金，收集期间另有 1 个风暴碎片约 65 金，移除卡牌 50 金（每次光顾 1 次）。买不起的变暗，已买的显示 SOLD。
- **View Deck**：地图上可查看当前牌组 + 封印中的风暴碎片，支持悬停放大。
- **胜利奖励**：约 1 秒胜利动画后，用真实卡面三选一；碎片标注 “STORM SHARD · SEALED”。
- **Divine Trial 测试关**：击败宙斯后进入。保留进度，对手是试炼宙斯（40 HP，2 伤害），对话会教 Shift（Z 键或左下按钮）。
- **修复**：奖励卡无法点击（见约定 6）；奖励界面遮罩加深到 90%。

---

## 6. 已知问题

- **卡面文字重叠**：部分卡图本身印有描述文字（如 “Add 3 Bleeding”），游戏又叠印了数据里的描述，手牌和奖励界面都会这样。需要决定：去掉卡图上的文字（推荐，需美术），或者隐藏叠印文字。
- **本批功能待完整实测**：商人购买/移除、View Deck、宙斯后进入试炼关、试炼中 Shift 是否可用。
- **Continue（继续游戏）**：按钮仍是禁用状态，没有存档系统。
- **第二关**：Divine Trial 只是测试关，打完回主菜单；正式的第二关还没做。
- **数值**：第一关一条路线约受 26 伤害，对 20 HP + 回血；还没经过多次试玩验证。

---

## 7. 建议的下一步

1. 在 Unity 里实测本批功能，确认后 commit 并 push 全部未提交内容（Conventional Commits，例如 `feat: Zeusonia region, branching map, gold & merchant`）。
2. 加自动化测试：`Assets/Tests/PlayMode/`（项目已有 `com.unity.test-framework` 1.6.0）。可以在项目副本上用 batchmode 跑，不影响打开的编辑器。优先覆盖奖励选择、商人扣金币、地图连通性、Skip Prologue、试炼关 Shift。
3. 解决卡面文字重叠。
4. 存档 / Continue。
5. 第二关设计（参考 GDD §5 与 `docs/level1-zeusonia.md` 的结构）。
6. 更新 README，补上商人、金币、试炼关和新的主菜单流程。

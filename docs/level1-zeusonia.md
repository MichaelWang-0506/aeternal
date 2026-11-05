# 第一关：Zeusonia（风暴群峰）设计

依据：GDD §5（关卡结构、卡组、Shift）、Level Design Document、Flash Fiction Story（Act III）。
状态：**第一版，数值待试玩验证**。

---

## 1. 在整局中的位置

```
New Game → （首次）教程 → 序章《The Fall》→ Thanatos 剧情杀 → "The Cycle Begins"
        → 第一关《Zeusonia》→ 宙斯的祝福 → To be continued（第二关待做）
```

- **序章**：主角处于全盛状态（Steampunk 卡组 + 宙斯卡组，可以 Shift），对应关卡设计文档里"教程以满状态的主角开始"。
- **第一关**：时间循环开始后，主角力量尽失，只剩 8 张基础牌，**不能 Shift**。打败宙斯、得到祝福后，宙斯卡组才会觉醒，Divine Shift 解锁。
- 区域之间用 `MapData.nextMap` 串起来，`RunManager.StartRun` 会清空上一区域的碎片和祝福，正对应时间循环里"重新开始"的设定。

## 2. 关卡流程：分支地图（类似杀戮尖塔）

进入 Zeusonia 时先播放开场旁白，然后显示**每局随机生成**的地图：从下往上共 6 层，每层 2–3 个节点，只能走向和当前节点相连的上一层节点，最顶层是宙斯。

| 层 | 节点 | 设计意图 |
|---|---|---|
| 1 | 普通敌人 | 用基础卡组重新热身 |
| 2 | 敌人 / 事件 / 宝藏 | 第一次选择路线 |
| 3 | 敌人 / 事件 / 休息，**至少 1 个事件** | GDD：每两个敌人之后一次事件 |
| 4 | 精英 / 敌人，**至少 1 个精英** | 冒险换更好的奖励 |
| 5 | 休息 / 事件 / 宝藏，**至少 1 个休息** | Boss 战前整备 |
| 6 | **Zeus** | — |

规则在 `MapData.floorPlan` 里，每层有节点数和各类型的权重；不填就用默认值。普通敌人池按难度从低到高排列，前几层只会从最弱的敌人里抽。

**模拟 2000 张地图的结果**：没有走不到的节点，连线不交叉，每条路线都通向 Boss。每张地图平均 7 条路线；每条路线有 2–4 场战斗（2 场 32%、3 场 49%、4 场 19%），64% 的路线有精英战，72% 的路线至少有一个事件。

| 节点 | 内容 |
|---|---|
| 敌人 | Storm-touched Goblin（12 血 / 3 伤害 / 单格）、Peak Raider（18 血 / 3 伤害 / 会打三格） |
| 精英 | Stormcaller（30 血 / 4 伤害 / 会打三格）；奖励里有 **2 张风暴碎片** |
| 事件 | Storm Altar 三选一：回 8 血 / 删一张牌 / 从 3 张风暴碎片中挑 1 张 |
| 休息 | 回 10 血（`MapData.restHeal`） |
| 宝藏 | 收集碎片的关卡给一张随机风暴碎片，否则给一张普通牌 |
| Boss | Zeus（60 血 / 4 伤害 / 会打三格 + 雷暴蓄力） |

**宙斯的雷暴蓄力**（`EncounterData.chargedAttack*`）：
- 每 3 回合放一次 **THUNDERBOLT**：打四格中的三格，伤害 ×2（8 点）。
- 前一回合会出现 "STORM GATHERING" 横幅，HUD 上挂 CHARGING 徽章。
- 安全格**绝不会是你当前站的格子**，所以必须移动才能躲开。
- 宙斯被晕眩时，这一击会落空。这是对保留 Skull Smash 的奖励。

## 3. 卡组获取：风暴碎片（Storm Shards）

**规则**：在第一关里，宙斯卡组处于封印状态。

1. 每场战斗胜利后的 3 选 1 奖励里，**有 2 张是普通牌**（进入当前卡组），**1 张是风暴碎片**（进入封印的宙斯卡组，现在用不了）。
2. 风暴祭坛可以从 3 张碎片里挑 1 张；精英战的奖励有 2 张碎片；宝藏会直接给 1 张。
3. 打败宙斯后：**宙斯卡组 = 6 张核心牌 + 所有收集到的碎片**，然后解锁 Divine Shift。

**为什么这样设计**：
- 每次选奖励都面临"**现在变强，还是为以后存**"的取舍，这正是 GDD 第 1 条支柱"渐进成长"和第 3 条"策略多样性而不是靠运气"的体现。
- 第一关打得越稳，就越敢多收集碎片，宙斯卡组也就越强。只顾眼前变强，第二关的 Shift 卡组就会很薄。
- 设定上也说得通：主角在宙斯的领地里拾得神力的碎片，宙斯的祝福让它们苏醒。

**一局下来的碎片数**取决于路线：每场战斗奖励最多 1 张（精英最多 2 张），事件和宝藏各 1 张，大约 **0–6 张**，宙斯卡组共 **6–12 张**。

## 4. 卡组构成

### 初始卡组 Remnant Deck（8 张，GDD：5 张攻击 / 2 张移动 / 1 张功能）

| 牌 | 数量 | AP | 效果 | 分类 |
|---|---|---|---|---|
| Slash | 2 | 1 | 3 伤害 + 2 层流血 | 攻击 |
| Sword Swing | 1 | 2 | 3 伤害 + 顺时针移动 1 格 | 攻击（兼移动） |
| Blood Reaper | 1 | 1 | 1 层流血，持续 5 回合 | 攻击 |
| Skull Smash | 1 | 2 | 2 伤害 + 晕眩 | 攻击 |
| Rotate | 1 | 2 | 顺时针移动 1 格 | 移动 |
| Backstab | 1 | 1 | 1 伤害 + 移动到对面 | 移动 |
| Recycle | 1 | 2 | 从弃牌堆回收 2 张 | 功能 |

### 普通奖励池（进入当前卡组）
Smash、Disembowel、Steam Mist、Backflip Kick、Sacrifice、Slash、Sword Swing。

### 宙斯核心卡组（6 张，祝福时获得）
Punch ×2、Thunder Strike、Thunder Spear、Teleport、Extraction。Shift 进入时附带 3 点雷电伤害。

### 风暴碎片池
Thunder Armor（护盾）、Gnaw（便宜的晕眩）、Combo Punch、Thunder's Rage（爆发）、Thunder Strike。

## 5. 平衡计算（第一版）

**规则参数**（来自场景和数据）：每回合 6 AP，手牌上限 4 张，每回合从弃牌堆回收 2 张，主角 20 血，生命值在战斗之间保留。

**输出**：
- Remnant Deck 8 张共 12 AP，一手 4 张平均 6 AP，**正好用完每回合的 AP**。
- 打完一轮卡组约 21 点伤害，每张牌约 2.6 点。扣掉用来移动的牌，每回合实际约 **7–9 点伤害**。
- 加入 Smash（2AP 打 8 点）、Disembowel（2AP 约 10 点）之后，每回合输出能提高 20–30%。

**预计需要的回合数**：Goblin 2 回合、Peak Raider 2–3 回合、Stormcaller 约 4 回合、宙斯 5–7 回合（蓄力落在第 3、6 回合）。

**预计受到的伤害**（按单格攻击躲中 65%、三格攻击躲中 55% 估算）：

| 战斗 | 预计受到 |
|---|---|
| Goblin | ~2 |
| Peak Raider | ~3 |
| Stormcaller | ~7 |
| Zeus（含 2 次雷霆） | ~14 |
| **合计** | **~26** |

可用的生命值是 **20 + 祭坛回复 8 = 28**，Boss 战会打到残血，但能打过。

**第一版就把伤害调低了**：原稿是 3 / 4 / 5 / 5，雷霆 10，那样预计要承受 40 点以上，基本必死。

## 6. 可调参数（都在 Inspector 里）

| 想调整 | 改哪里 |
|---|---|
| 敌人血量 | `Scriptable Objects/Zeusonia/* Stats.asset` → `maxHealth` |
| 敌人伤害、是否打范围攻击 | `* Encounter.asset` → `baseDamage`、`canAOE` |
| 雷暴的频率和倍率 | `Zeus Encounter.asset` → `chargedAttackEvery`、`chargedAttackMultiplier` |
| 祭坛回血量、碎片选项数 | `Maps/Zeusonia.asset` → `regionEvent` |
| 地图每层的节点数和类型权重 | `Maps/Zeusonia.asset` → `floorPlan`（不填就用默认规则） |
| 休息回血量 | `Maps/Zeusonia.asset` → `restHeal` |
| 敌人池、精英池、Boss | `Maps/Zeusonia.asset` → `normalEncounters`、`eliteEncounters`、`bossEncounter` |
| 奖励池、碎片池 | `Maps/Zeusonia.asset` → `cardRewardPool`、`stormShardPool` |
| 宙斯核心卡组 | `Zeusonia/Zeus Core Deck.asset` |
| 初始卡组 | `Zeusonia/Remnant Deck.asset` |

## 7. 未解决的问题

1. **Gnaw 比 Skull Smash 强**：Gnaw 1AP，效果和 2AP 的 Skull Smash 相同，暂时没改。它在碎片池里，所以影响的是第二关以后两套卡组之间的取舍。
2. **祝福之后的 DF 预算**：Shift 每次 1 DF，每场战斗 5 DF，而且 Shift 会**重新抽满一手牌**，在第二关里可能过强。建议祝福后 DF 上限设为 3，或者把 Shift 消耗改成 2。
3. **伤害类型没有区别**：雷电伤害和物理伤害目前完全一样。GDD 里"卡组之间的相互加成"还没做。
4. **美术**：第一关的小怪复用了 Goblin 和 Bandit 的图，需要 Storm Goblin、Peak Raider、Stormcaller 的专属形象，以及风暴群峰的背景。见 `art-requirements.md`。
5. **商店、Chronus 的局外加成**：GDD 里有，这一关还没做。
6. **第二关**：Zeusonia 的 `nextMap` 现在是空的，所以祝福之后显示 To be continued。

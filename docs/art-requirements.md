# Aeternal 美术需求清单

本清单列出让战斗画面"动起来"、信息更清楚所需的美术资源，按优先级排序。每项给出规格、文件放置位置，以及可直接用于 AI 绘图工具的英文提示词。

现阶段战斗中的角色 HUD、格子、攻击格红光都由代码绘制（`BattlefieldStyler` / `CharacterHUD`），**没有这些资源也能运行**；资源到位后逐项替换即可。

---

## 0. 统一风格

现有角色图（`Assets/Images/Characters/`）的风格，所有新资源都要与之一致：

- **放大的像素画**：原生约 128×192 像素，放大 8 倍导出为 1024×1536。
- **暗色调**：深灰、深棕、铜/黄铜色点缀（蒸汽朋克），Boss 用冷色（黑、蓝灰）加红色高光。
- **透明背景**；主角有一圈白色描边（贴纸感）。敌人是否加描边需统一决定。
- 正面略带 3/4 侧身，人物站姿脚底在画布下方约 13% 处（与现有图一致，便于 HUD 对齐）。

**通用提示词前缀**（每条提示词前加上）：

```
dark fantasy steampunk pixel art, 128x192 sprite upscaled 8x with nearest neighbor,
limited dark palette with copper and brass accents, transparent background,
full body, slight three-quarter view, consistent with a gritty roguelike card game
```

> **导入设置建议**：像素图应使用 `Filter Mode: Point (no filter)`、`Compression: None`，否则会被糊边。
> 现有角色图是 `Bilinear`，建议一并改为 Point（只改导入设置，不改图片）。

---

## 1. 角色动画序列帧（优先级：高）

每个角色一套，**单帧画布 1024×1536**（原生 128×192），与现有静态图同尺寸、同脚底位置，这样替换时不用重新对齐。

| 动作 | 帧数 | 循环 | 触发时机（代码已有事件） |
|---|---|---|---|
| Idle 待机（呼吸、披风摆动） | 4–6 | 是 | 默认 |
| Attack 攻击 | 5–6 | 否 | 敌人：`EnemyController.OnAttackExecuted`；主角：出牌（`CardUI` 打出时） |
| Hit 受击 | 2–3 | 否 | `HealthComponent.OnDamageTaken` |
| Death 死亡 | 6–8 | 否（停在最后一帧） | `HealthComponent.OnDeathLocal` |
| Move 移动（仅主角，可选） | 3–4 | 否 | `PlayerPositioningComponent.OnFacingChanged` |
| Cast 施法 / Shift（仅主角，可选） | 4–6 | 否 | `CombatManager.OnDeckShifted` |

**角色列表**

| 角色 | 现有图 | 需要的动作 | 提示词要点 |
|---|---|---|---|
| 主角 Protagonist | `protagonist.png` | 全部 6 个 | `bearded armored warrior with dark iron plate armor etched with glowing copper runes, tattered crimson cloak, bare fists` |
| Goblin | `goblin.png` | Idle / Attack / Hit / Death | `small green goblin with a rusty dagger, hunched, mischievous` |
| Bandit | `bandit.png` | Idle / Attack / Hit / Death | `hooded steampunk bandit with goggles and a short blade` |
| Thanatos（Boss） | `thanatos.png` | Idle / Attack（挥镰）/ Hit / Death，Attack 可做 8 帧 | `hooded grim reaper with glowing red eyes, giant scythe, wisps of pale blue souls swirling around` |
| Zeus（Shift 神祇） | `zeus.png` | 仅 Cast（Shift 时闪现）4–6 帧 | `stern god of thunder with a lightning bolt, crackling electricity` |

**示例提示词（Goblin 攻击）**

```
<通用前缀>, sprite sheet of 6 frames in a single row, small green goblin with a rusty dagger,
attack animation: wind-up, lunge forward, stab, recover, same character and scale in every frame
```

**交付格式**：每个动作一张横向 sprite sheet（例如 6 帧 → 6144×1536），或逐帧 PNG。
**放置位置**：`Assets/Images/Characters/<角色名>/<角色名>_<动作>.png`，例如 `Goblin/goblin_attack.png`。

---

## 2. 图标（优先级：高）

替换 HUD 里目前的彩色文字标签和箭头。**原生 16×16，导出 128×128**（8 倍），透明背景。

| 图标 | 用途 | 提示词要点 |
|---|---|---|
| Bleed 流血 | 状态 | `blood drop icon` |
| Stun / Blackout 眩晕 | 状态 | `dizzy stars over a closed eye` |
| Dodge 闪避 | 状态 | `swirl of steam mist, evasion` |
| Shield 护盾 | 血条旁护盾值 | `round brass shield` |
| Intent: Attack 单体攻击 | 敌人头顶意图 | `crossed blade with red glow` |
| Intent: AOE 范围攻击 | 敌人头顶意图 | `three red claw slashes` |
| Action Point | 左下角 AP | `small glowing copper gear` |
| Divine Focus | 左下角 DF | `glowing blue lightning orb` |

**示例提示词**：`<通用前缀改为 16x16 icon>, blood drop icon, crisp outline, readable at small size`
**放置位置**：`Assets/Images/Icons/icon_<名称>.png`

---

## 3. 战场（优先级：中）

| 资源 | 规格 | 说明 |
|---|---|---|
| 格子贴图 | 原生 32×32 → 256×256 | 黄铜包边的石砖/铁板；再出一张"被锁定"版本（红色裂纹/警示纹） |
| 中心格（敌人站位） | 同上 | 稍暗、带符文圈 |
| 战斗背景 Steampunk City | 1920×1080（16:9），可非像素或低分辨率像素 | 当前用的是主菜单的 `steampunkcity.png`（1024×1024，会被裁切）。需要一张专门的战斗背景：中部留空、四周是管道/齿轮/烟雾，整体偏暗以突出角色 |
| 每个新区域一张背景 | 同上 | 随地图扩展 |

**示例提示词（背景）**

```
dark steampunk city alley at night, copper pipes, gears, drifting steam, dim gas lamps,
empty open floor in the center for a battle, low contrast, muted colors, 16:9, pixel art
```

---

## 4. 技能特效（优先级：中）

横向 sprite sheet，**原生 64×64，导出 512×512 每帧**，透明背景，6–8 帧，不循环。

| 特效 | 对应卡牌 / 事件 |
|---|---|
| 斩击 Slash | Slash、Sword Swing、Backstab、Disembowel |
| 钝击 Impact | Smash、Skull Smash、Punch、Combo Punch |
| 雷击 Lightning | Thunder Strike、Thunder Spear、Thunder's Rage、Gnaw |
| 流血滴落 Bleed tick | 回合结束流血伤害 |
| 护盾气泡 Shield | Thunder Armor、获得护盾 |
| 蒸汽闪避 Dodge | Steam Mist、闪避成功 |
| 治疗光点 Heal | 治疗 |
| Shift 转换 | 蒸汽齿轮爆开（Steampunk 卡组）/ 雷光（Zeus 卡组） |

**放置位置**：`Assets/Images/VFX/vfx_<名称>.png`

---

## 5. UI 框体（优先级：低）

| 资源 | 规格 | 说明 |
|---|---|---|
| 血条外框 | 9-slice，原生 48×8 | 黄铜边框，中间透明 |
| 名字牌 | 9-slice，原生 48×10 | 深色底、铜边 |
| 意图气泡 | 9-slice，原生 32×12 | 敌人头顶意图底板，带向下小尖角 |

---

## 接入方式（资源到位后由程序完成）

1. **动画**：每个角色加一个 Animator（Idle / Attack / Hit / Death / …），新建 `CharacterAnimator` 组件监听上表中的现有事件来触发。`BattlefieldStyler` 已经统一找到了玩家和敌人，挂载点就在那里。
2. **图标**：`CharacterHUD` 的状态文字标签和意图箭头换成 `Image`；在 `BattlefieldStyle` 配置里加图标字段，无需改代码逻辑。
3. **格子与背景**：直接在 `Assets/Resources/BattlefieldStyle.asset` 里替换 `backgroundSprite`，并为格子贴图加字段。
4. **特效**：在 `CombatFXManager`（已经监听伤害、护盾、状态、Shift 等事件）里播放对应 sprite sheet。

## 建议优先顺序

1. 主角 + 当前遭遇敌人（Goblin、Bandit、Thanatos）的 Idle、Hit——最能让画面"活起来"
2. 状态 / 意图图标——信息最直接的提升
3. 各角色 Attack、Death
4. 专用战斗背景 + 格子贴图
5. 技能特效
6. UI 框体

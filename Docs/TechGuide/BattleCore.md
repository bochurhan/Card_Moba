# ⚠️ BattleCore 核心代码解读（V1 归档）

> **本文档已归档**（2026-03-04）。BattleCore V2 重构已启动，请参阅：
> - 新架构：[SystemArchitecture_V2.md](SystemArchitecture_V2.md)
> - 重构计划：[../Planning/BattleCoreRefactorPlan.md](../Planning/BattleCoreRefactorPlan.md)
>
> 本文档保留供历史参考，不再更新维护。

**文档版本**：V5.3（最终版，已停止维护）
**最后更新**：2026-03-03
**适用对象**：需要理解或修改结算逻辑的开发者
**前置阅读**：[QuickStart.md](QuickStart.md)、[SystemArchitecture.md](SystemArchitecture.md)（系统关系总览）、[../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md)
**阅读时间**：20 分钟

---

## 🎯 BattleCore 定位

| 特性 | 说明 |
|------|------|
| **位置** | `Shared/BattleCore/` |
| **职责** | 所有战斗结算逻辑的唯一实现 |
| **约束** | `noEngineReferences: true` — 禁止使用 UnityEngine |
| **消费者** | Unity Client、.NET Server 共同引用，保证 C/S 结果完全一致 |

### 核心原则

```
┌─────────────────────────────────────────────────────────┐
│             确定性 + 无状态 + 模块化                      │
├─────────────────────────────────────────────────────────┤
│  • 相同输入 → 相同输出（可复现，支持回放）                │
│  • 所有可变状态统一存储在 BattleContext 中               │
│  • 效果处理通过 IEffectHandler 模块化，可独立测试         │
│  • 随机使用 SeededRandom，种子由服务端下发               │
│  • PlayerId 统一使用 string 类型（服务端兼容）           │
└─────────────────────────────────────────────────────────┘
```

---

## 📁 目录结构

```
Shared/BattleCore/
├── Buff/                       # Buff 系统
│   ├── BuffConfig.cs           # Buff 配置
│   ├── BuffInstance.cs         # Buff 实例
│   ├── BuffManager.cs          # Buff 管理器
│   └── BuffType.cs             # Buff 类型枚举
│
├── Context/                    # 战斗上下文（所有可变状态）
│   ├── BattleContext.cs        # 主上下文
│   ├── PlayerBattleState.cs    # 玩家战斗状态
│   ├── LaneState.cs            # 分路状态
│   └── PlayedCard.cs           # 已打出的卡牌（结算专用）
│
├── Event/                      # 战斗事件
│   └── BattleEvent.cs          # 事件记录
│
├── Settlement/                 # 结算引擎
│   ├── SettlementEngine.cs     # 主结算引擎
│   ├── TargetResolver.cs       # 目标解析器
│   ├── DamageHelper.cs         # 伤害计算辅助
│   │
│   └── Handlers/               # ★ 模块化效果处理器
│       ├── IEffectHandler.cs   # Handler 接口
│       ├── HandlerRegistry.cs  # Handler 注册中心
│       ├── CommonHandlers.cs   # 通用处理器（Slow/Discard/ArmorOnHit 等）
│       ├── DamageHandler.cs    # 伤害处理
│       ├── HealHandler.cs      # 治疗处理
│       ├── ShieldHandler.cs    # 护盾处理
│       ├── StunHandler.cs      # 眩晕处理
│       └── CounterHandler.cs   # 反制处理
│
├── Random/                     # 确定性随机
│   └── SeededRandom.cs         # 种子随机数生成器
│
├── RoundStateMachine/          # 回合状态机
│   └── RoundManager.cs         # 回合管理器
│
└── Trigger/                    # 触发系统
    ├── TriggerManager.cs       # 触发管理器
    └── TriggerTypes.cs         # 触发时机枚举
```

---

## 🔧 EffectType 统一枚举体系

> **当前版本**：所有效果类型统一定义在 `Shared/Protocol/Enums/EffectType.cs`。
> 添加新效果时，先在枚举追加，再创建 Handler，再注册到 HandlerRegistry。

### Layer 0 — 反制层（最高优先级）

| 枚举值 | ID | 说明 |
|--------|----|------|
| `Counter` | 1 | 反制敌方卡牌。默认反制首张伤害牌；可通过 `TriggerCondition` 指定筛选条件；附带反弹效果需配合 `CardTag.Reflect` 标签 |

### Layer 1 — 防御/修正层

| 枚举值 | ID | 说明 |
|--------|----|------|
| `Shield` | 2 | 获得护盾（吸收伤害，一次性） |
| `Armor` | 3 | 获得护甲（持续减伤） |
| `AttackBuff` | 4 | 力量增益（增加造成的伤害） |
| `AttackDebuff` | 5 | 力量削减（降低目标造成的伤害） |
| `Reflect` | 6 | 反伤（受到伤害时等量反弹给攻击者） |
| `DamageReduction` | 7 | 伤害减免（百分比） |
| `Invincible` | 8 | 本回合无敌（完全免疫伤害） |

### Layer 2 — 伤害/触发层

| 枚举值 | ID | 说明 |
|--------|----|------|
| `Damage` | 10 | 对目标造成伤害（护盾→护甲→扣血→濒死判定） |
| `Lifesteal` | 11 | 吸血（造成伤害后恢复等比例生命） |
| `Thorns` | 12 | 荆棘（受击后对攻击者造成固定伤害，忽略护甲） |
| `ArmorOnHit` | 13 | 受击获甲（受到伤害时获得护甲） |
| `Pierce` | 14 | 穿透（伤害无视护甲/护盾，直接扣血） |

### Layer 3 — 功能/资源层（最低优先级）

| 枚举值 | ID | 说明 |
|--------|----|------|
| `Heal` | 20 | 恢复生命值 |
| `Stun` | 21 | 眩晕控制（跳过目标下 N 个操作期） |
| `Vulnerable` | 22 | 易伤（目标受到的伤害增加） |
| `Weak` | 23 | 虚弱（目标造成的伤害减少） |
| `Draw` | 24 | 抽取卡牌 |
| `Discard` | 25 | 强制弃牌 |
| `GainEnergy` | 26 | 获得能量 |
| `Silence` | 27 | 沉默（禁止目标使用技能牌 N 回合） |
| `Slow` | 28 | 减速（降低目标行动顺序优先级） |
| `DoubleStrength` | 29 | 力量翻倍（将施法者当前力量×2，消耗型） |

---

## 🃏 CardTag 标签体系

卡牌标签（`[Flags]` 枚举）用于 UI 分类、卡组规则验证和效果目标筛选，与 `EffectType` 正交设计：

- `EffectType` → 描述**效果机制**（Handler 分发依据）
- `CardTag`    → 描述**卡牌用途与特殊行为**（筛选/规则依据）

| 标签 | 位值 | 说明 |
|------|------|------|
| `Damage` | 1 | 伤害牌，可被 Counter 效果反制 |
| `Defense` | 2 | 防御牌 |
| `Counter` | 4 | 反制牌，本回合锁定下回合触发 |
| `Buff` | 8 | 增益牌 |
| `Debuff` | 16 | 减益牌 |
| `Control` | 128 | 控制牌（眩晕/减速/沉默） |
| `Reflect` | 32768 | 反弹：配合 `EffectType.Counter`，命中时将伤害反弹给攻击者 |
| `CrossLane` | 512 | 跨路生效 |
| `Exhaust` | 2048 | 使用后从游戏中移除 |
| `Recycle` | 1024 | 使用后返回牌库底部 |

---

## 🏗️ Handler 注册表（完整列表）

每个 `EffectType` 对应唯一一个 Handler，通过 `HandlerRegistry` 统一管理：

| Handler | 负责的 EffectType |
|---------|-----------------|
| `CounterHandler` | `Counter(1)` |
| `ShieldHandler` | `Shield(2)` |
| `ArmorHandler` | `Armor(3)` |
| `StrengthHandler` | `AttackBuff(4)`、`AttackDebuff(5)` |
| `ThornsHandler` | `Reflect(6)` |
| `DamageReductionHandler` | `DamageReduction(7)` |
| `InvincibleHandler` | `Invincible(8)` |
| `DamageHandler` | `Damage(10)` |
| `LifestealHandler` | `Lifesteal(11)` |
| `ThornsHandler` | `Thorns(12)` |
| `ArmorOnHitHandler` | `ArmorOnHit(13)` |
| `HealHandler` | `Heal(20)` |
| `StunHandler` | `Stun(21)` |
| `VulnerableHandler` | `Vulnerable(22)` |
| `WeakHandler` | `Weak(23)` |
| `DrawHandler` | `Draw(24)` |
| `DiscardHandler` | `Discard(25)` |
| `EnergyHandler` | `GainEnergy(26)` |
| `SilenceHandler` | `Silence(27)` |
| `SlowHandler` | `Slow(28)` |
| `DoubleStrengthHandler` | `DoubleStrength(29)` |

### 注册代码（HandlerRegistry.cs 实际内容）

```csharp
public static void Initialize()
{
    // Layer 0: 反制
    Register(EffectType.Counter,         new CounterHandler());

    // Layer 1: 防御/修正
    Register(EffectType.Shield,          new ShieldHandler());
    Register(EffectType.Armor,           new ArmorHandler());
    Register(EffectType.AttackBuff,      new StrengthHandler());
    Register(EffectType.AttackDebuff,    new StrengthHandler());
    Register(EffectType.Reflect,         new ThornsHandler());
    Register(EffectType.DamageReduction, new DamageReductionHandler());
    Register(EffectType.Invincible,      new InvincibleHandler());

    // Layer 2: 伤害/触发
    Register(EffectType.Damage,          new DamageHandler());
    Register(EffectType.Lifesteal,       new LifestealHandler());
    Register(EffectType.Thorns,          new ThornsHandler());
    Register(EffectType.ArmorOnHit,      new ArmorOnHitHandler());

    // Layer 3: 功能/资源
    Register(EffectType.Heal,            new HealHandler());
    Register(EffectType.Stun,            new StunHandler());
    Register(EffectType.Vulnerable,      new VulnerableHandler());
    Register(EffectType.Weak,            new WeakHandler());
    Register(EffectType.Draw,            new DrawHandler());
    Register(EffectType.Discard,         new DiscardHandler());
    Register(EffectType.GainEnergy,      new EnergyHandler());
    Register(EffectType.Silence,         new SilenceHandler());
    Register(EffectType.Slow,            new SlowHandler());
    Register(EffectType.DoubleStrength,  new DoubleStrengthHandler());
}
```

---

## 🔄 结算引擎执行流程

### 瞬策牌结算（即时执行）

瞬策牌在打出时立即结算，不进入定策队列：

```csharp
public bool ResolveInstantCard(BattleContext ctx, PlayedCard card)
{
    // 1. 解析目标
    card.ResolvedTargets = _targetResolver.Resolve(card, ctx);

    // 2. 触发 BeforePlayCard（前置 Hook，可被取消）
    ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.BeforePlayCard, ...);

    // 3. 检查是否被前置 Hook 取消
    if (cancelled) return false;

    // 4. 遍历效果列表，通过 Handler 分发执行
    foreach (var effect in card.Config.Effects)
    {
        ExecuteEffect(ctx, card, effect, source);
    }

    // 5. 触发 AfterPlayCard（后置 Hook）
    ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.AfterPlayCard, ...);

    return true;
}
```

### 定策牌结算（回合结束统一执行）

定策牌按 4 层优先级顺序结算，保证同层内同步、跨层有序：

```csharp
public void ResolvePlanCards(BattleContext ctx)
{
    // 1. 预处理：为所有待结算卡牌解析目标
    PreResolveTargets(ctx);

    // 2. Layer 0: 反制（最高优先级，标记被反制卡牌）
    ResolveLayer0_Counter(ctx);

    // 3. Layer 1: 防御与数值修正（护盾/护甲/增减攻等）
    ResolveLayer1_Defense(ctx);

    // 4. Layer 2: 伤害与触发（伤害/吸血/荆棘等）
    ResolveLayer2_Damage(ctx);

    // 5. Layer 3: 功能效果（治疗/抽牌/控制等）
    ResolveLayer3_Utility(ctx);
}
```

### 执行流程图

```
┌───────────────────────────────────────────────────────────────┐
│                    ResolvePlanCards                           │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  PreResolveTargets                                            │
│  └─ 为每张牌调用 TargetResolver.Resolve()                      │
│                     │                                         │
│                     ▼                                         │
│  Layer 0: ResolveLayer0_Counter                               │
│  ├─ 处理上回合提交的反制牌（Counter 定策牌）                   │
│  ├─ 根据 TriggerCondition / CardTag 匹配目标                   │
│  ├─ 标记命中的卡牌 IsCountered = true                          │
│  └─ 带 CardTag.Reflect → 反弹伤害给攻击者                      │
│                     │                                         │
│                     ▼                                         │
│  Layer 1: ResolveLayer1_Defense                               │
│  └─ 同步应用所有防御/修正效果                                  │
│     （Shield/Armor/AttackBuff/Weak/DamageReduction 等）        │
│                     │                                         │
│                     ▼                                         │
│  Layer 2: ResolveLayer2_Damage                                │
│  ├─ Step 1: 同步收集并应用所有伤害（护盾→护甲→扣血→濒死标记）  │
│  └─ Step 2: 处理触发效果（Lifesteal/Thorns/Vulnerable 等）     │
│                     │                                         │
│                     ▼                                         │
│  Layer 3: ResolveLayer3_Utility                               │
│  ├─ SubPhase 1: 普通功能（Heal/Draw/Stun/Slow/Silence 等）     │
│  └─ SubPhase 2: 传说特殊牌（独立优先级）                        │
│                                                               │
└───────────────────────────────────────────────────────────────┘
```

### 单个效果执行路径（含 ExecutionMode 路由）

```
ExecuteEffect(ctx, card, effect, source)
    │
    ├─ ExecutionMode == Passive
    │       └─ return（已在打出时由 PassiveHandler 注册，结算时跳过）
    │
    ├─ ExecutionMode == Conditional
    │       └─ EffectConditionChecker.EvaluateAll(effect.EffectConditions)
    │               ├─ false → return（条件不满足，记录日志）
    │               └─ true  → 继续
    │
    ├─ HandlerRegistry.GetHandler(effect.EffectType)
    │       ↓ 未注册
    │   ctx.RoundLog.Add("[Warning] 未注册的 EffectType: XXX")
    │   return
    │       ↓ 已注册
    └─ handler.Execute(card, effect, source, target, ctx)
```

> 📖 卡牌从打出到结算的完整生命周期，请参阅 [CardLifecycle.md](CardLifecycle.md)。

---

## 🔀 反制牌详解

反制效果（`EffectType.Counter`）有独特的执行规则：目标是**卡牌**而非玩家，在 Layer 0 执行。

### 反制目标筛选

通过 `CardEffect.TriggerCondition` 字段配置筛选条件：

| `TriggerCondition` | 行为 |
|--------------------|------|
| `""` （空） | 默认：反制敌方首张带 `CardTag.Damage` 的伤害牌 |
| `"tag:Damage"` | 反制所有带 Damage 标签的敌方牌 |
| `"tag:Counter"` | 反制敌方的反制牌（反反制） |
| `"layer:DamageTrigger"` | 反制属于 Layer 2 的所有牌 |

### 反弹效果（与 EffectType 解耦）

```
EffectType.Counter  →  是否反制（必选）
CardTag.Reflect     →  是否同时反弹伤害给攻击者（可选）
```

两者正交组合：普通反制、反制+反弹均使用同一个 `CounterHandler`，无需额外枚举变体。

---

## 📦 关键数据结构

### PlayedCard（已打出卡牌）

```csharp
public class PlayedCard
{
    // ── 卡牌标识 ──
    public string RuntimeId { get; set; }             // 运行时唯一 ID
    public CardConfig Config { get; set; }            // 关联配置

    // ── 施法者与目标 ──
    public string SourcePlayerId { get; set; }        // 施法者 ID（string）
    public int LaneIndex { get; set; }                // 所在分路
    public List<string> RawTargetGroup { get; set; }  // 玩家选择的原始目标
    public List<string> ResolvedTargets { get; set; } // 解析后的实际目标

    // ── 结算状态 ──
    public bool IsCountered { get; set; }             // 是否被反制（已标记则跳过结算）
}
```

### BattleContext（战斗上下文）

```csharp
public class BattleContext
{
    // ── 玩家状态 ──
    public List<PlayerBattleState> Players { get; set; }           // 遍历用
    // 注意：获取单个玩家请用 GetPlayer(id)，O(1) 字典查找

    // ── 待结算卡牌队列 ──
    public List<PlayedCard> PendingPlanCards { get; set; }    // 本回合定策牌
    public List<PlayedCard> PendingCounterCards { get; set; } // 上回合反制牌（在 Layer0 触发）
    public List<PlayedCard> ValidPlanCards { get; set; }      // 有效牌（未被反制）
    public List<PlayedCard> CounteredCards { get; set; }      // 已被反制的牌

    // ── 场景信息 ──
    public MatchPhase MatchPhase { get; set; }                // 当前比赛阶段

    // ── 日志 ──
    public List<string> RoundLog { get; set; }                // 当前回合日志
    public List<List<string>> HistoryLog { get; set; }        // 历史回合快照（R-08，永不丢失）

    // ── 快捷方法 ──
    public PlayerBattleState GetPlayer(string playerId)       // O(1)，推荐使用
    public void RegisterPlayer(PlayerBattleState player)      // 替代直接 Players.Add
    public BuffManager GetBuffManager(string playerId)        // 获取玩家 Buff 管理器
}
```

> ⚠️ 已移除字段（2026-03-01 R-05 清理）：
> - `PendingTriggerEffects`（旧触发路径列表）
> - `HasChainTriggeredThisRound`（旧连锁封顶标记）
> 触发式效果（吸血/反伤）现在统一由 `TriggerManager` 通过 `AfterDealDamage / AfterTakeDamage` 节点处理。

### PlayerBattleState（玩家战斗状态，关键字段）

```csharp
public class PlayerBattleState
{
    // ── 基础属性 ──
    public string PlayerId { get; set; }
    public int TeamId { get; set; }
    public int LaneIndex { get; set; }
    public int Hp { get; set; }
    public bool IsAlive { get; set; }

    // ── 防御属性 ──
    public int Shield { get; set; }           // 护盾（一次性，优先于护甲）
    public int Armor { get; set; }            // 护甲（持续减伤）
    public int ArmorOnHitValue { get; set; }  // 受击时触发的护甲增量

    // ── 攻击属性 ──
    public int AttackBuff { get; set; }       // 攻击力修正（正数增加，负数减少）

    // ── 状态标记 ──
    public bool IsStunned { get; set; }        // 眩晕（跳过下回合出牌）
    public bool IsSlowed { get; set; }         // 减速
    public int SlowCostIncrease { get; set; }  // 减速导致的费用增量
    public bool IsSilenced { get; set; }       // 沉默
    public bool IsVulnerable { get; set; }     // 易伤
    public bool IsMarkedForDeath { get; set; } // 濒死标记（待濒死判定期处理）
}
```

---

## ⚠️ 开发注意事项

### 禁止事项（CRITICAL）

| ❌ 禁止 | 原因 |
|--------|------|
| 使用 `UnityEngine.*` | asmdef `noEngineReferences: true` + 服务端兼容性 |
| 使用 `System.Random` 或 `UnityEngine.Random` | 必须用 `SeededRandom` 保证 C/S 确定性 |
| 在结算逻辑中使用浮点数 | 精度差异导致 C/S 结果不一致 |
| 在 Handler 中存储私有状态 | 破坏无状态原则，Handler 实例是单例 |
| 使用跳跃 ID 添加 EffectType | 保持 ID 连续，在最大 ID 后追加 |
| 在 Client-only 代码里实现结算规则 | 所有结算逻辑必须在 `Shared/BattleCore/` |

### 必须遵守

| ✅ 必须 | 原因 |
|--------|------|
| 新效果注册到 `HandlerRegistry` | 否则会触发"未注册"警告并跳过结算 |
| `Handler.Execute()` 处理 `null target` | 自身增益类效果没有目标玩家 |
| 新增 `EffectType` 同步更新本文档 | 保持文档与代码一致 |
| 使用 `#pragma warning disable CS8632` | Unity C# 版本不支持 nullable 注解语法 |

### 添加新效果的步骤

```
1. 在 Shared/Protocol/Enums/EffectType.cs 追加枚举值（当前最大 ID = 29）
2. 在 Shared/BattleCore/Settlement/Handlers/ 创建 XxxHandler.cs 实现 IEffectHandler
3. 在 HandlerRegistry.Initialize() 中 Register(EffectType.Xxx, new XxxHandler())
4. 在 Config/Excel/Effects.csv 中增加使用该效果的卡牌数据
5. 运行 Tools/ExcelConverter/convert.bat 重新生成 JSON
6. 更新本文档 Handler 注册表与 EffectType 表
7. 运行 BattleSimulator 验证效果
```

---

## 📝 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| V5.3 | 2026-03-03 | R-Counter 重构：移除 `SettlementEngine` 中的 `ProcessCounterCard` / `FindFirstDamageCard` / `ApplyCounterEffects` 硬编码方法；Counter 效果统一走 `PassiveHandler` + `TriggerManager` 体系；新增 `ExecutionMode`（Immediate/Conditional/Passive）路由；新增 `EffectConditionChecker` 条件评估器；新增 `PlayConditions` 打出条件；新增 `PassiveHandler` 触发器注册系统；`ExecuteEffect` 补充 ExecutionMode 路由分支；新建 `CardLifecycle.md` 文档 |
| V5.2 | 2026-03-02 | 同步更新文档结构：`Architecture.md` 重命名为 `SystemArchitecture.md`；`ConfigSystem.md` 标记为已完成；修正 `QuickStart.md` 中失效链接；`SystemArchitecture.md` 清理旧 `PendingTriggerEffects` 引用，更新集成缺口表格 |
| V5.1 | 2026-03-01 | R-05：删除 `PendingTriggerEffects` / `HasChainTriggeredThisRound` / `ResolveLayer2_Step2_Triggers` 旧触发路径；R-06：`GetPlayer` 改为 O(1) 字典查找，新增 `RegisterPlayer` 方法；R-08：新增 `HistoryLog` 回合日志快照持久化；更新 BattleContext 数据结构章节 |
| V5.0 | 2026-02-28 | 修正 EffectType ID 表（对齐实际枚举：跳跃分布 1,2-8,10-14,20-29）；更新 Handler 注册表（移除不存在的 ArmorBreak/ExecuteKill/HealTeam；新增 DoubleStrength(29)）；新增效果流程指向 ConfigSystem.md |
| V4.0 | 2026-02-26 | 彻底移除旧版 100+ 兼容范围；统一 EffectType 体系；CounterHandler 重构；补充 PlayerBattleState 字段；CardTag 新增 Reflect |
| V3.1 | 2026-02-25 | 清理旧版 API；完善 Handler 列表 |
| V3.0 | 初期 | Handler 模块化重构；PlayerId 改为 string；新增 PlayedCard |

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 结算规则设计 | [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md) |
| 卡牌系统设计 | [../GameDesign/CardSystem.md](../GameDesign/CardSystem.md) |
| **卡牌生命周期与 Buff 管理** | [CardLifecycle.md](CardLifecycle.md) ★ 新增 |
| 配置系统说明 | [ConfigSystem.md](ConfigSystem.md) |
| 快速入门 | [QuickStart.md](QuickStart.md) |
| 代码位置 | `Shared/BattleCore/Settlement/` |

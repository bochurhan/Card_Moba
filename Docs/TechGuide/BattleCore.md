\
# BattleCore 核心代码解读

**文档版本**：V4.0
**最后更新**：2026-02-26
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

> **V4.0 重要变更**：已彻底移除旧版兼容范围（100+），所有效果类型统一为连续 ID（1–28）。
> 添加新效果时，**必须**在此连续范围内追加，不得使用跳跃 ID。

### Layer 0 — 反制层（最高优先级）

| 枚举值 | ID | 说明 |
|--------|----|------|
| `Counter` | 1 | 反制敌方卡牌。默认反制首张伤害牌；可通过 `TriggerCondition` 指定筛选条件；附带反弹效果需配合 `CardTag.Reflect` 标签 |

### Layer 1 — 防御/修正层

| 枚举值 | ID | 说明 |
|--------|----|------|
| `Shield` | 2 | 获得护盾（吸收伤害，一次性） |
| `Armor` | 3 | 获得护甲（持续减伤） |
| `AttackBuff` | 4 | 增加攻击力 |
| `AttackDebuff` | 5 | 降低攻击力 |
| `ArmorBreak` | 6 | 破甲（降低护甲值） |
| `DamageReduction` | 7 | 伤害减免（百分比） |
| `Invincible` | 8 | 本回合无敌 |
| `ArmorOnHit` | 9 | 受击时获得护甲 |
| `Weak` | 10 | 施加虚弱（降低目标攻击力） |

### Layer 2 — 伤害/触发层

| 枚举值 | ID | 说明 |
|--------|----|------|
| `Damage` | 11 | 对目标造成伤害（护盾→护甲→扣血→濒死判定） |
| `Lifesteal` | 12 | 吸血（造成伤害的同时恢复等量生命） |
| `Thorns` | 13 | 荆棘反伤（受到伤害时对攻击者造成固定伤害） |
| `Vulnerable` | 14 | 易伤（目标受到的伤害增加） |
| `ExecuteKill` | 15 | 斩杀（对低血量目标直接击杀） |

### Layer 3 — 功能/资源层（最低优先级）

| 枚举值 | ID | 说明 |
|--------|----|------|
| `Heal` | 16 | 恢复生命值 |
| `HealTeam` | 17 | 恢复队友生命值 |
| `Draw` | 18 | 抽取卡牌 |
| `Discard` | 19 | 强制弃牌 |
| `GainEnergy` | 20 | 获得能量 |
| `Stun` | 21 | 眩晕控制（跳过目标下回合出牌） |
| `Slow` | 22 | 减速（目标下回合费用增加） |
| `Silence` | 27 | 沉默（阻止目标使用指定类型卡牌） |

> **注**：ID 23–26 为预留位，28 为扩展预留。添加新效果时从 ID 28 起追加。

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
| `ArmorBreakHandler` | `ArmorBreak(6)` |
| `DamageReductionHandler` | `DamageReduction(7)` |
| `InvincibleHandler` | `Invincible(8)` |
| `ArmorOnHitHandler` | `ArmorOnHit(9)` |
| `WeakHandler` | `Weak(10)` |
| `DamageHandler` | `Damage(11)` |
| `LifestealHandler` | `Lifesteal(12)` |
| `ThornsHandler` | `Thorns(13)` |
| `VulnerableHandler` | `Vulnerable(14)` |
| `ExecuteKillHandler` | `ExecuteKill(15)` |
| `HealHandler` | `Heal(16)`、`HealTeam(17)` |
| `DrawHandler` | `Draw(18)` |
| `DiscardHandler` | `Discard(19)` |
| `EnergyHandler` | `GainEnergy(20)` |
| `StunHandler` | `Stun(21)` |
| `SlowHandler` | `Slow(22)` |
| `SilenceHandler` | `Silence(27)` |

### 注册示例（HandlerRegistry.cs）

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
    Register(EffectType.ArmorBreak,      new ArmorBreakHandler());
    Register(EffectType.DamageReduction, new DamageReductionHandler());
    Register(EffectType.Invincible,      new InvincibleHandler());
    Register(EffectType.ArmorOnHit,      new ArmorOnHitHandler());
    Register(EffectType.Weak,            new WeakHandler());

    // Layer 2: 伤害/触发
    Register(EffectType.Damage,          new DamageHandler());
    Register(EffectType.Lifesteal,       new LifestealHandler());
    Register(EffectType.Thorns,          new ThornsHandler());
    Register(EffectType.Vulnerable,      new VulnerableHandler());
    Register(EffectType.ExecuteKill,     new ExecuteKillHandler());

    // Layer 3: 功能/资源
    Register(EffectType.Heal,            new HealHandler());
    Register(EffectType.HealTeam,        new HealHandler());
    Register(EffectType.Draw,            new DrawHandler());
    Register(EffectType.Discard,         new DiscardHandler());
    Register(EffectType.GainEnergy,      new EnergyHandler());
    Register(EffectType.Stun,            new StunHandler());
    Register(EffectType.Slow,            new SlowHandler());
    Register(EffectType.Silence,         new SilenceHandler());
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

### 单个效果执行路径

```
ExecuteEffect(ctx, card, effect, source)
    │
    ├─ HandlerRegistry.TryGet(effect.EffectType, out handler)
    │       ↓ 未注册
    │   ctx.RoundLog.Add("[警告] 未注册的 EffectType: XXX")
    │   return
    │       ↓ 已注册
    ├─ TargetResolver.ResolveForEffect(...)  → 确定本次效果目标
    │
    └─ handler.Execute(card, effect, source, target, ctx)
```

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
    public List<PlayerBattleState> Players { get; set; }

    // ── 待结算卡牌队列 ──
    public List<PlayedCard> PendingPlanCards { get; set; }    // 本回合定策牌
    public List<PlayedCard> PendingCounterCards { get; set; } // 上回合反制牌（在 Layer0 触发）
    public List<PlayedCard> ValidPlanCards { get; set; }      // 有效牌（未被反制）
    public List<PlayedCard> CounteredCards { get; set; }      // 已被反制的牌

    // ── 触发效果队列 ──
    public List<PendingTriggerEffect> PendingTriggerEffects { get; set; }
    public bool HasChainTriggeredThisRound { get; set; }      // 连锁封顶标记

    // ── 场景信息 ──
    public MatchPhase MatchPhase { get; set; }                // 当前比赛阶段

    // ── 日志 ──
    public List<string> RoundLog { get; set; }
}
```

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
1. 在 Protocol/Enums/EffectType.cs 追加枚举值（ID 从当前最大值+1 起）
2. 在 Handlers/ 创建 XxxHandler.cs 实现 IEffectHandler
3. 在 HandlerRegistry.Initialize() 中 Register(EffectType.Xxx, new XxxHandler())
4. 在 ConfigModels/Card/ 相关配置中填写效果参数
5. 更新本文档 Handler 注册表
6. 运行 BattleSimulator 验证效果
```

---

## 📝 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| V4.0 | 2026-02-26 | 彻底移除旧版 100+ 兼容范围；统一 EffectType 为连续 ID（1–28）；CounterHandler 重构为单枚举+条件+标签三维设计；补充 PlayerBattleState 关键字段说明；CardTag 新增 Reflect(32768) |
| V3.1 | 2025-02-25 | 清理旧版 API；统一 EffectType 体系（核心类型 1-10）；完善 Handler 列表 |
| V3.0 | 2024-01 | Handler 模块化重构；PlayerId 改为 string；新增 PlayedCard |
| V2.0 | 2024-01 | 引入 4 层结算栈；TargetResolver 分离 |
| V1.0 | 初始 | 基础结算引擎 |

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 结算规则设计 | [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md) |
| 卡牌系统设计 | [../GameDesign/CardSystem.md](../GameDesign/CardSystem.md) |
| 快速入门 | [QuickStart.md](QuickStart.md) |
| 代码位置 | `Shared/BattleCore/Settlement/` |

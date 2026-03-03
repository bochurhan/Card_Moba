# 卡牌生命周期与 Buff 管理

**文档版本**：V1.0  
**最后更新**：2026-03-03  
**适用对象**：需要理解卡牌执行链路、条件效果、被动机制和 Buff 生命周期的开发者  
**前置阅读**：[BattleCore.md](BattleCore.md)、[SystemArchitecture.md](SystemArchitecture.md)

---

## 概览

一张卡牌从玩家手中打出，到效果最终生效，需要经历三个阶段：

```
┌─────────────────────────────────────────────────────────────────┐
│  阶段①  打出阶段（RoundManager）                                  │
│    验证合法性 → 检查打出条件 → 注册 Passive 触发器 → 加入队列      │
├─────────────────────────────────────────────────────────────────┤
│  阶段②  拦截阶段（TriggerManager）                                │
│    BeforePlayCard 触发器链 → 是否被反制？                         │
├─────────────────────────────────────────────────────────────────┤
│  阶段③  结算阶段（SettlementEngine）                              │
│    Layer 0 筛选 → Layer 1 防御 → Layer 2 伤害 → Layer 3 功能      │
└─────────────────────────────────────────────────────────────────┘
```

---

## 第一章：卡牌效果的执行模式

每个 `CardEffect` 都有一个 `ExecutionMode` 字段，决定它在结算阶段的执行方式：

| 模式 | 枚举值 | 含义 | 典型用例 |
|------|--------|------|---------|
| **Immediate** | 0 | 直接执行，无附加条件 | 普通伤害、护盾、治疗 |
| **Conditional** | 1 | 先评估 `EffectConditions`，满足才执行 | 「观察弱点」—— 若敌方本回合打出伤害牌，获得 2 力量 |
| **Passive** | 2 | 打出时注册触发器，由事件驱动执行 | 「反制」—— 监听 `BeforePlayCard` 并拦截敌方卡牌 |

### 三种模式的执行链路对比

```
┌──────────────────────────────────────────────────────────────────┐
│ Immediate（直接）                                                  │
│                                                                    │
│  PlayCard/CommitPlanCard                                           │
│       │                                                            │
│       ▼                                                            │
│  SettlementEngine.ExecuteEffect                                    │
│       │                                                            │
│       ▼                                                            │
│  HandlerRegistry → Handler.Execute()  ✅ 立即生效                  │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ Conditional（条件）                                                │
│                                                                    │
│  PlayCard/CommitPlanCard                                           │
│       │                                                            │
│       ▼                                                            │
│  SettlementEngine.ExecuteEffect                                    │
│       │                                                            │
│       ├─ EffectConditionChecker.EvaluateAll(EffectConditions)      │
│       │       │                                                    │
│       │       ├─ 条件不满足 → 跳过，记录日志                       │
│       │       └─ 条件满足 → 继续                                   │
│       │                                                            │
│       ▼                                                            │
│  HandlerRegistry → Handler.Execute()  ✅ 条件成立时生效            │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ Passive（被动）                                                    │
│                                                                    │
│  PlayCard/CommitPlanCard                                           │
│       │                                                            │
│       ├─ PassiveHandler.RegisterPassiveEffects()                   │
│       │       └─ TriggerManager.RegisterTrigger(TriggerInstance)   │
│       │                                                            │
│       ▼（定策牌加入队列，或瞬策牌的 Immediate 效果继续结算）         │
│                                                                    │
│  [触发时机到达，如 BeforePlayCard / AfterDealDamage]               │
│       │                                                            │
│       ▼                                                            │
│  TriggerManager.FireTriggers()                                     │
│       └─ TriggerInstance.Effect(tCtx)                              │
│               └─ HandlerRegistry → Handler.Execute() ✅ 事件驱动   │
└──────────────────────────────────────────────────────────────────┘
```

---

## 第二章：打出条件（PlayConditions）

### 2.1 定义

`PlayConditions` 是 `CardConfig` 上的一个列表，表示"能否打出这张牌"的前置门槛。  
它与效果条件（`EffectConditions`）的区别如下：

| | `PlayConditions` | `EffectConditions` |
|--|------------------|-------------------|
| **评估时机** | 打出/提交时，由 `RoundManager` 检查 | 结算时，由 `SettlementEngine.ExecuteEffect` 检查 |
| **失败结果** | 卡牌无法打出，返回错误字符串给客户端 | 该效果跳过，其他效果继续结算 |
| **适用范围** | 整张卡牌 | 单个 `CardEffect` |
| **典型场景** | 「华丽收场」—— 牌库为空才可打出 | 「观察弱点」—— 敌方本回合出过伤害牌才获力量 |

### 2.2 条件类型（EffectConditionType）

| 枚举值 | 说明 | 示例配置 |
|--------|------|---------|
| `SelfDeckEmpty` | 我方牌库为空 | 华丽收场：打出条件 |
| `SelfHandCountLessEq` | 我方手牌数 ≤ N | 背水一战类型 |
| `EnemyHasPlayedDamage` | 敌方本回合已打出伤害牌 | 观察弱点：效果条件 |
| `EnemyHasPlayedDefense` | 敌方本回合已打出防御牌 | 看穿布局类型 |
| `SelfHpBelowPercent` | 我方 HP 低于 N% | 濒死激活类型 |
| `EnemyHpBelowPercent` | 敌方 HP 低于 N% | 处决类型 |
| `SelfHasBuffType` | 我方有指定 Buff | 技能连携类型 |
| `EnemyHasBuffType` | 敌方有指定 Buff | 毒爆发类型 |

### 2.3 执行流程

```
RoundManager.PlayCard(ctx, playerId, handIndex, targetPlayerId)
     │
     ├─ 1. 基础校验（玩家存在、阶段合法、能量足够等）
     │
     ├─ 2. PlayConditions 检查
     │       └─ EffectConditionChecker.EvaluateAll(
     │               card.PlayConditions, playerId, targetPlayerId, ctx)
     │               │
     │               ├─ false → 返回 "错误：不满足打出条件（原因）"
     │               └─ true  → 继续
     │
     ├─ 3. 扣除能量，从手牌移至弃牌堆
     │
     ├─ 4. 注册 Passive 效果（PassiveHandler.RegisterPassiveEffects）
     │
     └─ 5. 立即结算（瞬策）或加入 PendingPlanCards（定策）
```

---

## 第三章：完整生命周期（瞬策牌 vs 定策牌）

### 3.1 瞬策牌（Instant Card）完整流程

```
玩家调用 RoundManager.PlayCard()
│
├─ [阶段①-打出]
│   ├─ 基础合法性校验
│   ├─ PlayConditions 检查（EffectConditionChecker）
│   ├─ 扣除能量，移牌到弃牌堆
│   ├─ 创建 PlayedCard 实例（含 RuntimeId）
│   └─ PassiveHandler.RegisterPassiveEffects()
│           └─ 为每个 Passive 效果注册 TriggerInstance 到 TriggerManager
│
├─ [阶段②-拦截]（SettlementEngine.ResolveInstantCard 内部）
│   └─ TriggerManager.FireTriggers(BeforePlayCard)
│           ├─ 触发已注册的反制触发器（若敌方有反制牌）
│           ├─ cancelled = true → 进入 OnCardCountered 分支，返回 false
│           └─ cancelled = false → 继续
│
└─ [阶段③-结算]
    ├─ 遍历 card.Config.Effects
    │   ├─ ExecutionMode = Passive    → 跳过（已在①注册）
    │   ├─ ExecutionMode = Conditional → EffectConditionChecker → 满足才执行
    │   └─ ExecutionMode = Immediate  → 直接 Handler.Execute()
    │
    └─ TriggerManager.FireTriggers(AfterPlayCard)
```

### 3.2 定策牌（Plan Card）完整流程

```
玩家调用 RoundManager.CommitPlanCard()
│
├─ [阶段①-提交]
│   ├─ 基础合法性校验
│   ├─ PlayConditions 检查
│   ├─ 扣除能量，移牌到弃牌堆
│   ├─ 创建 PlayedCard 实例
│   ├─ PassiveHandler.RegisterPassiveEffects()  ← 提交时立即注册！
│   └─ 加入 ctx.PendingPlanCards

（等待回合结束，EndRound 触发）
│
├─ [阶段②-拦截] Layer 0
│   └─ ResolveLayer0_Counter()
│           ├─ 带 Counter 标签的本回合新牌 → 排除出 ValidPlanCards（下回合生效）
│           └─ 已在 CounteredCards 中的牌 → 排除出 ValidPlanCards（被反制跳过）
│               ↑
│               CounteredCards 由 Passive 触发器在 BeforePlayCard 时机写入
│
└─ [阶段③-结算] Layer 1 → Layer 2 → Layer 3
    └─ 每张 ValidPlanCard 的每个效果经 ExecuteEffect 路由：
        ├─ Passive    → 跳过
        ├─ Conditional → 条件评估 → 满足才执行
        └─ Immediate  → Handler.Execute()
```

### 3.3 关键时序图

```
时间轴  →→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→→
        操作期                    │  结算期
        ──────────────────────────┼──────────────────────────
Player  提交定策牌A               │
        │ PassiveHandler注册触发器 │
        │   (Counter/Passive效果) │
        │                         │  Layer0：筛选ValidPlanCards
        │ 提交定策牌B              │           ↑ Passive触发器在此前已执行
        │                         │  Layer1：护盾/力量效果
        │                         │  Layer2：伤害结算
        │                         │    ↓ AfterDealDamage 触发
        │                         │    ↓ AfterTakeDamage 触发
        │                         │  Layer3：控制/资源效果
        ──────────────────────────┼──────────────────────────
```

---

## 第四章：条件检查器（EffectConditionChecker）

`EffectConditionChecker` 是**无状态静态类**，负责评估 `EffectCondition` 列表。

### 4.1 核心签名

```csharp
// 评估单个条件（内部使用）
public static bool Evaluate(
    EffectCondition condition,
    string selfPlayerId,
    string? targetPlayerId,
    BattleContext ctx)

// 评估整个条件列表（AND 语义：所有条件都满足才返回 true）
public static bool EvaluateAll(
    List<EffectCondition> conditions,
    string selfPlayerId,
    string? targetPlayerId,
    BattleContext ctx)
```

### 4.2 "敌方本回合是否打出伤害牌"的实现

以「观察弱点」为例，条件类型为 `EnemyHasPlayedDamage`：

```csharp
case EffectConditionType.EnemyHasPlayedDamage:
    // 在 ValidPlanCards（已通过 Layer0 筛选的有效牌）中查找
    // 敌方打出的带 Damage 标签的牌
    foreach (var card in ctx.ValidPlanCards)
    {
        if (card.SourcePlayerId != selfPlayerId
            && card.Config.HasTag(CardTag.Damage))
            return true;
    }
    return false;
```

> **注意**：`ValidPlanCards` 是 Layer0 筛选后的结果，已排除被反制的牌。  
> 「观察弱点」是定策牌，在 Layer1 结算（属于防御层，`GetSettlementLayer() == 1`），  
> 此时 Layer0 已执行完毕，`ValidPlanCards` 已稳定。

### 4.3 条件的 Negate（取反）支持

每个 `EffectCondition` 都有 `Negate` 字段，支持"不满足时触发"：

```csharp
// 配置示例：若敌方本回合【没有】打出伤害牌，才获得效果
var condition = new EffectCondition
{
    ConditionType = EffectConditionType.EnemyHasPlayedDamage,
    Negate = true   // 取反
};
```

---

## 第五章：被动系统（PassiveHandler）

### 5.1 设计原则

Passive 效果的本质是"打出一张牌，注册一个短暂的事件监听器"。  
它与 Buff 触发器的区别：

| | Passive 效果触发器 | Buff 触发器 |
|--|-------------------|------------|
| **归属来源** | `TriggerSourceType.Card` | `TriggerSourceType.Buff` |
| **生命周期** | 由 `RemainingRounds` 控制（通常 = 1 回合） | 由 `BuffInstance` 生命周期控制 |
| **注册入口** | `PassiveHandler.RegisterPassiveEffects` | `BuffManager.RegisterBuffTriggers` |
| **典型场景** | 反制牌、本回合条件被动 | 吸血、荆棘、持续增益 |

### 5.2 PassiveHandler 注册流程

```csharp
// PassiveHandler.RegisterSinglePassiveEffect() 核心逻辑
var trigger = new TriggerInstance
{
    TriggerName       = $"[Passive]{card.CardName}-{effect.EffectType}",
    Timing            = (TriggerTiming)effect.PassiveTriggerTiming, // 效果配置的触发时机
    OwnerPlayerId     = sourcePlayerId,
    SourceId          = $"passive_{card.RuntimeId}_{effect.EffectType}",
    SourceType        = TriggerSourceType.Card,
    Priority          = effect.Priority,
    RemainingTriggers = -1,                       // 有效期内可无限次触发
    RemainingRounds   = effect.PassiveDuration,   // 通常 = 1（本回合结束清除）

    // 条件：只响应自身玩家触发的事件
    Condition = tCtx => tCtx.SourcePlayerId == sourcePlayerId,

    // 执行：路由到对应的 Handler
    Effect = tCtx => ExecutePassiveTrigger(card, effect, tCtx, ctx)
};

ctx.TriggerManager.RegisterTrigger(trigger);
```

### 5.3 反制牌的完整执行路径

反制牌（`CardTag.Counter` + `EffectType.Counter` + `ExecutionMode.Passive`）：

```
玩家 A 提交反制牌
    │
    ├─ PlayConditions 检查（如有）
    │
    ├─ PassiveHandler.RegisterPassiveEffects()
    │       └─ 注册 TriggerInstance{
    │               Timing = BeforePlayCard,      ← 监听对方打牌事件
    │               RemainingRounds = 1,           ← 仅本回合有效
    │               Condition = 对方玩家出牌且符合筛选条件,
    │               Effect = 将目标牌加入 ctx.CounteredCards
    │           }
    │
    └─ 加入 PendingPlanCards（带 Counter 标签，Layer0 会排除出 ValidPlanCards）

玩家 B 提交伤害牌
    │
    └─（此时触发器尚未激活，等待结算期 Layer0）

结算期 Layer0
    │
    ├─ [反制触发器激活] TriggerManager.FireTriggers(BeforePlayCard, ...)
    │       └─ CounterHandler 执行：将玩家 B 的伤害牌加入 ctx.CounteredCards
    │
    └─ ResolveLayer0_Counter() 筛选 ValidPlanCards
            ├─ 玩家 A 的反制牌（Counter 标签）→ 排除
            └─ 玩家 B 的伤害牌（在 CounteredCards 中）→ 排除
```

> ⚠️ **注意**：`BeforePlayCard` 对于定策牌的触发时机是 Layer0 入口处，  
> 而非玩家"提交"时。这确保了反制牌在看到本回合所有定策牌之后才做判断。

---

## 第六章：Buff 管理模式

### 6.1 Buff 系统总览

```
                ┌──────────────┐
    卡牌效果 ──→│  BuffManager  │──→ ctx.TriggerManager
                └──────┬───────┘
                       │ 持有
                ┌──────▼───────┐
                │ BuffInstance │ （每个激活的 Buff 实例）
                │  ・RuntimeId  │
                │  ・BuffType   │
                │  ・Value/     │
                │    Stacks    │
                │  ・Duration  │
                └──────────────┘
```

### 6.2 BuffManager 核心职责

`BuffManager` 是每个玩家独立持有的 Buff 管理器，负责：

1. **添加 Buff** → 同时向 `TriggerManager` 注册触发器
2. **移除 Buff** → 同时从 `TriggerManager` 注销触发器
3. **回合衰减** → 由 `BattleContext.OnRoundEnd()` 统一调用
4. **孤儿清理** → 与 `TriggerManager.ValidateOrphanTriggers()` 配合

### 6.3 核心方法签名

```csharp
// 添加 Buff（自动注册关联触发器）
BuffInstance AddBuff(
    BuffConfig config,
    string sourcePlayerId,
    int? value = null,
    int? duration = null)

// 按 RuntimeId 移除单个 Buff（自动注销触发器）
bool RemoveBuffByRuntimeId(string runtimeId)

// 按类型批量移除（自动注销触发器）
int RemoveBuffsByType(BuffType buffType)

// 查询
bool HasBuffType(BuffType buffType)
int  GetTotalStacks(BuffType buffType)
List<BuffInstance> GetBuffsByType(BuffType buffType)
```

### 6.4 Buff 添加时的完整链路

```
BuffManager.AddBuff(config, sourcePlayerId, value, duration)
    │
    ├─ 1. 创建 BuffInstance（分配 RuntimeId = "buff_{playerId}_{序号}"）
    │
    ├─ 2. 根据 BuffStackRule 处理叠加逻辑
    │       ├─ Stack    → 找到同类型 Buff，叠加 Stacks，刷新 Duration
    │       ├─ Refresh  → 找到同类型 Buff，只刷新 Duration
    │       └─ New      → 每次都创建新 BuffInstance
    │
    ├─ 3. 将 BuffInstance 加入 _buffs 列表
    │
    └─ 4. RegisterBuffTriggers(buffInstance)
              └─ 根据 BuffType 注册对应触发器，例如：
                  ・Lifesteal → AfterDealDamage → 按比例回血
                  ・Thorns    → AfterTakeDamage → 反弹固定伤害
                  ・Poison    → OnRoundEnd      → 每回合扣血
```

### 6.5 Buff 的生命周期管理

```
回合流转时序：

EndRound()
    └─ ctx.OnRoundEnd()
            └─ 遍历所有 BuffManager
                    └─ TickDuration()
                            ├─ Duration > 0 → Duration--
                            └─ Duration == 0 → 移除 Buff（自动注销触发器）

BeginNextRound()
    └─ ctx.ClearRoundData()（清空回合临时数据）
    └─ ctx.OnRoundStart()
            └─ 触发 OnRoundStart 时机的触发器（如毒/持续伤害）

孤儿触发器清理（防御性）：
    └─ BattleContext.CollectActiveBuffRuntimeIds()
            └─ TriggerManager.ValidateOrphanTriggers()
                    └─ 移除 SourceType=Buff 但 RuntimeId 不在活跃列表中的触发器
```

### 6.6 Buff 触发器注册模板

```csharp
// 在 BuffManager.RegisterBuffTriggers 中，以吸血为例：
ctx.TriggerManager.RegisterTrigger(new TriggerInstance
{
    TriggerName       = $"吸血-AfterDealDamage-{buff.RuntimeId}",
    Timing            = TriggerTiming.AfterDealDamage,
    OwnerPlayerId     = buff.OwnerPlayerId,
    SourceId          = buff.RuntimeId,          // ← 关键：用 RuntimeId 绑定生命周期
    SourceType        = TriggerSourceType.Buff,
    Priority          = 100,                     // 增益效果区间
    RemainingTriggers = -1,                      // 无限次触发
    RemainingRounds   = -1,                      // 跟随 Buff 生命周期（不独立衰减）

    Condition = tCtx => tCtx.SourcePlayerId == buff.OwnerPlayerId,

    Effect = tCtx =>
    {
        int healAmt = tCtx.Value * buff.Value / 100;  // 整数运算，禁止 float
        DamageHelper.ApplyHeal(tCtx.BattleContext,
            buff.OwnerPlayerId, buff.OwnerPlayerId, healAmt, "吸血");
    }
});
```

### 6.7 Buff 与 Passive 触发器的对比

```
              ┌─────────────────────────────────────────────────────┐
              │              TriggerManager                         │
              │                                                     │
              │   ┌─── SourceType = Buff ──────────────────────┐   │
              │   │  触发器 A: 吸血-AfterDealDamage             │   │
              │   │  触发器 B: 荆棘-AfterTakeDamage             │   │
              │   │  RemainingRounds = -1（跟随 Buff 清理）      │   │
              │   └────────────────────────────────────────────┘   │
              │                                                     │
              │   ┌─── SourceType = Card ──────────────────────┐   │
              │   │  触发器 C: [Passive]反制牌-Counter          │   │
              │   │  触发器 D: [Passive]观察弱点-AttackBuff     │   │
              │   │  RemainingRounds = 1（回合结束自动清除）     │   │
              │   └────────────────────────────────────────────┘   │
              │                                                     │
              └─────────────────────────────────────────────────────┘
```

**清理机制**：
- `SourceType.Buff` 触发器：随 `BuffManager.RemoveBuffByRuntimeId()` 一起注销
- `SourceType.Card` 触发器：随 `RemainingRounds` 倒计时到 0 自动失效，或通过孤儿清理移除

---

## 第七章：常见卡牌配置示例

### 7.1 普通伤害牌（Immediate）

```json
{
  "CardName": "直拳",
  "TrackType": "Plan",
  "EnergyCost": 1,
  "PlayConditions": [],
  "Effects": [
    {
      "EffectType": "Damage",
      "ExecutionMode": "Immediate",
      "Value": 6,
      "EffectConditions": []
    }
  ]
}
```

执行路径：`CommitPlanCard → Layer2.ExecuteEffect → DamageHandler.Execute`

---

### 7.2 条件增益牌（Conditional）

```json
{
  "CardName": "观察弱点",
  "TrackType": "Plan",
  "EnergyCost": 1,
  "PlayConditions": [],
  "Effects": [
    {
      "EffectType": "AttackBuff",
      "ExecutionMode": "Conditional",
      "Value": 2,
      "EffectConditions": [
        { "ConditionType": "EnemyHasPlayedDamage", "Negate": false }
      ]
    }
  ]
}
```

执行路径：  
`CommitPlanCard → Layer1.ExecuteEffect → ConditionChecker(EnemyHasPlayedDamage) → [满足] → StrengthHandler.Execute`

---

### 7.3 打出条件限制牌（PlayCondition + Immediate）

```json
{
  "CardName": "华丽收场",
  "TrackType": "Plan",
  "EnergyCost": 2,
  "PlayConditions": [
    { "ConditionType": "SelfDeckEmpty", "Negate": false }
  ],
  "Effects": [
    {
      "EffectType": "Damage",
      "ExecutionMode": "Immediate",
      "Value": 10,
      "EffectConditions": []
    }
  ]
}
```

执行路径：  
`CommitPlanCard → PlayConditionChecker(SelfDeckEmpty) → [牌库为空才可继续] → Layer2 → DamageHandler.Execute`

---

### 7.4 反制牌（Passive）

```json
{
  "CardName": "格挡",
  "TrackType": "Plan",
  "EnergyCost": 2,
  "Tags": ["Counter"],
  "PlayConditions": [],
  "Effects": [
    {
      "EffectType": "Counter",
      "ExecutionMode": "Passive",
      "PassiveTriggerTiming": "BeforePlayCard",
      "PassiveDuration": 1,
      "EffectConditions": [
        { "ConditionType": "EnemyHasPlayedDamage", "Negate": false }
      ]
    }
  ]
}
```

执行路径：  
`CommitPlanCard → PassiveHandler.RegisterPassiveEffects → TriggerManager.RegisterTrigger`  
→ 结算期 Layer0 触发 `BeforePlayCard` → `CounterHandler` 将敌方伤害牌加入 `CounteredCards`

---

## 第八章：开发清单与注意事项

### 8.1 新增卡牌效果的步骤

```
1. 确定 ExecutionMode：Immediate / Conditional / Passive？
2. 若是 Conditional：在 EffectConditionType 枚举添加条件类型，
                    在 EffectConditionChecker.Evaluate() 添加对应分支
3. 若是 Passive：确认需要监听的 TriggerTiming，
                在 CardEffect 的 PassiveTriggerTiming 字段填写
4. 实现或复用 IEffectHandler，注册到 HandlerRegistry
5. 在配表（Config/Excel/Cards.csv）中填写新字段
6. 运行 Tools/ExcelConverter/convert.bat 生成 JSON
7. 更新本文档的"常见卡牌配置示例"章节
```

### 8.2 常见错误与避免方式

| ❌ 错误做法 | ✅ 正确做法 |
|------------|------------|
| 在 `SettlementEngine` 里为特定卡牌名称写 `if` 分支 | 使用 `ExecutionMode` + `EffectConditions` 配置驱动 |
| 在 Passive 触发器的 `Effect` lambda 中捕获 `ctx` 闭包后修改局部变量 | 通过 `tCtx.BattleContext` 访问上下文（避免闭包陷阱） |
| 手动向 `TriggerManager` 注册 Buff 触发器（绕过 `BuffManager`） | 通过 `BuffManager.AddBuff()` 统一添加，自动处理触发器 |
| 在 `EffectConditionChecker` 中访问 `PendingPlanCards`（Layer0 前） | 使用 `ValidPlanCards`（Layer0 后，已稳定） |
| 在 Handler 中存储私有状态字段 | Handler 是无状态单例，状态全部写入 `BattleContext` |
| 使用浮点数计算伤害或治疗量 | 全部使用整数运算（`value * percent / 100`） |

### 8.3 调试技巧

- `ctx.RoundLog` 记录了每个阶段的详细日志，包括条件评估结果
- Passive 触发器注册时会输出：`[PassiveHandler] 玩家X的「Y」注册了 Passive 触发器（Z，持续N回合）`
- 条件评估失败时会输出：`[Settlement] 「卡牌名」效果 EffectType 条件不满足，跳过`
- 反制成功时会输出：`[Layer0] 「被反制牌」已被反制，跳过结算`

---

## 关联文档

| 主题 | 文档 |
|------|------|
| 结算引擎核心 | [BattleCore.md](BattleCore.md) |
| 系统架构总览 | [SystemArchitecture.md](SystemArchitecture.md) |
| 结算规则设计 | [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md) |
| 配置系统说明 | [ConfigSystem.md](ConfigSystem.md) |
| 快速入门 | [QuickStart.md](QuickStart.md) |

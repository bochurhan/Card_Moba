# ⚠️ BattleCore 系统架构关系图（V1 归档）

> **本文档已归档**（2026-03-04）。BattleCore V2 重构已启动，请参阅：
> - 新架构总览：[SystemArchitecture_V2.md](SystemArchitecture_V2.md) ← **当前有效文档**
> - 重构计划：[../Planning/BattleCoreRefactorPlan.md](../Planning/BattleCoreRefactorPlan.md)
>
> 本文档保留供历史参考，不再更新维护。

**文档版本**：V1.1（最终版，已停止维护）  
**最后更新**：2026-03-02  
**适用对象**：初次接触本项目战斗系统的开发者  
**下一步阅读**：[BattleCore.md](BattleCore.md)（代码级细节）、[../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md)（游戏规则）  
**阅读时间**：10 分钟

---

## 一句话定位

> `RoundManager` 是**导演**，`BattleContext` 是**舞台**，  
> `SettlementEngine + 4层Layer` 是**核心剧本**，  
> `Buff / Trigger / Event` 是挂在舞台上的**三个副系统**，  
> 它们全部通过 `BattleContext` 互相感知。

---

## 分层架构总览

```
┌─────────────────────────────────────────────────────────────┐
│                     RoundManager                            │
│  【导演层】驱动整局生命周期，管理7个阶段                        │
│  InitBattle → PlayCard / CommitPlanCard → EndRound          │
│            → BeginNextRound → ...                           │
└──────────────────────────┬──────────────────────────────────┘
                           │ 持有并读写
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    BattleContext                             │
│  【唯一状态容器 / 舞台】所有可变数据在此集中存储               │
│                                                             │
│  Players[]          PendingPlanCards    RoundLog            │
│  ValidPlanCards     CounteredCards      PendingCounterCards │
│  HistoryLog         MatchPhase          SeededRandom        │
│                                                             │
│  内嵌三个副系统的引用：                                       │
│   ├── TriggerManager   （触发系统）                          │
│   ├── EventRecorder    （事件系统）                          │
│   └── BuffManager[]    （Buff系统，按 PlayerId 索引）         │
└──────────┬──────────────────────┬───────────────────────────┘
           │ 传入 ctx              │ 传入 ctx
           ▼                      ▼
┌──────────────────┐   ┌──────────────────────────────────────┐
│ SettlementEngine │   │  三个副系统（均以 ctx 作为唯一入参）   │
│  【核心剧本层】   │   │                                       │
│                  │   │ ┌──────────────────────────────────┐  │
│ ResolveInstant   │   │ │  TriggerManager                  │  │
│  └─ HandlerRegis-│   │ │  注册触发器（如：受击时回血）      │  │
│     try.Execute  │   │ │  FireTriggers(ctx, timing)       │  │
│                  │   │ │  在 OnRoundStart/End 衰减         │  │
│ ResolvePlanCards │   │ └──────────────────────────────────┘  │
│  ├─ Layer 0      │   │                                       │
│  │  Counter      │   │ ┌──────────────────────────────────┐  │
│  ├─ Layer 1      │   │ │  BuffManager (per player)        │  │
│  │  Shield/Armor │   │ │  AddBuff / RemoveBuff            │  │
│  ├─ Layer 2      │   │ │  OnRoundStart/End → 叠层衰减     │  │
│  │  Damage       │   │ │  持久状态修正（非瞬时效果）       │  │
│  │  +Triggers    │   │ └──────────────────────────────────┘  │
│  └─ Layer 3      │   │                                       │
│     Heal/Stun    │   │ ┌──────────────────────────────────┐  │
│     Draw/Silence │   │ │  BattleEventRecorder             │  │
│                  │   │ │  Record(BattleEventType)         │  │
│ 每个 Layer 的     │   │ │  只记录，不影响结算               │  │
│ 实际执行单元：    │   │ │  用于回放 / UI 动画 / 日志        │  │
│  IEffectHandler  │   │ └──────────────────────────────────┘  │
│  HandlerRegistry │   │                                       │
└──────────────────┘   └──────────────────────────────────────┘
```

---

## 关键调用链（时序）

### 回合开始

```
RoundManager.BeginNextRound(ctx)
    │
    ├─ ctx.ClearRoundData()
    │     └─ 反制牌归档，清空上回合临时数据
    │
    ├─ ctx.OnRoundStart()
    │     ├─ EventRecorder.RecordRoundStart()          ← Event 系统
    │     ├─ TriggerManager.FireTriggers(OnRoundStart) ← Trigger 系统
    │     └─ BuffManager.OnRoundStart(ctx)             ← Buff 系统（叠层衰减）
    │
    └─ 发牌 / 能量回满
```

### 打出瞬策牌

```
RoundManager.PlayCard(ctx, card)
    └─ SettlementEngine.ResolveInstantCard(ctx, card)
            ├─ TargetResolver.Resolve()
            ├─ TriggerManager.FireTriggers(BeforePlayCard)
            ├─ HandlerRegistry → IEffectHandler.Execute(ctx, card)
            │     ↑ 对应 EffectType 1-28 中的某一个 Handler
            └─ TriggerManager.FireTriggers(AfterPlayCard)
```

### 回合结束（定策牌统一结算）

```
RoundManager.EndRound(ctx)
    └─ SettlementEngine.ResolvePlanCards(ctx)
            │
            ├─ Layer 0: CounterHandler
            │     └─ 标记被反制的牌，写入 ctx.CounteredCards
            │
            ├─ Layer 1: ShieldHandler / ArmorHandler
            │     └─ 读取 BuffManager 的防御层数，修正即将受到的伤害
            │
            ├─ Layer 2: DamageHandler（三阶段 A/B/C 批量结算）
            │     ├─ 阶段A：只读收集所有伤害，不修改状态
            │     ├─ 阶段B：批量写入护盾/HP 变化量
            │     └─ 阶段C：统一触发 AfterDealDamage / AfterTakeDamage
            │           └─ 吸血/反伤由 BuffManager 注册的 TriggerInstance 在此响应
            │
            └─ Layer 3: HealHandler / StunHandler / DrawHandler / SilenceHandler
                  └─ 写入 BuffManager 或直接修改 PlayerBattleState
```

---

## 三个副系统的职责边界

| 系统 | 写入时机 | 读取时机 | 与 4层Layer 的关系 |
|------|---------|---------|------------------|
| **Buff 系统** | Handler 执行时追加 Buff（如 `StunHandler` 调 `BuffManager.AddBuff`） | `OnRoundStart/End` 衰减；Layer 1 结算时读取 Vulnerable/Weak 层数 | Layer 1/2/3 的 Handler 可追加或消耗 Buff；**Buff 本身不主动触发结算** |
| **Trigger 系统** | Buff 添加时由 `BuffManager.RegisterBuffTriggers` 向 `TriggerManager` 注册触发器 | `FireTriggers` 读取注册列表，执行回调 | Layer 2 阶段C `FireTriggers(AfterDealDamage/AfterTakeDamage)` 统一触发；回合开始/结束也会 Fire |
| **Event 系统** | 回合开始/结束、战斗开始/结束等节点由 `BattleContext` 写入 | 客户端读取，播放 UI 动画和回放 | **完全被动，只记录，不参与任何结算计算** |

---

## Buff / Trigger 协作架构（定稿）

> 本节记录经过架构讨论后确定的 Buff + Trigger 协作模型，
> 当前代码尚未完全按此模型实现，重构规划见 [TODO.md](../Planning/TODO.md) TD-04。

### 核心原则

**Buff = 数据（状态） + 触发声明**  
**Trigger = 事件总线（驱动回调执行）**

```
PlayerBattleState.ActiveBuffs    ← Buff 数据住在玩家状态里（序列化友好）
        │
        │  每个 BuffInstance 声明：
        │    - 状态数据（Value, Duration, BuffType...）
        │    - TriggerTiming（我在哪个时机生效）
        ▼
TriggerSystem.Fire(timing, event)
        │
        │  当 Buff 被添加时 → 向 TriggerSystem 注册回调
        │  当 Buff 被移除时 → 从 TriggerSystem 注销回调
        │  当时机到达时    → TriggerSystem 遍历回调列表执行
        ▼
BuffHandlerTable[BuffType] → 静态处理函数（无状态，纯函数）
```

### 三层职责划分

| 层 | 职责 | 不该做什么 |
|----|------|-----------|
| **PlayerBattleState.ActiveBuffs** | 存储 Buff 数据，作为序列化单元 | 不包含任何逻辑函数 |
| **BuffManager（薄中介）** | 操作 ActiveBuffs 的 CRUD；AddBuff 时向 TriggerSystem 注册，RemoveBuff 时注销；回合衰减 | 不自己执行触发逻辑（不直接操作 HP/护甲） |
| **TriggerSystem** | 维护 Fire 节点；广播事件；按 BuffType 分派静态处理函数 | 不存储任何状态 |

### Fire 节点清单（固定，约 10-15 个）

所有触发时机均由以下节点的 `Fire()` 调用驱动，**新增 Buff / 卡牌效果不需要增加新节点**：

```
RoundStateMachine：
  OnRoundStart        — 回合开始（Buff衰减、回合开始触发）
  OnRoundEnd          — 回合结束（回合结束触发）
  OnCommandLock       — 指令锁定（可用于"出牌后"触发）

SettlementEngine：
  OnDamageTaken       — 扣血完成后（反伤/吸血/受击获甲）
  OnDamageDealt       — 造成伤害后（吸血治疗）
  OnHeal              — 治疗完成后
  OnArmorBroken       — 护甲被击穿后
  BeforePlayCard      — 打出卡牌前（拦截/沉默判定）
  AfterPlayCard       — 打出卡牌后

BuffManager：
  OnBuffAdded         — Buff 被施加时（叠加触发）
  OnBuffRemoved       — Buff 被移除时（移除触发）
```

### 回调方案：BuffType → 静态处理函数（方案 A）

```csharp
// TriggerSystem 维护一张分派表
static Dictionary<BuffType, Action<BuffInstance, TriggerEvent, BattleContext>> BuffHandlerTable;

// 触发时执行：
foreach (var buff in player.ActiveBuffs.Where(b => b.TriggerTiming == timing))
{
    if (BuffHandlerTable.TryGetValue(buff.BuffType, out var handler))
        handler(buff, evt, ctx);
}
```

**选择此方案而非 delegate 实例的原因**：`BuffInstance` 必须是可序列化的纯数据（Client/Server 同步），函数指针无法序列化。

### 递归触发保护

触发回调内部可能再次触发事件（例：反伤 → 再次 OnDamageTaken → 再次反伤），需限制触发深度：

```csharp
private int _triggerDepth = 0;
private const int MaxTriggerDepth = 8;  // 最多嵌套8层，超出截断并记录警告

public void Fire(TriggerTiming timing, TriggerEvent evt)
{
    if (_triggerDepth >= MaxTriggerDepth) { /* 记录警告，截断 */ return; }
    _triggerDepth++;
    // ... 执行回调
    _triggerDepth--;
}
```

---

## 数据流方向（一句话）

```
RoundManager 驱动时序
    → SettlementEngine 执行核心计算
        → 写入 BattleContext
            → Buff / Trigger 作为附加响应层挂在时序节点上
                → Event 被动记录全程
```

---

## 各系统文件位置速查

| 系统 | 目录 | 关键文件 |
|------|------|---------|
| RoundManager | `Shared/BattleCore/RoundStateMachine/` | `RoundManager.cs` |
| BattleContext | `Shared/BattleCore/Context/` | `BattleContext.cs`、`PlayerBattleState.cs` |
| SettlementEngine | `Shared/BattleCore/Settlement/` | `SettlementEngine.cs`、`DamageHelper.cs` |
| Handler 模块 | `Shared/BattleCore/Settlement/Handlers/` | `*Handler.cs`（每种 EffectType 一个文件） |
| Buff 系统 | `Shared/BattleCore/Buff/` | `BuffManager.cs`、`BuffInstance.cs` |
| Trigger 系统 | `Shared/BattleCore/Trigger/` | `TriggerManager.cs`、`TriggerTypes.cs` |
| Event 系统 | `Shared/BattleCore/Event/` | `BattleEvent.cs`、`BattleEventRecorder.cs` |
| EffectType 枚举 | `Shared/Protocol/Enums/` | `EffectType.cs`（ID 1–28，4层对应）|

---

## 当前已知集成缺口（待修复）

以下是现阶段系统集成不完整的地方，按优先级排列：

| # | 问题 | 影响 | 建议修复方向 | 优先级 |
|---|------|------|------------|--------|
| 1 | DOT（Poison/Burn/Bleed）触发器直接写 `_owner.Hp -= dmg`，绕过 `DamageHelper` | 无敌/护盾/减免对 DOT 全部失效，复活 Buff 不响应 DOT 致死 | 改为调用 `DamageHelper.ApplyDot()`（详见 TODO.md R-02） | 🔴 P0 |
| 2 | `BuffManager.AddBuff` 叠加时 `ApplyBuffModifiers` 使用全量值而非差值，数值型 Buff 叠加翻倍 | Armor/AttackBuff 等属性型 Buff 叠加值错误 | 叠加时先 `RemoveBuffModifiers(旧值)` 再 `ApplyBuffModifiers(新值)` | 🟡 P1 |
| 3 | `BattleEventRecorder` 只记录回合级事件，Handler 执行时未写入细粒度事件 | 客户端无法制作卡牌技能动画 | 在每个 Handler 的 `Execute()` 末尾调用 `ctx.EventRecorder.Record(...)` | 🟡 P2 |

---

*本文档描述架构关系，不涉及具体结算规则。  
具体规则见 [GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md)，  
具体代码结构见 [TechGuide/BattleCore.md](BattleCore.md)。*

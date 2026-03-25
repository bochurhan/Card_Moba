# BattleCore V2 系统架构总览

**文档版本**：V2.0  
**创建日期**：2026-03-04  
**适用对象**：参与 BattleCore V2 开发的所有开发者  
**阅读时间**：15 分钟  
**关联文档**：
- 重构计划：[../Planning/BattleCoreRefactorPlan.md](../Planning/BattleCoreRefactorPlan.md)
- 设计源文档：[../GameDesign/新框架.md](../GameDesign/新框架.md)
- V1 归档：[../_Archive/v1_battlecore/SystemArchitecture_V1.md](../_Archive/v1_battlecore/SystemArchitecture_V1.md)

---

## 一句话定位

> `RoundManager`（导演）驱动 `SettlementEngine`（结算引擎）执行效果，  
> 触发器通过 `PendingEffectQueue`（延迟队列）解耦，  
> `EventBus` 向外广播，`TriggerManager` 向内响应，  
> 所有状态统一存储在 `BattleContext`（唯一真相源）。

---

## 二、分层架构总览

```
┌──────────────────────────────────────────────────────────────────┐
│                         接入层                                    │
│  玩家操作输入 │ 客户端 UI 表现 │ 战斗回放 │ 手动选目标交互           │
└─────────────────────────────┬────────────────────────────────────┘
                              │ 调用
┌─────────────────────────────▼────────────────────────────────────┐
│                        调度层                                     │
│  RoundManager（回合状态机）                                        │
│  SettlementEngine（效果结算引擎，含 PendingEffectQueue 调度）       │
│  EventBus（战斗事件总线）                                          │
└──────────┬──────────────────┬────────────────────────────────────┘
           │ 调用              │ 订阅/发布
┌──────────▼──────────┐  ┌────▼───────────────────────────────────┐
│      业务层          │  │         外部消费层                       │
│  CardManager        │  │  StatManager（统计）                    │
│  BuffManager        │  │  LogManager（日志）                     │
│  TriggerManager     │  │  Client UI（动画/表现）                  │
│  ValueModifierMgr   │  └────────────────────────────────────────┘
└──────────┬──────────┘
           │ 读写
┌──────────▼──────────────────────────────────────────────────────┐
│                       基础能力层                                  │
│  BattleContext（唯一状态容器）                                     │
│  HandlerPool（效果 Handler 注册/分发）                             │
│  TargetResolver（目标解析，只读）                                  │
│  ConditionChecker（条件判断，只读）                                │
│  DynamicParamResolver（动态参数解析，只读）                        │
│  SeededRandom（确定性随机）                                       │
└──────────┬──────────────────────────────────────────────────────┘
           │ 引用
┌──────────▼──────────────────────────────────────────────────────┐
│                        数据层（Foundation）                       │
│  Entity │ BattleCard │ EffectUnit │ TriggerUnit │ BuffUnit        │
│  CardZone │ EffectType │ TriggerTiming（纯数据/枚举，无依赖）      │
└─────────────────────────────────────────────────────────────────┘
```

**依赖方向**：单向向下，禁止下层引用上层。

---

## 三、核心模块职责边界

### 3.1 调度层

#### RoundManager（导演）

```
职责：驱动回合生命周期，仅负责"何时"调用，不负责"如何"结算
├─ BeginRound()  → 发牌、能量、回合开始触发
├─ EndRound()    → 状态牌扫描、定策结算、Buff 衰减
└─ 不持有任何结算逻辑，只调用 SettlementEngine 和 CardManager
```

#### SettlementEngine（结算引擎）

```
职责：执行效果原子（EffectUnit），管理 PendingEffectQueue
├─ ResolveInstant(card)     → 瞬策牌即时结算
├─ ResolvePlanCards()       → 定策牌五层批量结算
│    ├─ Layer 0: 反制层
│    ├─ Layer 1: 防御/修正层
│    ├─ Pre-Layer 2: TakeDefenseSnapshots()  ← 防御快照拍摄
│    ├─ Layer 2: 伤害层（以快照值为基准计算防御）
│    ├─ Post-Layer 2: ClearDefenseSnapshots()
│    ├─ Layer 3: 资源/功能层
│    └─ Layer 4: Buff/特殊层
└─ DrainPendingQueue()      → 循环消化延迟效果队列
                               （每次主结算后调用，执行栈始终平坦）
```

**关键约束**：`SettlementEngine` 是唯一可写 `BattleContext` 的入口（Handler 内部通过 `ctx` 写入，Handler 由 `SettlementEngine` 调用）。

**防御快照语义**：Layer 2 开始前为每位玩家拍摄 `DefenseSnapshot`（Shield / Armor / IsInvincible）。伤害 Handler 读快照值计算防御吸收，并同步递减快照和实时值（防止同层多次命中重复消费护盾）。Layer 2 期间动态生成的护盾（如 `AfterTakeDamage` 触发）写入实时值但不更新快照，因此**不参与本回合防御，下回合才生效**。详见 [SettlementRules.md §Layer2 快照隔离约定](../GameDesign/SettlementRules.md)。

#### PendingEffectQueue（延迟队列）—— 解耦核心

```
问题：TriggerManager 响应伤害后，需要触发吸血/荆棘等子效果
旧方案：TriggerManager 直接调用 SettlementEngine → 双向调用，栈深不可控
新方案：TriggerManager 向 PendingQueue 推入 EffectUnit，SettlementEngine 主流程结束后统一消化

数据流：
SettlementEngine.ResolveDamage(A打B, 10)
  ├─ 写入：B.Hp -= 10
  ├─ 触发检查：TriggerManager.Fire(AfterTakeDamage)
  │             └─ 荆棘响应 → PendingQueue.Enqueue(反弹3伤害)
  │             └─ 吸血响应 → PendingQueue.Enqueue(回血5)
  └─ [当前结算栈帧结束]

DrainPendingQueue():
  └─ Dequeue → SettlementEngine.Resolve(反弹3) → 全新栈帧
  └─ Dequeue → SettlementEngine.Resolve(回血5)  → 全新栈帧
```

---

### 3.2 业务层

#### CardManager（卡牌区域管理）

```
职责：管理所有 BattleCard 实例的生命周期和区域流转
├─ InitBattleDeck()     → 战斗开始，卡组配置 × count → BattleCard 实例
├─ DrawCard()           → Deck → Hand
├─ DiscardCard()        → 任意区域 → Discard
├─ CommitPlanCard()     → Hand → StrategyZone
├─ ScanStatCards()      → 回合结束扫描 StatZone，触发状态牌效果
├─ DestroyTempCards()   → 结算后清理 tempCard=true 的牌
└─ MoveCard(card, zone) → 通用区域移动（不含逻辑，只移动）
```

**不含**：任何结算逻辑，卡牌区域移动触发的效果通过 `EventBus` 广播。

#### BuffManager（薄中介）

```
职责：BuffUnit 的 CRUD 操作 + 向 TriggerManager 注册/注销触发器
├─ AddBuff(config, owner, value, duration)
│    └─ 创建 BuffUnit → 存入 PlayerData.ActiveBuffs
│    └─ 向 TriggerManager 注册对应触发器
├─ RemoveBuff(buffId)
│    └─ 从 PlayerData.ActiveBuffs 移除
│    └─ 从 TriggerManager 注销触发器（生命周期同步）
└─ TickDecay()          → 回合结束，衰减 RemainingRounds，到期调用 RemoveBuff
```

**不含**：触发逻辑（吸血、荆棘等效果通过 TriggerManager → PendingQueue 执行）。

#### TriggerManager（内部逻辑总线）

```
职责：触发器注册、分组存储、事件响应、优先级排序
├─ Register(TriggerUnit)   → 按 Timing 分组存储
├─ Unregister(triggerId)   → 注销触发器
├─ Fire(timing, ctx)
│    ├─ 找到对应 Timing 的所有触发器
│    ├─ 按 Priority 排序（0-999，小数字优先）
│    ├─ ConditionChecker 逐一校验条件
│    └─ 满足条件的触发器 → 构建 EffectUnit → PendingQueue.Enqueue()
└─ 不持有 SettlementEngine 引用（单向依赖）
```

**优先级约定**：
| 优先级区间 | 用途 |
|-----------|------|
| 0 – 99 | 系统级（无敌、反制判定） |
| 100 – 199 | 增益响应（吸血、治疗） |
| 200 – 299 | 防御响应（护甲获取） |
| 300 – 399 | 削弱响应（易伤应用） |
| 400 – 499 | 伤害响应（荆棘、反伤） |
| 500 – 899 | 普通卡牌触发器 |
| 900 – 999 | 传说特效 |

#### ValueModifierManager（数值修正器）

```
职责：管理运行时数值修正器，在 SettlementEngine 计算效果值时动态应用
├─ AddModifier(modifier)   → 注册修正器（如：力量 Buff 增加 DirectDamage+5）
├─ RemoveModifier(id)      → 注销修正器
└─ Apply(effectType, baseValue, context) → 返回修正后的最终值

修正器类型：
├─ Add    : finalValue = baseValue + delta   （力量加成）
├─ Mul    : finalValue = baseValue × factor  （百分比增伤）
└─ Set    : finalValue = fixedValue          （强制覆盖，极少使用）

应用顺序（固定）：Add → Mul → Set
```

---

### 3.3 解析器层（只读，无副作用）

| 解析器 | 职责 |
|--------|------|
| `TargetResolver` | 根据 `EffectUnit.TargetType` 和当前战场状态，解析出实际目标 `Entity` 列表 |
| `ConditionChecker` | 评估 `TriggerUnit.Conditions` / 打出条件，返回 `bool` |
| `DynamicParamResolver` | 解析 `{{表达式}}` 占位符，支持 `preEffect.xxx`、`context.xxx` 路径读取和四则运算 |

---

### 3.4 EventBus（外部广播，单向）

```
职责：战斗内关键事件的统一广播，供外部系统订阅
├─ Publish(BattleEvent)    → 向所有订阅者广播
├─ Subscribe<T>(handler)   → 注册事件订阅
└─ 不参与战斗结算（完全被动）

订阅者（外部，只读 BattleContext）：
├─ StatManager   → 统计伤害/治疗/死亡数据
├─ LogManager    → 结构化战斗日志
└─ Client UI     → 驱动卡牌动画、伤害数字、死亡特效
```

**关键约束**：EventBus 订阅者**不可写** `BattleContext`，只读。

---

## 四、关键数据结构（V2 版）

### 4.1 BattleContext（唯一状态容器）

```csharp
public class BattleContext
{
    // 基础信息
    public string BattleId { get; }
    public int CurrentRound { get; set; }
    public BattlePhase CurrentPhase { get; set; }

    // 实体数据（O(1) 查找）
    public Dictionary<string, PlayerData> Players { get; }
    public PlayerData GetPlayer(string id);   // 首选，O(1)

    // 全局管理器
    public IEventBus EventBus { get; }
    public ITriggerManager TriggerManager { get; }
    public ICardManager CardManager { get; }
    public IBuffManager BuffManager { get; }
    public IValueModifierManager ValueModifierManager { get; }

    // 解析器（只读工具）
    public ITargetResolver TargetResolver { get; }
    public IConditionChecker ConditionChecker { get; }
    public IDynamicParamResolver DynamicParamResolver { get; }

    // 结算调度
    public PendingEffectQueue PendingQueue { get; }
    public SeededRandom Random { get; }

    // 日志
    public List<string> RoundLog { get; }
    public List<List<string>> HistoryLog { get; }  // 按回合存档
}
```

### 4.2 EffectUnit（效果原子）

```csharp
public class EffectUnit
{
    public string EffectId { get; set; }          // 效果唯一 ID（供动态参数引用）
    public EffectType Type { get; set; }           // 效果类型（决定调用哪个 Handler）
    public string TargetType { get; set; }         // 目标类型（Self/Enemy/AllEnemy/...）
    public string ValueExpression { get; set; }    // 数值表达式（支持 {{动态参数}}）
    public SettleLayer Layer { get; set; }         // 结算层（0-4）
    public List<string> Conditions { get; set; }  // 打出条件列表
    public Dictionary<string, string> Params { get; set; }  // 扩展参数
}
```

### 4.3 TriggerUnit（触发器）

```csharp
public class TriggerUnit
{
    public string TriggerId { get; set; }          // 运行时唯一 ID
    public string TriggerName { get; set; }        // 描述性名称（调试用）
    public TriggerTiming Timing { get; set; }      // 触发时机
    public string OwnerPlayerId { get; set; }      // 归属玩家
    public string SourceId { get; set; }           // 来源（BuffUnit ID / 卡牌 InstanceId）
    public int Priority { get; set; }              // 优先级（越小越先触发）
    public int RemainingTriggers { get; set; }     // 剩余触发次数（-1=无限）
    public int RemainingRounds { get; set; }       // 剩余回合数（-1=永久）
    public List<string> Conditions { get; set; }   // 触发条件
    public List<EffectUnit> Effects { get; set; }  // 触发后推入 PendingQueue 的效果列表
}
```

### 4.4 BattleCard（战斗时卡牌实例）

```csharp
public class BattleCard
{
    public string InstanceId { get; set; }         // 战斗唯一 ID（"bc_001"）
    public string ConfigId { get; set; }           // 关联配置 ID（"strike"）
    public string OwnerId { get; set; }            // 持有者 Player ID
    public CardZone Zone { get; set; }             // 当前所在区域
    public bool TempCard { get; set; }             // 临时牌（结算后销毁）
    public bool IsStatCard { get; set; }           // 状态牌（不可主动打出）
    public bool IsExhaust { get; set; }            // 消耗牌（使用后进 Consume 区）
    public Dictionary<string, object> ExtraData { get; set; }
}
```

### 4.5 BattleCardDefinition（卡牌运行时定义）

```csharp
/// <summary>
/// BattleCore 运行时卡牌定义，通过 BattleFactory.CardDefinitionProvider 委托注入。
/// BattleCore 只依赖可执行效果列表和少量生命周期标记，与上层完整配置模型解耦。
/// </summary>
public class BattleCardDefinition
{
    public string ConfigId { get; set; }           // 卡牌配置 ID（与 BattleCard.ConfigId 对应）
    public bool IsExhaust { get; set; }            // 是否为消耗牌
    public bool IsStatCard { get; set; }           // 是否为状态牌
    public List<EffectUnit> Effects { get; set; }  // 可执行效果列表
}
```

> **注意**：使用方通过 `BattleFactory.CardDefinitionProvider = configId => ...` 注入卡牌定义。
> `BattleContext.BuildCardEffects(configId)` 会自动克隆效果列表，避免多次结算污染同一对象。

### 4.6 IEffectHandler 接口（V2 签名）

```csharp
public interface IEffectHandler
{
    /// <summary>
    /// 执行效果原子
    /// </summary>
    /// <param name="ctx">战斗上下文（唯一写入入口）</param>
    /// <param name="effect">效果原子数据</param>
    /// <param name="source">施法源实体</param>
    /// <param name="targets">已解析的目标实体列表</param>
    /// <param name="priorResults">同一卡牌前置效果的执行结果（供动态参数引用）</param>
    /// <returns>本次效果的执行结果（供后续效果引用）</returns>
    EffectResult Execute(
        BattleContext ctx,
        EffectUnit effect,
        Entity source,
        List<Entity> targets,
        List<EffectResult> priorResults);
}
```

---

## 五、完整结算时序图

### 5.1 定策牌结算（一回合完整流程）

```
RoundManager.EndRound()
  │
  ├─ [Step 1] CardManager.ScanStatCards()
  │     └─ 遍历所有玩家 Hand 中 IsStatCard=true 的实例，触发 OnStatCardHeld → 推入 PendingQueue
  │     ⚠️ 当前状态牌不依赖 StatZone，主流程扫描手牌中持有的状态牌实例
  │
  ├─ [Step 2] SettlementEngine.ResolvePlanCards()
  │     │
  │     ├─ Layer 0: Counter 结算
  │     │     └─ 标记被反制牌（IsCountered = true）
  │     │     └─ 带 Reflect 标签 → 反弹效果推入 PendingQueue
  │     │
  │     ├─ Layer 1: Defense/Modification 结算
  │     │     └─ 护盾、护甲 Handler 执行
  │     │     └─ ValueModifierManager.Apply() 应用力量/虚弱修正
  │     │
  │     ├─ Layer 2: Damage 结算（己方顺序依赖 + 双方快照隔离）
  │     │     │
  │     │  ⚠️ 关键语义约定：
  │     │  ┌──────────────────────────────────────────────────────────────────┐
  │     │  │ 【己方内部】按出牌顺序逐张结算，后打出的牌看到前面牌造成的实时状态变化  │
  │     │  │   示例：打击（破甲5→0）→ 死亡收割（护甲已清零，实际伤害完整，吸血成功）│
  │     │  │                                                                  │
  │     │  │ 【双方隔离】双方计算对方伤害时，以"Layer 2 开始前的己方防御快照"为    │
  │     │  │   基准（HP/护盾/护甲），不受对方出牌顺序的实时影响                   │
  │     │  │   示例：对方无论先出什么牌，我方受伤计算始终基于 Layer 2 开始时的状态  │
  │     │  └──────────────────────────────────────────────────────────────────┘
  │     │
  │     ├─ Pre-Layer 2：为每位玩家创建防御快照（DefenseSnapshot）
  │     │     └─ 记录各玩家当前 HP / Shield / Armor（供对方伤害计算时使用）
  │     │
  │     ├─ 按出牌顺序逐张执行（每张牌完整走 A→B→C 后才执行下一张）：
  │     │     ├─ Phase A（只读）：计算本张牌修正后伤害（引用对方当前实时状态）
  │     │     ├─ Phase B（写入）：扣除对方护盾/HP，记录本张牌实际伤害量（EffectResult）
  │     │     └─ Phase C（触发）：Fire(AfterDealDamage) → 吸血等触发推入 PendingQueue
  │     │           ↑ 下一张牌执行 Phase A 时，对方状态已是被上张牌修改后的实时值
  │     │
  │     └─ 对方伤害计算基准：始终读取"己方 DefenseSnapshot"（而非己方实时 HP）
  │
  ├─ [Step 3] SettlementEngine.DrainPendingQueue()
  │     └─ while (queue.Count > 0)
  │           └─ SettlementEngine.Resolve(Dequeue())
  │                 └─ 可能再次推入 PendingQueue（递归消化，执行栈始终平坦）
  │
  ├─ [Step 4] TriggerManager.Fire(OnRoundEnd)
  │     └─ DOT 伤害（毒/灼烧）触发 → 推入 PendingQueue
  │     └─ DrainPendingQueue() 再次消化
  │
  └─ [Step 5] BuffManager.TickDecay()
        └─ RemainingRounds-- → 到期调用 RemoveBuff（同步注销触发器）
```

### 5.2 瞬策牌结算（即时）

```
SettlementEngine.ResolveInstant(battleCard)
  │
  ├─ ConditionChecker.Evaluate(card.PlayConditions) → false: 拦截，返回失败
  ├─ TriggerManager.Fire(BeforePlayCard) → 沉默/行为拦截检查
  ├─ TargetResolver.Resolve(card.EffectUnits[0].TargetType) → targetEntities
  │
  ├─ priorResults = []
  ├─ for each effectUnit in card.EffectUnits:
  │     targets = TargetResolver.Resolve(effectUnit.TargetType)
  │     value   = DynamicParamResolver.Resolve(effectUnit.ValueExpression, priorResults, ctx)
  │     result  = HandlerPool.Execute(effectUnit, source, targets, priorResults, ctx)
  │     priorResults.Add(result)
  │
  ├─ DrainPendingQueue()
  └─ TriggerManager.Fire(AfterPlayCard)
```

---

## 六、模块间依赖关系（精确版）

```
允许的依赖（→ 表示"可调用/引用"）：

RoundManager       → SettlementEngine, CardManager, BattleContext
SettlementEngine   → HandlerPool, TriggerManager, PendingEffectQueue, BattleContext
                   → TargetResolver, DynamicParamResolver（只读）
HandlerPool        → IEffectHandler（分发）
IEffectHandler     → BattleContext（写入）, EventBus（发布）
TriggerManager     → PendingEffectQueue（推入）, ConditionChecker（只读）
CardManager        → BattleContext（写入 CardZone）, EventBus（发布）
BuffManager        → TriggerManager（注册/注销）, PlayerData（写入）
StatManager        → EventBus（订阅）, BattleContext（只读统计数据）
ValueModifierMgr   → [被 SettlementEngine 调用]

严格禁止的依赖：
TriggerManager     ✗ SettlementEngine（禁止直接调用，必须走 PendingQueue）
Resolvers          ✗ BattleContext.写入（只读）
EventBus订阅者     ✗ BattleContext.写入（只读）
Foundation层       ✗ 任何 Manager（纯数据）
```

---

## 七、触发时机清单（TriggerTiming V2）

| 时机 | 触发节点 | 典型用途 |
|------|---------|---------|
| `OnRoundStart` | `RoundManager.BeginRound` 开始时 | 回合开始增益/减益 |
| `OnRoundEnd` | `RoundManager.EndRound` 末尾 | DOT 伤害、回合结束结算 |
| `BeforePlayCard` | `SettlementEngine.ResolveInstant` 前 | 沉默检查、行为拦截 |
| `AfterPlayCard` | `SettlementEngine.ResolveInstant` 后 | 出牌后触发器 |
| `BeforeDealDamage` | `DamageHandler` Phase A | 出伤修正（可取消） |
| `AfterDealDamage` | `DamageHandler` Phase C | 吸血、连击 |
| `BeforeTakeDamage` | `DamageHandler` Phase B | 无敌判定、受伤修正 |
| `AfterTakeDamage` | `DamageHandler` Phase C | 荆棘、受击获甲 |
| `OnShieldBroken` | `DamageHandler` Phase B 护盾归零时 | 破盾特效 |
| `OnNearDeath` | `DamageHandler` Phase B HP ≤ 0 时 | 复活技能 |
| `OnDeath` | `SettlementEngine` 死亡确认后 | 死亡触发器 |
| `OnBuffAdded` | `BuffManager.AddBuff` | Buff 叠加时触发 ✅ 已接线 |
| `OnBuffRemoved` | `BuffManager.RemoveBuff` | Buff 移除时触发 ✅ 已接线 |
| `OnCardDrawn` | `CardManager.DrawCards` | 抽牌触发器 ✅ 已接线 |
| `OnStatCardHeld` | `CardManager.ScanStatCards`（扫描 Hand 中 IsStatCard=true 的牌） | 状态牌持有时触发 |

---

## 八、卡牌类型与区域流转

```
卡组配置（CardConfig × N）
  ↓ CardManager.InitBattleDeck()
BattleCard 实例（InstanceId 唯一）

区域流转规则：
Deck ──抽牌──► Hand ──打出──► StrategyZone（定策）
                    ──打出──► [即时结算] ──► Discard（普通牌）
                                          → Consume（消耗牌）

弃牌堆 ◄── 结算完成（普通牌归位）
消耗区 ◄── 结算完成（消耗牌永久记录）
StatZone ◄── ⚠️ 遗留兼容区域，当前主流程不依赖此区域

状态牌（IsStatCard=true）：正常存在于 Deck / Hand / Discard，不可主动打出，
  回合末由 ScanStatCards() 扫描 Hand 中的状态牌触发 OnStatCardHeld → 之后随普通手牌弃置

临时牌：TempCard=true → 回合末 DestroyTempCards() 直接销毁（不进 Discard）
```

---

## 九、与 V1 的核心差异对照

| 维度 | V1 | V2 |
|------|----|----|
| **触发器调用** | TriggerManager 直接回调 SettlementEngine（双向） | TriggerManager 推入 PendingQueue（单向） |
| **Buff 数据** | BuffManager._buffs（私有，难序列化） | PlayerData.ActiveBuffs（公开，可序列化） |
| **BuffManager** | 重量级，含触发逻辑 | 薄中介，只含 CRUD + 触发器注册/注销 |
| **力量/属性** | `AttackBuff` 字段直接累加 | ValueModifier（Add 类型），动态计算 |
| **事件系统** | BattleEventRecorder（被动记录） | EventBus（主动广播） + StatManager（订阅统计） |
| **卡牌实例化** | 无，配置直接使用 | BattleCard 实例（唯一 ID，区域管理） |
| **动态参数** | 无 | DynamicParamResolver（表达式解析） |
| **结算层数** | 4层（0-3） | 5层（0-4，增加 Resource 层） |
| **Handler 签名** | Execute(card, effect, source, target, ctx) | Execute(ctx, effect, source, targets, priorResults) |
| **DOT 伤害** | 直接 Hp-=（绕过 DamageHelper） | 通过 PendingQueue，走完整 DamageHandler 流程 |
| **卡牌定义注入** | 无，配置直接耦合 | `BattleCardDefinition` + `CardDefinitionProvider` 委托解耦 |
| **触发目标预解析** | 无 | `TriggerSource`/`TriggerTarget` 在入队时锁定实体 ID，防止目标漂移 |
| **效果来源追踪** | 无 | 效果参数自动写入 `sourceCardInstanceId`/`sourceCardConfigId` |

---

*本文档描述 V2 架构设计，具体实现阶段规划见 [BattleCoreRefactorPlan.md](../Planning/BattleCoreRefactorPlan.md)*  
*卡牌规则设计见 [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md)*
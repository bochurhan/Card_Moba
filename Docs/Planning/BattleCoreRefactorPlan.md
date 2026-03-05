# BattleCore V2 重构计划

**文档版本**：V1.0  
**创建日期**：2026-03-04  
**状态**：🟡 规划中（尚未开始编码）  
**负责人**：开发团队  
**关联文档**：
- 架构设计：[../TechGuide/SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)
- 游戏设计源文档：[../GameDesign/新框架.md](../GameDesign/新框架.md)
- 卡牌内容参考：[../GameDesign/卡牌内容.md](../GameDesign/卡牌内容.md)
- V1 归档：[../_Archive/v1_battlecore/README.md](../_Archive/v1_battlecore/README.md)

---

## 一、重构动因

V1 BattleCore 在功能验证阶段积累了以下核心问题，修补成本已超过重建成本：

| 问题 | 影响 |
|------|------|
| `SettlementEngine` ↔ `TriggerManager` 双向调用，无调度队列 | 触发链状态不稳定，扩展性差 |
| `BuffManager` 既管数据又管触发逻辑，职责混乱 | 生命周期无法统一，孤儿触发器风险 |
| DOT 绕过 `DamageHelper`，无敌/护盾对毒无效 | 规则残缺，属 P0 级 bug |
| 无结构化 StatManager，无 EventBus | 无法支持战斗统计、复盘、客户端动画驱动 |
| 动态参数（如"死亡收割回复量=本次实际伤害"）无系统性支持 | 复杂卡牌效果无法配置化实现 |
| BattleCard 无实例化机制，配置与运行时数据混用 | 同张卡多副本/临时牌无法区分 |
| 无 `PendingEffectQueue`，触发器回调直接重入结算引擎 | 触发深度不可控 |

**结论**：推倒 `Shared/BattleCore/` 层，按 V2 架构重建。`Shared/ConfigModels/`、`Shared/Protocol/` 保留并逐步迁移。

---

## 二、V2 核心架构原则

> 完整架构详见 [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)

### 2.1 关键设计决策

**1. PendingEffectQueue（解耦核心）**  
触发器产生的效果**不直接调用结算引擎**，而是推入 `PendingEffectQueue`。`SettlementEngine` 在每次主结算完成后统一消化队列，执行栈永远是平的。

```
SettlementEngine.Resolve(主效果)
  └─ 写入状态
  └─ 触发检查 → TriggerManager → 向 PendingQueue 推入子效果
                                          ↓
[主效果栈帧结束]
while (PendingQueue.Count > 0)
  └─ SettlementEngine.Resolve(子效果)  ← 全新栈帧，状态稳定
```

**2. BattleCard 实例化**  
战斗开始时，`CardManager.InitBattleDeck()` 将卡组配置展开为运行时实例：

```
CardConfig（id:"strike", count:5）
  → BattleCard × 5（instanceId: "bc_001"~"bc_005", configId:"strike"）
```
- 临时牌（如复制牌）：`tempCard=true`，结算后自动销毁
- 状态牌（如灼烧）：`isStatCard=true`，绑定持有区域，不可主动打出

**3. EventBus + StatManager 并行**  
`EventBus` 负责战斗事件广播（外部消费），`TriggerManager` 负责战斗内逻辑响应（内部触发），二者职责分离、并行运行。

**4. 力量/属性 = ValueModifier**  
力量 Buff 注册一个 `ValueModifier`（Add 类型）到 `ValueModifierManager`，结算时动态修正 `DirectDamage` 的 `BaseDamage`，而不是直接写 `AttackBuff` 字段。

---

## 三、V2 模块清单与职责

```
Shared/BattleCore/  (全部重建)
├── Foundation/              ★ 基础数据结构（无依赖）
│   ├── Entity.cs            — 战斗单位（Player/Minion/Structure）
│   ├── BattleCard.cs        — 战斗时卡牌实例
│   ├── EffectUnit.cs        — 效果原子（最小执行单元）
│   ├── TriggerUnit.cs       — 触发器数据（事件-条件-动作）
│   └── BuffUnit.cs          — Buff 数据（纯状态，无逻辑）
│
├── Context/                 ★ 战斗上下文（唯一状态容器）
│   ├── BattleContext.cs     — 全局单例数据入口
│   ├── PlayerData.cs        — 玩家战斗状态（含手牌/卡组引用）
│   └── LaneData.cs          — 分路状态
│
├── Core/                    ★ 核心调度（调度层）
│   ├── RoundManager.cs      — 回合状态机（导演）
│   ├── SettlementEngine.cs  — 效果结算引擎
│   └── PendingEffectQueue.cs — 延迟效果队列（解耦核心）
│
├── Handlers/                ★ 效果原子执行器（无状态单例）
│   ├── IEffectHandler.cs    — Handler 接口
│   ├── HandlerPool.cs       — Handler 注册与分发
│   ├── DamageHandler.cs
│   ├── HealHandler.cs
│   ├── ShieldHandler.cs
│   ├── BuffAddHandler.cs
│   ├── CardDrawHandler.cs
│   ├── CardGenerateHandler.cs  — 生成卡牌到指定区域
│   ├── CardReplayHandler.cs    — 复刻（重播）卡牌效果
│   └── ...（按需扩展）
│
├── Managers/                ★ 业务层管理器
│   ├── CardManager.cs       — 卡牌区域管理（手牌/卡组/弃牌堆/消耗区）
│   ├── BuffManager.cs       — Buff CRUD（薄中介，不含触发逻辑）
│   ├── TriggerManager.cs    — 触发器注册/响应（内部逻辑总线）
│   ├── StatManager.cs       — 战斗统计（EventBus 订阅者）
│   └── ValueModifierManager.cs — 数值修正器管理
│
├── EventBus/                ★ 外部事件总线（解耦层）
│   ├── IEventBus.cs
│   ├── BattleEventBus.cs
│   └── BattleEvents.cs      — 所有事件类型定义
│
├── Resolvers/               ★ 解析器（只读，无副作用）
│   ├── TargetResolver.cs    — 目标解析
│   ├── ConditionChecker.cs  — 条件判断
│   └── DynamicParamResolver.cs — 动态参数解析（含表达式计算）
│
└── Random/
    └── SeededRandom.cs      — 确定性随机（保留）
```

---

## 四、结算流程（V2 定版）

### 4.1 回合生命周期

```
RoundManager.BeginRound()
  ├─ 1. 触发 OnRoundStart 事件链（TriggerManager 消化）
  ├─ 2. 回合开始效果结算（PendingQueue 消化）
  ├─ 3. 发牌 / 能量回满
  └─ 4. 开放玩家操作窗口

[玩家操作期]
  ├─ 打出瞬策牌 → SettlementEngine.ResolveInstant()
  ├─ 提交定策牌 → CardManager.CommitPlanCard()
  └─ 持有状态牌 → 不操作，等待 OnRoundEnd 扫描

RoundManager.EndRound()
  ├─ 1. CardManager 扫描手牌中的状态牌，触发 OnRoundEnd 状态牌效果
  ├─ 2. SettlementEngine.ResolvePlanCards()（五层结算）
  ├─ 3. PendingQueue 循环消化（触发器子效果）
  ├─ 4. 触发 OnRoundEnd 事件链
  └─ 5. Buff 衰减（BuffManager.TickDecay）
```

### 4.2 定策牌五层结算顺序

| 层级 | 类型 | 说明 |
|------|------|------|
| Layer 0 | Counter（反制） | 最高优先，标记被反制牌，带 Reflect 标签则反弹 |
| Layer 1 | Defense（防御/修正） | 护盾、护甲、力量增减（ValueModifier 应用时机） |
| Layer 2 | Damage（伤害） | 批量三阶段：A收集 → B写入 → C触发 |
| Layer 3 | Resource（资源） | 抽牌、弃牌、能量、卡牌生成 |
| Layer 4 | Buff/Special（增益/特殊） | Buff 附加、控制、复刻等 |

> Layer 2 必须严格遵守三阶段：阶段A只读，阶段B写入护盾/HP，阶段C触发后续链（推入 PendingQueue）

### 4.3 瞬策牌结算

```
SettlementEngine.ResolveInstant(card)
  ├─ 1. ConditionChecker 校验打出条件
  ├─ 2. TriggerManager.Fire(BeforePlayCard) → 拦截校验
  ├─ 3. TargetResolver 解析目标
  ├─ 4. 遍历 EffectUnit 列表，HandlerPool 分发执行
  │       每个效果执行后：priorResults 列表追加结果（供动态参数引用）
  ├─ 5. PendingQueue 消化（触发器响应）
  └─ 6. TriggerManager.Fire(AfterPlayCard)
```

### 4.4 动态参数示例

```
// 死亡收割：回复 = 本次对所有目标造成的实际伤害总和
EffectUnit {
    type: Heal,
    targetType: Self,
    value: "{{preEffect.dmg_01.totalRealHpDamage}}"  // 引用前置伤害效果的实际结果
}

// 霰弹：伤害 = (6 - 手牌数) × 3
EffectUnit {
    type: Damage,
    value: "{{(6 - context.self.hand.count) * 3}}"
}
```

---

## 五、BattleCard 实例化规范

### 5.1 实例化时机

| 时机 | 操作 |
|------|------|
| 战斗开始 | `CardManager.InitBattleDeck()` 将卡组配置展开为 `BattleCard` 实例 |
| 效果触发 | `CardGenerateHandler` 动态创建临时 `BattleCard`（`tempCard=true`） |
| 状态牌生成 | 专用效果生成 `isStatCard=true` 的 `BattleCard`，放入指定区域 |

### 5.2 BattleCard 核心字段

```csharp
public class BattleCard
{
    // 身份
    public string InstanceId { get; set; }    // 战斗唯一 ID（"bc_001"）
    public string ConfigId { get; set; }      // 关联配置 ID（"strike"）
    public string OwnerId { get; set; }       // 持有者 Player ID

    // 区域位置
    public CardZone Zone { get; set; }        // Hand / Deck / Discard / StrategyZone / Consume / StatZone

    // 临时性标记
    public bool TempCard { get; set; }        // 临时牌（结算后销毁）
    public bool IsStatCard { get; set; }      // 状态牌（不可主动打出）
    public bool IsExhaust { get; set; }       // 消耗牌（使用后进消耗区）

    // 运行时附加数据
    public Dictionary<string, object> ExtraData { get; set; }  // 自定义运行时属性
}
```

### 5.3 区域规则

| 区域（CardZone） | 说明 |
|-----------------|------|
| `Deck` | 卡组（未抽取） |
| `Hand` | 手牌（可操作） |
| `StrategyZone` | 定策区（本回合已提交的定策牌） |
| `Discard` | 弃牌堆（结算后归位，可被循环牌拉回） |
| `Consume` | 消耗区（消耗牌永久移除，但仍记录） |
| `StatZone` | 状态牌区（持有中的状态牌，绑定触发效果） |

---

## 六、关键模块交互关系

```
RoundManager（导演）
  │ 驱动回合生命周期
  ▼
SettlementEngine（结算引擎）─────────────────────────────────┐
  │ 调用 HandlerPool 分发效果                                  │
  │ 写入 BattleContext                                         │
  │ 触发检查 → TriggerManager                                  │
  │                  │ 推入子效果（不直接回调）                  │
  │                  ▼                                         │
  │           PendingEffectQueue ◄── SettlementEngine 消化 ◄──┘
  │
  ├── 单向发送事件 ──► EventBus ──► StatManager（统计）
  │                              ├── LogManager（日志）
  │                              └── Client UI（表现层）
  │
  └── 读取 ──► Resolvers（TargetResolver / ConditionChecker / DynamicParamResolver）
              只读，无副作用
```

**关键约束**：
- `TriggerManager` **不持有** `SettlementEngine` 引用，只向 `PendingQueue` 推送
- `Resolvers` **只读**，不写任何状态
- `EventBus` 订阅者（`StatManager` 等）**不写** `BattleContext`
- `HandlerPool` 中的 Handler **全部无状态**，状态必须写入 `BattleContext`

---

## 七、实施阶段规划

### Phase 0：基础数据结构（Foundation）

**目标**：搭建无依赖的基础数据结构层  
**预估工时**：2天  
**交付物**：

- [ ] `Foundation/Entity.cs` — 战斗单位基类
- [ ] `Foundation/BattleCard.cs` — 战斗时卡牌实例
- [ ] `Foundation/EffectUnit.cs` — 效果原子数据结构
- [ ] `Foundation/TriggerUnit.cs` — 触发器数据结构
- [ ] `Foundation/BuffUnit.cs` — Buff 纯数据
- [ ] `Foundation/CardZone.cs` — 区域枚举
- [ ] `Foundation/EffectType.cs` — 效果类型枚举（重新整理，替代旧版）

**原则**：此阶段所有文件**不引用任何管理器**，只定义数据结构和枚举。

---

### Phase 1：BattleContext + 管理器骨架

**目标**：搭建状态容器和所有管理器的接口与空实现  
**预估工时**：3天  
**交付物**：

- [ ] `Context/BattleContext.cs` — V2 版（挂载所有管理器引用）
- [ ] `Context/PlayerData.cs`
- [ ] `Context/LaneData.cs`
- [ ] `EventBus/IEventBus.cs` + `BattleEventBus.cs` + `BattleEvents.cs`
- [ ] `Managers/CardManager.cs`（骨架：InitBattleDeck / ZoneMove）
- [ ] `Managers/BuffManager.cs`（骨架：AddBuff / RemoveBuff / TickDecay）
- [ ] `Managers/TriggerManager.cs`（骨架：Register / Fire / Unregister）
- [ ] `Managers/StatManager.cs`（骨架：Subscribe EventBus）
- [ ] `Managers/ValueModifierManager.cs`（骨架：Add / Remove / Apply）
- [ ] `Core/PendingEffectQueue.cs`

---

### Phase 2：结算引擎（SettlementEngine + Handlers）

**目标**：实现核心结算逻辑，Handler 体系，五层结算  
**预估工时**：5天  
**交付物**：

- [ ] `Resolvers/TargetResolver.cs`（迁移 V1 逻辑，适配新接口）
- [ ] `Resolvers/ConditionChecker.cs`
- [ ] `Resolvers/DynamicParamResolver.cs`（新增，支持表达式解析）
- [ ] `Handlers/IEffectHandler.cs`（新签名含 `priorResults`）
- [ ] `Handlers/HandlerPool.cs`
- [ ] `Handlers/DamageHandler.cs`（含批量三阶段）
- [ ] `Handlers/HealHandler.cs`
- [ ] `Handlers/ShieldHandler.cs`
- [ ] `Handlers/BuffAddHandler.cs`
- [ ] `Handlers/CardDrawHandler.cs`
- [ ] `Handlers/CardGenerateHandler.cs`
- [ ] `Handlers/CardReplayHandler.cs`
- [ ] `Core/SettlementEngine.cs`（含五层结算 + PendingQueue 消化循环）
- [ ] `Core/RoundManager.cs`

---

### Phase 3：TriggerManager 完整实现

**目标**：触发器注册/响应/优先级/条件校验完整实现  
**预估工时**：3天  
**交付物**：

- [ ] `Managers/TriggerManager.cs` 完整实现
  - 按 `Timing` 分组存储
  - 优先级排序（0-999 区间约定）
  - 条件校验（`ConditionChecker`）
  - 向 `PendingEffectQueue` 推入结果（不直接调用 Settlement）
- [ ] 触发时机清单补全（`TriggerTiming.cs`）

---

### Phase 4：CardManager 完整实现

**目标**：卡牌区域管理、状态牌扫描、临时牌生命周期  
**预估工时**：2天  
**交付物**：

- [ ] `Managers/CardManager.cs` 完整实现
  - `InitBattleDeck()`：卡组配置 → BattleCard 实例化
  - `MoveCard()`：区域移动
  - `DrawCard()` / `DiscardCard()`
  - `ScanStatCards()`：回合结束扫描状态牌
  - 临时牌自动销毁

---

### Phase 5：StatManager + EventBus 完整实现

**目标**：战斗统计、日志、外部事件完整链路  
**预估工时**：2天  
**交付物**：

- [ ] `EventBus/BattleEventBus.cs` 完整实现
- [ ] `Managers/StatManager.cs` 完整实现
  - 订阅关键事件：伤害、治疗、死亡、卡牌使用
  - 输出：`DamageDealt` / `HealDealt` / `CardPlayed` 等统计数据

---

### Phase 6：集成测试 + 卡牌数据配置

**目标**：跑通第一批卡牌（战士/猎人/机器人各 5 张），验证全链路  
**预估工时**：5天  
**交付物**：

- [ ] 单元测试：每个 Handler 独立测试
- [ ] 集成测试：完整回合流转（RoundManager → SettlementEngine → TriggerManager → PendingQueue）
- [ ] 卡牌配置：战士职业 5 张基础卡
- [ ] 卡牌配置：猎人职业 5 张基础卡
- [ ] 卡牌配置：机器人职业 5 张基础卡
- [ ] 验证动态参数（死亡收割、霰弹）

---

## 八、V1 → V2 概念映射表

| V1 概念 | V2 概念 | 变化说明 |
|---------|---------|---------|
| `EffectType`（枚举，29种） | `EffectType`（重新整理） | 按 5 层结算重新归类，ID 体系重置 |
| `IEffectHandler.Execute(card, effect, source, target, ctx)` | `IEffectHandler.Execute(ctx, effect, source, targets, priorResults)` | 新增 `priorResults`，支持动态参数 |
| `HandlerRegistry` | `HandlerPool` | 职责相同，名称规范化 |
| `TriggerManager.FireTriggers()` → 直接回调 | `TriggerManager.Fire()` → 推入 `PendingQueue` | 解开双向调用 |
| `BuffManager`（per player，重量级） | `BuffManager`（薄中介，全局） + `BuffUnit`（纯数据） | 数据逻辑分离 |
| `BattleEventRecorder`（只记录） | `EventBus`（广播） + `StatManager`（订阅统计） | 被动记录 → 主动广播 |
| `PlayerBattleState`（含 Handler 方法） | `PlayerData`（纯数据） | 移除方法，只保留数据字段 |
| 无 BattleCard 实例化 | `CardManager.InitBattleDeck()` | 配置与运行时分离 |
| `AttackBuff` 字段 | `ValueModifier`（Add 类型） | 力量通过修正器动态生效 |
| `PendingTriggerEffects`（已删除） | `PendingEffectQueue`（新建，核心） | 解耦双向调用的根本方案 |

---

## 九、不变的约束（V2 继承 V1 红线）

| 约束 | 原因 |
|------|------|
| `Shared/BattleCore/` 禁止 `UnityEngine.*` | `noEngineReferences: true`，服务端兼容 |
| 禁止浮点数（`float`/`double`） | C/S 精度差异，一律整数 |
| 禁止 `System.Random` / `UnityEngine.Random` | 必须用 `ctx.Random`（SeededRandom） |
| Handler 无状态 | Handler 是无状态单例，状态写入 `BattleContext` |
| 禁止在阶段A修改状态 | 批量伤害同步性语义 |
| `GetPlayer`/`GetEntity` 必须 O(1) 字典查找 | 性能约束 |

---

*上次更新：2026-03-04*  
*下一步：开始 Phase 0 基础数据结构实现，参考 [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)*

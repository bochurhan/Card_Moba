# BattleCore V2 当前架构

**文档版本**: 2026-03-27  
**状态**: 当前契约  
**适用范围**: `Shared/BattleCore` 当前运行时架构

---

## 1. 目标

这份文档只描述当前 BattleCore 已落地的运行时架构，不混入历史实现和未来草案。

当前最重要的架构口径是：

- `BuffManager` 是 Buff 运行时真源
- 瞬策牌和定策牌都走卡牌实例与合法性校验
- Trigger 派生效果统一走 `PendingEffectQueue -> SettlementEngine -> HandlerPool`
- 定策牌当前采用“提交即生成快照、真实卡实例立即离手”的语义
- Layer 2 采用防御快照

关于：

- `Shared/` 顶层结构
- `BattleCore` 子目录职责
- `Rules` 模块设计草案

请看 [SharedArchitecture.md](SharedArchitecture.md)。

---

## 2. 运行时主分层

### 2.1 Core

主要类型：

- [RoundManager.cs](/d:/Card_Moba/Shared/BattleCore/Core/RoundManager.cs)
- [SettlementEngine.cs](/d:/Card_Moba/Shared/BattleCore/Core/SettlementEngine.cs)

职责：

- 驱动整回合流程
- 协调出牌、结算与死亡检查

### 2.2 Managers

主要类型：

- [CardManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/CardManager.cs)
- [BuffManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/BuffManager.cs)
- [TriggerManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/TriggerManager.cs)

职责：

- 维护牌区流转
- 维护 Buff 生命周期
- 维护 Trigger 注册与入队

### 2.3 Handlers

主要类型：

- [HandlerPool.cs](/d:/Card_Moba/Shared/BattleCore/Handlers/HandlerPool.cs)
- [CoreHandlers.cs](/d:/Card_Moba/Shared/BattleCore/Handlers/CoreHandlers.cs)

职责：

- 执行具体 effect
- 产出 `EffectResult`

### 2.4 Resolvers

主要类型：

- [DynamicParamResolver.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/DynamicParamResolver.cs)
- [ConditionChecker.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/ConditionChecker.cs)
- [TargetResolver.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/TargetResolver.cs)

职责：

- 解析数值表达式
- 判断条件
- 解析目标集合

### 2.5 Modifiers

主要类型：

- [ValueModifierManager.cs](/d:/Card_Moba/Shared/BattleCore/Modifiers/ValueModifierManager.cs)

职责：

- 对已确定的结算基础值做阶段修正

### 2.6 Costs

主要类型：

- [PlayCostResolver.cs](/d:/Card_Moba/Shared/BattleCore/Costs/PlayCostResolver.cs)

当前职责：

- 统一计算实际耗能

建议边界：

- 只计算耗能
- 不继续承载出牌规则副作用

---

## 3. BattleContext

[BattleContext.cs](/d:/Card_Moba/Shared/BattleCore/Context/BattleContext.cs) 是整局战斗的核心状态容器。

当前包含：

- `BattleId`
- `CurrentRound`
- `CurrentPhase`
- 玩家集合
- `PendingEffectQueue`
- `PendingPlanSnapshots`
- `RoundLog`
- `EventBus`
- 各类 Manager
- `CardDefinitionProvider`

它不再承担历史遗留的 `HistoryLog` 归档职责。

---

## 4. 卡牌运行时结构

### 4.1 BattleCard

[BattleCard.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/BattleCard.cs) 负责运行时实例状态：

- `InstanceId`
- `ConfigId`
- `OwnerId`
- `Zone`
- `TempCard`
- `IsExhaust`
- `IsStatCard`
- 当前升级投影

### 4.2 BattleCardDefinition

[BattleCardDefinition.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/BattleCardDefinition.cs) 是 BattleCore 的最小执行定义：

- `ConfigId`
- `EnergyCost`
- `IsExhaust`
- `IsStatCard`
- `Effects`

BattleCore 通过 `BattleContext.CardDefinitionProvider` 获取它。

---

## 5. 定策快照语义

当前定策牌不再把真实卡牌实例停留在一个运行时 `StrategyZone` 中等待结算。

当前语义是：

1. 提交定策牌时生成 [PendingPlanSnapshot.cs](/d:/Card_Moba/Shared/BattleCore/Context/PendingPlanSnapshot.cs)
2. 快照冻结：
   - 卡牌身份
   - 提交顺序
   - 已解析 effect 结构
   - 冻结输入 `frozen.*`
3. 真实卡牌实例立即离开手牌，进入实际结算后去向
4. 回合末 `SettlementEngine` 消费快照队列完成结算

这保证：

- 已提交定策不会被后续再打牌污染
- 同一实例在同一回合如果重新回到可打出区，可以再次打出并生成新快照

---

## 6. 回合流程

### 6.1 InitBattle

`RoundManager.InitBattle()` 负责：

- 初始化战斗状态
- 清空待结算快照与挂起队列
- 发布 `BattleStartEvent`

### 6.2 BeginRound

当前顺序：

1. 回合号 `+1`
2. 更新 `CurrentRound / CurrentPhase`
3. 重置玩家回合统计
4. 发布 `RoundStartEvent`
5. 触发 `OnRoundStart`
6. `DrainPendingQueue`
7. `CardManager.OnRoundStart()`
8. 再次 `DrainPendingQueue`
9. 进入 `PlayerAction`

### 6.3 EndRound

当前顺序：

1. 进入 `Settlement`
2. 扫描状态牌
3. `DrainPendingQueue`
4. 定策快照统一结算
5. 死亡检查
6. `OnRoundEnd`
7. `DrainPendingQueue`
8. `BuffManager.OnRoundEnd()`
9. 护盾清零
10. `TriggerManager.TickDecay()`
11. `CardManager.OnRoundEnd()`
12. `CardManager.DestroyTempCards()`
13. 发布 `RoundEndEvent`

---

## 7. 出牌链路

### 7.1 瞬策牌

入口：

- [RoundManager.cs](/d:/Card_Moba/Shared/BattleCore/Core/RoundManager.cs) 的 `PlayInstantCard()`

主逻辑：

- 校验实例归属
- 解析 `BattleCardDefinition`
- 校验出牌限制
- 记录打出统计
- 进入 `SettlementEngine.ResolveInstantFromCard()`

### 7.2 定策牌

入口：

- [RoundManager.cs](/d:/Card_Moba/Shared/BattleCore/Core/RoundManager.cs) 的 `CommitPlanCard()`

主逻辑：

- 校验实例归属
- 解析 `BattleCardDefinition`
- 校验出牌限制
- 生成 `PendingPlanSnapshot`
- 实例立即进入真实结算后去向
- 回合末统一结算快照

---

## 8. 五层结算

当前层级：

- `Counter`
- `Defense`
- `Damage`
- `Resource`
- `BuffSpecial`

其中：

- Layer 2 前拍防御快照
- Layer 2 后清理快照
- Buff、Trigger 派生效果最终也回到同一条 Handler 链路

---

## 9. Buff、Trigger、Modifier

### 9.1 BuffManager

职责：

- Add / Remove / Query Buff
- 维护 Buff 生命周期
- 同步注册或注销相关 Trigger 与 Modifier

### 9.2 TriggerManager

职责：

- 按时机筛选 Trigger
- 做条件检查
- 生成待执行效果
- 推入 `PendingEffectQueue`

### 9.3 ValueModifierManager

当前主要按两个方向区分：

- `OutgoingDamage`
- `IncomingDamage`

例如：

- `Strength / Weak` 作用于出伤
- `Armor / Vulnerable` 作用于入伤

---

## 10. 当前保留但不是主配置面的能力

以下能力仍有枚举或预留结构，但不应被当作当前主配置能力：

- `BeforeDealDamage`
- `BeforeTakeDamage`
- 更复杂的分路与团队目标系统

---

## 11. 当前有效参考

- [SharedArchitecture.md](SharedArchitecture.md)
- [SettlementRules.md](../GameDesign/SettlementRules.md)
- [CardSystem.md](../GameDesign/CardSystem.md)
- [ConfigSystem.md](ConfigSystem.md)

# BattleCore V2 当前架构

**文档版本**: 2026-03-28  
**状态**: 当前契约  
**适用范围**: `Shared/BattleCore` 当前运行时主流程

## 1. 目标

这份文档只描述当前已经落地的 BattleCore 运行时架构，不混入历史实现和未来草案。

关于以下内容，请看 [SharedArchitecture.md](SharedArchitecture.md)：

- `Shared/` 顶层结构
- `BattleCore` 子目录职责
- `Rules` 模块的设计边界

## 2. 当前运行时主分层

### 2.1 Core

当前主要类型：

- [RoundManager.cs](/d:/Card_Moba/Shared/BattleCore/Core/RoundManager.cs)
- [SettlementEngine.cs](/d:/Card_Moba/Shared/BattleCore/Core/SettlementEngine.cs)
- [BattleFactory.cs](/d:/Card_Moba/Shared/BattleCore/Core/BattleFactory.cs)
- [BattleRuleset.cs](/d:/Card_Moba/Shared/BattleCore/Core/BattleRuleset.cs)

职责：

- 驱动整回合流程
- 协调出牌、结算与死亡检查

### 2.2 Managers

当前主要类型：

- [CardManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/CardManager.cs)
- [BuffManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/BuffManager.cs)
- [TriggerManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/TriggerManager.cs)

职责：

- 维护牌区流转
- 维护 Buff 生命周期
- 维护 Trigger 注册与入队

### 2.3 Handlers

当前主要类型：

- [HandlerPool.cs](/d:/Card_Moba/Shared/BattleCore/Handlers/HandlerPool.cs)
- [CoreHandlers.cs](/d:/Card_Moba/Shared/BattleCore/Handlers/CoreHandlers.cs)

职责：

- 执行具体 effect
- 产出 `EffectResult`

### 2.4 Resolvers

当前主要类型：

- [DynamicParamResolver.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/DynamicParamResolver.cs)
- [ConditionChecker.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/ConditionChecker.cs)
- [TargetResolver.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/TargetResolver.cs)

职责：

- 解析数值表达式
- 解析条件
- 解析目标集合

### 2.5 Modifiers

当前主要类型：

- [ValueModifierManager.cs](/d:/Card_Moba/Shared/BattleCore/Modifiers/ValueModifierManager.cs)

职责：

- 对已确定的基础结算值做阶段修正

### 2.6 Costs

当前主要类型：

- [PlayCostResolver.cs](/d:/Card_Moba/Shared/BattleCore/Costs/PlayCostResolver.cs)

当前边界：

- 只计算实际耗能
- 不再承担腐化等规则副作用提交

### 2.7 Rules

当前第一阶段已经落地：

- [DrawRuleResolver.cs](/d:/Card_Moba/Shared/BattleCore/Rules/Draw/DrawRuleResolver.cs)
- [PlayRuleResolver.cs](/d:/Card_Moba/Shared/BattleCore/Rules/Play/PlayRuleResolver.cs)

当前纳入规则层的只有三条规则：

- `NoDrawThisTurn`
- `NoDamageCardThisTurn`
- `Corruption`

## 3. BattleContext

[BattleContext.cs](/d:/Card_Moba/Shared/BattleCore/Context/BattleContext.cs) 是整局战斗的核心状态容器。

当前包含：

- `BattleId`
- `CurrentRound`
- `CurrentPhase`
- `Ruleset`
- 玩家集合与队伍集合
- `PendingEffectQueue`
- `PendingPlanSnapshots`
- 通用实体注册表（含 team objective）
- `RoundLog`
- `EventBus`
- 各类 Manager / Resolver / Rule / Cost 服务
- `CardDefinitionProvider`

## 4. 卡牌运行时结构

### 4.1 BattleCard

[BattleCard.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/Cards/BattleCard.cs) 表示局内卡牌实例，负责：

- `InstanceId`
- `ConfigId`
- `OwnerId`
- `Zone`
- `TempCard`
- `IsExhaust`
- `IsStatCard`
- 当前升级投影状态

### 4.2 BattleCardDefinition

[BattleCardDefinition.cs](/d:/Card_Moba/Shared/BattleCore/Definitions/BattleCardDefinition.cs) 是 BattleCore 结算所需的最小卡牌定义，包含：

- `ConfigId`
- `EnergyCost`
- `IsExhaust`
- `IsStatCard`
- `Effects`

BattleCore 通过 `BattleContext.CardDefinitionProvider` 获取它。

## 5. 定策快照语义

当前定策牌不再把真实卡牌实例停在运行时 `StrategyZone` 中等待结算。

当前语义是：

1. 提交定策牌时生成 [PendingPlanSnapshot.cs](/d:/Card_Moba/Shared/BattleCore/Context/PendingPlanSnapshot.cs)
2. 快照冻结：
   - 卡牌身份
   - 提交顺序
   - 已解析的 effect 结构
   - 冻结输入 `frozen.*`
3. 真实卡牌实例立即离开手牌，进入真实结算后去向
4. 回合末由 `SettlementEngine` 消费快照并完成结算

这保证：

- 已提交定策不会被后续打牌反向污染
- 同一实例如果重新回到可打出区，可以再次打出并生成新快照

## 5.1 Team / Objective / Summary 支撑类型

当前 BattleCore 还新增了三类对接 `MatchFlow` 的支撑文件：

- [BattleTeamState.cs](/d:/Card_Moba/Shared/BattleCore/Context/BattleTeamState.cs)
  - battle 内队伍状态与共享 objective 入口
- [BattleRuleset.cs](/d:/Card_Moba/Shared/BattleCore/Core/BattleRuleset.cs)
  - 单场 battle 结束规则与 objective 终止策略
- [BattleSummary.cs](/d:/Card_Moba/Shared/BattleCore/Results/BattleSummary.cs)
  - battle 结束后交给 `MatchFlow` 的标准摘要

## 6. 回合流程

### 6.1 InitBattle

`RoundManager.InitBattle()` 负责：

- 初始化战斗状态
- 清空待结算快照与挂起队列
- 清空 `CompletedBattleSummary`
- 发布 `BattleStartEvent`

### 6.2 BeginRound

当前顺序：

1. 回合数 +1
2. 更新 `CurrentRound / CurrentPhase`
3. 重置玩家回合统计
4. 重置规则层回合态
5. 发布 `RoundStartEvent`
6. 触发 `OnRoundStart`
7. `DrainPendingQueue`
8. `CardManager.OnRoundStart()`
9. 再次 `DrainPendingQueue`
10. 进入 `PlayerAction`

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
14. 若 battle 结束，则自动写入 `CompletedBattleSummary`

## 7. 出牌链路

### 7.1 瞬策牌

入口：

- [RoundManager.cs](/d:/Card_Moba/Shared/BattleCore/Core/RoundManager.cs) 的 `PlayInstantCard()`

主逻辑：

- 校验实例归属
- 解析 `BattleCardDefinition`
- 校验出牌规则
- 计算耗能
- 记录打出统计
- 进入 `SettlementEngine.ResolveInstantFromCard()`

### 7.2 定策牌

入口：

- [RoundManager.cs](/d:/Card_Moba/Shared/BattleCore/Core/RoundManager.cs) 的 `CommitPlanCard()`

主逻辑：

- 校验实例归属
- 解析 `BattleCardDefinition`
- 校验出牌规则
- 计算耗能
- 生成 `PendingPlanSnapshot`
- 真实实例立即进入真实结算后去向
- 回合末统一结算快照

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
- Buff、Trigger 派生效果最终也统一回到同一条 `HandlerPool` 链路

## 9. Buff、Trigger、Modifier、Rule

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

当前主要区分两个方向：

- `OutgoingDamage`
- `IncomingDamage`

例如：

- `Strength / Weak` 作用于出伤
- `Armor / Vulnerable` 作用于入伤

### 9.4 Rules

当前第一阶段只承接三条规则：

- `NoDrawThisTurn`
- `NoDamageCardThisTurn`
- `Corruption`

边界是：

- `Buff` 是状态真源
- `Rule` 把状态解释成一次操作的规则结果
- `Cost` 只消费规则结果并计算耗能
- `Manager / Core` 在成功后提交规则副作用

## 10. 当前有效参考

- [SharedArchitecture.md](SharedArchitecture.md)
- [SettlementRules.md](../GameDesign/SettlementRules.md)
- [CardSystem.md](../GameDesign/CardSystem.md)
- [ConfigSystem.md](ConfigSystem.md)




# BattleCore V2 当前架构

**文档版本**: 2026-03-26  
**状态**: 当前契约  
**适用范围**: `Shared/BattleCore` 当前运行时架构

---

## 1. 目标

这份文档只描述当前 BattleCore 已经落地的运行时架构，不再混入历史设计和未接线规划。

当前最重要的架构口径是：

- `BuffManager` 是 Buff 唯一运行时真源
- 瞬策牌和定策牌都走卡牌实例与合法性校验
- Buff 子效果统一走 `TriggerManager -> PendingEffectQueue -> SettlementEngine -> Handler`
- Layer 2 采用防御快照

---

## 2. 模块分层

### 2.1 流程层

- `RoundManager`
- `SettlementEngine`

职责：

- 驱动整回合流程
- 协调出牌、定策结算和死亡检查

### 2.2 Manager 层

- `CardManager`
- `BuffManager`
- `TriggerManager`
- `ValueModifierManager`

职责：

- 管理卡牌区位和实例
- 管理 Buff 生命周期
- 管理 Trigger 注册与排队
- 管理数值修正

### 2.3 Resolver 层

- `TargetResolver`
- `ConditionChecker`
- `DynamicParamResolver`

职责：

- 解析目标
- 判断条件
- 解析动态表达式

### 2.4 Foundation / Context 层

- `BattleContext`
- `Entity`
- `BattleCard`
- `BattleCardDefinition`
- `EffectUnit`
- `TriggerUnit`
- `BuffUnit`

职责：

- 提供运行时数据结构
- 作为 BattleCore 真正共享的状态载体

---

## 3. 关键约束

### 3.1 Buff 真源

当前唯一真源是：

- `BuffManager`

不再把以下内容当作运行时权威：

- `Entity.ActiveBuffs`
- 玩家侧或实体侧镜像字段

### 3.2 卡牌主路径

当前不允许裸效果直接伪造出牌。

统一主路径是：

1. 拿到真实 `BattleCard`
2. 通过 `ConfigId` 取 `BattleCardDefinition`
3. 取出 `Effects`
4. 进入 `RoundManager`
5. 进入 `SettlementEngine`

### 3.3 Trigger 执行链

当前 TriggerManager 只负责：

- 注册
- 筛选
- 入队

它不直接执行效果。

统一执行链是：

`TriggerManager -> PendingEffectQueue -> SettlementEngine -> HandlerPool`

---

## 4. BattleContext

`BattleContext` 是一局战斗的核心状态容器。

当前包含：

- `BattleId`
- `CurrentRound`
- `CurrentPhase`
- 玩家集合
- `PendingEffectQueue`
- `RoundLog`
- `EventBus`
- 各类 Manager
- `CardDefinitionProvider`

它不再承担历史遗留的 `HistoryLog` 归档职责。

---

## 5. 卡牌架构

### 5.1 BattleCard

`BattleCard` 负责运行时实例状态：

- `InstanceId`
- `ConfigId`
- `OwnerId`
- `Zone`
- `TempCard`
- `IsExhaust`
- `IsStatCard`

### 5.2 BattleCardDefinition

`BattleCardDefinition` 负责 BattleCore 的最小执行定义：

- `ConfigId`
- `IsExhaust`
- `IsStatCard`
- `Effects`

BattleCore 通过 `BattleContext.CardDefinitionProvider` 获取它。

---

## 6. 回合流程

### 6.1 InitBattle

`RoundManager.InitBattle()` 当前负责：

- 初始化战斗状态
- 清空挂起定策列表
- 发布 `BattleStartEvent`

### 6.2 BeginRound

当前顺序：

1. 回合号 +1
2. 更新 `CurrentRound / CurrentPhase`
3. 重置玩家回合统计
4. 发布 `RoundStartEvent`
5. `OnRoundStart`
6. `DrainPendingQueue`
7. `CardManager.OnRoundStart()`
8. 再次 `DrainPendingQueue`
9. 进入 `PlayerAction`

### 6.3 EndRound

当前顺序：

1. 进入 `Settlement`
2. 扫描状态牌
3. `DrainPendingQueue`
4. 定策牌统一结算
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

- `RoundManager.PlayInstantCard()`

主逻辑：

- 校验实例归属
- 解析 `BattleCardDefinition`
- 校验出牌限制
- 记录回合统计
- 进入 `SettlementEngine.ResolveInstantFromCard()`

### 7.2 定策牌

入口：

- `RoundManager.CommitPlanCard()`

主逻辑：

- 校验实例归属
- 解析 `BattleCardDefinition`
- 校验出牌限制
- 移入 `StrategyZone`
- 写入 `PendingPlanCard`
- 回合末统一结算

---

## 8. 五层结算

当前层级：

- `Counter`
- `Defense`
- `Damage`
- `Resource`
- `BuffSpecial`

其中：

- Layer 2 前会拍防御快照
- Layer 2 后会清快照
- Buff、Trigger 派生效果最终也回到同一条 Handler 链路

---

## 9. Buff 与 Trigger

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

当前按两个方向区分：

- `OutgoingDamage`
- `IncomingDamage`

`Strength/Weak` 作用于出伤，`Armor/Vulnerable` 作用于入伤。

---

## 10. 当前保留但未主打的能力

以下能力仍存在枚举或预留结构，但不应被当作当前主配置能力：

- `BeforeDealDamage`
- `BeforeTakeDamage`
- 更复杂的分路与团队目标系统

---

## 11. 当前有效参考

- [SettlementRules.md](../GameDesign/SettlementRules.md)
- [CardSystem.md](../GameDesign/CardSystem.md)
- [ConfigSystem.md](ConfigSystem.md)

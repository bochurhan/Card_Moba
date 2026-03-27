# Shared 架构说明

**文档版本**: 2026-03-27  
**状态**: 当前契约  
**适用范围**: `Shared/` 顶层结构、`Shared/BattleCore/` 子目录职责、`Rules` 当前落地边界

## 1. 目标

这份文档只回答三件事：

- `Shared/` 三个顶层模块分别负责什么
- `Shared/BattleCore/` 当前各子目录的职责边界是什么
- `Rules` 这层当前已经落了什么，后续准备继续收什么

这份文档不替代：

- [SystemArchitecture_V2.md](SystemArchitecture_V2.md)：BattleCore 当前运行时主流程
- [ConfigSystem.md](ConfigSystem.md)：配置链路与作者入口
- [CardSystem.md](../GameDesign/CardSystem.md)：卡牌语义与出牌链路

## 2. Shared 顶层结构

当前 `Shared/` 只保留三块：

- `Protocol`
- `ConfigModels`
- `BattleCore`

### 2.1 Protocol

`Protocol` 是跨层共享的公共语言层。

它负责：

- 效果类型、Buff 类型、卡牌标签等公共枚举
- 前后端共享的协议常量与公共定义

它不负责：

- 配置语义
- 运行时结算

### 2.2 ConfigModels

`ConfigModels` 是配置语义层。

它负责：

- 卡牌配置模型
- 效果配置模型
- 作者侧真正可编辑的字段集合

它不负责：

- 运行时牌区状态
- 结算执行
- Unity 编辑器逻辑

`ConfigModels` 应依赖 `Protocol`，不应反向依赖 `BattleCore`。

### 2.3 BattleCore

`BattleCore` 是纯运行时战斗引擎。

它负责：

- 战斗状态
- 出牌流程
- 定策快照
- 结算流程
- Buff / Trigger / Modifier / Cost / Rule 等运行时机制

它不负责：

- Unity UI
- 客户端表现
- 配置文件读取

### 2.4 ConfigModels、Definitions、Foundation 的区别

这三个概念最容易混。

建议按下面这条边界理解：

- `ConfigModels`
  - 作者侧配置语义
  - 负责表达“策划真正编辑的配置长什么样”
  - 例如 [CardConfig.cs](../..//Shared/ConfigModels/Card/CardConfig.cs)
- `Definitions`
  - BattleCore 直接消费的静态定义
  - 负责表达“运行时会读取，但本身不会在战斗中变化的模板”
  - 例如 [BuffConfig.cs](../..//Shared/BattleCore/Definitions/BuffConfig.cs)
- `Foundation`
  - 当前已细分为 `Cards / Effects / Entities / Triggers`
  - BattleCore 运行时基础模型
  - 负责表达“战斗里当前到底有什么对象、这些对象当前状态是什么”
  - 例如 [BattleCard.cs](../..//Shared/BattleCore/Foundation/Cards/BattleCard.cs)、[BuffUnit.cs](../..//Shared/BattleCore/Foundation/Entities/BuffUnit.cs)

可以把这三层理解成：

- `ConfigModels`：作者世界
- `Definitions`：BattleCore 静态模板世界
- `Foundation`：BattleCore 运行时实例世界

当前这条边界已经基本成立。`BuffConfig` 和 `BattleCardDefinition` 都已经归入 `Definitions/`，但 `Foundation/` 仍然承载较多基础运行时模型，后续仍可继续细分。

## 3. 依赖方向

建议保持下面这条依赖方向不变：

- `Protocol` <- `ConfigModels`
- `Protocol` <- `BattleCore`

`ConfigModels` 和 `BattleCore` 应尽量解耦，由客户端适配层连接。

## 4. BattleCore 子目录职责

### 4.1 Context

`Context` 是运行时状态容器层。

当前主要类型：

- [BattleContext.cs](../..//Shared/BattleCore/Context/BattleContext.cs)
- [PlayerData.cs](../..//Shared/BattleCore/Context/PlayerData.cs)
- [PendingEffectQueue.cs](../..//Shared/BattleCore/Context/PendingEffectQueue.cs)
- [PendingPlanSnapshot.cs](../..//Shared/BattleCore/Context/PendingPlanSnapshot.cs)

职责：

- 保存整局战斗状态
- 保存玩家运行时状态
- 保存定策快照与待执行队列

它只表达“当前状态是什么”，不负责推进流程。

当前 1v1 主线不再保留分路状态容器；旧的 `LaneData` 已经归档，等待后续 3v3 / 分路系统重写。

### 4.2 Core

`Core` 是流程编排层。

当前主要类型：

- [RoundManager.cs](../..//Shared/BattleCore/Core/RoundManager.cs)
- [SettlementEngine.cs](../..//Shared/BattleCore/Core/SettlementEngine.cs)
- [BattleFactory.cs](../..//Shared/BattleCore/Core/BattleFactory.cs)

职责：

- 驱动回合生命周期
- 协调出牌与结算
- 创建 BattleCore 运行时对象

它回答的是“什么时候做什么”。

### 4.3 Costs

`Costs` 是费用计算层。

当前主要类型：

- [PlayCostResolver.cs](../..//Shared/BattleCore/Costs/PlayCostResolver.cs)
- [PlayCostResolution.cs](../..//Shared/BattleCore/Costs/PlayCostResolution.cs)

当前边界：

- 只负责计算这张牌这次实际耗能
- 输入是基础费用和规则结果
- 输出是 `BaseCost / FinalCost`

它不再负责：

- 腐化次数提交
- 出牌后去向改写

### 4.4 Handlers

`Handlers` 是 effect 执行层。

当前主要类型：

- [IEffectHandler.cs](../..//Shared/BattleCore/Handlers/IEffectHandler.cs)
- [HandlerPool.cs](../..//Shared/BattleCore/Handlers/HandlerPool.cs)
- [CoreHandlers.cs](../..//Shared/BattleCore/Handlers/CoreHandlers.cs)

职责：

- 执行具体 effect
- 产出 `EffectResult`

### 4.5 Resolvers

`Resolvers` 是配置语义解释层。

当前主要类型：

- [DynamicParamResolver.cs](../..//Shared/BattleCore/Resolvers/DynamicParamResolver.cs)
- [ConditionChecker.cs](../..//Shared/BattleCore/Resolvers/ConditionChecker.cs)
- [TargetResolver.cs](../..//Shared/BattleCore/Resolvers/TargetResolver.cs)

职责：

- 解析数值表达式
- 判断条件是否成立
- 解析目标集合

它们更像解释器，不持有长期状态。

### 4.6 Managers

`Managers` 是运行时状态协调层。

当前主要类型：

- [CardManager.cs](../..//Shared/BattleCore/Managers/CardManager.cs)
- [BuffManager.cs](../..//Shared/BattleCore/Managers/BuffManager.cs)
- [TriggerManager.cs](../..//Shared/BattleCore/Managers/TriggerManager.cs)
- [ICardManager.cs](../..//Shared/BattleCore/Managers/ICardManager.cs)
- [IBuffManager.cs](../..//Shared/BattleCore/Managers/IBuffManager.cs)
- [ITriggerManager.cs](../..//Shared/BattleCore/Managers/ITriggerManager.cs)

职责：

- 维护牌区流转
- 维护 Buff 生命周期
- 维护 Trigger 注册与入队

它们负责“状态怎么变化和维护”，不是“规则怎么解释”。

### 4.7 Modifiers

`Modifiers` 是结算数值修正规则层。

当前主要类型：

- [IValueModifierManager.cs](../..//Shared/BattleCore/Modifiers/IValueModifierManager.cs)
- [ValueModifierManager.cs](../..//Shared/BattleCore/Modifiers/ValueModifierManager.cs)
- [ValueModifier.cs](../..//Shared/BattleCore/Modifiers/ValueModifier.cs)
- [ModifierType.cs](../..//Shared/BattleCore/Modifiers/ModifierType.cs)
- [ModifierScope.cs](../..//Shared/BattleCore/Modifiers/ModifierScope.cs)

职责：

- 对已确定的基础结算值做阶段修正
- 当前主要服务伤害、治疗、护盾等结算值

它不是表达式解释器，也不是 Buff 本身。

### 4.8 Rules

`Rules` 是操作规则判定层。

当前第一阶段已经落地：

- [DrawRuleResolver.cs](../..//Shared/BattleCore/Rules/Draw/DrawRuleResolver.cs)
- [DrawRuleResolution.cs](../..//Shared/BattleCore/Rules/Draw/DrawRuleResolution.cs)
- [PlayRuleResolver.cs](../..//Shared/BattleCore/Rules/Play/PlayRuleResolver.cs)
- [PlayRuleResolution.cs](../..//Shared/BattleCore/Rules/Play/PlayRuleResolution.cs)
- [PlayOrigin.cs](../..//Shared/BattleCore/Rules/Play/PlayOrigin.cs)

当前职责：

- 判断一次抽牌是否允许
- 判断一次出牌是否允许
- 判断一次出牌是否命中腐化等规则
- 把规则结果拆成：
  - 费用影响
  - 成功出牌后的副作用影响

它不负责：

- Buff 生命周期
- 目标解析
- 具体 effect 执行
- 牌区移动

### 4.9 EventBus

`EventBus` 是对外观测层。

当前主要类型：

- [BattleEventBus.cs](../..//Shared/BattleCore/EventBus/BattleEventBus.cs)
- [BattleEvents.cs](../..//Shared/BattleCore/EventBus/BattleEvents.cs)

职责：

- 把 BattleCore 中的关键事件向外发布
- 服务测试观测、客户端日志与 UI 适配

它不是规则执行主链的一部分。

### 4.10 Foundation

`Foundation` 是 BattleCore 基础运行时模型层。

当前主要类型包括：

- `Foundation/Cards`
  - [BattleCard.cs](../..//Shared/BattleCore/Foundation/Cards/BattleCard.cs)
  - [CardZone.cs](../..//Shared/BattleCore/Foundation/Cards/CardZone.cs)
- `Foundation/Effects`
  - [EffectUnit.cs](../..//Shared/BattleCore/Foundation/Effects/EffectUnit.cs)
  - [EffectResult.cs](../..//Shared/BattleCore/Foundation/Effects/EffectResult.cs)
  - [EffectUnitCloner.cs](../..//Shared/BattleCore/Foundation/Effects/EffectUnitCloner.cs)
- `Foundation/Entities`
  - [Entity.cs](../..//Shared/BattleCore/Foundation/Entities/Entity.cs)
  - [BuffUnit.cs](../..//Shared/BattleCore/Foundation/Entities/BuffUnit.cs)
- `Foundation/Triggers`
  - [TriggerUnit.cs](../..//Shared/BattleCore/Foundation/Triggers/TriggerUnit.cs)
  - [TriggerContext.cs](../..//Shared/BattleCore/Foundation/Triggers/TriggerContext.cs)
  - [TriggerTiming.cs](../..//Shared/BattleCore/Foundation/Triggers/TriggerTiming.cs)

职责：

- 提供 BattleCore 共用的基础运行时数据结构

这里需要特别注意：

- [BattleCard.cs](../..//Shared/BattleCore/Foundation/Cards/BattleCard.cs) 和 [BuffUnit.cs](../..//Shared/BattleCore/Foundation/Entities/BuffUnit.cs) 是标准的运行时实例模型
- `Foundation` 现在更接近“BattleCore 基础运行时模型目录”，而不是“静态定义目录”

也就是说，`Foundation` 当前承载的是运行时对象和运行时结果，不再包含 `BattleCardDefinition` 这类静态模板。

### 4.11 Definitions

`Definitions` 是 BattleCore 静态定义目录。

当前文件：

- [BuffConfig.cs](../..//Shared/BattleCore/Definitions/BuffConfig.cs)
- [BattleCardDefinition.cs](../..//Shared/BattleCore/Definitions/BattleCardDefinition.cs)

这意味着：

- 运行时 Buff 实例是 [BuffUnit.cs](../..//Shared/BattleCore/Foundation/Entities/BuffUnit.cs)
- `Definitions/` 目录放的是 BattleCore 会直接读取的静态模板
- 这类模板本身不在战斗中变化

当前这里已经至少包含两类 BattleCore 静态定义：

- [BuffConfig.cs](../..//Shared/BattleCore/Definitions/BuffConfig.cs)
- [BattleCardDefinition.cs](../..//Shared/BattleCore/Definitions/BattleCardDefinition.cs)

这个目录现在已经具备独立存在的价值。

### 4.12 Random

`Random` 是确定性随机工具层。

当前主要类型：

- [SeededRandom.cs](../..//Shared/BattleCore/Random/SeededRandom.cs)

职责单一，保持现状即可。

## 5. 当前最清晰与仍需继续收口的部分

当前边界最清晰的部分：

- `Context`
- `Core`
- `Handlers`
- `Resolvers`
- `Modifiers`
- `Rules` 第一阶段
- `Definitions` 的概念边界

当前仍值得继续收口的部分：

- `Foundation`
- `EventBus`

原因分别是：

- `Foundation` 当前同时放了运行时实例模型和一部分 definition 语义模型
- `Foundation` 承载内容较多，后续可以按领域再细分
- `EventBus` 当前更接近单向观测输出口，而不是完整 pub/sub 系统

## 6. Rules 当前落地边界

当前已经迁入 Rule 的规则只有三条：

- `NoDrawThisTurn`
- `NoDamageCardThisTurn`
- `Corruption`

当前职责分工如下：

- `BuffManager`
  - 维护 Buff 真状态
- `DrawRuleResolver`
  - 判断一次抽牌是否允许
- `PlayRuleResolver`
  - 判断一次出牌是否允许
  - 判断一次出牌是否命中腐化
  - 产出费用与成功后副作用结果
- `PlayCostResolver`
  - 只根据规则结果计算实际耗能
- `RoundManager / CardManager`
  - 在抽牌、出牌流程中调用规则层
  - 在成功后提交规则副作用

## 7. Rule 与其他模块的关系

### 7.1 Rule 与 Buff

- `Buff` 是状态真源
- `Rule` 读取 Buff 状态并解释成这次操作的规则结果

### 7.2 Rule 与 Costs

- `Rule` 产出费用相关结果
- `Costs` 只消费这些结果并计算最终耗能

### 7.3 Rule 与 Modifiers

- `Rule` 修正的是操作本身
- `Modifiers` 修正的是结算值

### 7.4 Rule 与 Managers / Core

- `Managers` 和 `Core` 负责流程与状态变更
- `Rule` 给它们提供是否允许、费用如何变化、成功后要提交什么副作用

## 8. 后续整理方向

后续继续整理 `Shared/BattleCore` 时，建议优先顺序：

1. 继续收口 `EventBus` 的命名与定位；如果它长期只做观测，可考虑明确成单向事件输出口
2. 继续把操作规则从流程层收进 `Rules`，优先是“下一张定策牌额外结算一次”这类真正的操作规则
3. 视需要评估 `StrategyZone` 兼容枚举值是否还能继续保留

## 9. 当前有效参考

- [SystemArchitecture_V2.md](SystemArchitecture_V2.md)
- [ConfigSystem.md](ConfigSystem.md)
- [CardSystem.md](../GameDesign/CardSystem.md)
- [SettlementRules.md](../GameDesign/SettlementRules.md)

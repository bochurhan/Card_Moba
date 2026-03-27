# Shared 当前架构

**文档版本**: 2026-03-27  
**状态**: 当前契约  
**适用范围**: `Shared/` 当前代码组织、职责边界与 `Rules` 模块设计方向

---

## 1. 目标

这份文档回答三件事：

- `Shared/` 三个顶层模块各自负责什么
- `Shared/BattleCore/` 当前子目录的职责边界是什么
- `Rules` 为什么需要单独成层，以及建议怎样落地

这份文档不替代：

- [SystemArchitecture_V2.md](SystemArchitecture_V2.md)：BattleCore 当前运行时主流程
- [ConfigSystem.md](ConfigSystem.md)：配置链路
- [CardSystem.md](../GameDesign/CardSystem.md)：卡牌与出牌语义

---

## 2. Shared 顶层结构

当前 `Shared/` 只有三块：

- `Protocol`
- `ConfigModels`
- `BattleCore`

### 2.1 Protocol

`Protocol` 是共享枚举与协议层。

它负责：

- 效果类型、Buff 类型、卡牌标签等跨层公共枚举
- 前后端共用的协议定义

它不负责：

- 配置语义
- 运行时结算

可以把它理解成整个项目的公共语言层。

### 2.2 ConfigModels

`ConfigModels` 是配置语义层。

它负责：

- 卡牌配置长什么样
- 效果配置长什么样
- 哪些字段属于作者侧输入

它不负责：

- 战斗运行时状态
- 牌区流转
- 结算时真实执行

`ConfigModels` 应依赖 `Protocol`，但不应依赖 `BattleCore`。

### 2.3 BattleCore

`BattleCore` 是纯运行时战斗引擎。

它负责：

- 战斗状态容器
- 出牌流程
- 定策快照
- 结算流程
- Buff / Trigger / Modifier / Cost 等运行时机制

它不负责：

- Unity 表现
- 客户端 UI
- 配置文件读取

BattleCore 应只依赖：

- `Protocol`
- 少量必要的共享配置桥接结果

---

## 3. 依赖方向

建议把依赖关系明确成：

- `Protocol` <- `ConfigModels`
- `Protocol` <- `BattleCore`
- `ConfigModels` 与 `BattleCore` 尽量解耦

也就是说：

- `ConfigModels` 不要反向依赖 `BattleCore`
- `BattleCore` 不直接知道编辑器或配置文件格式
- 客户端通过适配层把 `ConfigModels` 转成 `BattleCore` 可执行结构

---

## 4. BattleCore 子目录职责

当前 `Shared/BattleCore/` 子目录如下：

- `Buff`
- `Context`
- `Core`
- `Costs`
- `EventBus`
- `Foundation`
- `Handlers`
- `Managers`
- `Modifiers`
- `Random`
- `Resolvers`

### 4.1 Context

`Context` 是运行时状态容器层。

当前主要类型：

- [BattleContext.cs](/d:/Card_Moba/Shared/BattleCore/Context/BattleContext.cs)
- [PlayerData.cs](/d:/Card_Moba/Shared/BattleCore/Context/PlayerData.cs)
- [PendingEffectQueue.cs](/d:/Card_Moba/Shared/BattleCore/Context/PendingEffectQueue.cs)
- [PendingPlanSnapshot.cs](/d:/Card_Moba/Shared/BattleCore/Context/PendingPlanSnapshot.cs)
- [LaneData.cs](/d:/Card_Moba/Shared/BattleCore/Context/LaneData.cs)

职责：

- 保存整局战斗状态
- 保存玩家运行时状态
- 保存待执行队列与定策快照

它只表达“当前状态是什么”，不负责流程推进。

### 4.2 Core

`Core` 是流程编排层。

当前主要类型：

- [RoundManager.cs](/d:/Card_Moba/Shared/BattleCore/Core/RoundManager.cs)
- [SettlementEngine.cs](/d:/Card_Moba/Shared/BattleCore/Core/SettlementEngine.cs)
- [BattleFactory.cs](/d:/Card_Moba/Shared/BattleCore/Core/BattleFactory.cs)

职责：

- 驱动回合生命周期
- 驱动瞬策与定策结算
- 创建 BattleCore 所需对象

它回答的是“什么时候做什么”。

### 4.3 Costs

`Costs` 是费用计算层。

当前主要类型：

- [PlayCostResolver.cs](/d:/Card_Moba/Shared/BattleCore/Costs/PlayCostResolver.cs)
- [PlayCostResolution.cs](/d:/Card_Moba/Shared/BattleCore/Costs/PlayCostResolution.cs)

建议职责边界：

- 只计算“这张牌这次实际要花多少能量”
- 不负责提交腐化次数
- 不负责结算后改去向

也就是说，`Costs` 只处理 `ActualCost`，不处理其他出牌副作用。

### 4.4 Handlers

`Handlers` 是 effect 执行层。

当前主要类型：

- [IEffectHandler.cs](/d:/Card_Moba/Shared/BattleCore/Handlers/IEffectHandler.cs)
- [HandlerPool.cs](/d:/Card_Moba/Shared/BattleCore/Handlers/HandlerPool.cs)
- [CoreHandlers.cs](/d:/Card_Moba/Shared/BattleCore/Handlers/CoreHandlers.cs)

职责：

- 真正执行单个效果
- 修改 HP、护盾、Buff、牌区等运行时状态

它回答的是“这个 effect 具体怎么落地执行”。

### 4.5 Resolvers

`Resolvers` 是语义解释层。

当前主要类型：

- [DynamicParamResolver.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/DynamicParamResolver.cs)
- [ConditionChecker.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/ConditionChecker.cs)
- [TargetResolver.cs](/d:/Card_Moba/Shared/BattleCore/Resolvers/TargetResolver.cs)

职责：

- 把表达式解析成数值
- 把目标语义解析成实体集合
- 把条件语义解析成布尔结果

它不持有长期状态，更像解释器。

### 4.6 Managers

`Managers` 是运行时状态协调层。

当前主要类型：

- [CardManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/CardManager.cs)
- [BuffManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/BuffManager.cs)
- [TriggerManager.cs](/d:/Card_Moba/Shared/BattleCore/Managers/TriggerManager.cs)
- [IManagers.cs](/d:/Card_Moba/Shared/BattleCore/Managers/IManagers.cs)

职责：

- 维护牌区流转
- 维护 Buff 生命周期
- 维护 Trigger 注册与入队

它回答的是“运行时状态怎么变化和维护”。

### 4.7 Modifiers

`Modifiers` 是数值修正规则层。

当前主要类型：

- [IValueModifierManager.cs](/d:/Card_Moba/Shared/BattleCore/Modifiers/IValueModifierManager.cs)
- [ValueModifierManager.cs](/d:/Card_Moba/Shared/BattleCore/Modifiers/ValueModifierManager.cs)
- [ValueModifier.cs](/d:/Card_Moba/Shared/BattleCore/Modifiers/ValueModifier.cs)
- [ModifierType.cs](/d:/Card_Moba/Shared/BattleCore/Modifiers/ModifierType.cs)
- [ModifierScope.cs](/d:/Card_Moba/Shared/BattleCore/Modifiers/ModifierScope.cs)

职责：

- 对已确定的基础数值做阶段修正
- 当前主要服务结算值修正

它不是表达式解释器，也不是 Buff 本身。

### 4.8 EventBus

`EventBus` 是对外观测层。

当前主要类型：

- [BattleEventBus.cs](/d:/Card_Moba/Shared/BattleCore/EventBus/BattleEventBus.cs)
- [BattleEvents.cs](/d:/Card_Moba/Shared/BattleCore/EventBus/BattleEvents.cs)

职责：

- 把 BattleCore 里的关键事件向外发布
- 服务测试观测、客户端日志和 UI 适配

它不是规则主流程的核心依赖。  
当前更接近“事件输出口”，而不是 BattleCore 内部规则总线。

### 4.9 Foundation

`Foundation` 是 BattleCore 基础运行时模型层。

当前主要类型包括：

- [BattleCard.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/BattleCard.cs)
- [BattleCardDefinition.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/BattleCardDefinition.cs)
- [Entity.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/Entity.cs)
- [EffectUnit.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/EffectUnit.cs)
- [EffectResult.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/EffectResult.cs)
- [TriggerUnit.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/TriggerUnit.cs)
- [TriggerContext.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/TriggerContext.cs)
- [TriggerTiming.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/TriggerTiming.cs)
- [BuffUnit.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/BuffUnit.cs)

职责：

- 提供运行时共享基础模型
- 作为 BattleCore 内部通用数据结构层

当前目录名还能用，但后续如果继续细分，最适合拆成：

- `Cards`
- `Effects`
- `Entities`
- `Triggers`

### 4.10 Buff

`Buff` 目录当前只剩配置定义：

- [BuffConfig.cs](/d:/Card_Moba/Shared/BattleCore/Buff/BuffConfig.cs)

当前语义应明确为：

- 这里放 Buff 配置定义
- 运行时 Buff 实例是 [BuffUnit.cs](/d:/Card_Moba/Shared/BattleCore/Foundation/BuffUnit.cs)

也就是说，这个目录已经不是“Buff 系统主实现目录”，更像定义目录。

### 4.11 Random

`Random` 是确定性随机工具层。

当前主要类型：

- [SeededRandom.cs](/d:/Card_Moba/Shared/BattleCore/Random/SeededRandom.cs)

职责清晰，保持现状即可。

---

## 5. 当前最清晰与最需要继续收口的部分

当前边界最清晰的部分：

- `Context`
- `Core`
- `Handlers`
- `Resolvers`
- `Modifiers`

当前还需要继续收口的部分：

- `Costs`
- `Buff`
- `Foundation`
- `EventBus`

原因分别是：

- `Costs` 开始承载出牌规则副作用语义
- `Buff` 现在只剩配置定义，目录命名和职责不完全一致
- `Foundation` 承载内容过多，名字过宽
- `EventBus` 当前更像单向事件输出口，而不是完整 pub/sub 总线

---

## 6. 为什么需要 Rules

当前最典型的问题是 `腐化`。

从产品语义看，`腐化` 包含三件事：

- 这次牌费用显示为 0
- 这次实际支付费用为 0
- 成功由玩家主动打出后：
  - 扣除腐化次数
  - 这张牌结算后改为 `Consume`

这里真正麻烦的点不是“费用变 0”，而是：

- 它依赖玩家身上的 Buff
- 它依赖每回合剩余次数
- 它依赖出牌来源是否为玩家主动从手牌打出
- 它还会带来出牌成功后的额外副作用

这已经超出了纯 `CostResolver` 的职责。

---

## 7. Rule 的职责

`Rule` 是操作规则判定层。

它负责回答：

- 这次操作是否允许
- 这次操作命中了哪些规则
- 这些规则会给这次操作带来什么结果

它不负责：

- Buff 生命周期
- 表达式解析
- 目标解析
- 具体效果执行
- 牌区流转

换句话说，`Rule` 不是新的大总管，而是把运行时状态解释成操作规则结果的那一层。

---

## 8. Rule 与其他模块的关系

### 8.1 Rule 与 Buff

- `Buff` 是状态真源
- `Rule` 读取 Buff 状态来判断规则是否命中

例如：

- `NoDrawThisTurn`
- `NoDamageCardThisTurn`
- `Corruption`

都仍然应该是 Buff，只是这些 Buff 对一次操作意味着什么，由 `Rule` 来解释。

### 8.2 Rule 与 Costs

- `Rule` 先产出费用相关指令
- `CostResolver` 再只做费用计算

例如：

- `Rule` 判断本次命中腐化
- 产出 `SetTo(0)`
- `CostResolver` 只根据这个指令算最终耗能

因此：

- `CostResolver` 只负责“这次要花多少”
- `Rule` 负责“为什么会这样，以及是否还带别的副作用”

### 8.3 Rule 与 Modifiers

- `Modifier` 修正的是结算值
- `Rule` 修正的是操作本身

也就是说：

- 力量、虚弱、易伤仍然属于 `Modifiers`
- `NoDrawThisTurn`、`NoDamageCardThisTurn`、`Corruption` 这类属于 `Rules`

### 8.4 Rule 与 Resolvers

- `Resolvers` 负责解释配置语义
- `Rule` 负责根据当前状态判断操作规则

可以简单理解成：

- `Resolvers` 更像解释器
- `Rule` 更像策略判定器

### 8.5 Rule 与 Managers

- `Managers` 负责状态维护
- `Rule` 负责状态解释

例如：

- `CardManager` 管抽牌
- `DrawRuleResolver` 判断这次抽牌是否允许

例如：

- `RoundManager` 管出牌主流程
- `PlayRuleResolver` 判断这次出牌是否允许、费用怎么变、成功后附带什么规则副作用

### 8.6 Rule 与 Core

- `Core` 负责流程编排
- `Rule` 负责给编排层提供规则判定结果

因此：

- `RoundManager` 不应该长期自己直接查 `NoDamageCardThisTurn`
- `CardManager` 也不应该长期自己直接查 `NoDrawThisTurn`

这些判断应该逐步迁入 `Rules`

---

## 9. Rules 模块设计草案

建议新增：

- `Shared/BattleCore/Rules/`

第一阶段先只做：

- `Rules/Draw/`
- `Rules/Play/`

### 9.1 建议分层

建议拆成三层：

1. `DrawRuleResolver`
- 判断这次抽牌是否允许

2. `PlayRuleResolver`
- 判断这次出牌是否允许
- 判断这次是否命中某条出牌规则
- 产出费用相关 directive
- 产出成功后要提交的 directive

3. `RoundManager / CardManager`
- 在操作成功后提交规则副作用

### 9.2 建议的数据结构

建议引入：

- `PlayOrigin`
  - `PlayerHandPlay`
  - `AutoCast`
  - `TriggerCast`
  - `SystemCast`

- `PlayRuleResolution`
  - `Allowed`
  - `BlockReason`
  - `AppliedRuleTags`
  - `CostDirectives`
  - `PostPlayDirectives`

- `DrawRuleResolution`
  - `Allowed`
  - `BlockReason`

- `CostDirective`
  - `Add`
  - `Subtract`
  - `SetTo`
  - `MinClamp`
  - `MaxClamp`

- `PostPlayDirective`
  - `ForceConsumeAfterResolve`
  - `ConsumeCorruptionCharge`

### 9.3 腐化示例

以 `腐化` 为例，理想流程应是：

1. 回合开始  
   - 从 `Corruption` Buff 读总值  
   - 重置 `PlayerData.CorruptionFreePlaysRemainingThisRound`

2. 玩家从手牌主动打牌  
   - `PlayOrigin = PlayerHandPlay`

3. `PlayRuleResolver` 判断  
   - 玩家有 `Corruption` Buff
   - 剩余次数 > 0
   - 来源是 `PlayerHandPlay`
   - 则命中腐化规则

4. `CostResolver` 只处理费用  
   - 看到 `SetTo(0)` 规则
   - 返回 `ActualCost = 0`

5. 出牌成功后  
   - `RoundManager` 根据 `PostPlayDirectives`
   - 扣腐化次数
   - 设置 `forceConsumeAfterResolve`

这样以后 `CostResolver` 就可以重新保持纯粹：

- 只负责算耗能
- 不负责决定腐化副作用何时提交

---

## 10. 建议的迁移顺序

如果继续整理当前架构，建议按这个顺序推进：

1. 保持 `Costs` 只负责实际耗能计算
2. 新增 `Rules/Draw/`、`Rules/Play/`
3. 先把三条规则迁进去：
   - `NoDrawThisTurn`
   - `NoDamageCardThisTurn`
   - `Corruption`
4. 再根据复杂度决定是否补：
   - `Rules/Resolve/`
   - `Rules/Restrictions/`

当前不建议：

- 把 `Cost` 直接并入 `Modifiers`
- 把 `Rules` 继续塞回 `RoundManager`

原因很简单：

- `Modifiers` 是结算值修正规则
- `Costs` 是资源消耗计算
- `Rules` 是操作合法性与副作用判定

它们相关，但不应混成一个模块。

---

## 11. 当前有效参考

- [SystemArchitecture_V2.md](SystemArchitecture_V2.md)
- [ConfigSystem.md](ConfigSystem.md)
- [CardSystem.md](../GameDesign/CardSystem.md)
- [SettlementRules.md](../GameDesign/SettlementRules.md)

这份文档主要补的是：

- `Shared/` 顶层职责
- `BattleCore` 子目录边界
- `Rules` 模块设计方向

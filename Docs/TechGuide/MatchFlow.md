# MatchFlow 当前架构

**文档版本**: 2026-03-28  
**状态**: 当前契约  
**适用范围**: `Shared/MatchFlow` 当前整局流程、构筑窗口、装备钩子与配置接入边界

## 1. 目标

`MatchFlow` 是位于 `BattleCore` 之上的整局状态机。

它回答的问题不是“单场战斗怎么结算”，而是：

- 一整局由哪些 step 组成
- 每个 step 如何启动一场 battle
- battle 结束后如何把结果回写到整局持久状态
- 何时进入 `BuildWindow`
- 职业池、构筑候选、装备被动如何作用于整局状态

当前分层如下：

- `ConfigModels`
  - 作者侧卡牌配置语义
- `MatchFlow.Catalog`
  - 把 `CardConfig` 装配成 `IBuildCatalog`
- `MatchFlow`
  - 整局流程、持久卡组、构筑窗口、装备 runtime
- `BattleCore`
  - 单场战斗结算内核

## 2. 当前主流程

当前 `MatchFlow` 的一次 step 推进顺序是：

1. `MatchManager.StartCurrentBattle()`
2. `BattleSetupBuilder` 把 `MatchContext` 当前 step 转成 `BattleFactory` 入参
3. `BattleFactory.CreateBattle()` 创建 `BattleContext + RoundManager`
4. `EquipmentRuntimeFactory` 为本场 battle 注册装备 runtime
5. `RoundManager` 运行 battle，并在结束时自动生成 `CompletedBattleSummary`
6. `MatchManager.CompleteCurrentBattle()` 消费 `BattleSummary`
7. `MatchStateApplier` 回写跨战斗 HP、死亡状态、额外构筑机会、objective 结果
8. 装备 runtime 执行 `OnBattleEnded`
9. 若当前 step 配置了构筑窗口，则打开 `BuildWindow`
10. 玩家完成构筑后提交回持久状态，进入下一场 battle

## 3. BuildWindow 当前规则

当前已经落地的构筑窗口规则：

- 默认每名玩家每个窗口有 `1` 次构筑机会
- `BattleSummary.ExtraBuildPickPlayerIds` 会给对应玩家额外 `+1` 次机会
- 上一场 battle 死亡的玩家进入 `ForcedRecovery`：
  - 只保留 `1` 次构筑机会
  - 只能选择 `Heal`
  - `Heal` 恢复 `40% MaxHp`
- 其他玩家的 `Heal` 恢复 `30% MaxHp`
- `UpgradeCard`
  - 从当前 `PreviewDeck` 中选 1 张可升级牌
- `RemoveCard`
  - 从当前 `PreviewDeck` 中选 1 张可删除牌
- `AddCard`
  - 生成 `2` 组候选
  - 每组 `3` 张
  - 每组可选 `0` 或 `1` 张
  - 不会出现传说牌
  - 稀有度权重为 `普通:罕见:稀有 = 6:3:1`
  - 每张候选有 `10%` 概率直接以升级版出现
- 窗口是同步推进：
  - 所有人都锁定后可提前进入下一 step
  - 超时则对剩余机会补默认动作
  - 当前默认超时动作是 `Heal`

## 4. 装备钩子当前设计

当前装备不是直接写进 `RoundManager`，而是由 `MatchFlow` 管理 battle 生命周期钩子。

当前入口：

- `EquipmentRuntimeFactory`
- `IEquipmentBattleRuntime`
- `HealAfterBattleFlatEquipmentRuntime`

当前模式：

1. 开始一场 battle 时，为当前参战玩家创建装备 runtime
2. runtime 可订阅本场 battle 的 `IEventBus`
3. battle 结束后，由 `MatchManager` 统一调用 `OnBattleEnded`
4. 装备效果回写到 `PlayerMatchState`

当前已实现的装备效果：

- `burning_blood`
  - 每场 battle 结束后恢复 6 点生命

## 5. 配置接入

`MatchFlow` 当前不直接读取 `cards.json`，而是通过 `CardConfig` 接配置。

当前配置接入链路是：

1. 客户端或服务端配置加载器产出 `CardConfig`
2. 客户端当前可通过 `CardConfigManager.CreateBuildCatalog()` 直接调用 `BuildCatalogAssembler`
3. 组装成 `InMemoryBuildCatalog`
4. `MatchManager / BuildOfferGenerator / BuildActionApplier` 通过 `IBuildCatalog` 读取构筑定义

`BuildCatalogAssembler` 当前默认行为：

- `ConfigId = CardId.ToString()`
- 稀有度映射：
  - `1 -> Common`
  - `2 -> Uncommon`
  - `3 -> Rare`
  - `>= 4 -> Legendary`
- 默认把以下卡排除出奖励池：
  - `Legendary`
  - `Status`
  - 作为其他卡升级目标的卡
- 默认按职业注册 class pool：
  - `class:Warrior`
  - `class:Assassin`
  - `class:Mage`
  - `class:Support`
  - `class:Tank`

当前装备定义尚未进入 `cards.json`，仍由上层手动注册 `EquipmentDefinition`。

## 6. 目录与文件职责

### 6.1 根目录

| 文件 | 职责 |
|------|------|
| `Shared/MatchFlow/CardMoba.MatchFlow.asmdef` | `MatchFlow` 纯 C# 程序集定义，当前依赖 `BattleCore`、`Protocol`、`ConfigModels` |

### 6.2 Context

| 文件 | 职责 |
|------|------|
| `Shared/MatchFlow/Context/MatchContext.cs` | 整局唯一运行时上下文，持有 `Players / Teams / ActiveBattle / ActiveBuildWindow` |
| `Shared/MatchFlow/Context/PlayerMatchState.cs` | 玩家跨战斗持久状态；包含 `PersistentHp`、`Deck`、`BonusBuildPickCount`、`Loadout`、`WasDefeatedInLastBattle` |
| `Shared/MatchFlow/Context/TeamMatchState.cs` | 队伍级持久状态；当前承载队伍成员和 objective 是否被摧毁 |
| `Shared/MatchFlow/Context/BuildWindowState.cs` | 构筑窗口运行时状态；包含每个玩家的 `PlayerBuildWindowState`、机会列表、候选和选择 |

### 6.3 Rules

| 文件 | 职责 |
|------|------|
| `Shared/MatchFlow/Rules/MatchPhase.cs` | 整局阶段枚举 |
| `Shared/MatchFlow/Rules/BattleStepMode.cs` | step 对应的 battle 形态枚举 |
| `Shared/MatchFlow/Rules/BuildActionType.cs` | 构筑动作枚举 |
| `Shared/MatchFlow/Rules/BuildWindowRules.cs` | 构筑窗口参数配置；包括默认/强制回血比例、拿牌组数、候选数量与稀有度权重 |
| `Shared/MatchFlow/Rules/MatchBattleStep.cs` | 单个整局 step 的规则描述；包含 `BattleRuleset`、参战玩家、objective 和 `BuildPoolId` |
| `Shared/MatchFlow/Rules/MatchRuleset.cs` | 整局规则入口；包含所有 steps 和 `BuildWindowRules` |

### 6.4 Deck

| 文件 | 职责 |
|------|------|
| `Shared/MatchFlow/Deck/PersistentDeckCard.cs` | 跨战斗卡牌实例；以 `PersistentCardId` 标识，记录基础牌与当前升级状态 |
| `Shared/MatchFlow/Deck/PersistentDeckState.cs` | 跨战斗卡组；支持查找、添加、删除、导出 `DeckConfig` |

### 6.5 Definitions

| 文件 | 职责 |
|------|------|
| `Shared/MatchFlow/Definitions/BuildDefinitions.cs` | 构筑系统静态定义；包含 `BuildCardDefinition`、`EquipmentDefinition`、`BuildCardRarity`、`EquipmentEffectType` |

### 6.6 Catalog

| 文件 | 职责 |
|------|------|
| `Shared/MatchFlow/Catalog/IBuildCatalog.cs` | 构筑定义读取接口；供 `BuildOfferGenerator` 和 `BuildActionApplier` 消费 |
| `Shared/MatchFlow/Catalog/BuildCatalogAssembler.cs` | `CardConfig -> BuildCatalog` 装配器；负责默认稀有度、奖励池过滤与职业默认池注册 |

### 6.7 Core

| 文件 | 职责 |
|------|------|
| `Shared/MatchFlow/Core/MatchFactory.cs` | 创建 `MatchContext`，注册玩家、队伍和规则 |
| `Shared/MatchFlow/Core/BattleSetupPlan.cs` | battle 启动计划对象；承载 battleId、种子、玩家与 objective 配置 |
| `Shared/MatchFlow/Core/BattleSetupBuilder.cs` | 把当前 step 的持久状态转换成 `BattleFactory` 入参 |
| `Shared/MatchFlow/Core/MatchStateApplier.cs` | 把 `BattleSummary` 回写到 `MatchContext` |
| `Shared/MatchFlow/Core/MatchManager.cs` | 整局状态机主入口；负责 battle、build window、推进和收束 |
| `Shared/MatchFlow/Core/BuildOfferGenerator.cs` | 生成构筑窗口候选；包括回血值、升级候选、删牌候选和两组拿牌候选 |
| `Shared/MatchFlow/Core/BuildActionApplier.cs` | 应用构筑动作到预览状态，并在推进时提交回 `PlayerMatchState` |
| `Shared/MatchFlow/Core/CompositeEventBus.cs` | 把 battle runtime event bus 和外部 event bus 组合起来 |
| `Shared/MatchFlow/Core/EquipmentRuntime.cs` | 装备 runtime 系统；包含 `IEquipmentBattleRuntime`、`EquipmentRuntimeFactory` 与 `burning_blood` 当前实现 |

### 6.8 Results

| 文件 | 职责 |
|------|------|
| `Shared/MatchFlow/Results/MatchSummary.cs` | 整局结果摘要预留类型 |
| `Shared/MatchFlow/Results/BuildWindowSummary.cs` | 构筑窗口结果摘要预留类型 |

## 7. MatchFlow 依赖的 BattleCore 新桥接文件

本轮 `MatchFlow` 搭建时，同时在 `BattleCore` 里新增了几类承接类型：

| 文件 | 职责 |
|------|------|
| `Shared/BattleCore/Core/BattleRuleset.cs` | 单场 battle 规则入口；支持 `RoundLimit / TeamElimination / ObjectiveDestroyed` 等模式 |
| `Shared/BattleCore/Context/BattleTeamState.cs` | battle 内队伍状态；持有 teamId、playerIds、objectiveEntityId |
| `Shared/BattleCore/Results/BattleSummary.cs` | 单场结束摘要；是 `MatchFlow` 推进的核心输入 |

## 8. 当前未完成部分

当前 `MatchFlow` 已能驱动整局骨架，但还没有全部完成：

- `BuildWindowSummary` 和 `MatchSummary` 仍是预留结构
- 真实服务端 `MatchSession` 还未接入
- `EquipmentDefinition` 仍未进入统一配置链路
- 当前 `ExtraBuildPickPlayerIds` 仍基于 `BattleSummary` 提供，未来 2v2 可能需要补团队级奖励规则

## 9. 推荐联动阅读

- [SharedArchitecture.md](SharedArchitecture.md)
- [SystemArchitecture_V2.md](SystemArchitecture_V2.md)
- [ConfigSystem.md](ConfigSystem.md)
- [../GameDesign/InGameDraft.md](../GameDesign/InGameDraft.md)


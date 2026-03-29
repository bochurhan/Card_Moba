# Shared 目录说明

**文档版本**: 2026-03-28  
**状态**: 当前契约

## 1. 目录定位

`Shared/` 放前后端共享的纯逻辑与共享模型。

当前主线包含四块：

- `Protocol`
- `ConfigModels`
- `BattleCore`
- `MatchFlow`

这四块的目标是：

- 统一公共枚举和协议语言
- 统一作者侧配置模型
- 统一单场战斗运行时逻辑
- 统一整局流程、构筑窗口与跨战斗持久状态

## 2. 顶层结构

### 2.1 Protocol

`Protocol/` 是公共语言层。

负责：

- 跨层共享的枚举
- 协议侧公共定义

不负责：

- 配置语义
- 运行时结算

### 2.2 ConfigModels

`ConfigModels/` 是作者侧配置语义层。

负责：

- 卡牌配置模型
- 效果配置模型
- 其他作者侧配置结构

不负责：

- 运行时战斗状态
- 结算执行
- Unity 编辑器逻辑

### 2.3 BattleCore

`BattleCore/` 是纯运行时单场战斗引擎。

负责：

- battle 内状态
- 出牌流程
- 定策快照
- 结算流程
- Buff / Trigger / Modifier / Cost / Rule 等运行时机制

不负责：

- Unity UI
- 服务器表现层
- 配置文件读取
- 整局多场对局流程

### 2.4 MatchFlow

`MatchFlow/` 是位于 `BattleCore` 之上的整局流程层。

负责：

- 整局 `MatchContext`
- 多场 battle step 编排
- 场间构筑窗口
- 跨战斗持久 HP / Deck
- 装备 battle 生命周期钩子
- `CardConfig -> IBuildCatalog` 的构筑侧配置装配

不负责：

- 单场 battle 结算细节
- Unity 配置读取
- 网络协议与房间管理

## 3. 依赖方向

建议保持下面这条依赖方向不变：

- `Protocol` <- `ConfigModels`
- `Protocol` <- `BattleCore`
- `Protocol` <- `MatchFlow`
- `ConfigModels` <- `MatchFlow`
- `BattleCore` <- `MatchFlow`

`ConfigModels` 与 `BattleCore` 仍应尽量解耦，由 `MatchFlow` 或更上层适配器连接。

## 4. 当前重要边界

### 4.1 ConfigModels、Definitions、Foundation

这三层不要混。

- `ConfigModels`
  - 作者侧配置语义
- `Definitions`
  - BattleCore 或 MatchFlow 直接消费的静态模板
- `Foundation`
  - BattleCore 运行时基础模型

当前例子：

- [CardConfig.cs](ConfigModels/Card/CardConfig.cs)
- [BuffConfig.cs](BattleCore/Definitions/BuffConfig.cs)
- [BattleCard.cs](BattleCore/Foundation/Cards/BattleCard.cs)
- [BuildDefinitions.cs](MatchFlow/Definitions/BuildDefinitions.cs)

### 4.2 BattleCore 与 MatchFlow

这两层也不要混。

- `BattleCore`
  - 只负责单场 battle 的即时状态与结算
- `MatchFlow`
  - 负责 battle 之外的整局状态机与持久状态

当前例子：

- 单场结束摘要：`BattleSummary`
- 整局推进：`MatchManager`
- battle 启动计划：`BattleSetupBuilder`
- 构筑窗口：`BuildOfferGenerator` / `BuildActionApplier`

## 5. 当前有效文档

建议从下面几份开始：

- [docs/README.md](../docs/README.md)
- [docs/TechGuide/SharedArchitecture.md](../docs/TechGuide/SharedArchitecture.md)
- [docs/TechGuide/SystemArchitecture_V2.md](../docs/TechGuide/SystemArchitecture_V2.md)
- [docs/TechGuide/MatchFlow.md](../docs/TechGuide/MatchFlow.md)
- [docs/TechGuide/ConfigSystem.md](../docs/TechGuide/ConfigSystem.md)
- [docs/GameDesign/CardSystem.md](../docs/GameDesign/CardSystem.md)
- [docs/GameDesign/InGameDraft.md](../docs/GameDesign/InGameDraft.md)

## 6. 当前不再保留在主线的内容

以下内容已经从当前主线移出，不应继续当成现行契约：

- 旧版分路状态容器
- 旧阶段系统预留枚举
- 把整局流程直接塞进 `BattleCore`

历史版本已归档到：

- [docs/_Archive](../docs/_Archive)

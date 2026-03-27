# Shared 目录说明

**文档版本**: 2026-03-27  
**状态**: 当前契约

## 1. 目录定位

`Shared/` 放前后端共享的纯逻辑与共享模型。

当前只保留三块：

- `Protocol`
- `ConfigModels`
- `BattleCore`

这三块的目标是：

- 统一公共枚举和协议语言
- 统一作者侧配置模型
- 统一 BattleCore 运行时战斗逻辑

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

`BattleCore/` 是纯运行时战斗引擎。

负责：

- 战斗状态
- 出牌流程
- 定策快照
- 结算流程
- Buff / Trigger / Modifier / Cost / Rule 等运行时机制

不负责：

- Unity UI
- 服务器表现层
- 配置文件读取

## 3. 依赖方向

建议保持下面这条依赖方向不变：

- `Protocol` <- `ConfigModels`
- `Protocol` <- `BattleCore`

`ConfigModels` 与 `BattleCore` 尽量解耦，由客户端适配层连接。

## 4. BattleCore 当前子目录

当前 `Shared/BattleCore/` 主体目录包括：

- `Context`
- `Core`
- `Costs`
- `Definitions`
- `EventBus`
- `Foundation`
- `Handlers`
- `Managers`
- `Modifiers`
- `Random`
- `Resolvers`
- `Rules`

这些目录的详细职责请看：

- [SharedArchitecture.md](../Docs/TechGuide/SharedArchitecture.md)

## 5. 当前重要边界

### 5.1 ConfigModels、Definitions、Foundation

这三层不要混。

- `ConfigModels`
  - 作者侧配置语义
- `Definitions`
  - BattleCore 直接消费的静态模板
- `Foundation`
  - BattleCore 运行时基础模型

当前例子：

- [CardConfig.cs](ConfigModels/Card/CardConfig.cs)
- [BuffConfig.cs](BattleCore/Definitions/BuffConfig.cs)
- [BattleCard.cs](BattleCore/Foundation/Cards/BattleCard.cs)

### 5.2 Costs、Rules、Modifiers

这三层也不要混。

- `Costs`
  - 只负责实际耗能计算
- `Rules`
  - 负责操作规则判定
- `Modifiers`
  - 负责结算值修正

例如：

- `Corruption`
  - Buff 是状态真源
  - Rule 判断这次是否命中
  - Cost 只计算最终耗能
- `Strength / Weak / Vulnerable`
  - 属于 `Modifiers`

## 6. 当前有效文档

建议从下面几份开始：

- [Docs/README.md](../Docs/README.md)
- [Docs/TechGuide/SharedArchitecture.md](../Docs/TechGuide/SharedArchitecture.md)
- [Docs/TechGuide/SystemArchitecture_V2.md](../Docs/TechGuide/SystemArchitecture_V2.md)
- [Docs/TechGuide/ConfigSystem.md](../Docs/TechGuide/ConfigSystem.md)
- [Docs/GameDesign/CardSystem.md](../Docs/GameDesign/CardSystem.md)

## 7. 当前不再保留在主线的内容

以下内容已经从当前 1v1 主线移出，不应继续当成现行契约：

- 分路状态容器
- 旧阶段系统预留枚举

历史版本已归档到：

- [Archive/Shared_3v3_PreRewrite](../Archive/Shared_3v3_PreRewrite)

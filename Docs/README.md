# Card_Moba 文档中心

**最后更新**: 2026-03-27  
**状态**: 当前导航

## 1. 文档分层

当前文档分成四类：

- `GameDesign/`
  - 产品规则与玩法语义
- `TechGuide/`
  - 架构、实现与当前契约说明
- `API/`
  - 协议与枚举参考入口
- `Planning/`
  - 路线图、TODO、历史计划

## 2. 当前有效文档

### 2.1 GameDesign

| 文档 | 说明 | 状态 |
|------|------|------|
| [Overview.md](GameDesign/Overview.md) | 产品层玩法总览 | 当前有效 |
| [CardSystem.md](GameDesign/CardSystem.md) | 卡牌实例、定义、出牌链路与定策快照 | 当前有效 |
| [SettlementRules.md](GameDesign/SettlementRules.md) | BattleCore 结算规则 | 当前有效 |
| [LaneSystem.md](GameDesign/LaneSystem.md) | 分路系统产品方向说明 | 待重写 / 非当前 1v1 契约 |
| [TeamObjective.md](GameDesign/TeamObjective.md) | 团队目标系统产品设计 | 产品层说明 |
| [InGameDraft.md](GameDesign/InGameDraft.md) | 局内构筑设计 | 产品层说明 |

### 2.2 TechGuide

| 文档 | 说明 | 状态 |
|------|------|------|
| [QuickStart.md](TechGuide/QuickStart.md) | 新人快速入门 | 当前有效 |
| [SystemArchitecture_V2.md](TechGuide/SystemArchitecture_V2.md) | BattleCore 当前运行时架构 | 当前有效 |
| [SharedArchitecture.md](TechGuide/SharedArchitecture.md) | `Shared/` 顶层结构、`BattleCore` 子目录职责与 `Rules` 当前边界 | 当前有效 |
| [ConfigSystem.md](TechGuide/ConfigSystem.md) | 配置链路与作者入口说明 | 当前有效 |
| [BattleCore.md](TechGuide/BattleCore.md) | V1 BattleCore 历史文档入口 | 归档入口 |
| [CardLifecycle.md](TechGuide/CardLifecycle.md) | 历史草案说明页 | 归档入口 |
| [新框架参考.md](TechGuide/新框架参考.md) | 历史草案说明页 | 归档入口 |

### 2.3 API

| 文档 | 说明 |
|------|------|
| [Enums.md](API/Enums.md) | 枚举参考入口 |
| [Protocol.md](API/Protocol.md) | 协议参考入口 |

### 2.4 Planning

| 文档 | 说明 |
|------|------|
| [Roadmap.md](Planning/Roadmap.md) | 路线图 |
| [TODO.md](Planning/TODO.md) | 待办与推进记录 |
| [BattleCoreRefactorPlan.md](Planning/BattleCoreRefactorPlan.md) | BattleCore V2 重构历史计划 |

## 3. 推荐阅读顺序

### 3.1 新人入门

1. [QuickStart.md](TechGuide/QuickStart.md)
2. [Overview.md](GameDesign/Overview.md)
3. [SystemArchitecture_V2.md](TechGuide/SystemArchitecture_V2.md)
4. [SharedArchitecture.md](TechGuide/SharedArchitecture.md)

### 3.2 理解卡牌与结算

1. [CardSystem.md](GameDesign/CardSystem.md)
2. [SettlementRules.md](GameDesign/SettlementRules.md)
3. [ConfigSystem.md](TechGuide/ConfigSystem.md)

### 3.3 理解 Shared 结构

1. [SharedArchitecture.md](TechGuide/SharedArchitecture.md)
2. [SystemArchitecture_V2.md](TechGuide/SystemArchitecture_V2.md)

## 4. 归档说明

`Docs/_Archive/` 下的文档只保留历史参考价值，不再作为当前实现依据。

当前仍保留的主归档入口：

- [TechGuide/BattleCore.md](TechGuide/BattleCore.md)

## 5. 文档维护约定

1. `Shared/` 或 `BattleCore` 目录职责变化时，先更新：
   - [SharedArchitecture.md](TechGuide/SharedArchitecture.md)
   - [SystemArchitecture_V2.md](TechGuide/SystemArchitecture_V2.md)
2. 卡牌语义或结算语义变化时，更新：
   - [CardSystem.md](GameDesign/CardSystem.md)
   - [SettlementRules.md](GameDesign/SettlementRules.md)
3. 配置字段或作者链路变化时，更新：
   - [ConfigSystem.md](TechGuide/ConfigSystem.md)
4. 历史方案不要继续写进当前契约文档，统一归档或在归档入口说明。

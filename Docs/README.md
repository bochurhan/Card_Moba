# Card_Moba 文档中心

**最后更新**: 2026-03-27  
**文档版本**: v2.4

---

## 项目简介

`Card_Moba` 是一套以卡牌对抗为核心的同步回合制项目。当前文档体系分成四类：

- `GameDesign/`：玩法与产品规则
- `TechGuide/`：实现与架构说明
- `API/`：协议与枚举参考
- `Planning/`：路线图、TODO 与历史计划

---

## 文档目录

### GameDesign

| 文档 | 说明 | 状态 |
|------|------|------|
| [Overview.md](GameDesign/Overview.md) | 产品层玩法总览 | 当前有效 |
| [CardSystem.md](GameDesign/CardSystem.md) | 卡牌实例、定义、出牌链路与定策快照 | 当前有效 |
| [SettlementRules.md](GameDesign/SettlementRules.md) | BattleCore 结算规则 | 当前有效 |
| [LaneSystem.md](GameDesign/LaneSystem.md) | 分路系统产品设计 | 产品层说明 |
| [TeamObjective.md](GameDesign/TeamObjective.md) | 团队目标系统产品设计 | 产品层说明 |
| [InGameDraft.md](GameDesign/InGameDraft.md) | 局内构筑设计 | 产品层说明 |

### TechGuide

| 文档 | 说明 | 状态 |
|------|------|------|
| [QuickStart.md](TechGuide/QuickStart.md) | 新人快速入门 | 当前有效 |
| [SystemArchitecture_V2.md](TechGuide/SystemArchitecture_V2.md) | BattleCore 当前运行时架构 | 当前有效 |
| [SharedArchitecture.md](TechGuide/SharedArchitecture.md) | `Shared/` 顶层结构、BattleCore 子目录职责与 `Rules` 设计方向 | 当前有效 |
| [ConfigSystem.md](TechGuide/ConfigSystem.md) | 配置链路与作者入口说明 | 当前有效 |
| [BattleCore.md](TechGuide/BattleCore.md) | V1 BattleCore 历史文档入口 | 归档入口 |
| [CardLifecycle.md](TechGuide/CardLifecycle.md) | 历史草案说明页 | 归档入口 |
| [新框架参考.md](TechGuide/新框架参考.md) | 历史草案说明页 | 归档入口 |

### API

| 文档 | 说明 |
|------|------|
| [Enums.md](API/Enums.md) | 枚举参考入口 |
| [Protocol.md](API/Protocol.md) | 协议参考入口 |

### Planning

| 文档 | 说明 |
|------|------|
| [Roadmap.md](Planning/Roadmap.md) | 路线图 |
| [TODO.md](Planning/TODO.md) | 待办与历史推进记录 |
| [BattleCoreRefactorPlan.md](Planning/BattleCoreRefactorPlan.md) | BattleCore V2 重构计划归档 |

---

## 推荐阅读顺序

### 新人入门

1. [QuickStart.md](TechGuide/QuickStart.md)
2. [Overview.md](GameDesign/Overview.md)
3. [SystemArchitecture_V2.md](TechGuide/SystemArchitecture_V2.md)
4. [SharedArchitecture.md](TechGuide/SharedArchitecture.md)

### 理解卡牌与结算

1. [CardSystem.md](GameDesign/CardSystem.md)
2. [SettlementRules.md](GameDesign/SettlementRules.md)
3. [ConfigSystem.md](TechGuide/ConfigSystem.md)

### 理解 Shared 代码结构

1. [SharedArchitecture.md](TechGuide/SharedArchitecture.md)
2. [SystemArchitecture_V2.md](TechGuide/SystemArchitecture_V2.md)

---

## 归档说明

`Docs/_Archive/` 下的文档只保留历史参考价值，不再作为当前实现依据。

当前仍保留的主归档入口：

- [TechGuide/BattleCore.md](TechGuide/BattleCore.md)

---

## 文档维护约定

1. 架构变更时，先更新：
   - [SystemArchitecture_V2.md](TechGuide/SystemArchitecture_V2.md)
   - [SharedArchitecture.md](TechGuide/SharedArchitecture.md)
2. 卡牌或结算语义变更时，更新：
   - [CardSystem.md](GameDesign/CardSystem.md)
   - [SettlementRules.md](GameDesign/SettlementRules.md)
3. 配置字段或作者链路变更时，更新：
   - [ConfigSystem.md](TechGuide/ConfigSystem.md)
4. 旧方案不继续写进当前契约文档，统一放归档或在归档入口说明

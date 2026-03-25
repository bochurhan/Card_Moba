# BattleCore 归档说明

**状态**：已归档，不再维护  
**归档日期**：2026-03-26

## 说明

原 `BattleCore.md` 正文描述的是 V1/V1.5 阶段的旧实现，包含大量已经退出当前契约的内容，例如：

- `TriggerCondition`
- `ExecutionMode / PassiveHandler`
- `HandlerRegistry`
- `PlayedCard`
- `effectIds + effects.json`
- `Effects.csv`
- `Shared/BattleCore/Settlement/*`

这些概念已经不再作为当前 BattleCore 的开发依据，因此本文件不再保留旧正文，避免继续误导实现。

## 历史副本

完整历史内容已保存在：

- [Docs/_Archive/v1_battlecore/BattleCore_V1.md](../_Archive/v1_battlecore/BattleCore_V1.md)

如需研究 V1 方案或追溯旧设计，请直接查看归档副本，不要以其作为当前实现依据。

## 当前有效文档

当前 BattleCore 与卡牌配置的有效参考文档为：

- [SystemArchitecture_V2.md](SystemArchitecture_V2.md)
- [ConfigSystem.md](ConfigSystem.md)
- [SettlementRules.md](../GameDesign/SettlementRules.md)
- [CardSystem.md](../GameDesign/CardSystem.md)

## 当前开发口径

- BattleCore 当前以 `Shared/BattleCore/` 实际代码为准。
- 卡牌配置当前只支持 `cards.json` 单表结构。
- 已删除的 legacy 字段与旧配置链路，不应再出现在新配置和新文档中。

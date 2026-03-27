# 分路系统说明

**文档版本**: 2026-03-27  
**状态**: 待重写  
**适用范围**: 3v3 / 分路系统的产品方向说明，不代表当前 1v1 BattleCore 契约

## 1. 当前口径

当前 BattleCore 主线聚焦 1v1。

已经从当前运行时代码中移出的内容：

- `BattleContext.Lanes`
- `LaneData`
- `Protocol.Enums.MatchPhase`

这些结构已经归档到仓库级 `Archive/`，等待后续 3v3 与阶段系统重写时参考。

## 2. 这份文档现在代表什么

这份文档当前不再描述“已经落地的运行时系统”，只保留两层意义：

- 分路系统的产品方向
- 未来重写时的需求约束入口

所以阅读时应默认：

- 当前代码没有真实分路状态容器
- 当前 BattleCore 没有分路阶段机
- 当前卡牌配置也不应假设存在稳定的分路目标解析

## 3. 为什么先归档

原因很直接：

- 现有分路系统和阶段系统设计已经发生较大变化
- 当前 1v1 主流程应保持结构收敛，不继续背 3v3 预留包袱
- 旧预留结构继续挂在主树里，只会误导当前开发

## 4. 后续重写建议

等 1v1 主线稳定后，再重新定义：

1. 分路状态容器应该放在哪一层
2. 阶段系统是 `BattleCore` 主流程的一部分，还是更上层的模式状态机
3. 卡牌、目标解析、Buff、规则系统如何感知“所在路”
4. 3v3 阶段切换与 1v1 当前回合流程如何并存

在这些边界明确之前，不建议把新的分路字段继续写回当前 BattleCore 主线。

## 5. 关联文档

- [Overview.md](Overview.md)
- [InGameDraft.md](InGameDraft.md)
- [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)
- [SharedArchitecture.md](../TechGuide/SharedArchitecture.md)

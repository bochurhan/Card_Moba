# 核心玩法概览

**文档版本**: 2026-03-26  
**状态**: 当前设计口径  
**适用范围**: 产品玩法概览，不直接替代 BattleCore 运行时契约

---

## 1. 产品定位

项目目标是做一套以同步决策为核心的卡牌对抗玩法。

当前口径分两层理解：

- 产品方向：
  - 中长期目标包含双路、团队目标、死斗收束等 MOBA 抽象
- 当前可运行实现：
  - BattleCore 先按单局同步卡牌对抗验证
  - 主循环围绕抽牌、出牌、统一结算、回合收尾展开

---

## 2. 核心体验

玩家每回合都围绕三件事做决策：

- 资源怎么花
- 这回合先铺即时收益，还是提交延后结算的定策
- 如何用手牌、Buff、牌堆和节奏压制对手

当前卡牌体系中，最重要的区分是：

- 瞬策牌：
  - 在操作阶段立即生效
  - 更偏资源、过牌、铺垫、短期增益
- 定策牌：
  - 在回合末统一公开并结算
  - 更偏伤害、对抗、控制、承诺式爆发

---

## 3. 当前可运行循环

BattleCore 当前实际主循环如下：

1. 回合开始
2. 触发回合开始效果
3. 抽牌、回能
4. 玩家操作阶段
5. 瞬策牌即时结算
6. 定策牌在回合末统一结算
7. Buff 衰减、护盾清空、弃手牌
8. 下一回合开始

如果需要看运行时细节，应优先参考：

- [SettlementRules.md](SettlementRules.md)
- [CardSystem.md](CardSystem.md)
- [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)

---

## 4. 当前与未来的边界

以下内容是产品方向，但当前 BattleCore 还没有完整接线：

- 双路对抗
- 团队目标争夺
- 死斗阶段
- 更复杂的跨路交互

因此阅读代码时要区分：

- 设计愿景
- 当前实现契约

如果设计文档与当前运行时代码冲突，以当前契约文档为准。

---

## 5. 关联文档

- [CardSystem.md](CardSystem.md)
- [SettlementRules.md](SettlementRules.md)
- [LaneSystem.md](LaneSystem.md)
- [TeamObjective.md](TeamObjective.md)
- [SystemArchitecture_V2.md](../TechGuide/SystemArchitecture_V2.md)

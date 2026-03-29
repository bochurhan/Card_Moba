# 局内构筑系统详解

**文档版本**：2026-03-28  
**前置阅读**：[Overview.md](Overview.md)  
**阅读时间**：6 分钟  
**状态**：当前产品规则 + 当前 MatchFlow 已落地契约

---

## 1. 设计目标

局内构筑系统需要同时满足以下要求：

1. 强化每局差异性，让卡组在本局中产生可感知成长；
2. 服务整局节奏，不打断多场 battle 的推进；
3. 不压过局外预构筑，职业和初始装备仍然是主骨架；
4. 控制操作负担，让每次窗口都能快速完成；
5. 能与击杀奖励、装备被动和团队目标自然衔接。

---

## 2. 当前窗口机制

当前 `BuildWindow` 采用“阶段 battle 结束后统一调整”的方式。

- 每个可构筑 step 结束后，进入一次同步构筑窗口；
- 窗口内每名玩家先拿到自己的候选和预览状态；
- 所有人都锁定后可提前推进；
- 若超时，系统会对剩余机会补默认动作；
- 当前默认超时动作是 `Heal`。

---

## 3. 构筑机会数

当前规则：

- 每名玩家每个窗口默认有 `1` 次构筑机会；
- 若本场 battle 摘要把玩家写入 `ExtraBuildPickPlayerIds`，则额外 `+1` 次机会；
- 多次机会按顺序结算，后一次会基于前一次的预览 `HP/Deck` 重新生成候选。

> 当前实现是按玩家粒度发放额外机会；未来 2v2 若要改成团队粒度奖励，应由 `BattleSummary` 或上层规则继续扩展。

---

## 4. 当前四类操作

### 4.1 Heal

- 正常情况下，恢复 `30% MaxHp`；
- 当前实现使用向上取整；
- 不会超过 `MaxHp`。

### 4.2 UpgradeCard

- 从当前预览卡组中选择 `1` 张可升级牌；
- 升级依据卡牌配置中的 `UpgradedCardConfigId`；
- 升级后会更新当前预览卡组，影响后续机会的候选。

### 4.3 RemoveCard

- 从当前预览卡组中选择 `1` 张可删除牌；
- 删除单位是 `PersistentCardId`，不是 battle 内实例。

### 4.4 AddCard

- 当前实现为 `2` 组候选；
- 每组 `3` 张；
- 每组可选 `0` 或 `1` 张；
- 两组都可以跳过；
- 被选中的牌会加入当前预览卡组。

---

## 5. AddCard 当前候选规则

当前 `MatchFlow` 的默认拿牌规则：

- 不出现传说牌；
- 稀有度权重为 `普通:罕见:稀有 = 6:3:1`；
- 每张候选有 `10%` 概率直接以升级版出现；
- 默认按职业池抽取；
- 当前 class pool 由 `BuildCatalogAssembler` 自动从 `CardConfig` 注册，例如 `class:Warrior`。

当前仍未接入：

- 按阶段倾向性变化的奖励池
- 团队目标驱动的特殊 pool
- 装备对候选数量或质量的改写

---

## 6. 死亡惩罚

当前规则：

- 上一场 battle 中死亡的玩家进入 `ForcedRecovery`；
- `ForcedRecovery` 只保留 `1` 次构筑机会；
- 该机会只能选择 `Heal`；
- 此时 `Heal` 改为恢复 `40% MaxHp`。

这条规则的目的：

- 把死亡明确建模为节奏损失；
- 允许失败方恢复到可继续参战的状态；
- 防止死亡后继续通过升级/删牌/拿牌扩大 build 收益。

---

## 7. 职业与装备的当前入口

当前 `BuildWindow` 已为职业与装备留出正式入口：

- 每名玩家持有 `PlayerLoadout`
- `PlayerLoadout` 当前包含：
  - `ClassId`
  - `HeroId`
  - `EquipmentIds`
  - `DefaultBuildPoolId`

当前已实现装备示例：

- `burning_blood`
  - 每场 battle 结束后恢复 6 点生命

装备当前通过 battle 生命周期 runtime 执行，而不是直接写死在 `RoundManager` 里。

---

## 8. 当前已落地与未落地边界

### 已落地

- `MatchContext` 驱动多场 battle + build window
- `BattleSummary -> MatchStateApplier` 回写跨战斗 HP
- `BuildOfferGenerator` 生成候选
- `BuildActionApplier` 提交回血、升级、删牌、拿牌
- 死亡强制恢复窗口
- `burning_blood` 装备钩子

### 尚未落地

- 2v2 团队共享的构筑奖励规则
- 更复杂的装备触发器
- 装备作者配置入口
- 按阶段/目标动态变化的奖励池

---

## 9. 关联文档

| 主题 | 文档 |
|------|------|
| 整局状态机与文件职责 | [../TechGuide/MatchFlow.md](../TechGuide/MatchFlow.md) |
| 配置如何进入构筑目录 | [../TechGuide/ConfigSystem.md](../TechGuide/ConfigSystem.md) |
| 核心玩法概述 | [Overview.md](Overview.md) |
| 团队目标系统 | [TeamObjective.md](TeamObjective.md) |
| 结算规则详解 | [SettlementRules.md](SettlementRules.md) |

# 配置系统说明

**文档版本**: 2026-03-28  
**状态**: 当前有效  
**适用范围**: 当前卡牌配置作者链路、BattleCore 配置适配与 MatchFlow 构筑目录装配

---

## 1. 结论

当前配置链路已经收口为两条消费方向：

- `BattleCore` 配置消费
  - `CardConfig` -> 客户端/服务端适配 -> `BattleCardDefinition`
- `MatchFlow` 构筑消费
  - `CardConfig` -> `CardConfigManager.CreateBuildCatalog()` -> `IBuildCatalog`

唯一作者真源仍然是：

- `Client/Assets/StreamingAssets/Config/cards.json`

---

## 2. 当前链路

### 2.1 编辑器与作者真源

`CardEditorWindow` 负责：

- 加载 `cards.json`
- 可视化编辑卡牌和效果
- 保存 `cards.json`
- 自动导出审阅 CSV

### 2.2 BattleCore 运行时读取

BattleCore 不直接读 `cards.json` 文件。

当前链路是：

1. 上层加载 `cards.json`
2. 解析为 `CardConfig`
3. 适配为 `BattleCardDefinition`
4. 通过 `BattleContext.CardDefinitionProvider` 提供给 `BattleCore`

### 2.3 MatchFlow 构筑读取

`MatchFlow` 同样不直接读取 `cards.json` 文件。

当前链路是：

1. 上层加载 `cards.json`
2. 解析为 `CardConfig`
3. 客户端当前可直接通过 `CardConfigManager.CreateBuildCatalog()` 调用 `BuildCatalogAssembler`
4. 组装为 `IBuildCatalog`
5. `BuildOfferGenerator` 和 `BuildActionApplier` 读取 `IBuildCatalog`

### 2.4 当前装备配置

装备配置尚未进入 `cards.json`。

当前状态：

- `EquipmentDefinition` 已在 `MatchFlow` 中建模
- 当前通过上层手动注册到 `IBuildCatalog`
- 已实现示例：`burning_blood`

后续若装备进入统一配置链路，应继续保持：

- 作者侧模型放在 `ConfigModels`
- 运行时消费通过 `MatchFlow.Catalog` 装配

---

## 3. 当前主格式

`cards.json` 当前采用单卡内嵌 `effects` 的结构。

每张卡直接保存：

- 卡牌基础字段
- `effects`
- `playConditions`

旧的 `effectIds + effects.json` 双表模型已移除。

---

## 4. MatchFlow 当前消费哪些字段

`BuildCatalogAssembler` 当前主要消费以下字段：

- `CardId`
- `HeroClass`
- `Rarity`
- `Tags`
- `UpgradedCardConfigId`

当前默认规则：

- `ConfigId = CardId.ToString()`
- 稀有度映射：
  - `1 -> Common`
  - `2 -> Uncommon`
  - `3 -> Rare`
  - `>= 4 -> Legendary`
- 默认从奖励池排除：
  - `Legendary`
  - `Status`
  - 作为其他卡升级目标的卡
- 默认按职业注册 class pool，例如：
  - `class:Warrior`
  - `class:Assassin`
  - `class:Mage`

---

## 5. BattleCore 当前支持范围

当前 BattleCore 主配置白名单包括：

- `Damage`
- `Pierce`
- `Heal`
- `Shield`
- `AddBuff`
- `Draw`
- `GenerateCard`
- `Lifesteal`
- `GainEnergy`
- `MoveSelectedCardToDeckTop`
- `ReturnSourceCardToHandAtRoundEnd`
- `UpgradeCardsInHand`

卡牌层主字段：

- `cardId`
- `cardName`
- `description`
- `trackType`
- `targetType`
- `heroClass`
- `effectRange`
- `layer`
- `tags`
- `energyCost`
- `rarity`
- `upgradedCardConfigId`
- `playConditions`
- `effects`

---

## 6. 已移除字段

以下旧字段已经从当前主配置链路移除：

- `effectIds`
- `ValueSource`
- `TriggerCondition`
- `IsDelayed`
- `ExecutionMode`
- `AppliesBuff`
- `BuffType`
- `BuffStackRule`
- `IsBuffDispellable`
- `passiveTriggerTiming`
- `passiveDuration`
- `passiveMaxTriggers`
- `effectParams`
- `subEffects`

---

## 7. 审阅产物

审阅文件包括：

- `Config/Excel/Cards.csv`
- `Config/Excel/Cards.xlsx`

它们用于：

- diff 审阅
- 策划/设计浏览
- Excel 输出

不用于直接驱动 BattleCore 或 MatchFlow。

---

## 8. 当前有效参考

当前判断配置是否符合契约时，应优先参考：

- [../GameDesign/CardSystem.md](../GameDesign/CardSystem.md)
- [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md)
- [MatchFlow.md](MatchFlow.md)
- [SystemArchitecture_V2.md](SystemArchitecture_V2.md)


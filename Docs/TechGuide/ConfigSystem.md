# 配置系统说明

**文档版本**: 2026-03-26  
**状态**: 当前有效  
**适用范围**: 当前卡牌配置作者链路与运行时读取链路

---

## 1. 结论

当前配置链路已经收口为：

- 唯一作者入口：
  - `CardEditorWindow`
- 唯一作者真源：
  - `Client/Assets/StreamingAssets/Config/cards.json`
- 运行时读取：
  - `cards.json`
- 审阅导出：
  - `Config/Excel/Cards.csv`
- Excel 审阅产物：
  - `Config/Excel/Cards.xlsx`

CSV 现在只承担审阅功能，不再是作者输入源。

---

## 2. 当前链路

### 2.1 编辑器

`CardEditorWindow` 负责：

- 加载 `cards.json`
- 可视化编辑卡牌和效果
- 保存 `cards.json`
- 自动导出审阅 CSV

### 2.2 运行时

运行时只读取：

- `Client/Assets/StreamingAssets/Config/cards.json`

BattleCore 不直接读取 CSV。

### 2.3 审阅产物

审阅文件包括：

- `Config/Excel/Cards.csv`
- `Config/Excel/Cards.xlsx`

它们用于：

- diff 审阅
- 策划/设计浏览
- Excel 输出

不用于直接回写运行时配置。

---

## 3. 当前主格式

`cards.json` 当前采用单卡内嵌 `effects` 的结构。

每张卡直接保存：

- 卡牌基础字段
- `effects`
- `playConditions`

旧的 `effectIds + effects.json` 双表模型已移除。

---

## 4. 当前支持范围

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
- `playConditions`
- `effects`

效果层主字段：

- `effectType`
- `value`
- `valueExpression`
- `repeatCount`
- `duration`
- `targetOverride`
- `buffConfigId`
- `generateCardConfigId`
- `generateCardZone`
- `generateCardIsTemp`
- `priority`
- `subPriority`
- `effectConditions`

---

## 5. 已移除字段

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

原则是：

- 新卡不再使用这些字段
- 编辑器不再暴露这些入口
- 运行时不再兼容读取这些字段

---

## 6. 审阅 CSV

`Cards.csv` 当前是一份导出审阅视图。

它保留：

- 基础卡牌信息
- `EffectSummary`
- `EffectsJson`

目的是方便 diff 和审阅，而不是承担复杂嵌套结构的作者输入。

---

## 7. Excel 导出

`Config/Excel/merge_to_xlsx.py` 当前输入：

- `Cards.csv`
- `Cards_Template_Enums.csv`

输出：

- `Cards.xlsx`

`Cards.xlsx` 同样是审阅产物，不是作者真源。

---

## 8. 当前有效参考

当前判断配置是否符合契约时，应优先参考：

- [CardSystem.md](../GameDesign/CardSystem.md)
- [SettlementRules.md](../GameDesign/SettlementRules.md)
- [SystemArchitecture_V2.md](SystemArchitecture_V2.md)

历史文档若与上述内容冲突，以当前契约为准。

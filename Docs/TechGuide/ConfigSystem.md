# 配置系统说明

**文档版本**：2026-03-25  
**状态**：当前有效

## 1. 结论

当前卡牌配置链路已经收口为：

- 唯一作者入口：`CardEditorWindow`
- 唯一作者真源：`Client/Assets/StreamingAssets/Config/cards.json`
- 运行时读取：`cards.json`
- 审阅导出：`Config/Excel/Cards.csv`
- Excel 整理产物：`Config/Excel/Cards.xlsx`

如有历史文档或旧工具与以上口径冲突，以当前文档和当前代码为准。

## 2. 当前链路

### 2.1 编辑入口

策划和设计只在 Unity 编辑器中的 `CardEditorWindow` 修改卡牌。  
编辑器负责：

- 加载 `cards.json`
- 可视化编辑卡牌与效果
- 保存 `cards.json`
- 自动导出审阅 CSV

### 2.2 运行时入口

运行时配置只读取：

- `Client/Assets/StreamingAssets/Config/cards.json`

BattleCore 和运行时适配层不直接读取 CSV。

### 2.3 审阅产物

`Config/Excel/Cards.csv` 是由编辑器自动导出的审阅文件，主要用于：

- diff 审阅
- 非程序同学快速浏览卡池
- 导出 `Cards.xlsx`

它不是作者输入源，也不应再手工维护后回写到运行时配置。

## 3. 当前主格式

`cards.json` 采用单卡内嵌 `effects` 的当前格式。  
每张卡直接包含：

- 卡牌基础字段
- `effects`
- `playConditions`

旧的 `effectIds + effects.json` 双表模式已经移除，不再属于当前配置链路。

## 4. 当前支持范围

当前 BattleCore 配置路径只保证以下 `EffectType`：

- `Damage`
- `Pierce`
- `Heal`
- `Shield`
- `AddBuff`
- `Draw`
- `GenerateCard`
- `Lifesteal`

当前主配置面的核心字段为：

- 卡牌层：`cardId / cardName / description / trackType / targetType / heroClass / effectRange / layer / tags / energyCost / rarity / playConditions / effects`
- 效果层：`effectType / value / valueExpression / repeatCount / duration / targetOverride / effectConditions / buffConfigId / generateCardConfigId / generateCardZone / priority / subPriority`

## 5. 已移除字段

以下字段已经从当前配置模型、编辑器保存链路和运行时加载链路中移除：

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

处理原则：

- 新卡配置只允许使用当前主字段集
- 编辑器不再提供这些字段的输入入口
- 运行时不再兼容读取这些字段

## 6. 审阅 CSV

`Config/Excel/Cards.csv` 当前是生成文件，不是作者源。  
它是一个审阅视图，包含：

- 一行一张卡
- 基础卡牌字段
- `EffectSummary`
- `EffectsJson`

这样既方便 diff，也不要求作者直接维护复杂嵌套结构。

## 7. Excel 导出

`Config/Excel/merge_to_xlsx.py` 会把当前审阅文件导出为 `Cards.xlsx`。  
当前输入只有：

- `Cards.csv`
- `Cards_Template_Enums.csv`

`Cards.xlsx` 也是审阅产物，不是作者源。

## 8. 当前有效参考

如需判断配置是否符合当前契约，以以下文档为准：

- [Docs/GameDesign/CardSystem.md](../GameDesign/CardSystem.md)
- [Docs/GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md)
- [Docs/TechGuide/SystemArchitecture_V2.md](SystemArchitecture_V2.md)

历史文档如与上述内容冲突，以当前契约为准。

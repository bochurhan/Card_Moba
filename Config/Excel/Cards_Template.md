# 卡牌审阅 CSV 说明

这份说明描述的是当前 `Cards.csv` 的用途和字段，不是作者输入模板。

## 1. 定位

当前卡牌配置链路是：

- `CardEditorWindow` 编辑卡牌
- 编辑器直写 `cards.json`
- 编辑器自动导出 `Config/Excel/Cards.csv`

因此：

- `cards.json` 是作者真源
- `Cards.csv` 是审阅导出物

不要把 `Cards.csv` 当成需要手工维护的主配置源。

## 2. 当前审阅列

`Cards.csv` 当前导出以下列：

- `CardId`
- `CardName`
- `Description`
- `TrackType`
- `TargetType`
- `HeroClass`
- `EffectRange`
- `Layer`
- `Tags`
- `EnergyCost`
- `Rarity`
- `EffectSummary`
- `EffectsJson`

## 3. 列用途

### 3.1 基础列

这些列用于快速浏览一张牌的基础信息：

- `CardId`
- `CardName`
- `TrackType`
- `TargetType`
- `EnergyCost`
- `Rarity`
- `Tags`

### 3.2 EffectSummary

`EffectSummary` 是给人看的摘要列，方便在 diff 或表格审阅时快速判断：

- 这张牌做什么
- 是否有重复次数
- 是否有目标覆盖
- 是否依赖条件
- 是否依赖 Buff / 生成牌参数

### 3.3 EffectsJson

`EffectsJson` 保留完整效果数组，便于：

- 审阅完整配置
- 追踪编辑器导出的真实效果内容
- 和 `cards.json` 做对照

## 4. 当前主格式

运行时主格式仍然是 `cards.json` 内嵌 `effects`。

`Cards.csv` 只是把当前 JSON 配置投影成更容易审阅的平面视图，不参与运行时加载。

## 5. 当前支持范围

当前 BattleCore 主配置面支持的 `EffectType`：

- `Damage`
- `Pierce`
- `Heal`
- `Shield`
- `AddBuff`
- `Draw`
- `GenerateCard`
- `Lifesteal`

## 6. 示例

```csv
CardId,CardName,Description,TrackType,TargetType,HeroClass,EffectRange,Layer,Tags,EnergyCost,Rarity,EffectSummary,EffectsJson
2001,打击,造成6点伤害,Plan,CurrentEnemy,Universal,SingleEnemy,DamageTrigger,Damage,1,1,Damage 6,"[{""effectType"":10,""value"":6,""repeatCount"":1,""duration"":0}]"
```

## 7. 参考文档

- `Docs/TechGuide/ConfigSystem.md`
- `Docs/GameDesign/CardSystem.md`
- `Docs/GameDesign/SettlementRules.md`
- `Docs/TechGuide/SystemArchitecture_V2.md`
